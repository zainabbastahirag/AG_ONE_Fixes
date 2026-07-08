/* =====================================================================
   AG ONE — Subscription / Entitlement schema
   Adds a `billing` schema that layers ON TOP of the existing RBAC tables
   (core.Users, core.Roles, core.Permissions, core.RolePermissions,
    core.UserRoles). Nothing in core.* is modified.

   Target: SQL Server 2019+ / Azure SQL.
   ===================================================================== */

IF SCHEMA_ID('billing') IS NULL EXEC('CREATE SCHEMA billing');
GO

/* ---- Products : one row per product (maps to a DB schema hire/learn/work) ---- */
IF OBJECT_ID('billing.Products') IS NULL
CREATE TABLE billing.Products
(
    ProductId   INT           NOT NULL PRIMARY KEY,   -- 1=Hire 2=Learn 3=Work 99=Platform
    [Key]       VARCHAR(20)   NOT NULL UNIQUE,         -- 'hire' | 'learn' | 'work' | 'platform'
    Name        NVARCHAR(100) NOT NULL
);
GO

/* ---- Features : the sellable capabilities ---- */
IF OBJECT_ID('billing.Features') IS NULL
CREATE TABLE billing.Features
(
    FeatureId     INT IDENTITY(1,1) PRIMARY KEY,
    [Key]         VARCHAR(100)  NOT NULL UNIQUE,       -- e.g. 'hire.document_management'
    ProductId     INT           NOT NULL REFERENCES billing.Products(ProductId),
    Name          NVARCHAR(150) NOT NULL,
    [Description]  NVARCHAR(400) NULL,
    IsComingSoon  BIT           NOT NULL DEFAULT 0
);
GO

/* ---- FeaturePermissions : THE BRIDGE to existing permissions ----
   Each feature declares which existing core.Permissions codes it unlocks.
   This is what lets subscription plans reuse the permissions you already wired up. */
IF OBJECT_ID('billing.FeaturePermissions') IS NULL
CREATE TABLE billing.FeaturePermissions
(
    FeatureId       INT         NOT NULL REFERENCES billing.Features(FeatureId) ON DELETE CASCADE,
    PermissionCode  VARCHAR(150) NOT NULL,             -- FK-by-value to core.Permissions.Code
    CONSTRAINT PK_FeaturePermissions PRIMARY KEY (FeatureId, PermissionCode)
);
GO

/* ---- Plans : commercial tiers per product ---- */
IF OBJECT_ID('billing.Plans') IS NULL
CREATE TABLE billing.Plans
(
    PlanId        INT IDENTITY(1,1) PRIMARY KEY,
    [Key]         VARCHAR(50)   NOT NULL UNIQUE,       -- e.g. 'hire.standard'
    ProductId     INT           NOT NULL REFERENCES billing.Products(ProductId),
    Tier          INT           NOT NULL,              -- 10 Starter 20 Lite 30 Standard 40 Enterprise
    Name          NVARCHAR(100) NOT NULL,
    [Description]  NVARCHAR(300) NULL
);
GO

/* ---- PlanFeatures : which features each plan bundles ---- */
IF OBJECT_ID('billing.PlanFeatures') IS NULL
CREATE TABLE billing.PlanFeatures
(
    PlanId    INT NOT NULL REFERENCES billing.Plans(PlanId)    ON DELETE CASCADE,
    FeatureId INT NOT NULL REFERENCES billing.Features(FeatureId) ON DELETE CASCADE,
    CONSTRAINT PK_PlanFeatures PRIMARY KEY (PlanId, FeatureId)
);
GO

/* ---- Tenants : customer organizations ---- */
IF OBJECT_ID('billing.Tenants') IS NULL
CREATE TABLE billing.Tenants
(
    TenantId  UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    Name      NVARCHAR(200)    NOT NULL,
    CreatedUtc DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME()
);
GO

