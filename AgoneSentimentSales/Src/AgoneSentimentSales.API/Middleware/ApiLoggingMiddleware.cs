using System.Diagnostics;
using AgoneSentimentSales.Domain.Entities;
using AgoneSentimentSales.Infrastructure.Data;

namespace AgoneSentimentSales.API.Middleware;

public class ApiLoggingMiddleware
{
    private readonly RequestDelegate _next;
    public ApiLoggingMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, SentimentSalesDbContext db)
    {
        var sw = Stopwatch.StartNew();
        await _next(context);
        sw.Stop();
        db.ApiRequestLogs.Add(new ApiRequestLog
        {
            Method = context.Request.Method,
            Path = context.Request.Path,
            StatusCode = context.Response.StatusCode,
            DurationMs = sw.ElapsedMilliseconds,
            ClientIp = context.Connection.RemoteIpAddress?.ToString()
        });
        try { await db.SaveChangesAsync(); } catch { }
    }
}
