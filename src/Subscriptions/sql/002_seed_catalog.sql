/* =====================================================================
   Seed the billing catalog. Mirrors PlanCatalog.cs exactly, so the DB
   and the code stay in lock-step. Re-runnable (idempotent-ish via MERGE
   on natural keys). Run after 001_billing_schema.sql.
   ===================================================================== */

SET NOCOUNT ON;

/* ---- Products ---- */
MERGE billing.Products AS t
USING (VALUES
    (1,  'hire',     N'Hire — HR'),
    (2,  'learn',    N'Learn — LMS'),
    (3,  'work',     N'Work — Operations'),
    (99, 'platform', N'Platform (cross-cutting)')
) AS s(ProductId,[Key],Name) ON t.ProductId = s.ProductId
WHEN NOT MATCHED THEN INSERT(ProductId,[Key],Name) VALUES(s.ProductId,s.[Key],s.Name);

/* ---- Features (Key, ProductKey, Name, Description, IsComingSoon) ---- */
DECLARE @Features TABLE([Key] VARCHAR(100), ProductKey VARCHAR(20), Name NVARCHAR(150), Descr NVARCHAR(400), Coming BIT);
INSERT INTO @Features VALUES
 ('hire.employee_master_data','hire',N'Employee Master Data',N'Central employee records, demographics, contacts',0),
 ('hire.employment_history','hire',N'Employment History',N'Job history, transfers, promotion tracking',0),
 ('hire.document_management','hire',N'Document Management',N'Employee document storage',0),
 ('hire.org_chart','hire',N'Org Chart Visualization',N'Interactive org hierarchy views',0),
 ('hire.punch_clock','hire',N'Punch Clock / Time Entry',N'Employee clock-in/out',0),
 ('hire.geofencing','hire',N'Geofencing / GPS Tracking',N'Location-based attendance',0),
 ('hire.leave_request','hire',N'Leave Request Workflow',N'Request and approve leave',0),
 ('hire.leave_balance','hire',N'Leave Balance Tracking',N'Accrual and entitlement management',0),
 ('hire.leave_calendar','hire',N'Leave Calendar View',N'Team availability visibility',0),
 ('hire.onboarding','hire',N'Onboarding Workflows',N'New hire task automation',0),
 ('hire.preboarding','hire',N'Preboarding Portal',N'Pre-start engagement',0),
 ('hire.personal_profile','hire',N'Personal Profile Management',N'Update own data',0),
 ('hire.personalized_home','hire',N'Personalized Home',N'Role-based landing pages',0),
 ('hire.social_learning','hire',N'Social Learning',N'Peer collaboration and sharing',0),
 ('hire.push_notifications','hire',N'Push Notifications',N'Real-time alerts',0),
 ('hire.rest_api','hire',N'REST API',N'Modern RESTful API access',0),
 ('hire.webhooks','hire',N'Webhooks',N'Real-time event notifications',0),
 ('hire.sso_saml','hire',N'SSO / SAML',N'Single sign-on support',0),
 ('hire.scim','hire',N'SCIM Provisioning',N'Automated user provisioning',0),
 ('hire.m365','hire',N'Microsoft 365 Integration',N'Teams, Outlook, SharePoint',0),
 ('hire.data_import','hire',N'Data Import (CSV/Excel)',N'Bulk data upload',0),
 ('hire.iso27001','hire',N'ISO 27001',N'ISO 27001 certification',0),
 ('hire.soc2','hire',N'SOC 2 Type 2',N'SOC 2 Type 2 attestation',0),
 ('hire.ai_assistant','hire',N'Agentic AI Assistant / Chatbot',N'AI-native assistant',1),
 ('hire.ai_native','hire',N'AI-Native Architecture',N'AI-native platform architecture',1),
 ('learn.course_catalog','learn',N'Course Catalog',N'Browse and manage courses',0),
 ('learn.enrollment','learn',N'Enrollment',N'Enroll and track learners',0),
 ('learn.assessments','learn',N'Assessments',N'Quizzes and grading',0),
 ('learn.social_learning','learn',N'Social Learning',N'Peer collaboration and sharing',0),
 ('learn.rest_api','learn',N'REST API',N'Modern RESTful API access',0),
 ('learn.sso_saml','learn',N'SSO / SAML',N'Single sign-on support',0),
 ('work.task_management','work',N'Task Management',N'Create and track tasks',0),
 ('work.timesheets','work',N'Timesheets',N'Log and approve time',0),
 ('work.approvals','work',N'Approvals',N'Approval workflows',0),
 ('work.rest_api','work',N'REST API',N'Modern RESTful API access',0),
 ('work.sso_saml','work',N'SSO / SAML',N'Single sign-on support',0);

