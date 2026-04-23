using AGONECompliance.Shared.Enums;
using AGONECompliance.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace AGONECompliance.API.Data;

public static class DbInitializer
{
    public static async Task InitializeAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await db.Database.MigrateAsync();

        if (await db.Projects.AnyAsync()) return;

        var projectId = Guid.Parse("A1B2C3D4-E5F6-7890-ABCD-EF1234567890");

        var project = new ComplianceProject
        {
            Id = projectId,
            Name = "Equities IPO - Demo Prospectus Review",
            Description = "B.3.1B Equities–IPO Demonstration Checks as provided for Vendor Demo",
            Status = JobStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
        db.Projects.Add(project);

        var rules = new List<GuidelineRule>
        {
            new()
            {
                Id = Guid.NewGuid(), ProjectId = projectId, RuleNumber = 1,
                Code = "PG-1.09/1.18", Paragraph = "PG Guidance to paragraph 1.09 of Part B and paragraph 1.18 of Part E – Prospectus exposure",
                Requirement = "The following information disclosed in the electronic copy of the prospectus for prospectus exposure may be redacted: (a) pricing of securities and related disclosures such as amount for utilisation of proceeds, market capitalisation, and pro forma effects of the issuance of the securities; (b) indicative timetable for the listing; and (c) salient terms of agreements relating to underwriting and cornerstone investors, if any.",
                Complexity = Complexity.Easy, Group = "Prospectus Exposure"
            },
            new()
            {
                Id = Guid.NewGuid(), ProjectId = projectId, RuleNumber = 2,
                Code = "PG-4.11", Paragraph = "PG 4.11",
                Requirement = "Disclose the involvement of each promoter, director, member of key senior management, or key technical personnel in the following, whether in or outside Malaysia: (a) bankruptcy/insolvency petition filed (and not struck out) in the last 10 years; (b) disqualification from acting as a director or management; (c) criminal charges/convictions or pending proceedings in the last 10 years; (d) judgments/findings involving breach of capital market law/regulatory requirement in the last 10 years; (e) civil proceedings alleging fraud/misrepresentation/dishonesty/incompetence/malpractice relating to capital markets in the last 10 years; (f) orders/rulings temporarily enjoining business practice/activity; (g) reprimand/warning by regulatory authority/exchange/professional body/government agency in the last 10 years; and (h) any unsatisfied judgment.",
                Complexity = Complexity.Easy, Group = "Director Disclosure"
            },
            new()
            {
                Id = Guid.NewGuid(), ProjectId = projectId, RuleNumber = 3,
                Code = "PG-1.06", Paragraph = "PG 1.06",
                Requirement = "The directory must contain the following details, where applicable: (a) name, designation, nationality and address of each director (including independent/non-independent); (b) name, address, and professional qualification (including memberships) of company secretary; (c) address, telephone number, email and website of registered office and head/management office; (d) names and addresses of key parties (principal adviser, legal adviser, issuing house, share registrar, underwriter, placement agent, Shariah adviser, others); (e) name, address and professional qualification (including memberships) of reporting accountant; (f) name, address and qualification of expert(s) and individuals responsible for expert reports/extracts; and (g) name of the stock exchange where shares are listed or listing is sought.",
                Complexity = Complexity.Easy, Group = "Directory"
            },
        };

        db.Rules.AddRange(rules);

        foreach (var rule in rules)
        {
            db.Checks.Add(new ComplianceCheck
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                RuleId = rule.Id,
                Result = CheckResult.Pending,
                CreatedAt = DateTime.UtcNow
            });
        }

        project.TotalChecks = rules.Count;
        project.PendingCount = rules.Count;

        await db.SaveChangesAsync();
    }
}
