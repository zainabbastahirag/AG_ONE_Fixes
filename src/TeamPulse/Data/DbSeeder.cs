using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TeamPulse.Models.Domain;

namespace TeamPulse.Data;

public static class DbSeeder
{
    public const string AdminEmail = "admin@teampulse.app";
    public const string AdminPassword = "Admin#2026";
    public const string DefaultUserPassword = "Pulse#2026";

    public static readonly string[] Roles = { "Admin", "TechLead", "Member" };

    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;

        var context = sp.GetRequiredService<ApplicationDbContext>();
        var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = sp.GetRequiredService<RoleManager<IdentityRole>>();

        await context.Database.MigrateAsync();

        foreach (var role in Roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }

        var admin = await userManager.FindByEmailAsync(AdminEmail);
        if (admin is null)
        {
            admin = new ApplicationUser
            {
                UserName = AdminEmail,
                Email = AdminEmail,
                EmailConfirmed = true,
                FullName = "Platform Admin",
                JobTitle = "Engineering Manager",
                AvatarColor = "#4f46e5"
            };
            await userManager.CreateAsync(admin, AdminPassword);
            await userManager.AddToRoleAsync(admin, "Admin");
        }

        if (await context.Teams.AnyAsync())
            return; // Already seeded with domain data.

        async Task<ApplicationUser> EnsureLead(string fullName, string color)
        {
            var email = $"{fullName.Split('/')[0].Trim().ToLowerInvariant().Replace(" ", ".")}@teampulse.app";
            var user = await userManager.FindByEmailAsync(email);
            if (user is null)
            {
                user = new ApplicationUser
                {
                    UserName = email,
                    Email = email,
                    EmailConfirmed = true,
                    FullName = fullName,
                    JobTitle = "Tech Lead",
                    AvatarColor = color
                };
                await userManager.CreateAsync(user, DefaultUserPassword);
                await userManager.AddToRoleAsync(user, "TechLead");
            }
            return user;
        }

        var abdullah = await EnsureLead("Abdullah", "#e11d48");
        var nastaran = await EnsureLead("Nastaran", "#ea580c");
        var ricky = await EnsureLead("Ricky", "#0d9488");
        var faisal = await EnsureLead("Faisal", "#2563eb");
        var logesh = await EnsureLead("Logesh", "#7c3aed");
        var majed = await EnsureLead("Majed", "#0891b2");
        var max = await EnsureLead("Max", "#db2777");
        var qaiser = await EnsureLead("Qaiser", "#16a34a");

