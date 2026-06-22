-- =============================================
-- CLEANUP: Remove duplicate roles, normalize role names,
-- reassign UserRoles to canonical roles, delete orphans
--
-- Database: agone-uat | Schema: core
-- TenantId: aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa
--
-- Run in a transaction — review results before COMMIT
-- =============================================

SET NOCOUNT ON;
BEGIN TRANSACTION;

DECLARE @TenantId UNIQUEIDENTIFIER = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa';
DECLARE @Now DATETIME2 = GETUTCDATE();

-- Product IDs (from your data)
DECLARE @WorkProd  UNIQUEIDENTIFIER = '3e904a4c-4e46-420a-9b72-59de8035de27';
DECLARE @LearnProd UNIQUEIDENTIFIER = '11111111-1111-1111-1111-111111111111';
DECLARE @SafeProd  UNIQUEIDENTIFIER = '101af08b-3b8d-4ca7-86fc-32236a62b3d9';
DECLARE @PulseProd UNIQUEIDENTIFIER = '44444444-4444-4444-4444-444444444444';

-- ═══════════════════════════════════════════════════════════════
-- STEP 1: Define the canonical roles we WANT to keep
-- ═══════════════════════════════════════════════════════════════

CREATE TABLE #CanonicalRoles (
    Id              UNIQUEIDENTIFIER,
    Name            NVARCHAR(100),
    DisplayName     NVARCHAR(256),
    Description     NVARCHAR(MAX),
    IsSystemRole    BIT,
    ProductId       UNIQUEIDENTIFIER NULL,
    IsPlatformRole  BIT,
    IsTenantRole    BIT
);

-- Platform roles (no product)
INSERT INTO #CanonicalRoles VALUES (NEWID(), 'PlatformAdmin',  'Platform Admin',  'Full access to everything - Manage all tenants, products, users, and system settings', 1, NULL, 1, 0);
INSERT INTO #CanonicalRoles VALUES (NEWID(), 'TenantOwner',    'Tenant Owner',    'Tenant (Customer) account highest authorised person',          0, NULL, 0, 1);
INSERT INTO #CanonicalRoles VALUES (NEWID(), 'TenantAdmin',    'Tenant Admin',    'Tenant (Customer) account administration',                     0, NULL, 0, 1);

-- AG ONE Work roles
INSERT INTO #CanonicalRoles VALUES (NEWID(), 'ProductOwner',       'Product Owner',       'Product (Customer) account highest authorised person',                                      0, @WorkProd, 0, 0);
INSERT INTO #CanonicalRoles VALUES (NEWID(), 'ProductAdmin',       'Product Admin',       'Product (Customer) account administration',                                                  0, @WorkProd, 0, 0);
INSERT INTO #CanonicalRoles VALUES (NEWID(), 'HRAdmin',            'HR Admin',            'Can manage all candidate / employee profile & lifecycle',                                    0, @WorkProd, 0, 0);
INSERT INTO #CanonicalRoles VALUES (NEWID(), 'ReportingManager',   'Reporting Manager',   'Can manage and review assigned employees',                                                   0, @WorkProd, 0, 0);
INSERT INTO #CanonicalRoles VALUES (NEWID(), 'Employee',           'Employee',            'Standard user with access to personal information and assigned system features',              0, @WorkProd, 0, 0);
INSERT INTO #CanonicalRoles VALUES (NEWID(), 'DepartmentOwner',    'Department Owner',    'Can have visibility of all employees within their assigned department',                      0, @WorkProd, 0, 0);

