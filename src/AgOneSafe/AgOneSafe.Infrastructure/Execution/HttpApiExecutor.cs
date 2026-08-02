using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AgOneSafe.Application.Abstractions;
using AgOneSafe.Domain;
using AgOneSafe.Domain.Tenants;
using AgOneSafe.Infrastructure.Emulation;
using AgOneSafe.Infrastructure.Security;
using Microsoft.Extensions.Logging;

namespace AgOneSafe.Infrastructure.Execution;

/// <summary>
/// Shape of the JSON payload stored on a Graph or ARM control action:
/// <code>
/// {
///   "method": "GET",
///   "path": "/v1.0/policies/authorizationPolicy",
///   "emulatorKey": "graph:authorizationPolicy",
///   "body": { "allowInvitesFrom": "adminsAndGuestInviters" },
///   "select": "value"
/// }
/// </code>
/// </summary>
public sealed class ApiRequestDescriptor
{
    public string Method { get; set; } = "GET";
    public string Path { get; set; } = string.Empty;
    public string? EmulatorKey { get; set; }
    public JsonNode? Body { get; set; }

    /// <summary>Optional property to unwrap from the response before assertions run.</summary>
    public string? Select { get; set; }
}

/// <summary>
/// Shared REST executor. Live mode acquires a token for the tenant's app registration and calls
/// the real endpoint; Simulation mode serves and mutates the tenant emulator's state document,
/// so exactly the same catalog entry works on a laptop and against a customer tenant.
/// </summary>
public abstract class HttpApiExecutor : IControlExecutor
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ITenantEmulator _emulator;
    private readonly IPayloadRenderer _renderer;
    private readonly ITokenProvider _tokenProvider;
    private readonly ILogger _logger;

    protected HttpApiExecutor(
        IHttpClientFactory httpClientFactory,
        ITenantEmulator emulator,
        IPayloadRenderer renderer,
        ITokenProvider tokenProvider,
        ILogger logger)
    {
        _httpClientFactory = httpClientFactory;
        _emulator = emulator;
        _renderer = renderer;
        _tokenProvider = tokenProvider;
        _logger = logger;
    }

    public abstract ExecutorType ExecutorType { get; }

    protected abstract string HttpClientName { get; }

    protected abstract string Scope { get; }

    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);

    public async Task<ExecutionResult> ExecuteAsync(ExecutionRequest request)
    {
        var action = request.Action;

        var body = request.DryRun && !string.IsNullOrWhiteSpace(action.DryRunPayload)
            ? action.DryRunPayload!
            : action.Payload;

        var rendered = _renderer.Render(action, body, request.Parameters);

        ApiRequestDescriptor descriptor;
        try
        {
            descriptor = JsonSerializer.Deserialize<ApiRequestDescriptor>(rendered,
                             new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                         ?? throw new JsonException("The request descriptor deserialised to null.");
        }
        catch (JsonException ex)
        {
            return ExecutionResult.Failure(
                $"The action payload is not a valid API request descriptor: {ex.Message}", rendered);
        }

        var command = $"{descriptor.Method.ToUpperInvariant()} {descriptor.Path}";

        if (request.DryRun && !IsReadOnly(descriptor.Method))
        {
            return new ExecutionResult
            {
                Succeeded = true,
                Json = JsonSerializer.Serialize(new { dryRun = true, executed = false, request = descriptor }),
                Output = $"Dry run: {command} would be sent with body {descriptor.Body?.ToJsonString() ?? "(none)"}.",
                ExecutedCommand = command
            };
        }

        return request.Tenant.ExecutionMode == ExecutionMode.Simulation
            ? await ExecuteSimulatedAsync(request.Tenant, descriptor, command, request.CancellationToken)
            : await ExecuteLiveAsync(request.Tenant, descriptor, command, request.CancellationToken);
    }

    private async Task<ExecutionResult> ExecuteSimulatedAsync(
        TenantConnection tenant,
        ApiRequestDescriptor descriptor,
        string command,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        var key = string.IsNullOrWhiteSpace(descriptor.EmulatorKey)
            ? descriptor.Path.Trim('/').Replace('/', ':')
            : descriptor.EmulatorKey!;

        if (IsReadOnly(descriptor.Method))
        {
            var value = await _emulator.ReadAsync(tenant, key, cancellationToken);
            stopwatch.Stop();

            if (value is null)
            {
                return new ExecutionResult
                {
                    Succeeded = false,
                    Error = $"The tenant emulator holds no state for '{key}'. Add it to App_Data/emulator/tenant-seed.json.",
                    ExecutedCommand = command,
                    DurationMs = (int)stopwatch.ElapsedMilliseconds
                };
            }

            var selected = Unwrap(value, descriptor.Select);

            return new ExecutionResult
            {
                Succeeded = true,
                Json = selected.ToJsonString(),
                Output = $"{command} -> 200 OK (simulated)",
                ExecutedCommand = command,
                DurationMs = (int)stopwatch.ElapsedMilliseconds
            };
        }

        var existing = await _emulator.ReadAsync(tenant, key, cancellationToken) ?? new JsonObject();

        if (existing is JsonObject target && descriptor.Body is JsonObject patch)
        {
            foreach (var property in patch)
            {
                target[property.Key] = property.Value?.DeepClone();
            }

            await _emulator.WriteAsync(tenant, key, target, cancellationToken);
            existing = target;
        }
        else if (descriptor.Body is not null)
        {
            await _emulator.WriteAsync(tenant, key, descriptor.Body, cancellationToken);
            existing = descriptor.Body;
        }

        stopwatch.Stop();

        return new ExecutionResult
        {
            Succeeded = true,
            Json = existing.ToJsonString(),
            Output = $"{command} -> 204 No Content (simulated); state '{key}' updated.",
            ExecutedCommand = command,
            DurationMs = (int)stopwatch.ElapsedMilliseconds
        };
    }

    private async Task<ExecutionResult> ExecuteLiveAsync(
        TenantConnection tenant,
        ApiRequestDescriptor descriptor,
        string command,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var token = await _tokenProvider.GetAccessTokenAsync(tenant, Scope, cancellationToken);

            using var client = _httpClientFactory.CreateClient(HttpClientName);
            using var message = new HttpRequestMessage(new HttpMethod(descriptor.Method), descriptor.Path);
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            if (descriptor.Body is not null)
            {
                message.Content = new StringContent(descriptor.Body.ToJsonString(), Encoding.UTF8, "application/json");
            }

            using var response = await client.SendAsync(message, cancellationToken);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            stopwatch.Stop();

            if (!response.IsSuccessStatusCode)
            {
                return new ExecutionResult
                {
                    Succeeded = false,
                    Error = $"{command} returned {(int)response.StatusCode} {response.ReasonPhrase}: {content}",
                    Output = content,
                    ExecutedCommand = command,
                    DurationMs = (int)stopwatch.ElapsedMilliseconds
                };
            }

            var node = string.IsNullOrWhiteSpace(content) ? new JsonObject() : JsonNode.Parse(content);
            var selected = Unwrap(node ?? new JsonObject(), descriptor.Select);

            return new ExecutionResult
            {
                Succeeded = true,
                Json = selected.ToJsonString(),
                Output = $"{command} -> {(int)response.StatusCode}",
                ExecutedCommand = command,
                DurationMs = (int)stopwatch.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogWarning(ex, "{Command} failed against tenant {Tenant}", command, tenant.TenantId);
            return ExecutionResult.Failure(ex.Message, command, (int)stopwatch.ElapsedMilliseconds);
        }
    }

    private static JsonNode Unwrap(JsonNode node, string? select) =>
        string.IsNullOrWhiteSpace(select) ? node : node[select]?.DeepClone() ?? node;

    private static bool IsReadOnly(string method) =>
        method.Equals("GET", StringComparison.OrdinalIgnoreCase) ||
        method.Equals("HEAD", StringComparison.OrdinalIgnoreCase);
}

