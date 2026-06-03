namespace AgoneSentimentSales.API.Middleware;

public class JobMonitoringMiddleware
{
    private readonly RequestDelegate _next;
    public JobMonitoringMiddleware(RequestDelegate next) => _next = next;
    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.Headers.Append("X-Agone-Product", "SentimentSales");
        await _next(context);
    }
}
