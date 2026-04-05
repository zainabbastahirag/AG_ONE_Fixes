// FILE: ITenantManagementService.cs → Shared/Interfaces/

using AGOne.Shared.DTOs;

namespace AGOne.Shared.Interfaces;

public interface ITenantManagementService
{
    // Company Profile
    Task<TenantManagement_CompanyProfileDto> GetCompanyProfileAsync(Guid tenantId);
    Task SaveCompanyProfileAsync(Guid tenantId, TenantManagement_CompanyProfileDto dto);

    // SSO
    Task<TenantManagement_SsoSettingsDto> GetSsoSettingsAsync(Guid tenantId);
    Task SaveSsoSettingsAsync(Guid tenantId, TenantManagement_SsoSettingsDto dto);

    // API Keys
    Task<List<TenantManagement_ApiKeyDto>> GetApiKeysAsync(Guid tenantId);
    Task<TenantManagement_CreateApiKeyResponse> CreateApiKeyAsync(Guid tenantId, TenantManagement_CreateApiKeyRequest request);
    Task RevokeApiKeyAsync(Guid tenantId, Guid keyId);

    // Audit
    Task<TenantManagement_AuditResultDto> GetAuditLogsAsync(Guid tenantId, TenantManagement_AuditQueryDto query);

    // Login History
    Task<List<TenantManagement_LoginHistoryDto>> GetLoginHistoryAsync(Guid tenantId, int page, int pageSize);
}
