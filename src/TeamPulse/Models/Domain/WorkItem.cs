using System.ComponentModel.DataAnnotations;

namespace TeamPulse.Models.Domain;

public class WorkItem
{
    public int Id { get; set; }

    [Required]
    [StringLength(160)]
    public string Title { get; set; } = string.Empty;

    [StringLength(2000)]
    public string? Description { get; set; }

    [Display(Name = "Team")]
    public int? TeamId { get; set; }
    public Team? Team { get; set; }

    [Display(Name = "Assignee")]
    public int? AssignedMemberId { get; set; }
    public Member? AssignedMember { get; set; }

    [Display(Name = "Sprint")]
    public int? SprintId { get; set; }
    public Sprint? Sprint { get; set; }

    public WorkItemStatus Status { get; set; } = WorkItemStatus.Backlog;
    public WorkItemPriority Priority { get; set; } = WorkItemPriority.Medium;
    public WorkItemType Type { get; set; } = WorkItemType.Task;

    [Range(0, 1000)]
    [Display(Name = "Story Points")]
    public int StoryPoints { get; set; }

    [Display(Name = "Due Date")]
    [DataType(DataType.Date)]
    public DateTime? DueDate { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
}
