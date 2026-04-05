-- =============================================
-- Assign User Roles from Excel Mapping
-- Database: agone-uat | Schema: core
-- TenantId: aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa
--
-- Behavior:
--   - User NOT found by email → SKIP (logged in output)
--   - UserRole already exists → UPDATE Priority
--   - UserRole not exists     → INSERT new
-- =============================================

SET NOCOUNT ON;
BEGIN TRANSACTION;

DECLARE @TenantId UNIQUEIDENTIFIER = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa';
DECLARE @Now DATETIME2 = GETUTCDATE();

-- ═══ Role IDs ═══
DECLARE @TenantOwner    UNIQUEIDENTIFIER = '0043fdd3-2830-4359-8a5b-5c43c690f9d5';
DECLARE @TenantAdmin    UNIQUEIDENTIFIER = '0fbdad30-c984-4bd7-a462-f86e89dcb1ac';
DECLARE @WorkProdAdmin  UNIQUEIDENTIFIER = 'a104b5d5-128b-4e61-994b-d3e575b061cd';
DECLARE @WorkHRAdmin    UNIQUEIDENTIFIER = 'f4199238-8072-419e-b6fe-99a4be055c8a';
DECLARE @WorkEmployee   UNIQUEIDENTIFIER = 'e3a8db4a-4ef5-4bc8-b8f4-9efabc5869bd';
DECLARE @WorkReportMgr  UNIQUEIDENTIFIER = '1c32f391-6f44-489a-8ff3-e2b6723225e5';
DECLARE @LearnProdAdmin UNIQUEIDENTIFIER = '126b6ef5-5dcc-401c-8412-b2acbf2144de';
DECLARE @LearnProdOwner UNIQUEIDENTIFIER = '5c37c972-668c-497d-84b0-93b9e1d4424c';
DECLARE @LearnEmployee  UNIQUEIDENTIFIER = '9542d31a-d792-465a-a58b-1516413e8510';
DECLARE @LearnReportMgr UNIQUEIDENTIFIER = '8f6a6e7d-ce34-44f2-b490-394e1536a90c';
DECLARE @LearnDeptOwner UNIQUEIDENTIFIER = 'ae62cf38-ae20-4f70-87be-b9d90e8dc552';
DECLARE @LearnLnD       UNIQUEIDENTIFIER = '46052b79-034a-444f-a713-7f711d3294c4';
DECLARE @SafeProdAdmin  UNIQUEIDENTIFIER = '60c0542a-1af4-4f2a-9f04-9ae79d7b948c';
DECLARE @SafeCompliance UNIQUEIDENTIFIER = '1de6a52f-1b74-4a18-8dfb-3d46719d7e27';

DECLARE @WorkProd  UNIQUEIDENTIFIER = '3e904a4c-4e46-420a-9b72-59de8035de27';
DECLARE @LearnProd UNIQUEIDENTIFIER = '11111111-1111-1111-1111-111111111111';
DECLARE @SafeProd  UNIQUEIDENTIFIER = '101af08b-3b8d-4ca7-86fc-32236a62b3d9';

-- ═══ Staging ═══
CREATE TABLE #Staging (
    Email     NVARCHAR(256),
    RoleId    UNIQUEIDENTIFIER,
    ProductId UNIQUEIDENTIFIER NULL,
    Priority  INT
);

-- ═══════════════════════════════════════════════════════════════
-- DATA
-- ═══════════════════════════════════════════════════════════════

-- mohan — Tenant Owner, Product Owner(All)
INSERT INTO #Staging VALUES
('mohan@aventragroup.com', @TenantOwner,    NULL,       1),
('mohan@aventragroup.com', @LearnProdOwner, @LearnProd, 2),
('mohan@aventragroup.com', @WorkProdAdmin,  @WorkProd,  3),
('mohan@aventragroup.com', @SafeProdAdmin,  @SafeProd,  4);

