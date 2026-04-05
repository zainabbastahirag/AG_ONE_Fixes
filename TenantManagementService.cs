// FILE: TenantManagementService.cs → Infrastructure/Services/
// Uses: AGOneDbContext with DbSets for TenantSettings, AuditLogs, LoginHistories, Users

using System.Security.Cryptography;
using AGOne.Shared.DTOs;
using AGOne.Shared.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AGOne.Infrastructure.Services;

public class TenantManagementService : ITenantManagementService
{
    private readonly AGOneDbContext _db;

    public TenantManagementService(AGOneDbContext db)
    {
        _db = db;
    }

    // ═══════════════════════════════════════════════════════════════
    // COMPANY PROFILE — stored in TenantSettings (Category = "CompanyProfile")
    // ═══════════════════════════════════════════════════════════════

    public async Task<TenantManagement_CompanyProfileDto> GetCompanyProfileAsync(Guid tenantId)
    {
        var settings = await GetSettingsByCategoryAsync(tenantId, "CompanyProfile");
        return new TenantManagement_CompanyProfileDto
        {
            Name = settings.GetValueOrDefault("Name", ""),
            Industry = settings.GetValueOrDefault("Industry", ""),
            Size = settings.GetValueOrDefault("Size", ""),
            Description = settings.GetValueOrDefault("Description", ""),
            Website = settings.GetValueOrDefault("Website", ""),
            Email = settings.GetValueOrDefault("Email", ""),
            Phone = settings.GetValueOrDefault("Phone", "")
        };
    }

    public async Task SaveCompanyProfileAsync(Guid tenantId, TenantManagement_CompanyProfileDto dto)
    {
        var pairs = new Dictionary<string, string?>
        {
            ["Name"] = dto.Name,
            ["Industry"] = dto.Industry,
            ["Size"] = dto.Size,
            ["Description"] = dto.Description,
            ["Website"] = dto.Website,
            ["Email"] = dto.Email,
            ["Phone"] = dto.Phone
        };
        await UpsertSettingsAsync(tenantId, "CompanyProfile", pairs);
    }

    // ═══════════════════════════════════════════════════════════════
    // SSO — stored in TenantSettings (Category = "SSO")
    // ═══════════════════════════════════════════════════════════════

    public async Task<TenantManagement_SsoSettingsDto> GetSsoSettingsAsync(Guid tenantId)
    {
        var settings = await GetSettingsByCategoryAsync(tenantId, "SSO");
        var activeUsers = await _db.Users.CountAsync(u => u.TenantId == tenantId && !u.IsDeleted && u.IsActive && u.SSOProvider != null);

        return new TenantManagement_SsoSettingsDto
        {
            IsEnabled = settings.GetValueOrDefault("IsEnabled", "false") == "true",
            InstanceUrl = settings.GetValueOrDefault("InstanceUrl", ""),
            TenantId = settings.GetValueOrDefault("TenantId", ""),
            ClientId = settings.GetValueOrDefault("ClientId", ""),
            ActiveUsers = activeUsers
        };
    }

    public async Task SaveSsoSettingsAsync(Guid tenantId, TenantManagement_SsoSettingsDto dto)
    {
        var pairs = new Dictionary<string, string?>
        {
            ["IsEnabled"] = dto.IsEnabled.ToString().ToLower(),
            ["InstanceUrl"] = dto.InstanceUrl,
            ["TenantId"] = dto.TenantId,
            ["ClientId"] = dto.ClientId
        };
        await UpsertSettingsAsync(tenantId, "SSO", pairs);
    }

    // ═══════════════════════════════════════════════════════════════
    // API KEYS — stored in TenantSettings (Category = "ApiKey")
    // Key = "ApiKey_{id}", Value = JSON { Name, KeyHash, KeyPrefix, KeySuffix, CreatedAt, LastUsedAt }
    // ═══════════════════════════════════════════════════════════════

