/* =====================================================================
   INJECTION into the EXISTING [agone-dev].[core] schema.

   You already have:
     RBAC       : core.Users, core.Roles, core.Permissions,
                  core.RolePermissions, core.UserRoles   (+ ProductId/TenantId)
     Catalog    : core.Products, core.ProductFeatureModules,
                  core.ProductFeatures (per-tier availability bits),
                  core.ProductPlanTiers, core.ProductPlanTierPrices,
                  core.ProductPlanTierFeatures, core.ProductPlanTerms
     Commercial : core.Subscriptions (Tenant x Product x plan, licenses, status)

   The ONLY thing missing to turn "what the plan sells" into "what the user
   may actually do" is a link from a ProductFeature to the Permission(s) it
   unlocks. That is the single new table below (+ one optional column on
   Subscriptions to pin the tier). Everything else is read-only views/functions.

   Safe to run on a live DB: additive only, no changes to existing columns.
   Target: SQL Server / Azure SQL.
   ===================================================================== */

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

/* ---------------------------------------------------------------------
   1) THE BRIDGE  — ProductFeature  ->  existing Permission
   Follows your table conventions (Id guid, CreatedAt/UpdatedAt/IsDeleted).
   --------------------------------------------------------------------- */
IF OBJECT_ID('core.ProductFeaturePermissions') IS NULL
BEGIN
    CREATE TABLE core.ProductFeaturePermissions
    (
        [Id]               UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_PFP_Id DEFAULT NEWID(),
        [ProductFeatureId] UNIQUEIDENTIFIER NOT NULL,
        [PermissionId]     UNIQUEIDENTIFIER NOT NULL,
        [CreatedAt]        DATETIME2(7)     NOT NULL CONSTRAINT DF_PFP_CreatedAt DEFAULT SYSUTCDATETIME(),
        [UpdatedAt]        DATETIME2(7)     NULL,
        [IsDeleted]        BIT              NOT NULL CONSTRAINT DF_PFP_IsDeleted DEFAULT (0),
        CONSTRAINT PK_ProductFeaturePermissions PRIMARY KEY CLUSTERED ([Id]),
        CONSTRAINT FK_PFP_ProductFeatures FOREIGN KEY ([ProductFeatureId])
            REFERENCES core.ProductFeatures ([Id]) ON DELETE CASCADE,
        CONSTRAINT FK_PFP_Permissions FOREIGN KEY ([PermissionId])
            REFERENCES core.Permissions ([Id]) ON DELETE CASCADE,
        CONSTRAINT UQ_PFP_Feature_Permission UNIQUE ([ProductFeatureId], [PermissionId])
    );
    CREATE INDEX IX_PFP_ProductFeatureId ON core.ProductFeaturePermissions ([ProductFeatureId]);
    CREATE INDEX IX_PFP_PermissionId     ON core.ProductFeaturePermissions ([PermissionId]);
END
GO

/* ---------------------------------------------------------------------
   2) PIN THE TIER ON THE SUBSCRIPTION  (recommended, optional)
   core.Subscriptions today links to PricingPlanId. To resolve which
   ProductPlanTier (Freemium/Lite/Standard/Enterprise) a subscription is on,
   add a direct FK. If your PricingPlans already carry the tier, you can skip
   this and adjust the view's tier-resolution join instead (see note below).
   --------------------------------------------------------------------- */
IF COL_LENGTH('core.Subscriptions', 'ProductPlanTierId') IS NULL
BEGIN
    ALTER TABLE core.Subscriptions ADD [ProductPlanTierId] UNIQUEIDENTIFIER NULL;
END
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Subscriptions_ProductPlanTiers')
   AND COL_LENGTH('core.Subscriptions', 'ProductPlanTierId') IS NOT NULL
BEGIN
    ALTER TABLE core.Subscriptions WITH CHECK
        ADD CONSTRAINT FK_Subscriptions_ProductPlanTiers
        FOREIGN KEY ([ProductPlanTierId]) REFERENCES core.ProductPlanTiers ([Id]);
END
GO

/* ---------------------------------------------------------------------
   3) TIER RESOLUTION  — one place that answers "which tier is this sub on?"
   Change ONLY this view if your tier lives somewhere else (e.g. PricingPlans).
   --------------------------------------------------------------------- */
CREATE OR ALTER VIEW core.vSubscriptionTier
AS
SELECT
    s.[Id]           AS SubscriptionId,
    s.[TenantId],
    s.[ProductId],
    t.[Id]           AS ProductPlanTierId,
    t.[PlanTier]     AS PlanTier          -- 'Freemium' | 'Lite' | 'Standard' | 'Enterprise'
FROM core.Subscriptions s
JOIN core.ProductPlanTiers t
      ON t.[Id] = s.[ProductPlanTierId]   -- swap for your PricingPlans->tier join if not using the new column
     AND t.[IsDeleted] = 0
