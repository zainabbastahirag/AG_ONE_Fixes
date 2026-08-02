using AgOneSafe.Domain;
using AgOneSafe.Domain.Catalog;
using AgOneSafe.Domain.Tenants;

namespace AgOneSafe.Application.Abstractions;

/// <summary>
/// Everything the engine needs to run one <see cref="ControlAction"/> against one tenant.
/// The engine never knows whether the work ends up as PowerShell, a Graph call or an ARM request.
/// </summary>
public sealed class ExecutionRequest
{
    public required TenantConnection Tenant { get; init; }
    public required ControlDefinition Control { get; init; }
    public required ControlAction Action { get; init; }

    /// <summary>Rehearsal mode: executors must not write to the tenant.</summary>
    public bool DryRun { get; init; }

    /// <summary>Values merged into the payload, on top of the action's stored parameters.</summary>
    public IReadOnlyDictionary<string, string> Parameters { get; init; } =
        new Dictionary<string, string>();

    public CancellationToken CancellationToken { get; init; }
}

/// <summary>Raw outcome of an executor run, before any pass/fail judgement is applied.</summary>
public sealed class ExecutionResult
{
    public bool Succeeded { get; init; }

    /// <summary>JSON document the action produced. Assertions are evaluated against this.</summary>
    public string Json { get; init; } = "{}";

    /// <summary>Human-readable transcript (stdout, HTTP response, instructions).</summary>
    public string Output { get; init; } = string.Empty;

    public string? Error { get; init; }

    /// <summary>The literal command executed, retained as audit evidence.</summary>
    public string ExecutedCommand { get; init; } = string.Empty;

    public int DurationMs { get; init; }

    /// <summary>True when the action needs a human, e.g. a portal-only check.</summary>
    public bool RequiresManualAction { get; init; }

    public static ExecutionResult Failure(string error, string command = "", int durationMs = 0) => new()
    {
        Succeeded = false,
        Error = error,
        ExecutedCommand = command,
        DurationMs = durationMs,
        Json = "{}"
    };
}

/// <summary>
/// Pluggable runtime. Register a new implementation and the catalog can immediately target it -
/// this is the extension point for "run any PowerShell script or any API".
/// </summary>
public interface IControlExecutor
{
    ExecutorType ExecutorType { get; }

    /// <summary>False when the runtime is missing on this host (e.g. pwsh not installed).</summary>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);

    Task<ExecutionResult> ExecuteAsync(ExecutionRequest request);
}

/// <summary>
/// Resolves the executor bound to an action. Executors decide for themselves whether a request
/// goes to the real tenant or the emulator, based on <see cref="TenantConnection.ExecutionMode"/>.
/// </summary>
public interface IExecutorRegistry
{
    IControlExecutor Resolve(ExecutorType executorType);

    IReadOnlyCollection<IControlExecutor> All { get; }
}