-- AG ONE Learn roles
INSERT INTO #CanonicalRoles VALUES (NEWID(), 'ProductOwner',       'Product Owner',       'Product (Customer) account highest authorised person',                                      0, @LearnProd, 0, 0);
INSERT INTO #CanonicalRoles VALUES (NEWID(), 'ProductAdmin',       'Product Admin',       'Product (Customer) account administration',                                                  0, @LearnProd, 0, 0);
INSERT INTO #CanonicalRoles VALUES (NEWID(), 'ReportingManager',   'Reporting Manager',   'Can manage and review assigned employees',                                                   0, @LearnProd, 0, 0);
INSERT INTO #CanonicalRoles VALUES (NEWID(), 'Employee',           'Employee',            'Standard user with access to personal information and assigned system features',              0, @LearnProd, 0, 0);
INSERT INTO #CanonicalRoles VALUES (NEWID(), 'DepartmentOwner',    'Department Owner',    'Can have visibility of all employees within their assigned department',                      0, @LearnProd, 0, 0);
INSERT INTO #CanonicalRoles VALUES (NEWID(), 'LearningAndDevelopment', 'Learning and Development', 'Learning & Development team role',                                                  0, @LearnProd, 0, 0);

-- AG ONE Safe roles
INSERT INTO #CanonicalRoles VALUES (NEWID(), 'ProductOwner',       'Product Owner',       'Product (Customer) account highest authorised person',                                      0, @SafeProd, 0, 0);
INSERT INTO #CanonicalRoles VALUES (NEWID(), 'ProductAdmin',       'Product Admin',       'Product (Customer) account administration',                                                  0, @SafeProd, 0, 0);
INSERT INTO #CanonicalRoles VALUES (NEWID(), 'ComplianceAdmin',    'Compliance Admin',    'Can access all features of the product + selective policy approving capabilities',            0, @SafeProd, 0, 0);

-- AG ONE Pulse roles
INSERT INTO #CanonicalRoles VALUES (NEWID(), 'ProductOwner',       'Product Owner',       'Product (Customer) account highest authorised person',                                      0, @PulseProd, 0, 0);
INSERT INTO #CanonicalRoles VALUES (NEWID(), 'ProductAdmin',       'Product Admin',       'Product (Customer) account administration',                                                  0, @PulseProd, 0, 0);
INSERT INTO #CanonicalRoles VALUES (NEWID(), 'ReportingManager',   'Reporting Manager',   'Can manage and review assigned employees',                                                   0, @PulseProd, 0, 0);
INSERT INTO #CanonicalRoles VALUES (NEWID(), 'Employee',           'Employee',            'Standard user with access to personal information and assigned system features',              0, @PulseProd, 0, 0);

-- ═══════════════════════════════════════════════════════════════
-- STEP 2: Build mapping from OLD role IDs → NEW canonical role IDs
-- Match by: role Name (normalized) + ProductId
-- ═══════════════════════════════════════════════════════════════

CREATE TABLE #RoleMapping (
    OldRoleId   UNIQUEIDENTIFIER,
    NewRoleId   UNIQUEIDENTIFIER
);

-- Map old roles to new canonical roles
INSERT INTO #RoleMapping (OldRoleId, NewRoleId)
SELECT
    old.Id,
    can.Id
FROM core.Roles old
CROSS APPLY (
    SELECT TOP 1 can.Id
    FROM #CanonicalRoles can
    WHERE (
        -- Exact name match
        LOWER(REPLACE(REPLACE(old.Name, ' ', ''), '-', '')) = LOWER(REPLACE(REPLACE(can.Name, ' ', ''), '-', ''))
        -- Or display name contains canonical name
        OR LOWER(old.DisplayName) LIKE '%' + LOWER(can.DisplayName) + '%'
        -- Special cases
        OR (LOWER(old.Name) = 'tenant owner' AND can.Name = 'TenantOwner')
        OR (LOWER(old.Name) = 'tenantadmin' AND can.Name = 'TenantAdmin')
        OR (LOWER(old.Name) LIKE '%product owner%' AND can.Name = 'ProductOwner')
        OR (LOWER(old.Name) LIKE '%product admin%' AND can.Name = 'ProductAdmin')
    )
    AND (
        -- Match product: both null (platform), or same product
        (old.ProductId IS NULL AND can.ProductId IS NULL)
        OR old.ProductId = can.ProductId
        -- Product-specific roles in old display name
        OR (old.DisplayName LIKE '%AG ONE Learn%' AND can.ProductId = @LearnProd)
        OR (old.DisplayName LIKE '%AG ONE Work%' AND can.ProductId = @WorkProd)
        OR (old.DisplayName LIKE '%AG ONE Safe%' AND can.ProductId = @SafeProd)
        OR (old.DisplayName LIKE '%AG ONE Pulse%' AND can.ProductId = @PulseProd)
    )
) can
WHERE old.TenantId = @TenantId OR old.TenantId IS NULL;

