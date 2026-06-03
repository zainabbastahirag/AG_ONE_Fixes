using AgoneSentimentSales.Domain.Entities;
using AgoneSentimentSales.Domain.Interfaces;
using AgoneSentimentSales.Shared.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace AgoneSentimentSales.API.Controllers;

[ApiController]
[Route("api/scraper-config")]
public class ScraperConfigController : ControllerBase
{
    private readonly IScraperConfigurationService _service;

    public ScraperConfigController(IScraperConfigurationService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ScraperConfigurationDto>>> GetAll(CancellationToken ct) =>
        Ok((await _service.GetAllAsync(ct)).Select(ToDto).ToList());

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ScraperConfigurationDto>> Get(int id, CancellationToken ct)
    {
        var c = await _service.GetByIdAsync(id, ct);
        return c == null ? NotFound() : Ok(ToDto(c));
    }

    [HttpPost]
    public async Task<ActionResult<ScraperConfigurationDto>> Create([FromBody] UpsertScraperConfigurationRequest request, CancellationToken ct)
    {
        var entity = FromRequest(request);
        var created = await _service.CreateAsync(entity, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, ToDto(created));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<ScraperConfigurationDto>> Update(int id, [FromBody] UpsertScraperConfigurationRequest request, CancellationToken ct)
    {
        var entity = FromRequest(request);
        entity.Id = id;
        var updated = await _service.UpdateAsync(entity, ct);
        return updated == null ? NotFound() : Ok(ToDto(updated));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct) =>
        await _service.DeleteAsync(id, ct) ? NoContent() : NotFound();

    private static ScraperConfigurationDto ToDto(ScraperConfiguration c) =>
        new(c.Id, c.SourceType, c.DisplayName, c.BaseUrlTemplate, c.IsEnabled,
            c.MaxItemsToScrape, c.DelayMsMin, c.DelayMsMax, c.Priority, c.Notes, c.UpdatedAt);

    private static ScraperConfiguration FromRequest(UpsertScraperConfigurationRequest r) => new()
    {
        SourceType = r.SourceType,
        DisplayName = r.DisplayName,
        BaseUrlTemplate = r.BaseUrlTemplate,
        IsEnabled = r.IsEnabled,
        MaxItemsToScrape = r.MaxItemsToScrape,
        DelayMsMin = r.DelayMsMin,
        DelayMsMax = r.DelayMsMax,
        Priority = r.Priority,
        Notes = r.Notes
    };
}
