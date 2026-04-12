using System.Diagnostics;
using System.Globalization;
using System.Text;
using VidCV.Web.Models;

namespace VidCV.Web.Services;

public class VideoGeneratorService
{
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<VideoGeneratorService> _logger;
    private const int W = 1280;
    private const int H = 720;
    private const int FPS = 30;

    public VideoGeneratorService(IWebHostEnvironment env, ILogger<VideoGeneratorService> logger)
    {
        _env = env;
        _logger = logger;
    }

    public async Task<string> GenerateVideoAsync(CvProfile profile, List<VideoSlide> slides, VideoTemplate template)
    {
        var outputDir = Path.Combine(_env.WebRootPath, "videos");
        Directory.CreateDirectory(outputDir);

        var ts = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        var safeName = SanitizeName(profile.FullName);
        var outputFile = $"{safeName}_{ts}.mp4";
        var outputPath = Path.Combine(outputDir, outputFile);
        var tmpDir = Path.Combine(Path.GetTempPath(), $"vidcv_{ts}");
        Directory.CreateDirectory(tmpDir);

        try
        {
            var segments = new List<string>();

            foreach (var slide in slides.OrderBy(s => s.Order))
            {
                var seg = Path.Combine(tmpDir, $"s{slide.Order:D2}.mp4");
                await RenderSlideAsync(slide, template, seg);
                segments.Add(seg);
            }

            await ConcatAsync(segments, outputPath, tmpDir);
            return $"/videos/{outputFile}";
        }
        finally
        {
            try { Directory.Delete(tmpDir, true); } catch { }
        }
    }

    private async Task RenderSlideAsync(VideoSlide slide, VideoTemplate template, string outPath)
    {
        var bg1 = template.BackgroundColor.TrimStart('#');
        var bg2 = template.AccentColor.TrimStart('#');
        var accent = template.AccentColor.TrimStart('#');
        var textClr = template.TextColor.TrimStart('#');
        var dur = slide.Duration;
        var boldFont = GetFont(true);
        var regFont = GetFont(false);

        var filters = new List<string>();

        // Animated gradient background using blend of two color sources
        // Base: solid background color
        // We add decorative elements via drawbox for visual interest

        // Accent stripe at bottom
        filters.Add($"drawbox=x=0:y={H - 6}:w={W}:h=6:color=0x{accent}@0.9:t=fill");

        // Floating accent circle (decorative, top-right)
        filters.Add($"drawbox=x={W - 160}:y=30:w=120:h=120:color=0x{accent}@0.08:t=fill");
        // Smaller decorative element (bottom-left)
        filters.Add($"drawbox=x=40:y={H - 180}:w=80:h=80:color=0x{accent}@0.06:t=fill");

        // Vertical accent bar on left
        filters.Add($"drawbox=x=0:y=0:w=5:h={H}:color=0x{accent}@0.7:t=fill");

        switch (slide.Type)
        {
            case SlideType.Intro:
                RenderIntro(filters, slide, textClr, accent, boldFont, regFont, dur);
                break;
            case SlideType.Content:
                RenderContent(filters, slide, textClr, accent, boldFont, regFont, dur);
                break;
            case SlideType.Skills:
                RenderSkills(filters, slide, textClr, accent, boldFont, regFont, dur);
                break;
            case SlideType.Contact:
                RenderContact(filters, slide, textClr, accent, boldFont, regFont, dur);
                break;
        }

        // Watermark
        filters.Add(Txt("VidCV.live", "888888", 12, $"{W - 120}", $"{H - 22}", regFont, fade: false));

        // Fade in/out for smooth transitions
        filters.Add($"fade=t=in:st=0:d=0.5");
        filters.Add($"fade=t=out:st={dur - 0.5}:d=0.5");

        var filterStr = string.Join(",", filters);

        var args = $"-f lavfi -i color=c=0x{bg1}:s={W}x{H}:d={dur}:r={FPS} " +
                   $"-vf \"{filterStr}\" " +
                   $"-c:v libx264 -preset fast -crf 23 -pix_fmt yuv420p " +
                   $"-t {dur} -y \"{outPath}\"";

        await RunFfmpegAsync(args);
    }

    // ─── SLIDE RENDERERS ─────────────────────────────────────────────

    private void RenderIntro(List<string> f, VideoSlide s, string txt, string acc, string bold, string reg, int dur)
    {
        // Big name with slide-in from left
        f.Add(Txt(s.Title, txt, 56, SlideIn(dur), CenterY(-50), bold, fade: true, fadeDelay: 0.3));

        // Accent line below name
        f.Add(AnimatedLine(acc, H / 2 - 5, 200, 0.5, dur));

        // Job title / subtitle
        if (!string.IsNullOrWhiteSpace(s.Subtitle))
            f.Add(Txt(s.Subtitle, acc, 28, CenterX(), CenterY(35), reg, fade: true, fadeDelay: 0.7));

        // Decorative dots
        f.Add(Txt("●  ●  ●", acc, 14, CenterX(), CenterY(80), reg, fade: true, fadeDelay: 1.0, alpha: 0.5));
    }