-- ═══════════════════════════════════════════════════════════════
-- STEP 3: Show the mapping (review this!)
-- ═══════════════════════════════════════════════════════════════

PRINT '=== ROLE MAPPING (Old → New) ===';
SELECT
    old.Name AS OldName,
    old.DisplayName AS OldDisplayName,
    p1.Name AS OldProduct,
    '→' AS [→],
    can.Name AS NewName,
    can.DisplayName AS NewDisplayName,
    COALESCE(p2.Name, 'Platform') AS NewProduct
FROM #RoleMapping rm
INNER JOIN core.Roles old ON old.Id = rm.OldRoleId
INNER JOIN #CanonicalRoles can ON can.Id = rm.NewRoleId
LEFT JOIN core.Products p1 ON p1.Id = old.ProductId
LEFT JOIN core.Products p2 ON p2.Id = can.ProductId
ORDER BY can.ProductId, can.Name;

-- Show unmapped old roles (these will be orphaned)
PRINT '=== UNMAPPED OLD ROLES (will be deleted) ===';
SELECT old.Id, old.Name, old.DisplayName, p.Name AS Product
FROM core.Roles old
LEFT JOIN #RoleMapping rm ON rm.OldRoleId = old.Id
LEFT JOIN core.Products p ON p.Id = old.ProductId
WHERE rm.OldRoleId IS NULL AND (old.TenantId = @TenantId OR old.TenantId IS NULL);

-- ═══════════════════════════════════════════════════════════════
-- STEP 4: Delete ALL existing roles for this tenant
-- ═══════════════════════════════════════════════════════════════

-- First reassign UserRoles to canonical role IDs
UPDATE ur
SET ur.RoleId = rm.NewRoleId,
    ur.ProductId = can.ProductId,
    ur.UpdatedAt = @Now
FROM core.UserRoles ur
INNER JOIN #RoleMapping rm ON ur.RoleId = rm.OldRoleId
INNER JOIN #CanonicalRoles can ON can.Id = rm.NewRoleId
WHERE ur.TenantId = @TenantId;

DECLARE @UserRolesUpdated INT = @@ROWCOUNT;
PRINT 'Updated ' + CAST(@UserRolesUpdated AS VARCHAR) + ' UserRole records to point to new canonical roles.';

