-- =============================================
-- TenantSettings: Central key-value config per tenant
-- Stores: Company Profile, SSO Config, API Keys, Rate Limits, etc.
-- =============================================

CREATE TABLE [core].[TenantConfigurations](
    [Id]          [uniqueidentifier] NOT NULL,
    [TenantId]    [uniqueidentifier] NOT NULL,
    [Category]    [nvarchar](100) NOT NULL,       -- 'CompanyProfile', 'SSO', 'ApiKey', 'RateLimit'
    [SettingKey]  [nvarchar](256) NOT NULL,        -- 'Name', 'Industry', 'InstanceUrl', etc.
    [SettingValue][nvarchar](max) NULL,
    [CreatedAt]   [datetime2](7) NOT NULL,
    [UpdatedAt]   [datetime2](7) NULL,
    [IsDeleted]   [bit] NOT NULL DEFAULT 0,
    [CreatedBy]   [nvarchar](256) NULL,
    CONSTRAINT [PK_TenantConfigurations] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_TenantConfigurations_Tenants] FOREIGN KEY ([TenantId]) REFERENCES [core].[Tenants]([Id])
);

CREATE UNIQUE INDEX [IX_TenantConfigurations_Unique] ON [core].[TenantConfigurations]([TenantId], [Category], [SettingKey]) WHERE [IsDeleted] = 0;
