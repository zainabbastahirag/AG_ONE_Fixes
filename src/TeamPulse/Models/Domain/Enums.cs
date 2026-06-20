namespace TeamPulse.Models.Domain;

public enum TeamStatus
{
    OnTrack = 0,
    AtRisk = 1,
    OnHold = 2,
    Blocked = 3,
    Completed = 4
}

public enum WorkItemStatus
{
    Backlog = 0,
    Todo = 1,
    InProgress = 2,
    InReview = 3,
    Blocked = 4,
    Done = 5
}

public enum WorkItemPriority
{
    Low = 0,
    Medium = 1,
    High = 2,
    Critical = 3
}

public enum WorkItemType
{
    Feature = 0,
    Bug = 1,
    Task = 2,
    Improvement = 3,
    Spike = 4
}

public enum ReleaseStatus
{
    Planned = 0,
    InProgress = 1,
    Testing = 2,
    Released = 3,
    OnHold = 4,
    Cancelled = 5
}

public enum InvitationStatus
{
    Pending = 0,
    Accepted = 1,
    Revoked = 2
}

public enum ReviewPeriodType
{
    Sprint = 0,
    Quarter = 1
}