MERGE billing.Features AS t
USING (SELECT f.[Key], p.ProductId, f.Name, f.Descr, f.Coming
       FROM @Features f JOIN billing.Products p ON p.[Key] = f.ProductKey) AS s
ON t.[Key] = s.[Key]
WHEN MATCHED THEN UPDATE SET ProductId=s.ProductId, Name=s.Name, [Description]=s.Descr, IsComingSoon=s.Coming
WHEN NOT MATCHED THEN INSERT([Key],ProductId,Name,[Description],IsComingSoon)
    VALUES(s.[Key],s.ProductId,s.Name,s.Descr,s.Coming);

/* ---- FeaturePermissions : the bridge to core.Permissions ---- */
DECLARE @FP TABLE(FeatureKey VARCHAR(100), PermissionCode VARCHAR(150));
INSERT INTO @FP VALUES
 ('hire.employee_master_data','hire.employee.read'),('hire.employee_master_data','hire.employee.write'),
 ('hire.employee_master_data','hire.demographics.read'),('hire.employee_master_data','hire.contacts.read'),
 ('hire.employment_history','hire.employment_history.read'),('hire.employment_history','hire.transfer.manage'),
 ('hire.employment_history','hire.promotion.track'),
 ('hire.document_management','hire.document.read'),('hire.document_management','hire.document.write'),
 ('hire.org_chart','hire.orgchart.view'),
 ('hire.punch_clock','hire.attendance.punch'),('hire.punch_clock','hire.attendance.read'),
 ('hire.geofencing','hire.attendance.geofence'),
 ('hire.leave_request','hire.leave.request'),('hire.leave_request','hire.leave.approve'),
 ('hire.leave_balance','hire.leave.balance.read'),('hire.leave_balance','hire.leave.accrual.manage'),
 ('hire.leave_calendar','hire.leave.calendar.view'),
 ('hire.onboarding','hire.onboarding.manage'),
 ('hire.preboarding','hire.preboarding.access'),
 ('hire.personal_profile','hire.profile.self.manage'),
 ('hire.personalized_home','hire.home.personalized'),
 ('hire.social_learning','hire.social.post'),('hire.social_learning','hire.social.share'),
 ('hire.push_notifications','hire.notifications.push'),
 ('hire.rest_api','hire.api.read'),('hire.rest_api','hire.api.write'),
 ('hire.webhooks','hire.webhook.manage'),
 ('hire.sso_saml','hire.sso.configure'),
 ('hire.scim','hire.scim.provision'),
 ('hire.m365','hire.integration.m365'),
 ('hire.data_import','hire.data.import'),
 ('hire.ai_assistant','hire.ai.assistant'),
 ('learn.course_catalog','learn.course.read'),
 ('learn.enrollment','learn.enrollment.manage'),
 ('learn.assessments','learn.assessment.manage'),
 ('learn.social_learning','learn.social.post'),('learn.social_learning','learn.social.share'),
 ('learn.rest_api','learn.api.read'),('learn.rest_api','learn.api.write'),
 ('learn.sso_saml','learn.sso.configure'),
 ('work.task_management','work.task.read'),('work.task_management','work.task.write'),
 ('work.timesheets','work.timesheet.manage'),
 ('work.approvals','work.approval.manage'),
 ('work.rest_api','work.api.read'),('work.rest_api','work.api.write'),
 ('work.sso_saml','work.sso.configure');

MERGE billing.FeaturePermissions AS t
USING (SELECT f.FeatureId, x.PermissionCode
       FROM @FP x JOIN billing.Features f ON f.[Key] = x.FeatureKey) AS s
ON t.FeatureId = s.FeatureId AND t.PermissionCode = s.PermissionCode
WHEN NOT MATCHED THEN INSERT(FeatureId,PermissionCode) VALUES(s.FeatureId,s.PermissionCode);

/* ---- Plans ---- */
DECLARE @Plans TABLE([Key] VARCHAR(50), ProductKey VARCHAR(20), Tier INT, Name NVARCHAR(100), Descr NVARCHAR(300));
INSERT INTO @Plans VALUES
 ('hire.starter','hire',10,N'Starter',N'Essential HR'),
 ('hire.lite','hire',20,N'Lite',N'Core HR and Employee Operations'),
 ('hire.standard','hire',30,N'Standard',N'Full HR Operations'),
 ('hire.enterprise','hire',40,N'Enterprise',N'Scale Organization'),
 ('learn.starter','learn',10,N'Starter',N'Essential Learning'),
 ('learn.standard','learn',30,N'Standard',N'Full Learning'),
 ('learn.enterprise','learn',40,N'Enterprise',N'Scale Learning'),
 ('work.starter','work',10,N'Starter',N'Essential Work'),
 ('work.standard','work',30,N'Standard',N'Full Work'),
 ('work.enterprise','work',40,N'Enterprise',N'Scale Work');

