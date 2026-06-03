-- AG ONE Sentiment Sales — ensure full schema (run if EF migrations were skipped)
-- Database: AgoneSentimentSales
-- Fixes errors like: Invalid object name 'sentimentsales.ScraperConfigurations'

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'sentimentsales')
    EXEC(N'CREATE SCHEMA [sentimentsales]');
GO

-- Migration history table
IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id
    WHERE s.name = N'sentimentsales' AND t.name = N'__EFMigrationsHistory')
BEGIN
    CREATE TABLE [sentimentsales].[__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END
GO

-- ScraperConfigurations (20260603145108_AddScraperConfigurations)
IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id
    WHERE s.name = N'sentimentsales' AND t.name = N'ScraperConfigurations')
BEGIN
    CREATE TABLE [sentimentsales].[ScraperConfigurations] (
        [Id] int NOT NULL IDENTITY(1,1),
        [SourceType] nvarchar(450) NOT NULL,
        [DisplayName] nvarchar(max) NOT NULL,
        [BaseUrlTemplate] nvarchar(max) NOT NULL,
        [IsEnabled] bit NOT NULL,
        [MaxItemsToScrape] int NOT NULL,
        [DelayMsMin] int NOT NULL,
        [DelayMsMax] int NOT NULL,
        [Priority] int NOT NULL,
        [Notes] nvarchar(max) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_ScraperConfigurations] PRIMARY KEY ([Id])
    );
    CREATE UNIQUE INDEX [IX_ScraperConfigurations_SourceType]
        ON [sentimentsales].[ScraperConfigurations] ([SourceType]);
END
GO

-- SourceExtractionEvents & SourcedDataPoints (20260603132913) — only if missing
IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id
    WHERE s.name = N'sentimentsales' AND t.name = N'SourceExtractionEvents')
BEGIN
    CREATE TABLE [sentimentsales].[SourceExtractionEvents] (
        [Id] uniqueidentifier NOT NULL,
        [ResearchJobId] uniqueidentifier NOT NULL,
        [LseCompanyId] int NULL,
        [CompanyName] nvarchar(max) NOT NULL,
        [SourceType] nvarchar(max) NOT NULL,
        [SourceLabel] nvarchar(max) NOT NULL,
        [SourceUrl] nvarchar(max) NOT NULL,
        [FieldName] nvarchar(max) NOT NULL,
        [ExtractedValue] nvarchar(max) NOT NULL,
        [RawSnippet] nvarchar(max) NULL,
        [ConfidenceScore] float NOT NULL,
        [ExtractedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_SourceExtractionEvents] PRIMARY KEY ([Id])
    );
    CREATE INDEX [IX_SourceExtractionEvents_ResearchJobId] ON [sentimentsales].[SourceExtractionEvents]([ResearchJobId]);
    CREATE INDEX [IX_SourceExtractionEvents_LseCompanyId] ON [sentimentsales].[SourceExtractionEvents]([LseCompanyId]);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id
    WHERE s.name = N'sentimentsales' AND t.name = N'SourcedDataPoints')
BEGIN
    CREATE TABLE [sentimentsales].[SourcedDataPoints] (
        [Id] int NOT NULL IDENTITY(1,1),
        [LseCompanyId] int NOT NULL,
        [EntityName] nvarchar(max) NOT NULL,
        [FieldName] nvarchar(max) NOT NULL,
        [FieldValue] nvarchar(max) NOT NULL,
        [SourceType] nvarchar(max) NOT NULL,
        [SourceUrl] nvarchar(max) NOT NULL,
        [ConfidenceScore] float NOT NULL,
        [RecordedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_SourcedDataPoints] PRIMARY KEY ([Id])
    );
    CREATE INDEX [IX_SourcedDataPoints_LseCompanyId] ON [sentimentsales].[SourcedDataPoints]([LseCompanyId]);
    CREATE INDEX [IX_SourcedDataPoints_LseCompanyId_FieldName] ON [sentimentsales].[SourcedDataPoints]([LseCompanyId],[FieldName]);
END
GO

-- Record migrations if missing
MERGE [sentimentsales].[__EFMigrationsHistory] AS t
USING (VALUES
    (N'20260603115443_InitialCreate', N'8.0.11'),
    (N'20260603132913_AddSourceExtractionTables', N'8.0.11'),
    (N'20260603145108_AddScraperConfigurations', N'8.0.11')
) AS s ([MigrationId], [ProductVersion])
ON t.[MigrationId] = s.[MigrationId]
WHEN NOT MATCHED THEN INSERT ([MigrationId], [ProductVersion]) VALUES (s.[MigrationId], s.[ProductVersion]);
GO

PRINT 'Schema check complete. Prefer: dotnet ef database update (see Docs/TROUBLESHOOTING.md)';
