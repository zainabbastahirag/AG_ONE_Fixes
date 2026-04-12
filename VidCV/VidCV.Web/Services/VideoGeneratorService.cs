using System.Diagnostics;
using System.Text;
using VidCV.Web.Models;

namespace VidCV.Web.Services;

public class VideoGeneratorService
{
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<VideoGeneratorService> _logger;

    public VideoGeneratorService(IWebHostEnvironment env, ILogger<VideoGeneratorService> logger)
    {
        _env = env;
        _logger = logger;
    }

    public async Task<string> GenerateVideoAsync(CvProfile profile, List<VideoSlide> slides, VideoTemplate template)
    {
        var outputDir = Path.Combine(_env.WebRootPath, "videos");
        Directory.CreateDirectory(outputDir);

        var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        var safeFileName = SanitizeFileName(profile.FullName);
        var outputFile = $"{safeFileName}_{timestamp}.mp4";
        var outputPath = Path.Combine(outputDir, outputFile);
        var tempDir = Path.Combine(Path.GetTempPath(), $"vidcv_{timestamp}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var segmentFiles = new List<string>();

            foreach (var slide in slides.OrderBy(s => s.Order))
            {
                var segmentPath = Path.Combine(tempDir, $"slide_{slide.Order:D2}.mp4");
                await CreateSlideVideoAsync(slide, template, segmentPath);
                segmentFiles.Add(segmentPath);
            }

            await ConcatenateVideosAsync(segmentFiles, outputPath, tempDir);

            return $"/videos/{outputFile}";
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    private async Task CreateSlideVideoAsync(VideoSlide slide, VideoTemplate template, string outputPath)
    {
        var bgColor = template.BackgroundColor.TrimStart('#');
        var accentColor = template.AccentColor.TrimStart('#');
        var textColor = template.TextColor.TrimStart('#');
        var width = 1280;
        var height = 720;
        var duration = slide.Duration;

        var drawTextFilters = new List<string>();

        switch (slide.Type)
        {
            case SlideType.Intro:
                drawTextFilters.Add(BuildDrawText(
                    slide.Title, textColor, 52, "center", height / 2 - 60,
                    width, fadeIn: true, bold: true));
                if (!string.IsNullOrWhiteSpace(slide.Subtitle))
                {
                    drawTextFilters.Add(BuildDrawText(
                        slide.Subtitle, accentColor, 28, "center", height / 2 + 20,
                        width, fadeIn: true, fadeDelay: 0.5));
                }
                drawTextFilters.Add(BuildAccentLine(accentColor, width, height / 2 - 10, 120));
                break;

            case SlideType.Content:
                drawTextFilters.Add(BuildDrawText(
                    slide.Title, accentColor, 36, "center", 80,
                    width, fadeIn: true, bold: true));
                drawTextFilters.Add(BuildAccentLine(accentColor, width, 130, 80));
                if (!string.IsNullOrWhiteSpace(slide.Content))
                {
                    var wrappedContent = WrapText(slide.Content, 55);
                    var yPos = 180;
                    foreach (var line in wrappedContent.Split('\n'))
                    {
                        drawTextFilters.Add(BuildDrawText(
                            line.Trim(), textColor, 22, "center", yPos,
                            width, fadeIn: true, fadeDelay: 0.3));
                        yPos += 36;
                    }
                }
                break;

            case SlideType.Skills:
                drawTextFilters.Add(BuildDrawText(
                    slide.Title, accentColor, 36, "center", 80,
                    width, fadeIn: true, bold: true));
                drawTextFilters.Add(BuildAccentLine(accentColor, width, 130, 80));
                if (!string.IsNullOrWhiteSpace(slide.Content))
                {
                    var skills = slide.Content.Split("  •  ");
                    var yPos = 190;
                    foreach (var skill in skills)
                    {
                        drawTextFilters.Add(BuildDrawText(
                            $"▸  {skill.Trim()}", textColor, 24, "center", yPos,
                            width, fadeIn: true, fadeDelay: 0.2));
                        yPos += 42;
                    }
                }
                break;

            case SlideType.Contact:
                drawTextFilters.Add(BuildDrawText(
                    slide.Title, accentColor, 40, "center", height / 2 - 100,
                    width, fadeIn: true, bold: true));
                drawTextFilters.Add(BuildAccentLine(accentColor, width, height / 2 - 50, 100));
                if (!string.IsNullOrWhiteSpace(slide.Subtitle))
                {
                    drawTextFilters.Add(BuildDrawText(
                        slide.Subtitle, textColor, 26, "center", height / 2,
                        width, fadeIn: true, fadeDelay: 0.4));
                }
                if (!string.IsNullOrWhiteSpace(slide.Content))
                {
                    drawTextFilters.Add(BuildDrawText(
                        slide.Content, textColor, 20, "center", height / 2 + 50,
                        width, fadeIn: true, fadeDelay: 0.6));
                }
                drawTextFilters.Add(BuildDrawText(
                    "Generated by VidCV.live", "888888", 14, "center", height - 40,
                    width, fadeIn: false));
                break;
        }

        var filterComplex = string.Join(",", drawTextFilters);

        var args = $"-f lavfi -i color=c=0x{bgColor}:s={width}x{height}:d={duration}:r=24 " +
                   $"-vf \"{filterComplex}\" " +
                   $"-c:v libx264 -preset ultrafast -tune stillimage -pix_fmt yuv420p " +
                   $"-t {duration} -y \"{outputPath}\"";

        await RunFfmpegAsync(args);
    }

    private string BuildDrawText(string text, string color, int fontSize, string align,
        int y, int width, bool fadeIn = false, double fadeDelay = 0, bool bold = false)
    {
        var escaped = text
            .Replace("\\", "\\\\")
            .Replace("'", "'\\''")
            .Replace(":", "\\:")
            .Replace("%", "%%")
            .Replace("[", "\\[")
            .Replace("]", "\\]");

        var x = align == "center" ? $"(w-text_w)/2" : "60";

        var alpha = fadeIn
            ? $":alpha='if(lt(t,{fadeDelay}),0,min(1,(t-{fadeDelay})/0.4))'"
            : "";

        return $"drawtext=text='{escaped}':fontcolor=0x{color}{alpha}" +
               $":fontsize={fontSize}:x={x}:y={y}" +
               (bold ? ":fontfile=/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf" : ":fontfile=/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf");
    }

    private string BuildAccentLine(string color, int width, int y, int lineWidth)
    {
        var x1 = (width - lineWidth) / 2;
        var x2 = x1 + lineWidth;
        return $"drawbox=x={x1}:y={y}:w={lineWidth}:h=3:color=0x{color}:t=fill";
    }

    private async Task ConcatenateVideosAsync(List<string> files, string outputPath, string tempDir)
    {
        var listFile = Path.Combine(tempDir, "filelist.txt");
        var sb = new StringBuilder();
        foreach (var f in files)
        {
            sb.AppendLine($"file '{f}'");
        }
        await File.WriteAllTextAsync(listFile, sb.ToString());

        var args = $"-f concat -safe 0 -i \"{listFile}\" -c copy -y \"{outputPath}\"";
        await RunFfmpegAsync(args);
    }

    private async Task RunFfmpegAsync(string arguments)
    {
        _logger.LogInformation("FFmpeg: {Args}", arguments.Length > 200 ? arguments[..200] + "..." : arguments);

        var psi = new ProcessStartInfo
        {
            FileName = "ffmpeg",
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi)!;
        var stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            _logger.LogError("FFmpeg failed (exit {Code}): {Error}", process.ExitCode, stderr);
            throw new Exception($"FFmpeg failed with exit code {process.ExitCode}: {stderr[..Math.Min(stderr.Length, 500)]}");
        }
    }

    private static string WrapText(string text, int maxLineLength)
    {
        var words = text.Split(' ');
        var lines = new List<string>();
        var current = "";

        foreach (var word in words)
        {
            if ((current + " " + word).Trim().Length > maxLineLength)
            {
                if (current.Length > 0) lines.Add(current.Trim());
                current = word;
            }
            else
            {
                current += " " + word;
            }
            if (lines.Count >= 8) break;
        }
        if (current.Trim().Length > 0 && lines.Count < 9)
            lines.Add(current.Trim());

        return string.Join("\n", lines);
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
        return sanitized.Replace(' ', '_').ToLower();
    }
}
