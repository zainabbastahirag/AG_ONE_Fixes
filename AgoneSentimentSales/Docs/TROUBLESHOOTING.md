# Troubleshooting

## `Invalid object name 'sentimentsales.ScraperConfigurations'`

This means the **AddScraperConfigurations** EF migration has not been applied to your SQL Server database yet.

### Fix (recommended) — apply all pending migrations

1. **Start SQL Server** (Docker example):

   ```bash
   cd AgoneSentimentSales
   docker compose up -d sqlserver
   ```

   Wait 20–40 seconds until SQL accepts connections.

2. **Confirm connection string** in `Src/AgoneSentimentSales.API/appsettings.json` matches your server:

   ```json
   "DefaultConnection": "Server=localhost,1433;Database=AgoneSentimentSales;User Id=sa;Password=AgoneSentiment!2026;TrustServerCertificate=True;Encrypt=False;"
   ```

3. **Apply migrations** from the `Src` folder:

   ```bash
   cd AgoneSentimentSales/Src
   dotnet ef database update --project AgoneSentimentSales.Infrastructure --startup-project AgoneSentimentSales.API
   ```

   Or use the script:

   ```bash
   chmod +x AgoneSentimentSales/scripts/update-database.sh
   ./AgoneSentimentSales/scripts/update-database.sh
   ```

4. **Restart the API**:

   ```bash
   cd AgoneSentimentSales/Src
   dotnet run --project AgoneSentimentSales.API --urls http://localhost:5080
   ```

   On startup you should see log lines like: `Applying migrations (attempt 1)...`

5. **Verify the table exists** (optional, in `sqlcmd` or Azure Data Studio):

   ```sql
   SELECT * FROM sentimentsales.ScraperConfigurations;
   SELECT MigrationId FROM sentimentsales.__EFMigrationsHistory ORDER BY MigrationId;
   ```

   You should see migration id: `20260603145108_AddScraperConfigurations`.

### If `dotnet ef` is not installed

```bash
dotnet tool install --global dotnet-ef
dotnet ef --version
```

Then repeat step 3.

### Manual SQL (only if EF update fails)

Run this against database **AgoneSentimentSales**:

```sql
IF NOT EXISTS (SELECT 1 FROM sys.tables t
    JOIN sys.schemas s ON t.schema_id = s.schema_id
    WHERE s.name = 'sentimentsales' AND t.name = 'ScraperConfigurations')
BEGIN
    CREATE TABLE [sentimentsales].[ScraperConfigurations] (
        [Id] int NOT NULL IDENTITY,
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

IF NOT EXISTS (SELECT 1 FROM [sentimentsales].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603145108_AddScraperConfigurations')
    INSERT INTO [sentimentsales].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260603145108_AddScraperConfigurations', N'8.0.11');
```

Restart the API after this so seed data can load.

### Still failing?

- Ensure you are on the latest code (branch with `AddScraperConfigurations` migration).
- Delete and recreate the database only in **dev** if you have no data to keep:

  ```bash
  # Destructive — removes all research data
  dotnet ef database drop --project AgoneSentimentSales.Infrastructure --startup-project AgoneSentimentSales.API --force
  dotnet ef database update --project AgoneSentimentSales.Infrastructure --startup-project AgoneSentimentSales.API
  ```