-- venkatesan — Tenant Admin
INSERT INTO #Staging VALUES
('venkatesan.rajendran@aventragroup.com', @TenantAdmin, NULL, 1);

-- ram.gopal — Tenant Owner, Product Admin(All), Learn: Employee,RM,DeptOwner
INSERT INTO #Staging VALUES
('ram.gopal@aventragroup.com', @TenantOwner,    NULL,       1),
('ram.gopal@aventragroup.com', @WorkProdAdmin,  @WorkProd,  2),
('ram.gopal@aventragroup.com', @LearnProdAdmin, @LearnProd, 3),
('ram.gopal@aventragroup.com', @SafeProdAdmin,  @SafeProd,  4),
('ram.gopal@aventragroup.com', @LearnEmployee,  @LearnProd, 5),
('ram.gopal@aventragroup.com', @LearnReportMgr, @LearnProd, 6),
('ram.gopal@aventragroup.com', @LearnDeptOwner, @LearnProd, 7);

-- jacky.tan — Tenant Owner, Product Admin(All), Learn: Employee,RM,DeptOwner
INSERT INTO #Staging VALUES
('jacky.tan@aventragroup.com', @TenantOwner,    NULL,       1),
('jacky.tan@aventragroup.com', @WorkProdAdmin,  @WorkProd,  2),
('jacky.tan@aventragroup.com', @LearnProdAdmin, @LearnProd, 3),
('jacky.tan@aventragroup.com', @SafeProdAdmin,  @SafeProd,  4),
('jacky.tan@aventragroup.com', @LearnEmployee,  @LearnProd, 5),
('jacky.tan@aventragroup.com', @LearnReportMgr, @LearnProd, 6),
('jacky.tan@aventragroup.com', @LearnDeptOwner, @LearnProd, 7);

-- avvinash — Tenant Admin, Product Admin(All), Learn: Employee,RM
INSERT INTO #Staging VALUES
('avvinash.ravi@aventragroup.com', @TenantAdmin,    NULL,       1),
('avvinash.ravi@aventragroup.com', @WorkProdAdmin,  @WorkProd,  2),
('avvinash.ravi@aventragroup.com', @LearnProdAdmin, @LearnProd, 3),
('avvinash.ravi@aventragroup.com', @SafeProdAdmin,  @SafeProd,  4),
('avvinash.ravi@aventragroup.com', @LearnEmployee,  @LearnProd, 5),
('avvinash.ravi@aventragroup.com', @LearnReportMgr, @LearnProd, 6);

-- ahmad.kamalhazmi — Product Admin(Learn), Learn: Employee
INSERT INTO #Staging VALUES
('ahmad.kamalhazmi@aventragroup.com', @LearnProdAdmin, @LearnProd, 1),
('ahmad.kamalhazmi@aventragroup.com', @LearnEmployee,  @LearnProd, 2);

-- shama — Product Admin(Safe), Learn: Employee
INSERT INTO #Staging VALUES
('shama.m@aventragroup.com', @SafeProdAdmin, @SafeProd,  1),
('shama.m@aventragroup.com', @LearnEmployee, @LearnProd, 2);

-- Ngan.Le — Product Admin(Work), Learn: Employee
INSERT INTO #Staging VALUES
('ngan.le@aventragroup.com', @WorkProdAdmin, @WorkProd,  1),
('ngan.le@aventragroup.com', @LearnEmployee, @LearnProd, 2);

-- pebbles — Tenant Admin, Product Admin(All), Learn: Employee,RM
INSERT INTO #Staging VALUES
('pebbles.tan@aventragroup.com', @TenantAdmin,    NULL,       1),
('pebbles.tan@aventragroup.com', @WorkProdAdmin,  @WorkProd,  2),
('pebbles.tan@aventragroup.com', @LearnProdAdmin, @LearnProd, 3),
('pebbles.tan@aventragroup.com', @SafeProdAdmin,  @SafeProd,  4),
('pebbles.tan@aventragroup.com', @LearnEmployee,  @LearnProd, 5),
('pebbles.tan@aventragroup.com', @LearnReportMgr, @LearnProd, 6);

