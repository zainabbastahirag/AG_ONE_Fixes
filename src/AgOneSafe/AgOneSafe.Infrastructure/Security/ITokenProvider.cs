using AgOneSafe.Domain.Tenants;
using Microsoft.Extensions.Logging;

namespace AgOneSafe.Infrastructure.Security;

/// <summary>Reads a tenant credential. Production binds this to Azure Key Vault.</summary>
public interface ISecretStore
{
    Task<string?> GetSecretAsync(string reference, CancellationToken cancellationToken = default);
}

/// <summary>Issues an access token for a customer tenant using its onboarded app registration.</summary>
public interface ITokenProvider
{
    Task<string> GetAccessTokenAsync(TenantConnection tenant, string scope, CancellationToken cancellationToken = default);
}

/// <summary>
/// Development stand-in. It reads secrets from configuration so the sample runs with no cloud
/// dependency; the production registration swaps in <c>Azure.Security.KeyVault.Secrets</c> with a
/// managed identity, which is the only supported path for customer credentials.
/// </summary>
public sealed class ConfigurationSecretStore : ISecretStore
{
    private readonly Microsoft.Extensions.Configuration.IConfiguration _configuration;

    public ConfigurationSecretStore(Microsoft.Extensions.Configuration.IConfiguration configuration) =>
        _configuration = configuration;

    public Task<string?> GetSecretAsync(string reference, CancellationToken cancellationToken = default) =>
        Task.FromResult(_configuration[$"TenantSecrets:{reference}"]);
}

/// <summary>
/// Client-credentials token provider. Simulation mode never reaches the network, so the sample
/// short-circuits with a placeholder rather than failing a demo scan on a missing secret.
/// </summary>
public sealed class ClientCredentialsTokenProvider : ITokenProvider
{
    private readonly ISecretStore _secrets;
    private readonly ILogger<ClientCredentialsTokenProvider> _logger;

    public ClientCredentialsTokenProvider(ISecretStore secrets, ILogger<ClientCredentialsTokenProvider> logger)
    {
        _secrets = secrets;
        _logger = logger;
    }

    public async Task<string> GetAccessTokenAsync(
        TenantConnection tenant,
        string scope,
        CancellationToken cancellationToken = default)
    {
        if (tenant.ExecutionMode == Domain.ExecutionMode.Simulation)
        {
            return "simulation-token";
        }

        var secret = await _secrets.GetSecretAsync(tenant.SecretReference, cancellationToken);

        if (string.IsNullOrWhiteSpace(secret))
        {
            throw new InvalidOperationException(
                $"No credential is available at '{tenant.SecretReference}' for tenant {tenant.PrimaryDomain}.");
        }

        _logger.LogDebug("Acquiring {Scope} token for tenant {Tenant}", scope, tenant.TenantId);

        // Production: Azure.Identity ClientSecretCredential / ClientCertificateCredential with a
        // cached token per (tenant, scope). Kept out of the sample so it has no cloud dependency.
        throw new NotSupportedException(
            "Live token acquisition is not wired up in the sample. Register an Azure.Identity-backed " +
            "ITokenProvider in Program.cs before switching a tenant to Live mode.");
    }
}
