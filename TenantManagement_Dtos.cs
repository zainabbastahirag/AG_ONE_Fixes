// FILE: TenantManagement_Dtos.cs → Shared/DTOs/

namespace AGOne.Shared.DTOs;

// ═══ Company Profile ═══
public class TenantManagement_CompanyProfileDto
{
    public string Name { get; set; } = "";
    public string Industry { get; set; } = "";
    public string Size { get; set; } = "";
    public string Description { get; set; } = "";
    public string Website { get; set; } = "";
    public string Email { get; set; } = "";
    public string Phone { get; set; } = "";
}

// ═══ SSO ═══
public class TenantManagement_SsoSettingsDto
{
    public bool IsEnabled { get; set; }
    public string InstanceUrl { get; set; } = "";
    public string TenantId { get; set; } = "";
    public string ClientId { get; set; } = "";
    public int ActiveUsers { get; set; }
}

// ═══ API Keys ═══
public class TenantManagement_ApiKeyDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string KeyPrefix { get; set; } = "";
    public string KeySuffix { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
}

public class TenantManagement_CreateApiKeyRequest
{
    public string Name { get; set; } = "";
}

public class TenantManagement_CreateApiKeyResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string FullKey { get; set; } = "";
}

// ═══ Audit ═══
public class TenantManagement_AuditEntryDto
{
    public Guid Id { get; set; }
    public DateTime Timestamp { get; set; }
    public string User { get; set; } = "";
    public string Type { get; set; } = "";
    public string Action { get; set; } = "";
    public string Details { get; set; } = "";
}

public class TenantManagement_AuditQueryDto
{
    public string? Search { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}

public class TenantManagement_AuditResultDto
{
    public List<TenantManagement_AuditEntryDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int TotalEvents7d { get; set; }
    public int UserChanges { get; set; }
    public int RoleChanges { get; set; }
    public int ApiCalls { get; set; }
}

// ═══ Login History ═══
public class TenantManagement_LoginHistoryDto
{
    public Guid Id { get; set; }
    public DateTime Timestamp { get; set; }
    public string UserName { get; set; } = "";
    public string UserEmail { get; set; } = "";
    public string? IpAddress { get; set; }
    public string? Browser { get; set; }
    public string? OperatingSystem { get; set; }
    public string? Location { get; set; }
    public bool IsSuccessful { get; set; }
    public string? FailureReason { get; set; }
    public string? ProductName { get; set; }
}
