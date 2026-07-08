using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace AgOne.Entitlements.AspNetCore.Authorization;

/// <summary>
/// Materializes authorization policies on demand from the "perm:" / "feat:" prefixes so you
/// never have to register a named policy per permission or feature. Any string the
/// <see cref="RequirePermissionAttribute"/> or <see cref="RequireFeatureAttribute"/> produces
/// resolves to an <see cref="EntitlementRequirement"/> automatically.
/// </summary>
public sealed class EntitlementPolicyProvider : IAuthorizationPolicyProvider
{
    private readonly DefaultAuthorizationPolicyProvider _fallback;

    public EntitlementPolicyProvider(IOptions<AuthorizationOptions> options)
        => _fallback = new DefaultAuthorizationPolicyProvider(options);

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync() => _fallback.GetDefaultPolicyAsync();
    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() => _fallback.GetFallbackPolicyAsync();

    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (policyName.StartsWith(RequirePermissionAttribute.PolicyPrefix, StringComparison.Ordinal))
        {
            var code = policyName[RequirePermissionAttribute.PolicyPrefix.Length..];
            return Task.FromResult<AuthorizationPolicy?>(Build(EntitlementRequirement.ForPermission(code)));
        }

        if (policyName.StartsWith(RequireFeatureAttribute.PolicyPrefix, StringComparison.Ordinal))
        {
            var key = policyName[RequireFeatureAttribute.PolicyPrefix.Length..];
            return Task.FromResult<AuthorizationPolicy?>(Build(EntitlementRequirement.ForFeature(key)));
        }

        return _fallback.GetPolicyAsync(policyName);
    }

    private static AuthorizationPolicy Build(EntitlementRequirement requirement) =>
        new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .AddRequirements(requirement)
            .Build();
}
