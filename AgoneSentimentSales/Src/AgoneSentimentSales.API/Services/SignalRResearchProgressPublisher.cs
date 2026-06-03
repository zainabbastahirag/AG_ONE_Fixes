using AgoneSentimentSales.API.Hubs;
using AgoneSentimentSales.Domain.Interfaces;
using AgoneSentimentSales.Shared.DTOs;
using Microsoft.AspNetCore.SignalR;

namespace AgoneSentimentSales.API.Services;

public class SignalRResearchProgressPublisher : IResearchProgressPublisher
{
    private readonly IHubContext<ResearchProgressHub> _hub;

    public SignalRResearchProgressPublisher(IHubContext<ResearchProgressHub> hub) => _hub = hub;

    public Task PublishProgressAsync(
        Guid jobId,
        string phase,
        int processed,
        int total,
        string? companyName = null,
        string? message = null,
        CancellationToken cancellationToken = default)
    {
        var dto = new ResearchProgressDto(jobId, phase, processed, total, companyName, message, DateTime.UtcNow);
        return _hub.Clients.Group(jobId.ToString()).SendAsync("ResearchProgress", dto, cancellationToken);
    }
}
