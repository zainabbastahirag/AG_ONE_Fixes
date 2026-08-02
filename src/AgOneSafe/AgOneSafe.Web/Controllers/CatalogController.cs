using AgOneSafe.Application.Abstractions;
using AgOneSafe.Domain;
using AgOneSafe.Domain.Catalog;
using AgOneSafe.Domain.Identity;
using AgOneSafe.Infrastructure.Catalog;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AgOneSafe.Web.Controllers;

public class CatalogFilter
{
    public BenchmarkFamily? Benchmark { get; set; }
    public string? Domain { get; set; }
    public ProfileLevel? Level { get; set; }
    public Severity? Severity { get; set; }
    public bool? AutomatedOnly { get; set; }
    public string? Search { get; set; }
}

public class CatalogIndexViewModel
{
    public CatalogFilter Filter { get; init; } = new();
    public IReadOnlyList<ControlDefinition> Controls { get; init; } = Array.Empty<ControlDefinition>();
    public IReadOnlyList<string> Domains { get; init; } = Array.Empty<string>();
    public int TotalControls { get; init; }
    public int AutomatedAudits { get; init; }
    public int AutomatedRemediations { get; init; }
    public int LicenseGated { get; init; }
}

public class CatalogController : AgControllerBase
{
    private readonly IExcelCatalogImporter _importer;

    public CatalogController(IAgOneSafeDbContext db, IExcelCatalogImporter importer) : base(db) =>
        _importer = importer;

    public async Task<IActionResult> Index(CatalogFilter filter, CancellationToken cancellationToken)
    {
        await GetCurrentTenantAsync(cancellationToken);

        var query = Db.ControlDefinitions
            .AsNoTracking()
            .Include(c => c.Actions)
            .Where(c => c.IsEnabled);

        if (filter.Benchmark is { } benchmark)
        {
            query = query.Where(c => c.Benchmark == benchmark);
        }

        if (!string.IsNullOrWhiteSpace(filter.Domain))
        {
            query = query.Where(c => c.Domain == filter.Domain);
        }

        if (filter.Level is { } level)
        {
            query = query.Where(c => c.Level == level);
        }

        if (filter.Severity is { } severity)
        {
            query = query.Where(c => c.Severity == severity);
        }

        if (filter.AutomatedOnly == true)
        {
            query = query.Where(c => c.IsAutomatedRemediation);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim();
            query = query.Where(c => c.Title.Contains(term) || c.VendorReference.Contains(term));
        }

        var model = new CatalogIndexViewModel
        {
            Filter = filter,
            Controls = await query
                .OrderBy(c => c.Benchmark)
                .ThenBy(c => c.VendorReference)
                .ToListAsync(cancellationToken),
            Domains = await Db.ControlDefinitions
                .AsNoTracking()
                .Select(c => c.Domain)
                .Distinct()
                .OrderBy(d => d)
                .ToListAsync(cancellationToken),
            TotalControls = await Db.ControlDefinitions.CountAsync(c => c.IsEnabled, cancellationToken),
            AutomatedAudits = await Db.ControlDefinitions.CountAsync(c => c.IsEnabled && c.IsAutomatedAudit, cancellationToken),
            AutomatedRemediations = await Db.ControlDefinitions.CountAsync(c => c.IsEnabled && c.IsAutomatedRemediation, cancellationToken),
            LicenseGated = await Db.ControlDefinitions.CountAsync(c => c.IsEnabled && c.RequiredLicense != null, cancellationToken)
        };

        return View(model);
    }

    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        await GetCurrentTenantAsync(cancellationToken);

        var control = await Db.ControlDefinitions
            .AsNoTracking()
            .Include(c => c.Actions)
            .ThenInclude(a => a.Assertions)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        return control is null ? NotFound() : View(control);
    }

    [Authorize(Roles = AgRoles.CanExecute)]
    public async Task<IActionResult> Import(CancellationToken cancellationToken)
    {
        await GetCurrentTenantAsync(cancellationToken);
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = AgRoles.CanExecute)]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public async Task<IActionResult> Import(
        IFormFile workbook,
        string benchmark,
        string version,
        CancellationToken cancellationToken)
    {
        await GetCurrentTenantAsync(cancellationToken);

        if (workbook is null || workbook.Length == 0)
        {
            ModelState.AddModelError(string.Empty, "Choose a .xlsx benchmark workbook to import.");
            return View();
        }

        try
        {
            await using var stream = workbook.OpenReadStream();
            var result = await _importer.ImportAsync(
                stream,
                string.IsNullOrWhiteSpace(benchmark) ? "Microsoft365" : benchmark,
                string.IsNullOrWhiteSpace(version) ? "Imported workbook" : version,
                cancellationToken);

            ViewBag.Result = result;
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, $"The workbook could not be imported: {ex.Message}");
        }

        return View();
    }
}
