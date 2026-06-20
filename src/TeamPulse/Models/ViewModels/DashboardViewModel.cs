using TeamPulse.Models.Domain;

namespace TeamPulse.Models.ViewModels;

public class DashboardViewModel
{
    public int TotalTeams { get; set; }
    public int TotalMembers { get; set; }
    public int TeamsOnTrack { get; set; }
    public int TeamsAtRisk { get; set; }
    public int TeamsOnHold { get; set; }
    public int OpenWorkItems { get; set; }
    public int BlockedWorkItems { get; set; }
    public int CompletedWorkItems { get; set; }
    public int UpcomingReleases { get; set; }
    public int ReviewsThisQuarter { get; set; }
    public double AverageRating { get; set; }
    public List<Team> Teams { get; set; } = new();
    public List<Release> RecentReleases { get; set; } = new();
}