    private void RenderContent(List<string> f, VideoSlide s, string txt, string acc, string bold, string reg, int dur)
    {
        // Section header with accent background
        f.Add($"drawbox=x=50:y=55:w=6:h=40:color=0x{acc}:t=fill");
        f.Add(Txt(s.Title.ToUpper(), acc, 30, "70", "60", bold, fade: true, fadeDelay: 0.2));

        // Thin line under header
        f.Add(AnimatedLine(acc, 110, W - 120, 0.3, dur, x: 60));

        // Content text — wrapped into lines
        if (!string.IsNullOrWhiteSpace(s.Content))
        {
            var lines = WrapText(s.Content, 60);
            var y = 145;
            var delay = 0.5;
            foreach (var line in lines.Split('\n'))
            {
                f.Add(Txt(line.Trim(), txt, 22, "70", y.ToString(), reg, fade: true, fadeDelay: delay));
                y += 38;
                delay += 0.15;
                if (y > H - 80) break;
            }
        }
    }

    private void RenderSkills(List<string> f, VideoSlide s, string txt, string acc, string bold, string reg, int dur)
    {
        // Section header
        f.Add($"drawbox=x=50:y=55:w=6:h=40:color=0x{acc}:t=fill");
        f.Add(Txt(s.Title.ToUpper(), acc, 30, "70", "60", bold, fade: true, fadeDelay: 0.2));
        f.Add(AnimatedLine(acc, 110, W - 120, 0.3, dur, x: 60));

        if (!string.IsNullOrWhiteSpace(s.Content))
        {
            var skills = s.Content.Split("  •  ");
            var col1X = 90;
            var col2X = W / 2 + 40;
            var y = 160;
            var delay = 0.4;

            for (int i = 0; i < skills.Length && i < 10; i++)
            {
                var x = i % 2 == 0 ? col1X : col2X;
                var row = i / 2;
                var yPos = y + row * 52;

                // Skill pill background
                f.Add($"drawbox=x={x - 10}:y={yPos - 4}:w=280:h=36:color=0x{acc}@0.12:t=fill");
                // Accent dot
                f.Add(Txt("▸", acc, 20, (x + 5).ToString(), (yPos + 2).ToString(), reg, fade: true, fadeDelay: delay));
                // Skill name
                f.Add(Txt(skills[i].Trim(), txt, 20, (x + 30).ToString(), (yPos + 2).ToString(), reg, fade: true, fadeDelay: delay + 0.1));

                if (i % 2 == 1) delay += 0.2;
            }
        }
    }

    private void RenderContact(List<string> f, VideoSlide s, string txt, string acc, string bold, string reg, int dur)
    {
        // "Let's Connect" big text
        f.Add(Txt(s.Title, acc, 44, CenterX(), CenterY(-100), bold, fade: true, fadeDelay: 0.3));

        // Decorative line
        f.Add(AnimatedLine(acc, H / 2 - 55, 180, 0.5, dur));

        // Email
        if (!string.IsNullOrWhiteSpace(s.Subtitle))
        {
            f.Add(Txt("✉", txt, 22, $"{W / 2 - 160}", CenterY(-10), reg, fade: true, fadeDelay: 0.7));
            f.Add(Txt(s.Subtitle, txt, 24, $"{W / 2 - 130}", CenterY(-10), reg, fade: true, fadeDelay: 0.8));
        }

        // LinkedIn
        if (!string.IsNullOrWhiteSpace(s.Content) && s.Content.Contains("linkedin"))
        {
            f.Add(Txt("🔗", txt, 20, $"{W / 2 - 160}", CenterY(30), reg, fade: true, fadeDelay: 1.0));
            f.Add(Txt(s.Content, txt, 18, $"{W / 2 - 130}", CenterY(32), reg, fade: true, fadeDelay: 1.1));
        }

        // "Thank you" tagline
        f.Add(Txt("Thank you for watching!", txt, 18, CenterX(), CenterY(90), reg, fade: true, fadeDelay: 1.4, alpha: 0.6));

        // Bottom branding bar
        f.Add($"drawbox=x=0:y={H - 50}:w={W}:h=50:color=0x{acc}@0.15:t=fill");
        f.Add(Txt("Generated with VidCV.live — Free Professional Intro Videos", txt, 13, CenterX(), $"{H - 35}", reg, fade: true, fadeDelay: 1.5, alpha: 0.5));
    }

    // ─── FFmpeg TEXT HELPERS ──────────────────────────────────────────

