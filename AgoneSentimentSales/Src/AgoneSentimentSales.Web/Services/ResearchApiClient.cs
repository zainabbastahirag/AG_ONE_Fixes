
using System.Net.Http.Json;
using AgoneSentimentSales.Application.DTOs;

namespace AgoneSentimentSales.Web.Services;

public class ResearchApiClient
{
    private readonly HttpClient _http;
    public ResearchApiClient(HttpClient http) => _http = http;

    public Task<DashboardSummaryDto?> GetDashboardAsync() => _http.GetFromJsonAsync<DashboardSummaryDto>("api/research/dashboard");
    public Task<List<CompanySummaryDto>?> GetCompaniesAsync(string? sector = null)
    {
        var url = string.IsNullOrEmpty(sector) ? "api/research/companies" : $"api/research/companies?sector={Uri.EscapeDataString(sector)}";
        return _http.GetFromJsonAsync<List<CompanySummaryDto>>(url);
    }
    public async Task<ResearchJobResponse?> StartResearchAsync(int count = 100)
    {
        var res = await _http.PostAsJsonAsync("api/research/start", new StartResearchRequest(count));
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<ResearchJobResponse>();
    }
    public Task<ResearchJobResponse?> GetJobAsync(Guid id) => _http.GetFromJsonAsync<ResearchJobResponse>($"api/research/jobs/{id}");
    public string GetExcelDownloadUrl() => new Uri(_http.BaseAddress!, "api/export/excel").ToString();
}
