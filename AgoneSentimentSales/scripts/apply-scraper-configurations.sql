-- Run against database AgoneSentimentSales if EF migration 20260603145108 was not applied.
-- Fixes: Invalid object name 'sentimentsales.ScraperConfigurations'

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'sentimentsales')
    EXEC('CREATE SCHEMA [sentimentsales]');

IF NOT EXISTS (SELECT 1 FROM sys.tables t
    INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
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

IF NOT EXISTS (SELECT 1 FROM [sentimentsales].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603145108_AddScraperConfigurations')
BEGIN
    INSERT INTO [sentimentsales].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260603145108_AddScraperConfigurations', N'8.0.11');
END
GO