-- sanhita — Work: Employee, HR Admin, RM
INSERT INTO #Staging VALUES
('sanhita.indulkar@aventragroup.com', @WorkEmployee,  @WorkProd, 1),
('sanhita.indulkar@aventragroup.com', @WorkHRAdmin,   @WorkProd, 2),
('sanhita.indulkar@aventragroup.com', @WorkReportMgr, @WorkProd, 3);

-- sheela — Learn: Employee, L&D, RM, DeptOwner
INSERT INTO #Staging VALUES
('sheela.mani@aventragroup.com', @LearnEmployee,  @LearnProd, 1),
('sheela.mani@aventragroup.com', @LearnLnD,       @LearnProd, 2),
('sheela.mani@aventragroup.com', @LearnReportMgr, @LearnProd, 3),
('sheela.mani@aventragroup.com', @LearnDeptOwner, @LearnProd, 4);

-- sandra, sajeev — Safe: Compliance Admin
INSERT INTO #Staging VALUES
('sandra.segar@aventragroup.com', @SafeCompliance, @SafeProd, 1),
('sajeev.raj@aventragroup.com',   @SafeCompliance, @SafeProd, 1);

-- valli — Work: Employee, HR Admin, RM
INSERT INTO #Staging VALUES
('valli.sujatha@aventragroup.com', @WorkEmployee,  @WorkProd, 1),
('valli.sujatha@aventragroup.com', @WorkHRAdmin,   @WorkProd, 2),
('valli.sujatha@aventragroup.com', @WorkReportMgr, @WorkProd, 3);

-- Work: Employee + HR Admin
INSERT INTO #Staging VALUES
('naveen.naik@aventragroup.com',            @WorkEmployee, @WorkProd, 1),
('naveen.naik@aventragroup.com',            @WorkHRAdmin,  @WorkProd, 2),
('priyalatha.satiaceelan@aventragroup.com', @WorkEmployee, @WorkProd, 1),
('priyalatha.satiaceelan@aventragroup.com', @WorkHRAdmin,  @WorkProd, 2),
('tuanh.pham@aventragroup.com',             @WorkEmployee, @WorkProd, 1),
('tuanh.pham@aventragroup.com',             @WorkHRAdmin,  @WorkProd, 2),
('vipin.k@aventragroup.com',                @WorkEmployee, @WorkProd, 1),
('vipin.k@aventragroup.com',                @WorkHRAdmin,  @WorkProd, 2),
('karthikeyan.sekar@aventragroup.com',      @WorkEmployee, @WorkProd, 1),
('karthikeyan.sekar@aventragroup.com',      @WorkHRAdmin,  @WorkProd, 2),
('safurah.bee@aventragroup.com',            @WorkEmployee, @WorkProd, 1),
('safurah.bee@aventragroup.com',            @WorkHRAdmin,  @WorkProd, 2);

-- shamala — Work: Employee, HR Admin, RM
INSERT INTO #Staging VALUES
('shamala.nair@aventragroup.com', @WorkEmployee,  @WorkProd, 1),
('shamala.nair@aventragroup.com', @WorkHRAdmin,   @WorkProd, 2),
('shamala.nair@aventragroup.com', @WorkReportMgr, @WorkProd, 3);

-- raffli — Work: Employee
INSERT INTO #Staging VALUES
('raffli.ramzi@aventragroup.com', @WorkEmployee, @WorkProd, 1);

-- Learn: Employee + L&D
INSERT INTO #Staging VALUES
('sabrin.tahir@aventragroup.com',   @LearnEmployee, @LearnProd, 1),
('sabrin.tahir@aventragroup.com',   @LearnLnD,      @LearnProd, 2),
('nuraliaa.elina@aventragroup.com', @LearnEmployee, @LearnProd, 1),
('nuraliaa.elina@aventragroup.com', @LearnLnD,      @LearnProd, 2);

