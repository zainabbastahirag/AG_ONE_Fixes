using System.Text;
using System.Text.Json;
using AGONECompliance.API.Data;
using AGONECompliance.Shared.Enums;
using AGONECompliance.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace AGONECompliance.API.Services;

public class ComplianceCheckService
{
    private readonly AppDbContext _db;
    private readonly IHttpClientFactory _httpFactory;
    private readonly IConfiguration _cfg;
    private readonly ILogger<ComplianceCheckService> _log;

    public ComplianceCheckService(AppDbContext db, IHttpClientFactory httpFactory, IConfiguration cfg, ILogger<ComplianceCheckService> log)
    {
        _db = db;
        _httpFactory = httpFactory;
        _cfg = cfg;
        _log = log;
    }

    public async Task RunComplianceChecksAsync(Guid projectId, CancellationToken ct = default)
    {
        var project = await _db.Projects.FindAsync(new object[] { projectId }, ct);
        if (project == null) return;

        project.Status = JobStatus.Processing;
        await _db.SaveChangesAsync(ct);

        try
        {
            var prospectus = await _db.Documents
                .Where(d => d.ProjectId == projectId && d.DocType == DocumentType.Prospectus && d.ExtractionStatus == JobStatus.Completed)
                .FirstOrDefaultAsync(ct);

            if (prospectus == null || string.IsNullOrEmpty(prospectus.ExtractedText))
            {
                project.Status = JobStatus.Failed;
                await _db.SaveChangesAsync(ct);
                _log.LogWarning("No extracted prospectus found for project {Id}", projectId);
                return;
            }

            var checks = await _db.Checks
                .Include(c => c.Rule)
                .Where(c => c.ProjectId == projectId && c.Result == CheckResult.Pending)
                .ToListAsync(ct);

            var compliant = 0;
            var nonCompliant = 0;

            foreach (var check in checks)
            {
                if (check.Rule == null) continue;

                var result = await EvaluateCheckAsync(check.Rule, prospectus.ExtractedText, ct);

                check.Result = result.Result;
                check.Finding = result.Finding;
                check.Evidence = result.Evidence;
                check.PageReference = result.PageReference;
                check.SectionReference = result.SectionReference;
                check.ConfidenceScore = result.Confidence;
                check.ProspectusDocId = prospectus.Id;
                check.CheckedAt = DateTime.UtcNow;

                if (result.Result == CheckResult.Compliant) compliant++;
                else nonCompliant++;
            }

            project.CompliantCount = compliant;
            project.NonCompliantCount = nonCompliant;
            project.PendingCount = 0;
            project.Status = JobStatus.Completed;
            project.CompletedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync(ct);
            _log.LogInformation("Compliance check complete for {Id}: {C} compliant, {NC} non-compliant",
                projectId, compliant, nonCompliant);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Compliance check failed for {Id}", projectId);
            project.Status = JobStatus.Failed;
            await _db.SaveChangesAsync(ct);
        }
    }

