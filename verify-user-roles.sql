-- =============================================
-- Verify User Role Assignments
-- Database: agone-uat
-- Schema: core
-- TenantId: aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa
-- =============================================

SET NOCOUNT ON;

DECLARE @TenantId UNIQUEIDENTIFIER = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa';

-- ═══════════════════════════════════════════════════════════════
-- 1. FULL VIEW: Every user + their assigned roles (pivoted by product)
-- ═══════════════════════════════════════════════════════════════
SELECT
    u.Email,
    u.FirstName + ' ' + u.LastName AS FullName,
    STRING_AGG(
        CASE WHEN r.ProductId IS NULL THEN r.DisplayName END, ', '
    ) AS [AG ONE],
    STRING_AGG(
        CASE WHEN p.Code = 'AGOneWork' THEN r.DisplayName END, ', '
    ) AS [AG ONE Work],
    STRING_AGG(
        CASE WHEN p.Code = 'AGOneSafe' THEN r.DisplayName END, ', '
    ) AS [AG ONE Safe],
    STRING_AGG(
        CASE WHEN p.Code = 'AGOneLearn' THEN r.DisplayName END, ', '
    ) AS [AG ONE Learn],
    STRING_AGG(
        CASE WHEN p.Code = 'AGOnePulse' THEN r.DisplayName END, ', '
    ) AS [AG ONE Pulse],
    COUNT(ur.Id) AS TotalRoles
FROM core.Users u
LEFT JOIN core.UserRoles ur ON ur.UserId = u.Id AND ur.TenantId = @TenantId AND ur.IsDeleted = 0
LEFT JOIN core.Roles r ON r.Id = ur.RoleId AND r.IsDeleted = 0
LEFT JOIN core.Products p ON p.Id = r.ProductId
WHERE u.TenantId = @TenantId AND u.IsDeleted = 0
GROUP BY u.Email, u.FirstName, u.LastName
ORDER BY u.Email;

-- ═══════════════════════════════════════════════════════════════
-- 2. DETAILED VIEW: Every UserRole row with priority
-- ═══════════════════════════════════════════════════════════════
SELECT
    u.Email,
    r.DisplayName AS RoleName,
    r.Name AS RoleCode,
    COALESCE(p.Name, 'Platform') AS Product,
    ur.Priority,
    ur.CreatedAt
FROM core.UserRoles ur
INNER JOIN core.Users u ON u.Id = ur.UserId
INNER JOIN core.Roles r ON r.Id = ur.RoleId
LEFT JOIN core.Products p ON p.Id = r.ProductId
WHERE ur.TenantId = @TenantId AND ur.IsDeleted = 0 AND u.IsDeleted = 0
ORDER BY u.Email, ur.Priority;

-- ═══════════════════════════════════════════════════════════════
-- 3. USERS WITH NO ROLES
-- ═══════════════════════════════════════════════════════════════
SELECT u.Email, u.FirstName + ' ' + u.LastName AS FullName
FROM core.Users u
WHERE u.TenantId = @TenantId AND u.IsDeleted = 0
  AND NOT EXISTS (
      SELECT 1 FROM core.UserRoles ur
      WHERE ur.UserId = u.Id AND ur.TenantId = @TenantId AND ur.IsDeleted = 0
  )
ORDER BY u.Email;

-- ═══════════════════════════════════════════════════════════════
-- 4. DUPLICATE ROLE CHECK (same user + same role assigned twice)
-- ═══════════════════════════════════════════════════════════════
SELECT
    u.Email,
    r.DisplayName AS RoleName,
    COUNT(*) AS DuplicateCount
FROM core.UserRoles ur
INNER JOIN core.Users u ON u.Id = ur.UserId
INNER JOIN core.Roles r ON r.Id = ur.RoleId
WHERE ur.TenantId = @TenantId AND ur.IsDeleted = 0
GROUP BY u.Email, r.DisplayName
HAVING COUNT(*) > 1
ORDER BY u.Email;

-- ═══════════════════════════════════════════════════════════════
-- 5. ROLE COUNT SUMMARY
-- ═══════════════════════════════════════════════════════════════
SELECT
    r.DisplayName AS RoleName,
    COALESCE(p.Name, 'Platform') AS Product,
    COUNT(ur.Id) AS AssignedUsers
FROM core.Roles r
LEFT JOIN core.Products p ON p.Id = r.ProductId
LEFT JOIN core.UserRoles ur ON ur.RoleId = r.Id AND ur.TenantId = @TenantId AND ur.IsDeleted = 0
WHERE r.TenantId = @TenantId AND r.IsDeleted = 0
GROUP BY r.DisplayName, p.Name
ORDER BY p.Name, r.DisplayName;

-- ═══════════════════════════════════════════════════════════════
-- 6. SPOT CHECK: Verify specific users match the Excel
-- ═══════════════════════════════════════════════════════════════
SELECT
    u.Email,
    STRING_AGG(r.DisplayName, ', ') WITHIN GROUP (ORDER BY ur.Priority) AS AllRoles
FROM core.UserRoles ur
INNER JOIN core.Users u ON u.Id = ur.UserId
INNER JOIN core.Roles r ON r.Id = ur.RoleId
WHERE ur.TenantId = @TenantId AND ur.IsDeleted = 0 AND u.IsDeleted = 0
  AND u.Email IN (
      'mohan@aventragroup.com',
      'ram.gopal@aventragroup.com',
      'jacky.tan@aventragroup.com',
      'avvinash.ravi@aventragroup.com',
      'sanhita.indulkar@aventragroup.com',
      'sheela.mani@aventragroup.com',
      'sandra.segar@aventragroup.com',
      'pebbles.tan@aventragroup.com'
  )
GROUP BY u.Email
ORDER BY u.Email;