WHERE s.[IsDeleted] = 0
  AND s.[Status] IN (N'Active', N'Trialing')          -- adjust to your Status vocabulary
  AND (s.[EndDate] IS NULL OR s.[EndDate] >= SYSUTCDATETIME());
GO

/* ---------------------------------------------------------------------
   4) ENTITLED PERMISSIONS per (Tenant, Product)
   = permissions unlocked by the product's features that are AVAILABLE at the
     subscription's tier (per-tier bits) and not Coming Soon.
   --------------------------------------------------------------------- */
CREATE OR ALTER VIEW core.vTenantEntitledPermissions
AS
SELECT DISTINCT
    st.[TenantId],
    st.[ProductId],
    fpp.[PermissionId],
    p.[Code] AS PermissionCode
FROM core.vSubscriptionTier st
JOIN core.ProductFeatureModules m
      ON m.[ProductId] = st.[ProductId] AND m.[IsDeleted] = 0
JOIN core.ProductFeatures pf
      ON pf.[ProductFeatureModuleId] = m.[Id]
     AND pf.[IsDeleted]    = 0
     AND pf.[IsComingSoon] = 0
     AND CASE st.[PlanTier]
             WHEN N'Freemium'   THEN pf.[AvailableFreemium]
             WHEN N'Lite'       THEN pf.[AvailableLite]
             WHEN N'Standard'   THEN pf.[AvailableStandard]
             WHEN N'Enterprise' THEN pf.[AvailableEnterprise]
             ELSE CONVERT(bit, 0)
         END = 1
JOIN core.ProductFeaturePermissions fpp
      ON fpp.[ProductFeatureId] = pf.[Id] AND fpp.[IsDeleted] = 0
JOIN core.Permissions p
      ON p.[Id] = fpp.[PermissionId] AND p.[IsDeleted] = 0;
GO

/* ---------------------------------------------------------------------
   5) EFFECTIVE PERMISSIONS for a user  =  RBAC  INTERSECT  entitlement
   This is the whole point: a plan can only ever REMOVE permissions the
   user's role already grants — never add new ones.
   --------------------------------------------------------------------- */
CREATE OR ALTER FUNCTION core.fnEffectivePermissions
(
    @TenantId UNIQUEIDENTIFIER,
    @UserId   UNIQUEIDENTIFIER
)
RETURNS TABLE
AS
RETURN
(
    SELECT DISTINCT p.[Id] AS PermissionId, p.[Code] AS PermissionCode, p.[ProductId]
    FROM core.UserRoles ur
    JOIN core.RolePermissions rp
          ON rp.[RoleId] = ur.[RoleId] AND rp.[IsDeleted] = 0
    JOIN core.Permissions p
          ON p.[Id] = rp.[PermissionId] AND p.[IsDeleted] = 0
    JOIN core.vTenantEntitledPermissions ep
          ON ep.[PermissionId] = p.[Id]
         AND ep.[TenantId]     = @TenantId
    WHERE ur.[UserId]   = @UserId
      AND ur.[TenantId] = @TenantId
      AND ur.[IsDeleted] = 0
);
GO

/* ---------------------------------------------------------------------
   6) SINGLE-PERMISSION CHECK (handy for API guards / debugging)
   Returns 1 when the user's role grants it AND the plan entitles it.
   --------------------------------------------------------------------- */
CREATE OR ALTER FUNCTION core.fnUserHasEffectivePermission
(
    @TenantId       UNIQUEIDENTIFIER,
    @UserId         UNIQUEIDENTIFIER,
    @PermissionCode NVARCHAR(200)
)
RETURNS BIT
AS
BEGIN
    RETURN (
        CASE WHEN EXISTS (
            SELECT 1 FROM core.fnEffectivePermissions(@TenantId, @UserId)
            WHERE PermissionCode = @PermissionCode
        ) THEN 1 ELSE 0 END
    );
END
GO

/* ---------------------------------------------------------------------
   7) SEED THE BRIDGE (example)
   Map an existing ProductFeature to existing Permission(s) by their codes.
   Repeat per feature; this is the only ongoing authoring you add.
   --------------------------------------------------------------------- */
-- Example: unlock hire employee read/write when the "Employee Master Data" feature is in the tier
/*
INSERT INTO core.ProductFeaturePermissions (ProductFeatureId, PermissionId)
SELECT pf.Id, p.Id
FROM core.ProductFeatures pf
JOIN core.Permissions p ON p.Code IN (N'hire.employee.read', N'hire.employee.write')
WHERE pf.Name = N'Employee Master Data'
  AND NOT EXISTS (SELECT 1 FROM core.ProductFeaturePermissions x
                  WHERE x.ProductFeatureId = pf.Id AND x.PermissionId = p.Id);
*/
GO

PRINT 'Entitlement injection complete: core.ProductFeaturePermissions + tier resolution views/functions.';
GO