    private async Task<CheckEvaluation> EvaluateCheckAsync(GuidelineRule rule, string prospectusText, CancellationToken ct)
    {
        var endpoint = _cfg["Azure:OpenAIEndpoint"];
        var key = _cfg["Azure:OpenAIKey"];
        var deployment = _cfg["Azure:OpenAIDeployment"] ?? "gpt-4.1";

        if (string.IsNullOrEmpty(endpoint) || string.IsNullOrEmpty(key))
        {
            return EvaluateWithHeuristics(rule, prospectusText);
        }

        try
        {
            return await EvaluateWithAIAsync(rule, prospectusText, endpoint, key, deployment, ct);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "AI evaluation failed for rule {Num}, falling back to heuristics", rule.RuleNumber);
            return EvaluateWithHeuristics(rule, prospectusText);
        }
    }

    private async Task<CheckEvaluation> EvaluateWithAIAsync(
        GuidelineRule rule, string prospectusText, string endpoint, string key, string deployment, CancellationToken ct)
    {
        var client = _httpFactory.CreateClient();
        client.DefaultRequestHeaders.Add("api-key", key);

        var truncatedText = prospectusText.Length > 12000 ? prospectusText[..12000] : prospectusText;

        var prompt = $@"You are a compliance analyst. Evaluate whether the following prospectus text meets this regulatory requirement.

REQUIREMENT (Rule #{rule.RuleNumber}, {rule.Code}):
{rule.Requirement}

PROSPECTUS TEXT:
{truncatedText}

Respond in this exact JSON format:
{{
  ""result"": ""Compliant"" or ""NonCompliant"" or ""PartiallyCompliant"",
  ""confidence"": 0.0 to 1.0,
  ""finding"": ""Brief explanation of your finding"",
  ""evidence"": ""Exact quote from prospectus that supports your finding"",
  ""page_reference"": ""Page number(s) where evidence was found"",
  ""section_reference"": ""Section/heading where evidence was found""
}}";

        var body = new
        {
            messages = new[] { new { role = "user", content = prompt } },
            max_tokens = 500,
            temperature = 0.1
        };

        var response = await client.PostAsync(
            $"{endpoint}/openai/deployments/{deployment}/chat/completions?api-version=2024-02-01",
            new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"), ct);

        var json = await response.Content.ReadAsStringAsync(ct);
        var doc = JsonDocument.Parse(json);
        var content = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "{}";

        var start = content.IndexOf('{');
        var end = content.LastIndexOf('}');
        if (start >= 0 && end > start)
            content = content[start..(end + 1)];

        var aiResult = JsonSerializer.Deserialize<AiCheckResult>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        return new CheckEvaluation
        {
            Result = aiResult?.Result?.ToLower() switch
            {
                "compliant" => CheckResult.Compliant,
                "noncompliant" or "non-compliant" => CheckResult.NonCompliant,
                "partiallycompliant" or "partially-compliant" => CheckResult.PartiallyCompliant,
                _ => CheckResult.Pending
            },
            Confidence = aiResult?.Confidence ?? 0.5,
            Finding = aiResult?.Finding ?? "AI evaluation completed",
            Evidence = aiResult?.Evidence,
            PageReference = aiResult?.PageReference,
            SectionReference = aiResult?.SectionReference,
        };
    }

    private static CheckEvaluation EvaluateWithHeuristics(GuidelineRule rule, string text)
    {
        var keywords = ExtractKeywords(rule.Requirement);
        var textLower = text.ToLower();
        var matchCount = keywords.Count(k => textLower.Contains(k.ToLower()));
        var matchRate = keywords.Length > 0 ? (double)matchCount / keywords.Length : 0;

        var pageRef = "N/A";
        var pageMatch = System.Text.RegularExpressions.Regex.Match(text, @"\[Page (\d+)\]");
        if (pageMatch.Success) pageRef = $"Page {pageMatch.Groups[1].Value}";

        return new CheckEvaluation
        {
            Result = matchRate >= 0.6 ? CheckResult.Compliant
                   : matchRate >= 0.3 ? CheckResult.PartiallyCompliant
                   : CheckResult.NonCompliant,
            Confidence = Math.Round(matchRate, 2),
            Finding = matchRate >= 0.6
                ? $"Found {matchCount}/{keywords.Length} key requirements in the prospectus document."
                : $"Only {matchCount}/{keywords.Length} key requirements were found. Missing disclosures detected.",
            Evidence = $"Keyword match analysis: {matchCount} of {keywords.Length} required terms found.",
            PageReference = pageRef,
            SectionReference = "Full document scan",
        };
    }

    private static string[] ExtractKeywords(string requirement)
    {
        var stopWords = new HashSet<string> { "the", "and", "or", "of", "in", "to", "a", "an", "is", "are", "was", "were", "be", "been", "for", "with", "as", "by", "on", "at", "if", "any", "each", "such", "may", "must", "shall", "not", "from", "whether", "where", "which", "that", "its", "including" };

        return requirement
            .Split(new[] { ' ', ',', ';', '(', ')', '/', '-', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(w => w.Trim().ToLower().TrimEnd('.', ':'))
            .Where(w => w.Length > 3 && !stopWords.Contains(w))
            .Distinct()
            .Take(20)
            .ToArray();
    }

    private class AiCheckResult
    {
        public string? Result { get; set; }
        public double Confidence { get; set; }
        public string? Finding { get; set; }
        public string? Evidence { get; set; }
        public string? PageReference { get; set; }
        public string? SectionReference { get; set; }
    }

    private class CheckEvaluation
    {
        public CheckResult Result { get; set; }
        public double Confidence { get; set; }
        public string? Finding { get; set; }
        public string? Evidence { get; set; }
        public string? PageReference { get; set; }
        public string? SectionReference { get; set; }
    }
}