/* ---- Subscriptions : a tenant's subscription to one product ---- */
IF OBJECT_ID('billing.Subscriptions') IS NULL
CREATE TABLE billing.Subscriptions
(
    SubscriptionId       UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    TenantId             UNIQUEIDENTIFIER NOT NULL REFERENCES billing.Tenants(TenantId),
    ProductId            INT              NOT NULL REFERENCES billing.Products(ProductId),
    PlanId               INT              NOT NULL REFERENCES billing.Plans(PlanId),
    [Status]             TINYINT          NOT NULL,   -- 1 Trialing 2 Active 3 PastDue 4 Canceled 5 Expired
    StartUtc             DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    CurrentPeriodEndUtc  DATETIME2        NULL,
    TrialEndUtc          DATETIME2        NULL,
    GraceDays            INT              NOT NULL DEFAULT 7,
    CONSTRAINT UQ_Subscription_Tenant_Product UNIQUE (TenantId, ProductId)
);
GO

/* ---- SubscriptionAddOns : features sold on top of the plan ---- */
IF OBJECT_ID('billing.SubscriptionAddOns') IS NULL
CREATE TABLE billing.SubscriptionAddOns
(
    SubscriptionId UNIQUEIDENTIFIER NOT NULL REFERENCES billing.Subscriptions(SubscriptionId) ON DELETE CASCADE,
    FeatureId      INT              NOT NULL REFERENCES billing.Features(FeatureId),
    CONSTRAINT PK_SubscriptionAddOns PRIMARY KEY (SubscriptionId, FeatureId)
);
GO

/* =====================================================================
   Effective-entitlement view.
   Returns, per tenant+product, the permission codes currently entitled by
   the active plan (+ add-ons), excluding coming-soon features and inactive
   subscriptions (respecting trial / period-end / past-due grace window).
   ===================================================================== */
CREATE OR ALTER VIEW billing.vEntitledPermissions
AS
WITH ActiveSub AS (
    SELECT s.SubscriptionId, s.TenantId, s.ProductId, s.PlanId
    FROM billing.Subscriptions s
    WHERE
        (s.[Status] = 2 AND (s.CurrentPeriodEndUtc IS NULL OR s.CurrentPeriodEndUtc >= SYSUTCDATETIME()))  -- Active
     OR (s.[Status] = 1 AND (s.TrialEndUtc IS NULL OR s.TrialEndUtc >= SYSUTCDATETIME()))                   -- Trialing
     OR (s.[Status] = 3 AND DATEADD(DAY, s.GraceDays, ISNULL(s.CurrentPeriodEndUtc, SYSUTCDATETIME())) >= SYSUTCDATETIME()) -- PastDue in grace
),
EntitledFeatures AS (
    SELECT a.TenantId, a.ProductId, pf.FeatureId
    FROM ActiveSub a
    JOIN billing.PlanFeatures pf ON pf.PlanId = a.PlanId
    UNION
    SELECT a.TenantId, a.ProductId, ao.FeatureId
    FROM ActiveSub a
    JOIN billing.SubscriptionAddOns ao ON ao.SubscriptionId = a.SubscriptionId
)
SELECT DISTINCT
    ef.TenantId,
    ef.ProductId,
    fp.PermissionCode
FROM EntitledFeatures ef
JOIN billing.Features f  ON f.FeatureId = ef.FeatureId AND f.IsComingSoon = 0
JOIN billing.FeaturePermissions fp ON fp.FeatureId = f.FeatureId;
GO

/* =====================================================================
   Effective permissions for a specific user =
        RBAC (role-granted) INTERSECT entitlement (plan-included).
   Adjust the core.* joins to match your exact RBAC column names.
   ===================================================================== */
CREATE OR ALTER FUNCTION billing.fnEffectivePermissions
(
    @TenantId UNIQUEIDENTIFIER,
    @UserId   UNIQUEIDENTIFIER
)
RETURNS TABLE
AS
RETURN
(
    SELECT DISTINCT p.Code AS PermissionCode
    FROM core.UserRoles ur
    JOIN core.RolePermissions rp ON rp.RoleId = ur.RoleId
    JOIN core.Permissions p      ON p.PermissionId = rp.PermissionId
    JOIN billing.vEntitledPermissions ep
         ON ep.PermissionCode = p.Code
        AND ep.TenantId = @TenantId
    WHERE ur.UserId = @UserId
);
GO