        var teams = new List<(Team team, string[] members, ApplicationUser? linkLead)>
        {
            (new Team { Name = "AG ONE", ColorHex = "#e11d48", Status = TeamStatus.OnTrack, TechLeadUserId = abdullah.Id,
                CurrentFocus = "Post-launch stabilization & monitoring, Role and Permissions stabilization, master data dedup sync" },
                new[] { "HEMANTH KUMAR", "Shivam", "Abdullah", "Yasmeen" }, abdullah),

            (new Team { Name = "OneWork", ColorHex = "#ea580c", Status = TeamStatus.OnTrack, TechLeadUserId = nastaran.Id,
                CurrentFocus = "Asset automation, Brio HR Automation" },
                new[] { "Javed", "Roshi", "Ramshya", "Nastaran" }, nastaran),

            (new Team { Name = "Learn", ColorHex = "#0d9488", Status = TeamStatus.OnTrack, TechLeadUserId = ricky.Id,
                CurrentFocus = "Assessment Review" },
                new[] { "Ricky", "Loc", "Than", "Faisal" }, ricky),

            (new Team { Name = "Safe", ColorHex = "#2563eb", Status = TeamStatus.OnTrack, TechLeadUserId = faisal.Id,
                CurrentFocus = "Fixing bugs and stabilizing the MVP version" },
                new[] { "Faisal", "Surya", "Karimuteopa", "Waseem" }, faisal),

            (new Team { Name = "OneFlow", ColorHex = "#7c3aed", Status = TeamStatus.OnTrack, TechLeadUserId = logesh.Id,
                CurrentFocus = "Converting to Web app from copilot agent" },
                new[] { "Logesh", "Hanis", "Fatin", "Unmesh" }, logesh),

            (new Team { Name = "Spot", ColorHex = "#65a30d", Status = TeamStatus.AtRisk, TechLeadUserId = majed.Id,
                CurrentFocus = "Converting to align with One series design and architecture, target to finish 24 April",
                KeyBlocker = "Finalizing all reported bugs before alignment & deployment (target 24/4/2026)" },
                new[] { "Ricky", "Loc", "Than", "Majed", "Hema" }, majed),

            (new Team { Name = "Pulse", ColorHex = "#1d4ed8", Status = TeamStatus.OnHold, TechLeadUserId = majed.Id,
                CurrentFocus = "AG ONE series version ready",
                KeyBlocker = "On Hold on 22/4/2026 - using resources from here on other projects" },
                new[] { "Hanis", "Fatin", "Majed", "Hema", "Rahmya", "Umashanwar" }, majed),

            (new Team { Name = "AG ONE Design", ColorHex = "#db2777", Status = TeamStatus.OnTrack, TechLeadUserId = max.Id,
                CurrentFocus = "Define centralized components for Header, Footer, Sidebar, Buttons" },
                new[] { "Geena", "Max" }, max),

            (new Team { Name = "AG ONE Experience", ColorHex = "#0891b2", Status = TeamStatus.OnTrack, TechLeadUserId = majed.Id,
                CurrentFocus = "Experience layer for the AG ONE series" },
                new[] { "Hema", "Majed", "Abdullah", "Shubank" }, majed),

            (new Team { Name = "AG ONE Sentiment Sales Agent", ColorHex = "#16a34a", Status = TeamStatus.OnTrack, TechLeadUserId = qaiser.Id,
                CurrentFocus = "Sentiment-driven sales agent prototype" },
                new[] { "Qaiser" }, qaiser),
        };

        var leadByName = new Dictionary<string, ApplicationUser>(StringComparer.OrdinalIgnoreCase)
        {
            ["Abdullah"] = abdullah,
            ["Nastaran"] = nastaran,
            ["Ricky"] = ricky,
            ["Faisal"] = faisal,
            ["Logesh"] = logesh,
            ["Majed"] = majed,
            ["Max"] = max,
            ["Qaiser"] = qaiser,
        };

        foreach (var (team, memberNames, _) in teams)
        {
            context.Teams.Add(team);
            await context.SaveChangesAsync();

            foreach (var name in memberNames)
            {
                var member = new Member
                {
                    FullName = name,
                    TeamId = team.Id,
                    RoleTitle = leadByName.ContainsKey(name) ? "Tech Lead" : "Engineer",
                    AllocationPercent = 100,
                    ApplicationUserId = leadByName.TryGetValue(name, out var lead) ? lead.Id : null
                };
                context.Members.Add(member);
            }
            await context.SaveChangesAsync();
        }

        // Sprints
        var year = 2026;
        var sprint = new Sprint
        {
            Name = "Sprint 7",
            Number = 7,
            Quarter = 2,
            Year = year,
            StartDate = new DateTime(year, 4, 14),
            EndDate = new DateTime(year, 4, 28),
            Goal = "Stabilize AG ONE series & align Spot to One series architecture",
            IsActive = true
        };
        var sprintPrev = new Sprint
        {
            Name = "Sprint 6",
            Number = 6,
            Quarter = 2,
            Year = year,
            StartDate = new DateTime(year, 3, 31),
            EndDate = new DateTime(year, 4, 13),
            Goal = "MVP stabilization across teams",
            IsActive = false
        };
        context.Sprints.AddRange(sprintPrev, sprint);
        await context.SaveChangesAsync();

        var agOne = await context.Teams.FirstAsync(t => t.Name == "AG ONE");
        var spot = await context.Teams.FirstAsync(t => t.Name == "Spot");
        var safe = await context.Teams.FirstAsync(t => t.Name == "Safe");

        async Task<Member?> M(string name, int teamId) =>
            await context.Members.FirstOrDefaultAsync(m => m.FullName == name && m.TeamId == teamId);

