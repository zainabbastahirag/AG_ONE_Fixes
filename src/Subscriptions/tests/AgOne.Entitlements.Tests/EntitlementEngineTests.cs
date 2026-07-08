using AgOne.Entitlements.Catalog;
using AgOne.Entitlements.Domain;
using AgOne.Entitlements.Services;
using Xunit;

namespace AgOne.Entitlements.Tests;

public class EntitlementEngineTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 8, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid Tenant = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid User = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static PlanCatalog Catalog() => new();

    private static (IEntitlementService ent, IAccessResolver acc, FakeSubscriptionStore subs, FakeUserPermissionProvider rbac, FixedClock clock)
        Build()
    {
        var catalog = Catalog();
        var clock = new FixedClock(Now);
        var subs = new FakeSubscriptionStore();
        var rbac = new FakeUserPermissionProvider();
        var ent = new EntitlementService(subs, catalog, clock);
        var acc = new AccessResolver(ent, rbac, catalog);
        return (ent, acc, subs, rbac, clock);
    }

    private static Subscription Sub(string planKey, ProductKey product,
        SubscriptionStatus status = SubscriptionStatus.Active,
        DateTimeOffset? periodEnd = null, DateTimeOffset? trialEnd = null,
        params string[] addOns) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = Tenant,
        Product = product,
        PlanKey = planKey,
        Status = status,
        StartUtc = Now.AddMonths(-1),
        CurrentPeriodEndUtc = periodEnd,
        TrialEndUtc = trialEnd,
        AddOnFeatureKeys = addOns
    };

    // ---------------------------------------------------------------- Catalog

    [Fact]
    public void Hire_tiers_are_cumulative()
    {
        var c = Catalog();
        var starter = c.FindPlan(PlanKeys.HireStarter)!.FeatureKeys.ToHashSet();
        var lite = c.FindPlan(PlanKeys.HireLite)!.FeatureKeys.ToHashSet();
        var standard = c.FindPlan(PlanKeys.HireStandard)!.FeatureKeys.ToHashSet();
        var enterprise = c.FindPlan(PlanKeys.HireEnterprise)!.FeatureKeys.ToHashSet();

        Assert.True(starter.IsProperSubsetOf(lite));
        Assert.True(lite.IsProperSubsetOf(standard));
        Assert.True(standard.IsProperSubsetOf(enterprise));
    }

    [Fact]
    public void Every_plan_feature_key_exists_in_catalog()
    {
        var c = Catalog();
        foreach (var plan in c.Plans)
            foreach (var fk in plan.FeatureKeys)
                Assert.True(c.FindFeature(fk) is not null, $"Missing feature '{fk}' referenced by plan '{plan.Key}'.");
    }

    // ---------------------------------------------------------------- Snapshot

    [Fact]
    public async Task Standard_plan_entitles_orgchart_but_starter_does_not()
    {
        var (ent, _, subs, _, _) = Build();

        subs.Add(Sub(PlanKeys.HireStandard, ProductKey.Hire));
        var standard = await ent.GetSnapshotAsync(Tenant, ProductKey.Hire);
        Assert.True(standard.HasFeature(FeatureKeys.OrgChart));
        Assert.True(standard.EntitlesPermission("hire.orgchart.view"));

        subs.Add(Sub(PlanKeys.HireStarter, ProductKey.Hire));
        var starter = await ent.GetSnapshotAsync(Tenant, ProductKey.Hire);
        Assert.False(starter.HasFeature(FeatureKeys.OrgChart));
        Assert.False(starter.EntitlesPermission("hire.orgchart.view"));
    }

    [Fact]
    public async Task ComingSoon_features_never_entitle_even_on_enterprise()
    {
        var (ent, _, subs, _, _) = Build();
        subs.Add(Sub(PlanKeys.HireEnterprise, ProductKey.Hire));

        var snap = await ent.GetSnapshotAsync(Tenant, ProductKey.Hire);

        Assert.False(snap.HasFeature(FeatureKeys.AiAssistant));
        Assert.False(snap.EntitlesPermission("hire.ai.assistant"));
    }

    [Fact]
    public async Task No_subscription_denies_all_access()
    {
        var (ent, _, _, _, _) = Build();
        var snap = await ent.GetSnapshotAsync(Tenant, ProductKey.Hire);

        Assert.False(snap.AccessGranted);
        Assert.Empty(snap.EntitledPermissionCodes);
    }

    [Fact]
    public async Task AddOn_feature_grants_its_permissions_on_top_of_plan()
    {
        var (ent, _, subs, _, _) = Build();
        // Starter plan + Document Management add-on.
        subs.Add(Sub(PlanKeys.HireStarter, ProductKey.Hire,
            addOns: FeatureKeys.DocumentManagement));

        var snap = await ent.GetSnapshotAsync(Tenant, ProductKey.Hire);

        Assert.True(snap.HasFeature(FeatureKeys.DocumentManagement));
        Assert.True(snap.EntitlesPermission("hire.document.write"));
    }

    // ---------------------------------------------------------------- RBAC ∩ entitlement

    [Fact]
    public async Task Allowed_only_when_role_grants_AND_plan_includes()
    {
        var (_, acc, subs, rbac, _) = Build();
        subs.Add(Sub(PlanKeys.HireStandard, ProductKey.Hire));
        rbac.Grant(User, "hire.employee.write");

        var decision = await acc.AuthorizePermissionAsync(Tenant, User, "hire.employee.write");

        Assert.True(decision.Allowed);
    }

    [Fact]
    public async Task Role_grants_but_plan_missing_feature_is_upsell()
    {
        var (_, acc, subs, rbac, _) = Build();
        subs.Add(Sub(PlanKeys.HireStarter, ProductKey.Hire)); // no org chart
        rbac.Grant(User, "hire.orgchart.view");

        var decision = await acc.AuthorizePermissionAsync(Tenant, User, "hire.orgchart.view");

        Assert.False(decision.Allowed);
        Assert.Equal(AccessDenyReason.NotIncludedInPlan, decision.Reason);
    }

    [Fact]
    public async Task Plan_includes_but_role_missing_is_forbidden()
    {
        var (_, acc, subs, _, _) = Build();
        subs.Add(Sub(PlanKeys.HireStandard, ProductKey.Hire)); // includes org chart
        // user granted nothing

        var decision = await acc.AuthorizePermissionAsync(Tenant, User, "hire.orgchart.view");

        Assert.False(decision.Allowed);
        Assert.Equal(AccessDenyReason.NotPermittedByRole, decision.Reason);
    }

    [Fact]
    public async Task Effective_permissions_are_intersection_of_rbac_and_plan()
    {
        var (_, acc, subs, rbac, _) = Build();
        subs.Add(Sub(PlanKeys.HireLite, ProductKey.Hire));
        // Role grants more than the plan entitles.
        rbac.Grant(User, "hire.employee.write", "hire.document.write", "hire.orgchart.view");

        var effective = await acc.GetEffectivePermissionsAsync(Tenant, User, ProductKey.Hire);

        Assert.Contains("hire.employee.write", effective);  // in Lite + role
        Assert.Contains("hire.document.write", effective);  // in Lite + role
        Assert.DoesNotContain("hire.orgchart.view", effective); // role has it, Lite does not
    }

    // ---------------------------------------------------------------- Billing lifecycle

    [Fact]
    public async Task Expired_subscription_blocks_even_permitted_users()
    {
        var (_, acc, subs, rbac, _) = Build();
        subs.Add(Sub(PlanKeys.HireEnterprise, ProductKey.Hire, SubscriptionStatus.Expired));
        rbac.Grant(User, "hire.employee.write");

        var decision = await acc.AuthorizePermissionAsync(Tenant, User, "hire.employee.write");

        Assert.False(decision.Allowed);
        Assert.Equal(AccessDenyReason.SubscriptionInactive, decision.Reason);
    }

    [Fact]
    public async Task PastDue_within_grace_still_grants_but_after_grace_denies()
    {
        var (ent, _, subs, _, clock) = Build();
        var periodEnd = Now.AddDays(-3); // ended 3 days ago
        subs.Add(new Subscription
        {
            Id = Guid.NewGuid(),
            TenantId = Tenant,
            Product = ProductKey.Hire,
            PlanKey = PlanKeys.HireStandard,
            Status = SubscriptionStatus.PastDue,
            StartUtc = Now.AddMonths(-2),
            CurrentPeriodEndUtc = periodEnd,
            GracePeriod = TimeSpan.FromDays(7)
        });

        var within = await ent.GetSnapshotAsync(Tenant, ProductKey.Hire);
        Assert.True(within.AccessGranted); // 3 days < 7 day grace

        clock.UtcNow = periodEnd.AddDays(8); // now beyond grace
        var after = await ent.GetSnapshotAsync(Tenant, ProductKey.Hire);
        Assert.False(after.AccessGranted);
    }

    [Fact]
    public async Task Trial_grants_until_it_expires()
    {
        var (ent, _, subs, _, clock) = Build();
        var trialEnd = Now.AddDays(2);
        subs.Add(Sub(PlanKeys.HireStandard, ProductKey.Hire, SubscriptionStatus.Trialing, trialEnd: trialEnd));

        Assert.True((await ent.GetSnapshotAsync(Tenant, ProductKey.Hire)).AccessGranted);

        clock.UtcNow = trialEnd.AddHours(1);
        Assert.False((await ent.GetSnapshotAsync(Tenant, ProductKey.Hire)).AccessGranted);
    }

    // ---------------------------------------------------------------- Product isolation

    [Fact]
    public async Task Learn_subscription_does_not_grant_hire_permissions()
    {
        var (_, acc, subs, rbac, _) = Build();
        subs.Add(Sub(PlanKeys.LearnEnterprise, ProductKey.Learn));
        rbac.Grant(User, "hire.employee.write", "learn.course.read");

        // hire permission -> resolves to Hire product -> no hire subscription
        var hire = await acc.AuthorizePermissionAsync(Tenant, User, "hire.employee.write");
        Assert.False(hire.Allowed);
        Assert.Equal(AccessDenyReason.SubscriptionInactive, hire.Reason);

        // learn permission -> resolves to Learn product -> granted
        var learn = await acc.AuthorizePermissionAsync(Tenant, User, "learn.course.read");
        Assert.True(learn.Allowed);
    }

    [Fact]
    public async Task Same_engine_serves_all_three_products()
    {
        var (ent, _, subs, _, _) = Build();
        subs.Add(Sub(PlanKeys.HireStarter, ProductKey.Hire));
        subs.Add(Sub(PlanKeys.LearnStandard, ProductKey.Learn));
        subs.Add(Sub(PlanKeys.WorkEnterprise, ProductKey.Work));

        Assert.True((await ent.GetSnapshotAsync(Tenant, ProductKey.Hire)).HasFeature(FeatureKeys.EmployeeMasterData));
        Assert.True((await ent.GetSnapshotAsync(Tenant, ProductKey.Learn)).HasFeature(FeatureKeys.LearnSocial));
        Assert.True((await ent.GetSnapshotAsync(Tenant, ProductKey.Work)).HasFeature(FeatureKeys.WorkRestApi));
    }

    [Fact]
    public async Task HasFeature_convenience_resolves_product_from_feature()
    {
        var (ent, _, subs, _, _) = Build();
        subs.Add(Sub(PlanKeys.HireStandard, ProductKey.Hire));

        Assert.True(await ent.HasFeatureAsync(Tenant, FeatureKeys.LeaveCalendar));
        Assert.False(await ent.HasFeatureAsync(Tenant, FeatureKeys.Preboarding)); // enterprise-only
    }
}