    public async Task<List<TenantManagement_ApiKeyDto>> GetApiKeysAsync(Guid tenantId)
    {
        var settings = await _db.TenantConfigurations
            .Where(s => s.TenantId == tenantId && s.Category == "ApiKey" && !s.IsDeleted)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();

        return settings.Select(s =>
        {
            var json = System.Text.Json.JsonSerializer.Deserialize<ApiKeyData>(s.SettingValue ?? "{}");
            return new TenantManagement_ApiKeyDto
            {
                Id = s.Id,
                Name = json?.Name ?? s.SettingKey,
                KeyPrefix = json?.KeyPrefix ?? "",
                KeySuffix = json?.KeySuffix ?? "",
                CreatedAt = s.CreatedAt,
                LastUsedAt = json?.LastUsedAt
            };
        }).ToList();
    }

    public async Task<TenantManagement_CreateApiKeyResponse> CreateApiKeyAsync(Guid tenantId, TenantManagement_CreateApiKeyRequest request)
    {
        var keyBytes = RandomNumberGenerator.GetBytes(32);
        var fullKey = $"sk_live_{Convert.ToBase64String(keyBytes).Replace("+", "").Replace("/", "").Replace("=", "")[..32]}";
        var keyHash = Convert.ToBase64String(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(fullKey)));

        var id = Guid.NewGuid();
        var data = new ApiKeyData
        {
            Name = request.Name,
            KeyHash = keyHash,
            KeyPrefix = fullKey[..12],
            KeySuffix = fullKey[^4..],
            LastUsedAt = null
        };

        _db.TenantConfigurations.Add(new TenantConfiguration
        {
            Id = id,
            TenantId = tenantId,
            Category = "ApiKey",
            SettingKey = $"ApiKey_{id}",
            SettingValue = System.Text.Json.JsonSerializer.Serialize(data),
            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        });

        await _db.SaveChangesAsync();

