namespace AgOneSafe.Domain;

/// <summary>Benchmark family a control belongs to.</summary>
public enum BenchmarkFamily
{
    Microsoft365 = 0,
    Azure = 1
}

/// <summary>CIS profile applicability level. Customers pick this as their target maturity.</summary>
public enum ProfileLevel
{
    L1 = 1,
    L2 = 2
}

public enum Severity
{
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4
}

/// <summary>
/// Runtime that carries out an audit or remediation action. New executors can be plugged in
/// without touching the catalog: the catalog only stores the executor name plus its payload.
/// </summary>
public enum ExecutorType
{
    PowerShell = 0,
    MicrosoftGraph = 1,
    AzureRest = 2,
    AzureResourceGraph = 3,
    Manual = 4
}

/// <summary>Workload-specific session a PowerShell action needs before it can run.</summary>
public enum ConnectorModule
{
    None = 0,
    MicrosoftGraph = 1,
    ExchangeOnline = 2,
    MicrosoftTeams = 3,
    SharePointOnline = 4,
    SecurityCompliance = 5,
    AzureResourceManager = 6
}

/// <summary>Comparison applied to a value pulled out of an executor's JSON result.</summary>
public enum AssertionOperator
{
    Equals = 0,
    NotEquals = 1,
    Contains = 2,
    NotContains = 3,
    GreaterThan = 4,
    GreaterThanOrEqual = 5,
    LessThan = 6,
    LessThanOrEqual = 7,
    Between = 8,
    IsTrue = 9,
    IsFalse = 10,
    IsEmpty = 11,
    IsNotEmpty = 12,
    Regex = 13,
    CountEquals = 14,
    CountGreaterThanOrEqual = 15,
    CountLessThanOrEqual = 16,
    CountBetween = 17,
    AllEqual = 18,
    AnyEqual = 19,
    Exists = 20,
    NotExists = 21
}

public enum AssertionLogic
{
    All = 0,
    Any = 1
}

public enum ControlOutcome
{
    Pass = 0,
    Fail = 1,
    Error = 2,
    Manual = 3,
    NotApplicable = 4,
    Skipped = 5
}

public enum FindingStatus
{
    Open = 0,
    Approved = 1,
    Scheduled = 2,
    InProgress = 3,
    Remediated = 4,
    Verified = 5,
    VerificationFailed = 6,
    RolledBack = 7,
    RiskAccepted = 8,
    BlockedByLicense = 9,
    ManualActionRequired = 10
}

public enum AssessmentStatus
{
    Queued = 0,
    Running = 1,
    Completed = 2,
    Failed = 3,
    Cancelled = 4
}

public enum AssessmentTrigger
{
    Manual = 0,
    Scheduled = 1,
    PostRemediationVerification = 2,
    Onboarding = 3
}

public enum RemediationMode
{
    DryRun = 0,
    Execute = 1
}

public enum RemediationStatus
{
    Draft = 0,
    PendingApproval = 1,
    Approved = 2,
    Rejected = 3,
    Scheduled = 4,
    Running = 5,
    Succeeded = 6,
    Failed = 7,
    RolledBack = 8,
    Cancelled = 9,
    DryRunCompleted = 10
}

public enum ApprovalDecision
{
    Pending = 0,
    Approved = 1,
    Rejected = 2
}

public enum TenantConnectionStatus
{
    NotConnected = 0,
    Connected = 1,
    Degraded = 2,
    Failed = 3
}

/// <summary>NIST CSF v2 core functions, used to roll findings up for executive reporting.</summary>
public enum NistCsfFunction
{
    Govern = 0,
    Identify = 1,
    Protect = 2,
    Detect = 3,
    Respond = 4,
    Recover = 5
}

public enum EscalationType
{
    LicenseUpgrade = 0,
    ProfessionalServices = 1
}

public enum EscalationStatus
{
    Draft = 0,
    Submitted = 1,
    InProgress = 2,
    Fulfilled = 3,
    Rejected = 4
}

/// <summary>
/// Whether executors talk to a real tenant or to the deterministic in-process tenant emulator
/// used for demos, developer machines and CI.
/// </summary>
public enum ExecutionMode
{
    Simulation = 0,
    Live = 1
}

public enum AuditEventType
{
    TenantConnected = 0,
    TenantHealthCheck = 1,
    AssessmentStarted = 2,
    AssessmentCompleted = 3,
    ControlEvaluated = 4,
    RemediationPlanned = 5,
    RemediationDryRun = 6,
    ApprovalRequested = 7,
    ApprovalGranted = 8,
    ApprovalRejected = 9,
    RemediationExecuted = 10,
    SnapshotCaptured = 11,
    RollbackExecuted = 12,
    VerificationCompleted = 13,
    EscalationRaised = 14,
    CatalogImported = 15,
    RiskAccepted = 16
}