MERGE billing.Plans AS t
USING (SELECT pl.[Key], p.ProductId, pl.Tier, pl.Name, pl.Descr
       FROM @Plans pl JOIN billing.Products p ON p.[Key] = pl.ProductKey) AS s
ON t.[Key] = s.[Key]
WHEN MATCHED THEN UPDATE SET ProductId=s.ProductId, Tier=s.Tier, Name=s.Name, [Description]=s.Descr
WHEN NOT MATCHED THEN INSERT([Key],ProductId,Tier,Name,[Description])
    VALUES(s.[Key],s.ProductId,s.Tier,s.Name,s.Descr);

/* ---- PlanFeatures (cumulative tiers, authored explicitly) ---- */
DECLARE @PF TABLE(PlanKey VARCHAR(50), FeatureKey VARCHAR(100));
-- hire.starter
INSERT INTO @PF VALUES
 ('hire.starter','hire.employee_master_data'),('hire.starter','hire.personal_profile'),
 ('hire.starter','hire.personalized_home'),('hire.starter','hire.data_import');
-- hire.lite = starter + more
INSERT INTO @PF SELECT 'hire.lite', FeatureKey FROM @PF WHERE PlanKey='hire.starter';
INSERT INTO @PF VALUES
 ('hire.lite','hire.employment_history'),('hire.lite','hire.document_management'),
 ('hire.lite','hire.punch_clock'),('hire.lite','hire.leave_request'),('hire.lite','hire.push_notifications');
-- hire.standard = lite + more
INSERT INTO @PF SELECT 'hire.standard', FeatureKey FROM @PF WHERE PlanKey='hire.lite';
INSERT INTO @PF VALUES
 ('hire.standard','hire.org_chart'),('hire.standard','hire.geofencing'),('hire.standard','hire.leave_balance'),
 ('hire.standard','hire.leave_calendar'),('hire.standard','hire.onboarding'),('hire.standard','hire.social_learning'),
 ('hire.standard','hire.rest_api'),('hire.standard','hire.m365');
-- hire.enterprise = standard + more
INSERT INTO @PF SELECT 'hire.enterprise', FeatureKey FROM @PF WHERE PlanKey='hire.standard';
INSERT INTO @PF VALUES
 ('hire.enterprise','hire.preboarding'),('hire.enterprise','hire.webhooks'),('hire.enterprise','hire.sso_saml'),
 ('hire.enterprise','hire.scim'),('hire.enterprise','hire.iso27001'),('hire.enterprise','hire.soc2'),
 ('hire.enterprise','hire.ai_assistant'),('hire.enterprise','hire.ai_native');
-- learn
INSERT INTO @PF VALUES ('learn.starter','learn.course_catalog'),('learn.starter','learn.enrollment');
INSERT INTO @PF SELECT 'learn.standard', FeatureKey FROM @PF WHERE PlanKey='learn.starter';
INSERT INTO @PF VALUES ('learn.standard','learn.assessments'),('learn.standard','learn.social_learning');
INSERT INTO @PF SELECT 'learn.enterprise', FeatureKey FROM @PF WHERE PlanKey='learn.standard';
INSERT INTO @PF VALUES ('learn.enterprise','learn.rest_api'),('learn.enterprise','learn.sso_saml');
-- work
INSERT INTO @PF VALUES ('work.starter','work.task_management');
INSERT INTO @PF SELECT 'work.standard', FeatureKey FROM @PF WHERE PlanKey='work.starter';
INSERT INTO @PF VALUES ('work.standard','work.timesheets'),('work.standard','work.approvals');
INSERT INTO @PF SELECT 'work.enterprise', FeatureKey FROM @PF WHERE PlanKey='work.standard';
INSERT INTO @PF VALUES ('work.enterprise','work.rest_api'),('work.enterprise','work.sso_saml');

MERGE billing.PlanFeatures AS t
USING (SELECT pl.PlanId, f.FeatureId
       FROM @PF x
       JOIN billing.Plans pl ON pl.[Key] = x.PlanKey
       JOIN billing.Features f ON f.[Key] = x.FeatureKey) AS s
ON t.PlanId = s.PlanId AND t.FeatureId = s.FeatureId
WHEN NOT MATCHED THEN INSERT(PlanId,FeatureId) VALUES(s.PlanId,s.FeatureId);

PRINT 'billing catalog seeded.';
GO