    private static string Txt(string text, string color, int size, string x, string y,
        string fontFile, bool fade = true, double fadeDelay = 0, double alpha = 1.0)
    {
        var escaped = text
            .Replace("\\", "\\\\\\\\")
            .Replace("'", "\u2019")
            .Replace(":", "\\:")
            .Replace("%", "%%")
            .Replace("[", "\\[")
            .Replace("]", "\\]")
            .Replace(";", "\\;")
            .Replace("\"", "");

        var alphaExpr = fade
            ? $":alpha='if(lt(t\\,{F(fadeDelay)})\\,0\\,min({F(alpha)}\\,(t-{F(fadeDelay)})/0.4))'"
            : (alpha < 1.0 ? $":alpha={F(alpha)}" : "");

        return $"drawtext=text='{escaped}':fontcolor=0x{color}{alphaExpr}" +
               $":fontsize={size}:x={x}:y={y}:fontfile={fontFile}";
    }

    private static string AnimatedLine(string color, int y, int width, double delay, int dur, int x = -1)
    {
        var startX = x >= 0 ? x : (W - width) / 2;
        // Animate width growing from 0 to full
        return $"drawbox=x={startX}:y={y}:w='if(lt(t\\,{F(delay)})\\,0\\,min({width}\\,{width}*(t-{F(delay)})/0.5))'" +
               $":h=3:color=0x{color}@0.8:t=fill";
    }

    private static string SlideIn(int dur)
    {
        // Text slides in from left to center
        return $"if(lt(t\\,0.3)\\,-200+(200+(w-text_w)/2)*(t/0.3)\\,(w-text_w)/2)";
    }

    private static string CenterX() => "(w-text_w)/2";
    private static string CenterY(int offset = 0)
    {
        return offset == 0 ? "(h-text_h)/2" : $"(h-text_h)/2+{offset}";
    }

    private static string F(double val) => val.ToString("0.0##", CultureInfo.InvariantCulture);

    private static string GetFont(bool bold)
    {
        // Try common font paths across platforms
        string[] boldPaths = {
            "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf",
            "C\\\\:/Windows/Fonts/arialbd.ttf",
            "C\\\\:/Windows/Fonts/segoeui.ttf"
        };
        string[] regPaths = {
            "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf",
            "C\\\\:/Windows/Fonts/arial.ttf",
            "C\\\\:/Windows/Fonts/segoeui.ttf"
        };

        var paths = bold ? boldPaths : regPaths;
        foreach (var p in paths)
        {
            var realPath = p.Replace("\\\\", "");
            if (File.Exists(realPath)) return p;
        }

        // Return Windows fonts as default (most users deploy on Windows/IIS)
        return bold ? "C\\\\:/Windows/Fonts/arialbd.ttf" : "C\\\\:/Windows/Fonts/arial.ttf";
    }

    // ─── CONCATENATION ───────────────────────────────────────────────

    private async Task ConcatAsync(List<string> files, string output, string tmpDir)
    {
        var listFile = Path.Combine(tmpDir, "list.txt");
        var sb = new StringBuilder();
        foreach (var f in files) sb.AppendLine($"file '{f}'");
        await File.WriteAllTextAsync(listFile, sb.ToString());

        await RunFfmpegAsync($"-f concat -safe 0 -i \"{listFile}\" -c copy -y \"{output}\"");
    }

    private async Task RunFfmpegAsync(string args)
    {
        _logger.LogInformation("FFmpeg: {Args}", args.Length > 300 ? args[..300] + "..." : args);

        var psi = new ProcessStartInfo
        {
            FileName = "ffmpeg",
            Arguments = args,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var proc = Process.Start(psi)!;
        var stderr = await proc.StandardError.ReadToEndAsync();
        await proc.WaitForExitAsync();

        if (proc.ExitCode != 0)
        {
            _logger.LogError("FFmpeg exit {Code}: {Err}", proc.ExitCode, stderr[..Math.Min(stderr.Length, 800)]);
            throw new Exception($"FFmpeg failed ({proc.ExitCode}): {stderr[..Math.Min(stderr.Length, 500)]}");
        }
    }

    private static string WrapText(string text, int maxLen)
    {
        var words = text.Split(' ');
        var lines = new List<string>();
        var cur = "";
        foreach (var w in words)
        {
            if ((cur + " " + w).Trim().Length > maxLen)
            {
                if (cur.Length > 0) lines.Add(cur.Trim());
                cur = w;
            }
            else cur += " " + w;
            if (lines.Count >= 10) break;
        }
        if (cur.Trim().Length > 0 && lines.Count < 11) lines.Add(cur.Trim());
        return string.Join("\n", lines);
    }

    private static string SanitizeName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray())
            .Replace(' ', '_').ToLower();
    }
}
