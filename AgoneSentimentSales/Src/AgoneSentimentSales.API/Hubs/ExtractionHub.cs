using Microsoft.AspNetCore.SignalR;

namespace AgoneSentimentSales.API.Hubs;

public class ExtractionHub : Hub
{
    public Task JoinJob(string jobId) => Groups.AddToGroupAsync(Context.ConnectionId, jobId);
    public Task LeaveJob(string jobId) => Groups.RemoveFromGroupAsync(Context.ConnectionId, jobId);
}
