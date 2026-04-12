using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using VidCV.Web.Data;
using VidCV.Web.Models;
using VidCV.Web.Services;

namespace VidCV.Web.Pages;

public class IndexModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly CvParserService _parser;
    private readonly ScriptGeneratorService _scriptGen;
    private readonly VideoGeneratorService _videoGen;
    private readonly IWebHostEnvironment _env;

    public IndexModel(
        AppDbContext db,
        CvParserService parser,
        ScriptGeneratorService scriptGen,
        VideoGeneratorService videoGen,
        IWebHostEnvironment env)
    {
        _db = db;
        _parser = parser;
        _scriptGen = scriptGen;
        _videoGen = videoGen;
        _env = env;
    }

    public List<VideoTemplate> Templates { get; set; } = new();
    public List<CvProfile> RecentVideos { get; set; } = new();

    [BindProperty] public IFormFile? CvFile { get; set; }
    [BindProperty] public string? LinkedInUrl { get; set; }
    [BindProperty] public string? FullName { get; set; }
    [BindProperty] public string? JobTitle { get; set; }
    [BindProperty] public string? Summary { get; set; }
    [BindProperty] public string? Skills { get; set; }
    [BindProperty] public string? Email { get; set; }
    [BindProperty] public int TemplateId { get; set; }
    [BindProperty] public string InputMode { get; set; } = "upload";

    public CvProfile? GeneratedProfile { get; set; }
    public string? ErrorMessage { get; set; }
    public string? SuccessMessage { get; set; }
    public int TotalVideosGenerated { get; set; }

    public async Task OnGetAsync()
    {
        Templates = await _db.VideoTemplates.Where(t => t.IsActive).ToListAsync();
        RecentVideos = await _db.CvProfiles
            .Where(p => p.Status == VideoStatus.Completed && p.VideoUrl != null)
            .OrderByDescending(p => p.CompletedAt)
            .Take(6)
            .ToListAsync();
        TotalVideosGenerated = await _db.CvProfiles.CountAsync(p => p.Status == VideoStatus.Completed);
    }

    public async Task<IActionResult> OnPostAsync()
    {
        Templates = await _db.VideoTemplates.Where(t => t.IsActive).ToListAsync();
        TotalVideosGenerated = await _db.CvProfiles.CountAsync(p => p.Status == VideoStatus.Completed);

        try
        {
            CvProfile profile;

            if (InputMode == "upload" && CvFile != null && CvFile.Length > 0)
            {
                var uploadsDir = Path.Combine(_env.WebRootPath, "uploads");
                Directory.CreateDirectory(uploadsDir);
                var filePath = Path.Combine(uploadsDir, $"{Guid.NewGuid()}_{CvFile.FileName}");
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await CvFile.CopyToAsync(stream);
                }

                profile = _parser.ParsePdf(filePath, CvFile.FileName);
                profile.CvFilePath = filePath;
            }
            else if (InputMode == "manual")
            {
                if (string.IsNullOrWhiteSpace(FullName) || string.IsNullOrWhiteSpace(Email))
                {
                    ErrorMessage = "Please provide at least your name and email.";
                    return Page();
                }

                profile = new CvProfile
                {
                    FullName = FullName!,
                    Email = Email!,
                    JobTitle = JobTitle,
                    Summary = Summary,
                    Skills = Skills,
                    LinkedInUrl = LinkedInUrl,
                };
            }
            else if (InputMode == "linkedin" && !string.IsNullOrWhiteSpace(LinkedInUrl))
            {
                profile = new CvProfile
                {
                    FullName = FullName ?? "Professional",
                    Email = Email ?? "",
                    LinkedInUrl = LinkedInUrl,
                    JobTitle = JobTitle,
                    Summary = Summary,
                    Skills = Skills,
                };
            }
            else
            {
                ErrorMessage = "Please upload a CV or fill in your details.";
                return Page();
            }

            var template = await _db.VideoTemplates.FindAsync(TemplateId)
                           ?? await _db.VideoTemplates.FirstAsync(t => t.IsActive);

            profile.Status = VideoStatus.Processing;
            _db.CvProfiles.Add(profile);
            await _db.SaveChangesAsync();

            var script = await _scriptGen.GenerateScriptAsync(profile);
            profile.VideoScript = script;
            await _db.SaveChangesAsync();

            var slides = _scriptGen.GenerateSlides(profile, script, template);
            var videoUrl = await _videoGen.GenerateVideoAsync(profile, slides, template);

            profile.VideoUrl = videoUrl;
            profile.VideoPath = videoUrl;
            profile.Status = VideoStatus.Completed;
            profile.CompletedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            GeneratedProfile = profile;
            SuccessMessage = "Your professional intro video is ready!";

            RecentVideos = await _db.CvProfiles
                .Where(p => p.Status == VideoStatus.Completed && p.VideoUrl != null)
                .OrderByDescending(p => p.CompletedAt)
                .Take(6)
                .ToListAsync();
            TotalVideosGenerated = await _db.CvProfiles.CountAsync(p => p.Status == VideoStatus.Completed);

            return Page();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Something went wrong: {ex.Message}";
            return Page();
        }
    }
}