-- Delete UserRoles that point to roles we can't map
DELETE ur
FROM core.UserRoles ur
LEFT JOIN #RoleMapping rm ON ur.RoleId = rm.OldRoleId
WHERE ur.TenantId = @TenantId
  AND rm.OldRoleId IS NULL
  AND NOT EXISTS (SELECT 1 FROM #CanonicalRoles c WHERE c.Id = ur.RoleId);

DECLARE @OrphanUserRolesDeleted INT = @@ROWCOUNT;
PRINT 'Deleted ' + CAST(@OrphanUserRolesDeleted AS VARCHAR) + ' orphan UserRole records.';

-- Delete RolePermissions for old roles
DELETE rp
FROM core.RolePermissions rp
INNER JOIN core.Roles r ON r.Id = rp.RoleId
WHERE (r.TenantId = @TenantId OR r.TenantId IS NULL)
  AND NOT EXISTS (SELECT 1 FROM #CanonicalRoles c WHERE c.Id = rp.RoleId);

DECLARE @RolePermsDeleted INT = @@ROWCOUNT;
PRINT 'Deleted ' + CAST(@RolePermsDeleted AS VARCHAR) + ' old RolePermission records.';

-- Delete old roles
DELETE FROM core.Roles
WHERE (TenantId = @TenantId OR TenantId IS NULL)
  AND Id NOT IN (SELECT Id FROM #CanonicalRoles);

DECLARE @OldRolesDeleted INT = @@ROWCOUNT;
PRINT 'Deleted ' + CAST(@OldRolesDeleted AS VARCHAR) + ' old Role records.';

-- ═══════════════════════════════════════════════════════════════
-- STEP 5: Insert canonical roles
-- ═══════════════════════════════════════════════════════════════

INSERT INTO core.Roles (Id, Name, DisplayName, Description, IsSystemRole, ProductId, CreatedAt, IsDeleted, TenantId, IsPlatformRole, IsTenantRole)
SELECT
    Id, Name, DisplayName, Description, IsSystemRole, ProductId, @Now, 0,
    CASE WHEN IsPlatformRole = 1 THEN NULL ELSE @TenantId END,
    IsPlatformRole, IsTenantRole
FROM #CanonicalRoles c
WHERE NOT EXISTS (SELECT 1 FROM core.Roles r WHERE r.Id = c.Id);

DECLARE @NewRolesInserted INT = @@ROWCOUNT;
PRINT 'Inserted ' + CAST(@NewRolesInserted AS VARCHAR) + ' new canonical roles.';

-- Remove duplicate UserRoles (same user + same role)
;WITH Dupes AS (
    SELECT Id, ROW_NUMBER() OVER (PARTITION BY UserId, RoleId, TenantId ORDER BY Priority, CreatedAt) AS rn
    FROM core.UserRoles
    WHERE TenantId = @TenantId AND IsDeleted = 0
)
DELETE FROM core.UserRoles WHERE Id IN (SELECT Id FROM Dupes WHERE rn > 1);

DECLARE @DupeUserRolesDeleted INT = @@ROWCOUNT;
PRINT 'Deleted ' + CAST(@DupeUserRolesDeleted AS VARCHAR) + ' duplicate UserRole records.';

-- ═══════════════════════════════════════════════════════════════
-- STEP 6: Verify final state
-- ═══════════════════════════════════════════════════════════════

PRINT '';
PRINT '=== FINAL ROLES ===';
SELECT
    r.Name,
    r.DisplayName,
    COALESCE(p.Name, 'Platform') AS Product,
    r.IsSystemRole,
    r.IsPlatformRole,
    r.IsTenantRole,
    (SELECT COUNT(*) FROM core.UserRoles ur WHERE ur.RoleId = r.Id AND ur.TenantId = @TenantId AND ur.IsDeleted = 0) AS UserCount
FROM core.Roles r
LEFT JOIN core.Products p ON p.Id = r.ProductId
WHERE r.TenantId = @TenantId OR r.TenantId IS NULL
ORDER BY r.IsPlatformRole DESC, r.IsTenantRole DESC, p.Name, r.Name;

PRINT '';
PRINT '=== USER ROLE ASSIGNMENTS ===';
SELECT
    u.Email,
    r.DisplayName AS RoleName,
    COALESCE(p.Name, 'Platform') AS Product
FROM core.UserRoles ur
INNER JOIN core.Users u ON u.Id = ur.UserId
INNER JOIN core.Roles r ON r.Id = ur.RoleId
LEFT JOIN core.Products p ON p.Id = r.ProductId
WHERE ur.TenantId = @TenantId AND ur.IsDeleted = 0
ORDER BY u.Email, p.Name, r.Name;

DROP TABLE #CanonicalRoles;
DROP TABLE #RoleMapping;

-- Review everything above, then:
-- COMMIT TRANSACTION;
-- Or if something looks wrong:
-- ROLLBACK TRANSACTION;
