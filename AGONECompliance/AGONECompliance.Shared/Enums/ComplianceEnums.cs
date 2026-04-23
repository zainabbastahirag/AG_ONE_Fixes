namespace AGONECompliance.Shared.Enums;

public enum JobStatus
{
    Pending = 0,
    Processing = 1,
    Completed = 2,
    Failed = 3
}

public enum CheckResult
{
    Pending = 0,
    Compliant = 1,
    NonCompliant = 2,
    PartiallyCompliant = 3,
    NotApplicable = 4
}

public enum DocumentType
{
    Guide = 0,
    Appendix = 1,
    Prospectus = 2
}

public enum Complexity
{
    Easy = 0,
    Medium = 1,
    Hard = 2
}