public sealed class MicrosoftGraphExecutor : HttpApiExecutor
{
    public MicrosoftGraphExecutor(
        IHttpClientFactory httpClientFactory,
        ITenantEmulator emulator,
        IPayloadRenderer renderer,
        ITokenProvider tokenProvider,
        ILogger<MicrosoftGraphExecutor> logger)
        : base(httpClientFactory, emulator, renderer, tokenProvider, logger)
    {
    }

    public override ExecutorType ExecutorType => ExecutorType.MicrosoftGraph;

    protected override string HttpClientName => "graph";

    protected override string Scope => "https://graph.microsoft.com/.default";
}

public sealed class AzureRestExecutor : HttpApiExecutor
{
    public AzureRestExecutor(
        IHttpClientFactory httpClientFactory,
        ITenantEmulator emulator,
        IPayloadRenderer renderer,
        ITokenProvider tokenProvider,
        ILogger<AzureRestExecutor> logger)
        : base(httpClientFactory, emulator, renderer, tokenProvider, logger)
    {
    }

    public override ExecutorType ExecutorType => ExecutorType.AzureRest;

    protected override string HttpClientName => "arm";

    protected override string Scope => "https://management.azure.com/.default";
}

public sealed class AzureResourceGraphExecutor : HttpApiExecutor
{
    public AzureResourceGraphExecutor(
        IHttpClientFactory httpClientFactory,
        ITenantEmulator emulator,
        IPayloadRenderer renderer,
        ITokenProvider tokenProvider,
        ILogger<AzureResourceGraphExecutor> logger)
        : base(httpClientFactory, emulator, renderer, tokenProvider, logger)
    {
    }

    public override ExecutorType ExecutorType => ExecutorType.AzureResourceGraph;

    protected override string HttpClientName => "arm";

    protected override string Scope => "https://management.azure.com/.default";
}