-- Learn: Employee + RM
INSERT INTO #Staging VALUES
('aswin.natarajan@aventragroup.com',   @LearnEmployee,  @LearnProd, 1),
('aswin.natarajan@aventragroup.com',   @LearnReportMgr, @LearnProd, 2),
('ranjith.ravindran@aventragroup.com', @LearnEmployee,  @LearnProd, 1),
('ranjith.ravindran@aventragroup.com', @LearnReportMgr, @LearnProd, 2),
('kannan.perumal@aventragroup.com',    @LearnEmployee,  @LearnProd, 1),
('kannan.perumal@aventragroup.com',    @LearnReportMgr, @LearnProd, 2),
('danabalan.shamugam@aventragroup.com',@LearnEmployee,  @LearnProd, 1),
('danabalan.shamugam@aventragroup.com',@LearnReportMgr, @LearnProd, 2),
('kannan.krishnasamy@aventragroup.com',@LearnEmployee,  @LearnProd, 1),
('kannan.krishnasamy@aventragroup.com',@LearnReportMgr, @LearnProd, 2);

-- gurmit — Learn: Employee, RM, DeptOwner
INSERT INTO #Staging VALUES
('gurmit.kaur@aventragroup.com', @LearnEmployee,  @LearnProd, 1),
('gurmit.kaur@aventragroup.com', @LearnReportMgr, @LearnProd, 2),
('gurmit.kaur@aventragroup.com', @LearnDeptOwner, @LearnProd, 3);

