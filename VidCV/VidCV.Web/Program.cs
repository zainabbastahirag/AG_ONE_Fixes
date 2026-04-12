using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using VidCV.Web.Data;
using VidCV.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")!;

// LocalDB workaround: if the standard connection fails, resolve the named pipe
// directly from sqllocaldb and retry. This fixes the common "Named Pipes Provider,
// error: 40" issue on Windows 10/11 with Microsoft.Data.SqlClient v5+.
if (connectionString.Contains("(localdb)", StringComparison.OrdinalIgnoreCase))
{
    try
    {
        var psi = new ProcessStartInfo("sqllocaldb", "info MSSQLLocalDB")
        {
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using var proc = Process.Start(psi);
        if (proc != null)
        {
            var output = await proc.StandardOutput.ReadToEndAsync();
            await proc.WaitForExitAsync();
            var pipeLine = output.Split('\n')
                .FirstOrDefault(l => l.Contains("pipe", StringComparison.OrdinalIgnoreCase));
            if (pipeLine != null)
            {
                var pipe = pipeLine.Split(new[] { ':' }, 2).LastOrDefault()?.Trim();
                if (!string.IsNullOrWhiteSpace(pipe))
                {
                    var pipeConnStr = connectionString
                        .Replace("(localdb)\\MSSQLLocalDB", pipe, StringComparison.OrdinalIgnoreCase)
                        .Replace("(localdb)\\\\MSSQLLocalDB", pipe, StringComparison.OrdinalIgnoreCase);
                    builder.Configuration["ConnectionStrings:FallbackConnection"] = pipeConnStr;
                    Console.WriteLine($"[VidCV] LocalDB pipe resolved: {pipe}");
                }
            }
        }
    }
    catch
    {
        // sqllocaldb not available — continue with original connection string
    }
}

builder.Services.AddDbContext<AppDbContext>((sp, options) =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var cs = config.GetConnectionString("DefaultConnection")!;
    options.UseSqlServer(cs, sql => sql.EnableRetryOnFailure(3));
});

builder.Services.AddScoped<CvParserService>();
builder.Services.AddScoped<ScriptGeneratorService>();
builder.Services.AddScoped<VideoGeneratorService>();

var app = builder.Build();

// Try standard connection first; if it fails, try pipe fallback
try
{
    await DbInitializer.InitializeAsync(app.Services);
}
catch (Exception ex) when (ex.ToString().Contains("Named Pipes Provider") || ex.ToString().Contains("error: 40"))
{
    var fallback = app.Configuration.GetConnectionString("FallbackConnection");
    if (!string.IsNullOrWhiteSpace(fallback))
    {
        Console.WriteLine("[VidCV] Standard connection failed, retrying with named pipe...");
        app.Configuration["ConnectionStrings:DefaultConnection"] = fallback;
        await DbInitializer.InitializeAsync(app.Services);
        Console.WriteLine("[VidCV] Connected via named pipe successfully!");
    }
    else
    {
        throw;
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();
app.MapRazorPages();

app.Run();
