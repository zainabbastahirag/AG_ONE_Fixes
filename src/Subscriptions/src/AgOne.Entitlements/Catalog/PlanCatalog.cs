using AgOne.Entitlements.Abstractions;
using AgOne.Entitlements.Domain;
using static AgOne.Entitlements.Catalog.FeatureKeys;
using static AgOne.Entitlements.Catalog.PlanKeys;

namespace AgOne.Entitlements.Catalog;

/// <summary>
/// The canonical, code-authored plan/feature catalog. This is the single source of truth
/// for what each feature unlocks and which features each plan bundles. It is also the exact
/// content the SQL seed (billing.Features / billing.Plans / billing.FeaturePermissions /
/// billing.PlanFeatures) mirrors, so the DB and code never drift.
/// <para>
/// Higher hire tiers are authored cumulatively: Starter ⊂ Lite ⊂ Standard ⊂ Enterprise.
/// </para>
/// </summary>
public sealed class PlanCatalog : ICatalogProvider
{
    private readonly Dictionary<string, Feature> _features;
    private readonly Dictionary<string, Plan> _plans;

    public IReadOnlyCollection<Feature> Features => _features.Values;
    public IReadOnlyCollection<Plan> Plans => _plans.Values;

    public Feature? FindFeature(string featureKey) =>
        featureKey is not null && _features.TryGetValue(featureKey, out var f) ? f : null;

    public Plan? FindPlan(string planKey) =>
        planKey is not null && _plans.TryGetValue(planKey, out var p) ? p : null;

    public PlanCatalog()
    {
        _features = BuildFeatures().ToDictionary(f => f.Key, StringComparer.OrdinalIgnoreCase);
        _plans = BuildPlans().ToDictionary(p => p.Key, StringComparer.OrdinalIgnoreCase);
    }

    private static IEnumerable<Feature> BuildFeatures()
    {
        // ---------- Hire (HR) ----------
        yield return F(EmployeeMasterData, ProductKey.Hire, "Employee Master Data",
            "Central employee records, demographics, contacts",
            "hire.employee.read", "hire.employee.write", "hire.demographics.read", "hire.contacts.read");
        yield return F(EmploymentHistory, ProductKey.Hire, "Employment History",
            "Job history, transfers, promotion tracking",
            "hire.employment_history.read", "hire.transfer.manage", "hire.promotion.track");
        yield return F(DocumentManagement, ProductKey.Hire, "Document Management",
            "Employee document storage",
            "hire.document.read", "hire.document.write");
        yield return F(OrgChart, ProductKey.Hire, "Org Chart Visualization",
            "Interactive org hierarchy views",
            "hire.orgchart.view");
        yield return F(PunchClock, ProductKey.Hire, "Punch Clock / Time Entry",
            "Employee clock-in/out",
            "hire.attendance.punch", "hire.attendance.read");
        yield return F(Geofencing, ProductKey.Hire, "Geofencing / GPS Tracking",
            "Location-based attendance",
            "hire.attendance.geofence");
        yield return F(LeaveRequest, ProductKey.Hire, "Leave Request Workflow",
            "Request and approve leave",
            "hire.leave.request", "hire.leave.approve");
        yield return F(LeaveBalance, ProductKey.Hire, "Leave Balance Tracking",
            "Accrual and entitlement management",
            "hire.leave.balance.read", "hire.leave.accrual.manage");
        yield return F(LeaveCalendar, ProductKey.Hire, "Leave Calendar View",
            "Team availability visibility",
            "hire.leave.calendar.view");
        yield return F(Onboarding, ProductKey.Hire, "Onboarding Workflows",
            "New hire task automation",
            "hire.onboarding.manage");
        yield return F(Preboarding, ProductKey.Hire, "Preboarding Portal",
            "Pre-start engagement",
            "hire.preboarding.access");
        yield return F(PersonalProfile, ProductKey.Hire, "Personal Profile Management",
            "Update own data",
            "hire.profile.self.manage");
        yield return F(PersonalizedHome, ProductKey.Hire, "Personalized Home",
            "Role-based landing pages",
            "hire.home.personalized");
        yield return F(SocialLearning, ProductKey.Hire, "Social Learning",
            "Peer collaboration and sharing",
            "hire.social.post", "hire.social.share");
        yield return F(PushNotifications, ProductKey.Hire, "Push Notifications",
            "Real-time alerts",
            "hire.notifications.push");
        yield return F(RestApi, ProductKey.Hire, "REST API",
            "Modern RESTful API access",
            "hire.api.read", "hire.api.write");
        yield return F(Webhooks, ProductKey.Hire, "Webhooks",
            "Real-time event notifications",
            "hire.webhook.manage");
        yield return F(SsoSaml, ProductKey.Hire, "SSO / SAML",
            "Single sign-on support",
            "hire.sso.configure");
        yield return F(ScimProvisioning, ProductKey.Hire, "SCIM Provisioning",
            "Automated user provisioning",
            "hire.scim.provision");
        yield return F(Microsoft365, ProductKey.Hire, "Microsoft 365 Integration",
            "Teams, Outlook, SharePoint",
            "hire.integration.m365");
        yield return F(DataImport, ProductKey.Hire, "Data Import (CSV/Excel)",
            "Bulk data upload",
            "hire.data.import");
        // Compliance features unlock no runtime permission; they are attestations surfaced in UI.
        yield return F(Iso27001, ProductKey.Hire, "ISO 27001", "ISO 27001 certification");
        yield return F(Soc2, ProductKey.Hire, "SOC 2 Type 2", "SOC 2 Type 2 attestation");
        // AI — Coming Soon: catalog-visible, never entitles access until launched.
        yield return FComing(AiAssistant, ProductKey.Hire, "Agentic AI Assistant / Chatbot",
            "AI-native assistant", "hire.ai.assistant");
        yield return FComing(AiNative, ProductKey.Hire, "AI-Native Architecture",
            "AI-native platform architecture");

        // ---------- Learn ----------
        yield return F(LearnCourseCatalog, ProductKey.Learn, "Course Catalog",
            "Browse and manage courses", "learn.course.read");
        yield return F(LearnEnrollment, ProductKey.Learn, "Enrollment",
            "Enroll and track learners", "learn.enrollment.manage");
        yield return F(LearnAssessments, ProductKey.Learn, "Assessments",
            "Quizzes and grading", "learn.assessment.manage");
        yield return F(LearnSocial, ProductKey.Learn, "Social Learning",
            "Peer collaboration and sharing", "learn.social.post", "learn.social.share");
        yield return F(LearnRestApi, ProductKey.Learn, "REST API",
            "Modern RESTful API access", "learn.api.read", "learn.api.write");
        yield return F(LearnSso, ProductKey.Learn, "SSO / SAML",
            "Single sign-on support", "learn.sso.configure");

        // ---------- Work ----------
        yield return F(WorkTaskManagement, ProductKey.Work, "Task Management",
            "Create and track tasks", "work.task.read", "work.task.write");
        yield return F(WorkTimesheets, ProductKey.Work, "Timesheets",
            "Log and approve time", "work.timesheet.manage");
        yield return F(WorkApprovals, ProductKey.Work, "Approvals",
            "Approval workflows", "work.approval.manage");
        yield return F(WorkRestApi, ProductKey.Work, "REST API",
            "Modern RESTful API access", "work.api.read", "work.api.write");
        yield return F(WorkSso, ProductKey.Work, "SSO / SAML",
            "Single sign-on support", "work.sso.configure");
    }

