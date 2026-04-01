namespace AGOne.Authorization.Constants;

/// <summary>
/// Compile-time permission code constants for every product.
/// Use these with [RequirePermission(Permissions.AgOne.Users.Create)] or policy checks.
/// </summary>
public static class Permissions
{
    public static class AgOne
    {
        public static class Users
        {
            public const string Create = "agone.users.create";
            public const string Read   = "agone.users.read";
            public const string Update = "agone.users.update";
            public const string Delete = "agone.users.delete";
        }

        public static class Roles
        {
            public const string Create = "agone.roles.create";
            public const string Read   = "agone.roles.read";
            public const string Update = "agone.roles.update";
            public const string Delete = "agone.roles.delete";
        }

        public static class PermissionsMgmt
        {
            public const string Create = "agone.permissions.create";
            public const string Read   = "agone.permissions.read";
            public const string Update = "agone.permissions.update";
            public const string Delete = "agone.permissions.delete";
        }

        public static class Tenant
        {
            public const string Create = "agone.tenant.create";
            public const string Read   = "agone.tenant.read";
            public const string Update = "agone.tenant.update";
            public const string Delete = "agone.tenant.delete";
        }

        public static class Subscription
        {
            public const string Create = "agone.subscription.create";
            public const string Read   = "agone.subscription.read";
            public const string Update = "agone.subscription.update";
            public const string Delete = "agone.subscription.delete";
        }

        public static class Billing
        {
            public const string Create = "agone.billing.create";
            public const string Read   = "agone.billing.read";
            public const string Update = "agone.billing.update";
            public const string Delete = "agone.billing.delete";
        }

        public static class MasterData
        {
            public const string Create = "agone.masterdata.create";
            public const string Read   = "agone.masterdata.read";
            public const string Update = "agone.masterdata.update";
            public const string Delete = "agone.masterdata.delete";
        }

        public static class Audit
        {
            public const string Read = "agone.audit.read";
        }
    }

    public static class Work
    {
        public static class Employee
        {
            public const string Create = "work.employee.create";
            public const string Read   = "work.employee.read";
            public const string Update = "work.employee.update";
            public const string Delete = "work.employee.delete";
        }

        public static class Recruitment
        {
            public const string Create = "work.recruitment.create";
            public const string Read   = "work.recruitment.read";
            public const string Update = "work.recruitment.update";
            public const string Delete = "work.recruitment.delete";
        }

        public static class ActivateProfile
        {
            public const string Create = "work.activate.create";
            public const string Read   = "work.activate.read";
            public const string Update = "work.activate.update";
            public const string Delete = "work.activate.delete";
        }

        public static class MasterData
        {
            public const string Create = "work.masterdata.create";
            public const string Read   = "work.masterdata.read";
            public const string Update = "work.masterdata.update";
            public const string Delete = "work.masterdata.delete";
        }
    }

    public static class Learn
    {
        public static class LearningPath
        {
            public const string Create = "learn.path.create";
            public const string Read   = "learn.path.read";
            public const string Update = "learn.path.update";
            public const string Delete = "learn.path.delete";
        }

        public static class DataSource
        {
            public const string Create = "learn.datasource.create";
            public const string Read   = "learn.datasource.read";
            public const string Update = "learn.datasource.update";
            public const string Delete = "learn.datasource.delete";
        }

        public static class Assignment
        {
            public const string Create = "learn.assignment.create";
            public const string Read   = "learn.assignment.read";
            public const string Update = "learn.assignment.update";
            public const string Delete = "learn.assignment.delete";
        }

        public static class Assessment
        {
            public const string Create = "learn.assessment.create";
            public const string Read   = "learn.assessment.read";
            public const string Update = "learn.assessment.update";
            public const string Delete = "learn.assessment.delete";
        }
    }

    public static class Safe
    {
        public static class Policy
        {
            public const string Create = "safe.policy.create";
            public const string Read   = "safe.policy.read";
            public const string Update = "safe.policy.update";
            public const string Delete = "safe.policy.delete";
        }

        public static class Compliance
        {
            public const string Create = "safe.compliance.create";
            public const string Read   = "safe.compliance.read";
            public const string Update = "safe.compliance.update";
            public const string Delete = "safe.compliance.delete";
        }

        public static class DataLibrary
        {
            public const string Create = "safe.datalibrary.create";
            public const string Read   = "safe.datalibrary.read";
            public const string Update = "safe.datalibrary.update";
            public const string Delete = "safe.datalibrary.delete";
        }
    }

    public static class Pulse
    {
        public static class Survey
        {
            public const string Create = "pulse.survey.create";
            public const string Read   = "pulse.survey.read";
            public const string Update = "pulse.survey.update";
            public const string Delete = "pulse.survey.delete";
        }

        public static class Analytics
        {
            public const string Read = "pulse.analytics.read";
        }
    }

    public static class Spot
    {
        public static class CityData
        {
            public const string Create = "spot.city.create";
            public const string Read   = "spot.city.read";
            public const string Update = "spot.city.update";
            public const string Delete = "spot.city.delete";
        }
    }

    public const string ClaimType = "permissions";
    public const string PermissionVersionClaim = "perm_version";
}
