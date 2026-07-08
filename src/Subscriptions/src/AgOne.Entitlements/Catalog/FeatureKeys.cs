namespace AgOne.Entitlements.Catalog;

/// <summary>
/// Strongly-typed feature keys. Keep these as constants so controllers/views reference
/// features without magic strings. Keys are stable identifiers; names/descriptions live in the catalog.
/// </summary>
public static class FeatureKeys
{
    // ---- Hire (HR) product : mirrors the commercial plan matrix ----
    public const string EmployeeMasterData = "hire.employee_master_data";
    public const string EmploymentHistory = "hire.employment_history";
    public const string DocumentManagement = "hire.document_management";
    public const string OrgChart = "hire.org_chart";
    public const string PunchClock = "hire.punch_clock";
    public const string Geofencing = "hire.geofencing";
    public const string LeaveRequest = "hire.leave_request";
    public const string LeaveBalance = "hire.leave_balance";
    public const string LeaveCalendar = "hire.leave_calendar";
    public const string Onboarding = "hire.onboarding";
    public const string Preboarding = "hire.preboarding";
    public const string PersonalProfile = "hire.personal_profile";
    public const string PersonalizedHome = "hire.personalized_home";
    public const string SocialLearning = "hire.social_learning";
    public const string PushNotifications = "hire.push_notifications";
    public const string RestApi = "hire.rest_api";
    public const string Webhooks = "hire.webhooks";
    public const string SsoSaml = "hire.sso_saml";
    public const string ScimProvisioning = "hire.scim";
    public const string Microsoft365 = "hire.m365";
    public const string DataImport = "hire.data_import";
    public const string Iso27001 = "hire.iso27001";
    public const string Soc2 = "hire.soc2";
    public const string AiAssistant = "hire.ai_assistant";      // Coming Soon
    public const string AiNative = "hire.ai_native";            // Coming Soon

    // ---- Learn product ----
    public const string LearnCourseCatalog = "learn.course_catalog";
    public const string LearnEnrollment = "learn.enrollment";
    public const string LearnAssessments = "learn.assessments";
    public const string LearnSocial = "learn.social_learning";
    public const string LearnRestApi = "learn.rest_api";
    public const string LearnSso = "learn.sso_saml";

    // ---- Work product ----
    public const string WorkTaskManagement = "work.task_management";
    public const string WorkTimesheets = "work.timesheets";
    public const string WorkApprovals = "work.approvals";
    public const string WorkRestApi = "work.rest_api";
    public const string WorkSso = "work.sso_saml";
}

/// <summary>Stable plan keys used by subscription rows.</summary>
public static class PlanKeys
{
    public const string HireStarter = "hire.starter";
    public const string HireLite = "hire.lite";
    public const string HireStandard = "hire.standard";
    public const string HireEnterprise = "hire.enterprise";

    public const string LearnStarter = "learn.starter";
    public const string LearnStandard = "learn.standard";
    public const string LearnEnterprise = "learn.enterprise";

    public const string WorkStarter = "work.starter";
    public const string WorkStandard = "work.standard";
    public const string WorkEnterprise = "work.enterprise";
}
