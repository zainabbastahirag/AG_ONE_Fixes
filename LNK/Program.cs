using LNK.Configuration;
using LNK.Data;
using LNK.Jobs;
using LNK.Models;
using LNK.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Quartz;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/lnk-.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

builder.Services.Configure<OllamaSettings>(builder.Configuration.GetSection(OllamaSettings.SectionName));
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection(EmailSettings.SectionName));
builder.Services.Configure<AppOptions>(builder.Configuration.GetSection(AppOptions.SectionName));

var conn = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Server=localhost,1433;Database=LNK;User Id=sa;Password=Your_password123;TrustServerCertificate=True;";

builder.Services.AddDbContext<ApplicationDbContext>(o =>
    o.UseSqlServer(conn, sql => sql.EnableRetryOnFailure(3)));

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(o =>
    {
        o.Password.RequireDigit = true;
        o.Password.RequireLowercase = true;
        o.Password.RequireUppercase = true;
        o.Password.RequiredLength = 8;
        o.SignIn.RequireConfirmedAccount = false;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(o =>
{
    o.LoginPath = "/Account/Login";
    o.AccessDeniedPath = "/Account/AccessDenied";
});

builder.Services.AddHttpClient<IOllamaService, OllamaService>();
builder.Services.AddScoped<IPostGenerationService, PostGenerationService>();
builder.Services.AddScoped<IEmailService, EmailService>();

builder.Services.AddQuartz(q =>
{
    var jobKey = new JobKey("daily-posts");
    q.AddJob<DailyPostGenerationJob>(opts => opts.WithIdentity(jobKey));
    q.AddTrigger(opts => opts
        .ForJob(jobKey)
        .WithIdentity("daily-posts-trigger")
        .WithCronSchedule("0 */15 * * * ?"));
});
builder.Services.AddQuartzHostedService(o => o.WaitForJobsToComplete = true);

builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

var app = builder.Build();

await DbSeeder.SeedAsync(app.Services);

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapRazorPages();

app.Run();
