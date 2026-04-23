using System.Net.Http.Json;
using AGONECompliance.Shared.DTOs;

namespace AGONECompliance.Client.Services;

public class ApiService
{
    private readonly HttpClient _http;
    public ApiService(HttpClient http) => _http = http;

    public async Task<DashboardDto?> GetDashboardAsync()
        => (await _http.GetFromJsonAsync<ApiResult<DashboardDto>>("api/projects/dashboard"))?.Data;

    public async Task<List<ProjectSummaryDto>> GetProjectsAsync()
        => (await _http.GetFromJsonAsync<ApiResult<List<ProjectSummaryDto>>>("api/projects"))?.Data ?? new();

    public async Task<ProjectDetailDto?> GetProjectAsync(Guid id)
        => (await _http.GetFromJsonAsync<ApiResult<ProjectDetailDto>>($"api/projects/{id}"))?.Data;

    public async Task<ProjectSummaryDto?> CreateProjectAsync(CreateProjectRequest req)
    {
        var resp = await _http.PostAsJsonAsync("api/projects", req);
        var result = await resp.Content.ReadFromJsonAsync<ApiResult<ProjectSummaryDto>>();
        return result?.Data;
    }

    public async Task<bool> UploadDocumentAsync(Guid projectId, Stream fileStream, string fileName, string docType)
    {
        using var content = new MultipartFormDataContent();
        content.Add(new StreamContent(fileStream), "file", fileName);
        var resp = await _http.PostAsync($"api/projects/{projectId}/documents?docType={docType}", content);
        return resp.IsSuccessStatusCode;
    }

    public async Task<bool> RunComplianceCheckAsync(Guid projectId)
    {
        var resp = await _http.PostAsync($"api/projects/{projectId}/run", null);
        return resp.IsSuccessStatusCode;
    }

    public async Task<ComplianceReportDto?> GetReportAsync(Guid id)
        => (await _http.GetFromJsonAsync<ApiResult<ComplianceReportDto>>($"api/projects/{id}/report"))?.Data;

    public async Task<List<JobActivityDto>> GetActivitiesAsync(Guid id)
        => (await _http.GetFromJsonAsync<ApiResult<List<JobActivityDto>>>($"api/projects/{id}/activities"))?.Data ?? new();

    public async Task<List<JobActivityDto>> GetAllActivitiesAsync()
        => (await _http.GetFromJsonAsync<ApiResult<List<JobActivityDto>>>("api/projects/activities/all"))?.Data ?? new();
}
