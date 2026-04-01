using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace AGOne.Authorization.Handlers;

/// <summary>
/// Dynamic policy provider that creates PermissionRequirement policies on-the-fly
/// based on the [RequirePermission(...)] attribute's policy string.
/// </summary>
public sealed class PermissionPolicyProvider : IAuthorizationPolicyProvider
{
    private readonly DefaultAuthorizationPolicyProvider _fallback;

    public PermissionPolicyProvider(IOptions<AuthorizationOptions> options)
    {
        _fallback = new DefaultAuthorizationPolicyProvider(options);
    }

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync() => _fallback.GetDefaultPolicyAsync();

    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() => _fallback.GetFallbackPolicyAsync();

    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (!policyName.StartsWith(RequirePermissionAttribute.PolicyPrefix))
            return _fallback.GetPolicyAsync(policyName);

        var permissionsCsv = policyName[RequirePermissionAttribute.PolicyPrefix.Length..];
        var permissions = permissionsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries);

        var policy = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .AddRequirements(new PermissionRequirement(permissions))
            .Build();

        return Task.FromResult<AuthorizationPolicy?>(policy);
    }
}