    private static IEnumerable<Plan> BuildPlans()
    {
        // Hire tiers — cumulative.
        var starter = new[]
        {
            EmployeeMasterData, PersonalProfile, PersonalizedHome, DataImport
        };
        var lite = starter.Concat(new[]
        {
            EmploymentHistory, DocumentManagement, PunchClock, LeaveRequest, PushNotifications
        }).ToArray();
        var standard = lite.Concat(new[]
        {
            OrgChart, Geofencing, LeaveBalance, LeaveCalendar, Onboarding, SocialLearning, RestApi, Microsoft365
        }).ToArray();
        var enterprise = standard.Concat(new[]
        {
            Preboarding, Webhooks, SsoSaml, ScimProvisioning, Iso27001, Soc2, AiAssistant, AiNative
        }).ToArray();

        yield return P(HireStarter, ProductKey.Hire, PlanTier.Starter, "Starter", "Essential HR", starter);
        yield return P(HireLite, ProductKey.Hire, PlanTier.Lite, "Lite", "Core HR and Employee Operations", lite);
        yield return P(HireStandard, ProductKey.Hire, PlanTier.Standard, "Standard", "Full HR Operations", standard);
        yield return P(HireEnterprise, ProductKey.Hire, PlanTier.Enterprise, "Enterprise", "Scale Organization", enterprise);

        // Learn tiers.
        var learnStarter = new[] { LearnCourseCatalog, LearnEnrollment };
        var learnStandard = learnStarter.Concat(new[] { LearnAssessments, LearnSocial }).ToArray();
        var learnEnterprise = learnStandard.Concat(new[] { LearnRestApi, LearnSso }).ToArray();
        yield return P(LearnStarter, ProductKey.Learn, PlanTier.Starter, "Starter", "Essential Learning", learnStarter);
        yield return P(LearnStandard, ProductKey.Learn, PlanTier.Standard, "Standard", "Full Learning", learnStandard);
        yield return P(LearnEnterprise, ProductKey.Learn, PlanTier.Enterprise, "Enterprise", "Scale Learning", learnEnterprise);

        // Work tiers.
        var workStarter = new[] { WorkTaskManagement };
        var workStandard = workStarter.Concat(new[] { WorkTimesheets, WorkApprovals }).ToArray();
        var workEnterprise = workStandard.Concat(new[] { WorkRestApi, WorkSso }).ToArray();
        yield return P(WorkStarter, ProductKey.Work, PlanTier.Starter, "Starter", "Essential Work", workStarter);
        yield return P(WorkStandard, ProductKey.Work, PlanTier.Standard, "Standard", "Full Work", workStandard);
        yield return P(WorkEnterprise, ProductKey.Work, PlanTier.Enterprise, "Enterprise", "Scale Work", workEnterprise);
    }

    private static Feature F(string key, ProductKey product, string name, string? desc, params string[] perms) =>
        new() { Key = key, Product = product, Name = name, Description = desc, PermissionCodes = perms };

    private static Feature FComing(string key, ProductKey product, string name, string? desc, params string[] perms) =>
        new() { Key = key, Product = product, Name = name, Description = desc, IsComingSoon = true, PermissionCodes = perms };

    private static Plan P(string key, ProductKey product, PlanTier tier, string name, string? desc, string[] featureKeys) =>
        new() { Key = key, Product = product, Tier = tier, Name = name, Description = desc, FeatureKeys = featureKeys };
}
