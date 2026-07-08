using AgOne.Entitlements.Catalog;
using AgOne.Entitlements.Domain;
using AgOne.Entitlements.Gateway;
using AgOne.Entitlements.Services;
using Xunit;

namespace AgOne.Entitlements.Tests;

public class GatewayEntitlementTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 8, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid Tenant = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid User = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static Subscription Sub(string planKey, ProductKey product) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = Tenant,
        Product = product,
        PlanKey = planKey,
        Status = SubscriptionStatus.Active,
        StartUtc = Now.AddMonths(-1)
    };

    private static (GatewayEntitlementService gw, FakeSubscriptionStore subs, FakeUserPermissionProvider rbac) Build()
    {
        var catalog = new PlanCatalog();
        var subs = new FakeSubscriptionStore();
        var rbac = new FakeUserPermissionProvider();
        var ent = new EntitlementService(subs, catalog, new FixedClock(Now));
        var access = new AccessResolver(ent, rbac, catalog);
        return (new GatewayEntitlementService(access, ent), subs, rbac);
    }

    [Fact]
    public async Task Gateway_returns_effective_permissions_per_product_and_union()
    {
        var (gw, subs, rbac) = Build();
        subs.Add(Sub(PlanKeys.HireStandard, ProductKey.Hire));
        subs.Add(Sub(PlanKeys.LearnStandard, ProductKey.Learn));
        // Role grants more than each plan entitles (org chart is in Standard; a random perm is not).
        rbac.Grant(User, "hire.employee.write", "hire.orgchart.view", "hire.zzz.notinplan",
                          "learn.course.read", "learn.social.post");

        var res = await gw.BuildAsync(Tenant, User, new[] { ProductKey.Hire, ProductKey.Learn });

        var hire = res.Products.Single(p => p.Product == ProductKey.Hire);
        Assert.Contains("hire.employee.write", hire.EffectivePermissionCodes);
        Assert.Contains("hire.orgchart.view", hire.EffectivePermissionCodes);
        Assert.DoesNotContain("hire.zzz.notinplan", hire.EffectivePermissionCodes); // role has it, plan doesn't

        var learn = res.Products.Single(p => p.Product == ProductKey.Learn);
        Assert.Contains("learn.course.read", learn.EffectivePermissionCodes);
        Assert.Contains("learn.social.post", learn.EffectivePermissionCodes);

        // Union spans both products.
        Assert.Contains("hire.employee.write", res.AllPermissionCodes);
        Assert.Contains("learn.course.read", res.AllPermissionCodes);
        Assert.Equal(600, res.CacheTtlSeconds);
        Assert.False(string.IsNullOrWhiteSpace(res.Version));
    }

    [Fact]
    public async Task Version_changes_only_when_effective_access_changes()
    {
        var (gw, subs, rbac) = Build();
        subs.Add(Sub(PlanKeys.HireLite, ProductKey.Hire));
        rbac.Grant(User, "hire.employee.write");

        var v1 = (await gw.BuildAsync(Tenant, User, new[] { ProductKey.Hire })).Version;
        var v1b = (await gw.BuildAsync(Tenant, User, new[] { ProductKey.Hire })).Version;
        Assert.Equal(v1, v1b); // stable for identical inputs

        // Upgrade the tenant's plan -> effective set grows -> version must change.
        subs.Add(Sub(PlanKeys.HireEnterprise, ProductKey.Hire));
        rbac.Grant(User, "hire.orgchart.view");
        var v2 = (await gw.BuildAsync(Tenant, User, new[] { ProductKey.Hire })).Version;
        Assert.NotEqual(v1, v2);
    }

    [Fact]
    public async Task Inactive_subscription_yields_no_permissions_for_that_product()
    {
        var (gw, subs, rbac) = Build();
        subs.Add(new Subscription
        {
            Id = Guid.NewGuid(), TenantId = Tenant, Product = ProductKey.Hire,
            PlanKey = PlanKeys.HireEnterprise, Status = SubscriptionStatus.Expired, StartUtc = Now.AddMonths(-2)
        });
        rbac.Grant(User, "hire.employee.write");

        var res = await gw.BuildAsync(Tenant, User, new[] { ProductKey.Hire });
        var hire = res.Products.Single();

        Assert.False(hire.AccessGranted);
        Assert.Empty(hire.EffectivePermissionCodes);
        Assert.Empty(res.AllPermissionCodes);
    }
}