        return new TenantManagement_CreateApiKeyResponse
        {
            Id = id,
            Name = request.Name,
            FullKey = fullKey
        };
    }

    public async Task RevokeApiKeyAsync(Guid tenantId, Guid keyId)
    {
        var setting = await _db.TenantConfigurations
            .FirstOrDefaultAsync(s => s.Id == keyId && s.TenantId == tenantId && s.Category == "ApiKey" && !s.IsDeleted);

        if (setting != null)
        {
            setting.IsDeleted = true;
            setting.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // AUDIT — reads from core.AuditLogs
    // ═══════════════════════════════════════════════════════════════

    public async Task<TenantManagement_AuditResultDto> GetAuditLogsAsync(Guid tenantId, TenantManagement_AuditQueryDto query)
    {
        var sevenDaysAgo = DateTime.UtcNow.AddDays(-7);

        var baseQuery = _db.AuditLogs
            .Where(a => a.TenantId == tenantId && !a.IsDeleted);

        var totalEvents7d = await baseQuery.CountAsync(a => a.CreatedAt >= sevenDaysAgo);
        var userChanges = await baseQuery.CountAsync(a => a.EntityType == "User" && a.CreatedAt >= sevenDaysAgo);
        var roleChanges = await baseQuery.CountAsync(a => (a.EntityType == "Role" || a.EntityType == "UserRole") && a.CreatedAt >= sevenDaysAgo);
        var apiCalls = await baseQuery.CountAsync(a => a.EntityType == "ApiKey" && a.CreatedAt >= sevenDaysAgo);

        var itemsQuery = baseQuery.AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLower();
            itemsQuery = itemsQuery.Where(a =>
                (a.UserName != null && a.UserName.ToLower().Contains(term)) ||
                (a.Action != null && a.Action.ToLower().Contains(term)) ||
                (a.EntityType != null && a.EntityType.ToLower().Contains(term)));
        }

        var totalCount = await itemsQuery.CountAsync();

        var items = await itemsQuery
            .OrderByDescending(a => a.CreatedAt)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(a => new TenantManagement_AuditEntryDto
            {
                Id = a.Id,
                Timestamp = a.CreatedAt,
                User = a.UserName ?? a.UserEmail ?? "System",
                Type = a.EntityType,
                Action = a.Action,
                Details = a.NewValues ?? a.AdditionalInfo ?? ""
            })
            .ToListAsync();

        return new TenantManagement_AuditResultDto
        {
            Items = items,
            TotalCount = totalCount,
            TotalEvents7d = totalEvents7d,
            UserChanges = userChanges,
            RoleChanges = roleChanges,
            ApiCalls = apiCalls
        };
    }

    // ═══════════════════════════════════════════════════════════════
    // LOGIN HISTORY — reads from core.LoginHistories
    // ═══════════════════════════════════════════════════════════════

    public async Task<List<TenantManagement_LoginHistoryDto>> GetLoginHistoryAsync(Guid tenantId, int page, int pageSize)
    {
        return await _db.LoginHistories
            .Where(lh => lh.TenantId == tenantId && !lh.IsDeleted)
            .OrderByDescending(lh => lh.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Join(_db.Users, lh => lh.UserId, u => u.Id, (lh, u) => new TenantManagement_LoginHistoryDto
            {
                Id = lh.Id,
                Timestamp = lh.CreatedAt,
                UserName = u.FirstName + " " + u.LastName,
                UserEmail = u.Email,
                IpAddress = lh.IpAddress,
                Browser = lh.Browser,
                OperatingSystem = lh.OperatingSystem,
                Location = lh.Location,
                IsSuccessful = lh.IsSuccessful,
                FailureReason = lh.FailureReason,
                ProductName = lh.Product != null ? lh.Product.Name : null
            })
            .ToListAsync();
    }

    // ═══════════════════════════════════════════════════════════════
    // HELPERS
    // ═══════════════════════════════════════════════════════════════

    private async Task<Dictionary<string, string>> GetSettingsByCategoryAsync(Guid tenantId, string category)
    {
        return await _db.TenantConfigurations
            .Where(s => s.TenantId == tenantId && s.Category == category && !s.IsDeleted)
            .ToDictionaryAsync(s => s.SettingKey, s => s.SettingValue ?? "");
    }

    private async Task UpsertSettingsAsync(Guid tenantId, string category, Dictionary<string, string?> pairs)
    {
        var existing = await _db.TenantConfigurations
            .Where(s => s.TenantId == tenantId && s.Category == category && !s.IsDeleted)
            .ToListAsync();

        var existingMap = existing.ToDictionary(s => s.SettingKey);

        foreach (var (key, value) in pairs)
        {
            if (existingMap.TryGetValue(key, out var setting))
            {
                setting.SettingValue = value;
                setting.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                _db.TenantConfigurations.Add(new TenantConfiguration
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    Category = category,
                    SettingKey = key,
                    SettingValue = value,
                    CreatedAt = DateTime.UtcNow,
                    IsDeleted = false
                });
            }
        }

        await _db.SaveChangesAsync();
    }

    private class ApiKeyData
    {
        public string Name { get; set; } = "";
        public string KeyHash { get; set; } = "";
        public string KeyPrefix { get; set; } = "";
        public string KeySuffix { get; set; } = "";
        public DateTime? LastUsedAt { get; set; }
    }
}

// ═══════════════════════════════════════════════════════════════
// EF Core Entity — add to your AGOneDbContext:
//   public DbSet<TenantConfiguration> TenantConfigurations => Set<TenantConfiguration>();
//
// In OnModelCreating:
//   modelBuilder.Entity<TenantConfiguration>().ToTable("TenantConfigurations", "core");
// ═══════════════════════════════════════════════════════════════

public class TenantConfiguration
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Category { get; set; } = "";
    public string SettingKey { get; set; } = "";
    public string? SettingValue { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
    public string? CreatedBy { get; set; }
}