-- Learn: Employee only (bulk)
INSERT INTO #Staging (Email, RoleId, ProductId, Priority) VALUES
('yusri.abdullah@aventragroup.com',          @LearnEmployee, @LearnProd, 1),
('siekhin.chong@aventragroup.com',           @LearnEmployee, @LearnProd, 1),
('asher.lim@aventragroup.com',               @LearnEmployee, @LearnProd, 1),
('yogapriya.murugesan@aventragroup.com',     @LearnEmployee, @LearnProd, 1),
('nurfarina.ahmadrizal@aventragroup.com',    @LearnEmployee, @LearnProd, 1),
('alvin.ng@aventragroup.com',                @LearnEmployee, @LearnProd, 1),
('sreevina.ramesh@aventragroup.com',         @LearnEmployee, @LearnProd, 1),
('muhammad.aizat@aventragroup.com',          @LearnEmployee, @LearnProd, 1),
('adina.hadi@aventragroup.com',              @LearnEmployee, @LearnProd, 1),
('syed.khaidir@aventragroup.com',            @LearnEmployee, @LearnProd, 1),
('badrul.amin@aventragroup.com',             @LearnEmployee, @LearnProd, 1),
('manjit.singh@aventragroup.com',            @LearnEmployee, @LearnProd, 1),
('kalidasan.guroosamy@aventragroup.com',     @LearnEmployee, @LearnProd, 1),
('sunil.singh@aventragroup.com',             @LearnEmployee, @LearnProd, 1),
('nidhi.s@aventragroup.com',                 @LearnEmployee, @LearnProd, 1),
('sai.kiran@aventragroup.com',               @LearnEmployee, @LearnProd, 1),
('paulpradeep.p@aventragroup.com',           @LearnEmployee, @LearnProd, 1),
('karthic.kumar@aventragroup.com',           @LearnEmployee, @LearnProd, 1),
('chandana.utpala@aventragroup.com',         @LearnEmployee, @LearnProd, 1),
('attluri.susmanand@aventragroup.com',       @LearnEmployee, @LearnProd, 1),
('jeenu.mariappan@aventragroup.com',         @LearnEmployee, @LearnProd, 1),
('vijayaraj.r@aventragroup.com',             @LearnEmployee, @LearnProd, 1),
('devansh.sharma@aventragroup.com',          @LearnEmployee, @LearnProd, 1),
('sasikaran.mahandaran@aventragroup.com',    @LearnEmployee, @LearnProd, 1),
('zati.nurhanani@aventragroup.com',          @LearnEmployee, @LearnProd, 1),
('ali.soban@aventragroup.com',               @LearnEmployee, @LearnProd, 1),
('phyoe.thiha@aventragroup.com',             @LearnEmployee, @LearnProd, 1),
('andal.pandi@aventragroup.com',             @LearnEmployee, @LearnProd, 1),
('pradeep.singh@aventragroup.com',           @LearnEmployee, @LearnProd, 1),
('gigishan.karunarathne@aventragroup.com',   @LearnEmployee, @LearnProd, 1),
('srri.ganapati@aventragroup.com',           @LearnEmployee, @LearnProd, 1),
('mubarish.ahmed@aventragroup.com',          @LearnEmployee, @LearnProd, 1),
('mohamed.haneef@aventragroup.com',          @LearnEmployee, @LearnProd, 1),
('ahmad.syamil@aventragroup.com',            @LearnEmployee, @LearnProd, 1),
('afrina.aida@aventragroup.com',             @LearnEmployee, @LearnProd, 1),
('fhaliq.naabil@aventragroup.com',           @LearnEmployee, @LearnProd, 1),
('winslet.raaj@aventragroup.com',            @LearnEmployee, @LearnProd, 1),
('irdina.batrisyia@aventragroup.com',        @LearnEmployee, @LearnProd, 1),
('ariff.amirul@aventragroup.com',            @LearnEmployee, @LearnProd, 1),
('varshitha.cheruvupalli@aventragroup.com',  @LearnEmployee, @LearnProd, 1),
('syed.sulthan@aventragroup.com',            @LearnEmployee, @LearnProd, 1),
('chiranjib.biswal@aventragroup.com',        @LearnEmployee, @LearnProd, 1),
('pham.tinh@aventragroup.com',               @LearnEmployee, @LearnProd, 1),
('merna.elenani@aventragroup.com',           @LearnEmployee, @LearnProd, 1),
('alexander.panganiban@aventragroup.com',    @LearnEmployee, @LearnProd, 1),
('sethuraja.ramani@aventragroup.com',        @LearnEmployee, @LearnProd, 1),
('praful.armarkar@aventragroup.com',         @LearnEmployee, @LearnProd, 1),
('anupriya.sonkar@aventragroup.com',         @LearnEmployee, @LearnProd, 1),
('revathi.nagalla@aventragroup.com',         @LearnEmployee, @LearnProd, 1),
('ali.mohamed@aventragroup.com',             @LearnEmployee, @LearnProd, 1),
('shashank.kulkarni@aventragroup.com',       @LearnEmployee, @LearnProd, 1),
('pranali.patil@aventragroup.com',           @LearnEmployee, @LearnProd, 1),
('ahamed.ameen@aventragroup.com',            @LearnEmployee, @LearnProd, 1),
('raymark.cosme@aventragroup.com',           @LearnEmployee, @LearnProd, 1),
('sanjeevi.bhargavi@aventragroup.com',       @LearnEmployee, @LearnProd, 1),
('mohan.kumar@aventragroup.com',             @LearnEmployee, @LearnProd, 1),
('wan.nashruddin@aventragroup.com',          @LearnEmployee, @LearnProd, 1),
('muhammad.adam@aventragroup.com',           @LearnEmployee, @LearnProd, 1),
('hariratnam.v@aventragroup.com',            @LearnEmployee, @LearnProd, 1),
('siti.farahnasihah@aventragroup.com',       @LearnEmployee, @LearnProd, 1),
('mohammad.farooq@aventragroup.com',         @LearnEmployee, @LearnProd, 1),
('srirama.duggiraju@aventragroup.com',       @LearnEmployee, @LearnProd, 1),
('tahoor.ahmed@aventragroup.com',            @LearnEmployee, @LearnProd, 1);