        var workItems = new List<WorkItem>
        {
            new() { Title = "Role & permission stabilization", TeamId = agOne.Id, SprintId = sprint.Id,
                Status = WorkItemStatus.InProgress, Priority = WorkItemPriority.High, Type = WorkItemType.Improvement,
                StoryPoints = 5, AssignedMemberId = (await M("Abdullah", agOne.Id))?.Id },
            new() { Title = "Master data dedup sync", TeamId = agOne.Id, SprintId = sprint.Id,
                Status = WorkItemStatus.InReview, Priority = WorkItemPriority.Medium, Type = WorkItemType.Task,
                StoryPoints = 3, AssignedMemberId = (await M("HEMANTH KUMAR", agOne.Id))?.Id },
            new() { Title = "Align Spot to One series architecture", TeamId = spot.Id, SprintId = sprint.Id,
                Status = WorkItemStatus.InProgress, Priority = WorkItemPriority.Critical, Type = WorkItemType.Feature,
                StoryPoints = 8, DueDate = new DateTime(year, 4, 24), AssignedMemberId = (await M("Majed", spot.Id))?.Id },
            new() { Title = "Triage reported bugs before deployment", TeamId = spot.Id, SprintId = sprint.Id,
                Status = WorkItemStatus.Todo, Priority = WorkItemPriority.High, Type = WorkItemType.Bug,
                StoryPoints = 5, AssignedMemberId = (await M("Hema", spot.Id))?.Id },
            new() { Title = "Stabilize MVP - critical bug fixes", TeamId = safe.Id, SprintId = sprint.Id,
                Status = WorkItemStatus.InProgress, Priority = WorkItemPriority.High, Type = WorkItemType.Bug,
                StoryPoints = 5, AssignedMemberId = (await M("Faisal", safe.Id))?.Id },
            new() { Title = "Backlog grooming for next sprint", TeamId = agOne.Id,
                Status = WorkItemStatus.Backlog, Priority = WorkItemPriority.Low, Type = WorkItemType.Task, StoryPoints = 2 },
        };
        context.WorkItems.AddRange(workItems);

        var releases = new List<Release>
        {
            new() { Name = "AG ONE GA", Version = "1.0.0", TeamId = agOne.Id, Status = ReleaseStatus.Released,
                ReleasedDate = new DateTime(year, 3, 15), ProgressPercent = 100, Notes = "General availability launch." },
            new() { Name = "Spot One-Series Alignment", Version = "0.9.0", TeamId = spot.Id, Status = ReleaseStatus.Testing,
                TargetDate = new DateTime(year, 4, 24), ProgressPercent = 80, Notes = "Pending final bug bash." },
            new() { Name = "Safe MVP Hardening", Version = "0.5.1", TeamId = safe.Id, Status = ReleaseStatus.InProgress,
                TargetDate = new DateTime(year, 5, 10), ProgressPercent = 55 },
        };
        context.Releases.AddRange(releases);
        await context.SaveChangesAsync();

        // Reviewer assignment + sample review: Abdullah reviews Hemanth
        var hemanth = await M("HEMANTH KUMAR", agOne.Id);
        if (hemanth != null)
        {
            context.ReviewerAssignments.Add(new ReviewerAssignment
            {
                ReviewerUserId = abdullah.Id,
                MemberId = hemanth.Id,
                Note = "Quarterly performance review owner"
            });
            context.PerformanceReviews.Add(new PerformanceReview
            {
                MemberId = hemanth.Id,
                ReviewerUserId = abdullah.Id,
                SprintId = sprint.Id,
                PeriodType = ReviewPeriodType.Sprint,
                Quarter = 2,
                Year = year,
                RatingDelivery = 4,
                RatingQuality = 4,
                RatingCollaboration = 5,
                RatingOwnership = 4,
                RatingInnovation = 3,
                Strengths = "Strong delivery on master data sync, great collaboration.",
                Improvements = "Add more automated test coverage.",
                Comments = "Solid sprint contribution."
            });
            await context.SaveChangesAsync();
        }
    }
}