-- ═══════════════════════════════════════════════════════════════
-- NORMALIZE: lowercase all staging emails
-- ═══════════════════════════════════════════════════════════════
UPDATE #Staging SET Email = LOWER(LTRIM(RTRIM(Email)));

-- ═══════════════════════════════════════════════════════════════
-- RESOLVE: Join staging to Users using fuzzy email match
-- ═══════════════════════════════════════════════════════════════
CREATE TABLE #Resolved (
    UserId    UNIQUEIDENTIFIER,
    Email     NVARCHAR(256),
    RoleId    UNIQUEIDENTIFIER,
    ProductId UNIQUEIDENTIFIER NULL,
    Priority  INT
);

INSERT INTO #Resolved
SELECT u.Id, s.Email, s.RoleId, s.ProductId, s.Priority
FROM #Staging s
INNER JOIN core.Users u ON LOWER(LTRIM(RTRIM(u.Email))) = s.Email
    AND u.TenantId = @TenantId
    AND u.IsDeleted = 0;

-- ═══════════════════════════════════════════════════════════════
-- REPORT: Skipped emails (user not found)
-- ═══════════════════════════════════════════════════════════════
PRINT '=== SKIPPED (user not found) ===';

SELECT DISTINCT s.Email AS [SKIPPED - User Not Found]
FROM #Staging s
WHERE NOT EXISTS (
    SELECT 1 FROM #Resolved r WHERE r.Email = s.Email
);

DECLARE @SkipCount INT = (
    SELECT COUNT(DISTINCT s.Email) FROM #Staging s
    WHERE NOT EXISTS (SELECT 1 FROM #Resolved r WHERE r.Email = s.Email)
);
PRINT 'Skipped ' + CAST(@SkipCount AS VARCHAR) + ' users (not found in Users table).';

-- ═══════════════════════════════════════════════════════════════
-- REPORT: Found users
-- ═══════════════════════════════════════════════════════════════
DECLARE @FoundCount INT = (SELECT COUNT(DISTINCT Email) FROM #Resolved);
PRINT 'Found ' + CAST(@FoundCount AS VARCHAR) + ' users in Users table.';

-- ═══════════════════════════════════════════════════════════════
-- STEP 1: DELETE all existing UserRoles for this tenant
-- ═══════════════════════════════════════════════════════════════
DELETE FROM core.UserRoles WHERE TenantId = @TenantId;

DECLARE @Deleted INT = @@ROWCOUNT;
PRINT 'Deleted ' + CAST(@Deleted AS VARCHAR) + ' old UserRole records.';

-- ═══════════════════════════════════════════════════════════════
-- STEP 2: INSERT fresh from Excel mapping
-- ═══════════════════════════════════════════════════════════════
INSERT INTO core.UserRoles (Id, UserId, RoleId, TenantId, CreatedAt, IsDeleted, Priority, ProductId)
SELECT
    NEWID(),
    r.UserId,
    r.RoleId,
    @TenantId,
    @Now,
    0,
    r.Priority,
    r.ProductId
FROM #Resolved r;

DECLARE @Inserted INT = @@ROWCOUNT;
PRINT 'Inserted ' + CAST(@Inserted AS VARCHAR) + ' new UserRole records from Excel.';

-- ═══════════════════════════════════════════════════════════════
-- SUMMARY: Show what was assigned
-- ═══════════════════════════════════════════════════════════════
SELECT
    r.Email,
    ro.DisplayName AS RoleName,
    COALESCE(p.Name, 'Platform') AS Product,
    r.Priority,
    'INSERTED' AS [Action]
FROM #Resolved r
INNER JOIN core.Roles ro ON ro.Id = r.RoleId
LEFT JOIN core.Products p ON p.Id = ro.ProductId
ORDER BY r.Email, r.Priority;

DROP TABLE #Staging;
DROP TABLE #Resolved;

-- Review results then:
-- COMMIT TRANSACTION;
-- or: ROLLBACK TRANSACTION;
