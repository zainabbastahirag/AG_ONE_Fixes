"""
Generates the end-to-end implementation Word document for the AG ONE
Subscription & Feature Entitlement layer.

Run:  python3 docs/generate_word_doc.py
Output: docs/AG-ONE-Subscription-Entitlement-Implementation.docx
Requires: python-docx, and the diagram PNGs in /opt/cursor/artifacts (rendered from the .drawio files).
"""
import os
from docx import Document
from docx.shared import Pt, Inches, RGBColor
from docx.enum.text import WD_ALIGN_PARAGRAPH
from docx.enum.table import WD_TABLE_ALIGNMENT
from docx.oxml.ns import qn
from docx.oxml import OxmlElement

ART = "/opt/cursor/artifacts"
OUT = os.path.join(os.path.dirname(__file__), "AG-ONE-Subscription-Entitlement-Implementation.docx")

ACCENT = RGBColor(0x1F, 0x4E, 0x79)
GREEN = RGBColor(0x2E, 0x7D, 0x32)

doc = Document()

# ---- base styles ----
normal = doc.styles["Normal"]
normal.font.name = "Calibri"
normal.font.size = Pt(11)

def shade(cell, hexcolor):
    tcPr = cell._tc.get_or_add_tcPr()
    sh = OxmlElement("w:shd")
    sh.set(qn("w:val"), "clear"); sh.set(qn("w:color"), "auto"); sh.set(qn("w:fill"), hexcolor)
    tcPr.append(sh)

def code_block(text):
    """A single-cell shaded table holding monospaced code."""
    t = doc.add_table(rows=1, cols=1)
    t.alignment = WD_TABLE_ALIGNMENT.CENTER
    cell = t.cell(0, 0)
    shade(cell, "F3F4F6")
    cell.width = Inches(6.5)
    # borders
    tblPr = t._tbl.tblPr
    borders = OxmlElement("w:tblBorders")
    for edge in ("top", "left", "bottom", "right"):
        e = OxmlElement(f"w:{edge}")
        e.set(qn("w:val"), "single"); e.set(qn("w:sz"), "4"); e.set(qn("w:color"), "D0D0D0")
        borders.append(e)
    tblPr.append(borders)
    p = cell.paragraphs[0]
    p.paragraph_format.space_after = Pt(0)
    for i, line in enumerate(text.strip("\n").split("\n")):
        run = p.add_run(("" if i == 0 else "\n") + line)
        run.font.name = "Consolas"; run.font.size = Pt(8.5)
        r = run._element.rPr.rFonts; r.set(qn("w:ascii"), "Consolas"); r.set(qn("w:hAnsi"), "Consolas")
    doc.add_paragraph()

def img(path, width=6.5, caption=None):
    full = os.path.join(ART, path)
    if os.path.exists(full):
        doc.add_picture(full, width=Inches(width))
        doc.paragraphs[-1].alignment = WD_ALIGN_PARAGRAPH.CENTER
        if caption:
            c = doc.add_paragraph(caption); c.alignment = WD_ALIGN_PARAGRAPH.CENTER
            c.runs[0].italic = True; c.runs[0].font.size = Pt(9); c.runs[0].font.color.rgb = RGBColor(0x60,0x60,0x60)
    else:
        doc.add_paragraph(f"[diagram missing: {path}]")

def h(text, level=1):
    p = doc.add_heading(text, level=level)
    return p

def bullets(items):
    for it in items:
        doc.add_paragraph(it, style="List Bullet")

def numbered(items):
    for it in items:
        doc.add_paragraph(it, style="List Number")

def table(headers, rows, widths=None):
    t = doc.add_table(rows=1, cols=len(headers))
    t.style = "Light Grid Accent 1"
    t.alignment = WD_TABLE_ALIGNMENT.CENTER
    for i, hcell in enumerate(headers):
        c = t.rows[0].cells[i]; c.text = ""
        run = c.paragraphs[0].add_run(hcell); run.bold = True; run.font.size = Pt(10)
    for row in rows:
        cells = t.add_row().cells
        for i, val in enumerate(row):
            cells[i].text = ""
            run = cells[i].paragraphs[0].add_run(str(val)); run.font.size = Pt(10)
    if widths:
        for i, w in enumerate(widths):
            for r in t.rows:
                r.cells[i].width = Inches(w)
    doc.add_paragraph()

# ==================================================================== TITLE
title = doc.add_paragraph()
title.alignment = WD_ALIGN_PARAGRAPH.CENTER
r = title.add_run("AG ONE\nSubscription & Feature Entitlement")
r.bold = True; r.font.size = Pt(28); r.font.color.rgb = ACCENT
sub = doc.add_paragraph(); sub.alignment = WD_ALIGN_PARAGRAPH.CENTER
rs = sub.add_run("End-to-End Implementation Guide"); rs.font.size = Pt(16); rs.font.color.rgb = RGBColor(0x40,0x40,0x40)
meta = doc.add_paragraph(); meta.alignment = WD_ALIGN_PARAGRAPH.CENTER
meta.add_run("Adding a Permission → Feature (plan tier) layer on top of existing RBAC\n"
             "Products: Hire · Learn · Work   |   Gateway: AG ONE   |   Platform: .NET 8 / Azure App Services / SQL Server\n"
             "Version 1.0").font.size = Pt(11)
doc.add_paragraph()

# ==================================================================== TOC note
h("Contents", 1)
bullets([
    "1. Executive Summary", "2. Goals & Non-Goals", "3. Current State",
    "4. Core Concept — RBAC ∩ Entitlement", "5. Architecture Overview",
    "6. Data Model & Database Injection", "7. Runtime Flows (login, permission fetch, DFD, process)",
    "8. Database Implementation (SQL)", "9. Application Implementation (.NET C#)",
    "10. Enforcement Model — Baseline vs Strict", "11. Plan × Feature Matrix",
    "12. Rollout & Migration Plan", "13. Security Considerations",
    "14. Testing & Verification", "15. Effort & Ownership", "16. Appendix",
])
doc.add_page_break()

# ==================================================================== 1
h("1. Executive Summary", 1)
doc.add_paragraph(
    "AG ONE already authenticates every user (SSO / non-SSO), checks which products a tenant has "
    "subscribed to, issues a JWT, and exposes a permissions service that Hire, Learn and Work call "
    "to obtain a user's permissions (cached ~10 minutes per app). What is missing is a link between "
    "the subscription plan tier (Freemium / Lite / Standard / Enterprise) and the permissions a user "
    "is actually allowed to exercise.")
doc.add_paragraph(
    "This guide adds that link with minimal change. The rule is a single intersection:")
code_block("Effective access  =  RBAC permission (role grants it)  AND  plan-tier entitlement (feature is in the tier)")
doc.add_paragraph(
    "We compute the intersection inside AG ONE's existing permissions service, so the three apps keep "
    "their JWT handling, 10-minute cache and enforcement unchanged. On the database, the only new object "
    "is one bridge table, core.ProductFeaturePermissions, that maps a ProductFeature to the existing "
    "Permission(s) it unlocks.")

# ==================================================================== 2
h("2. Goals & Non-Goals", 1)
h("Goals", 2)
bullets([
    "Gate access by subscription tier reusing the permissions already defined in core.Permissions.",
    "Zero (or near-zero) change to the Hire / Learn / Work applications.",
    "One additive DB change; nothing in existing tables is modified.",
    "Clear upgrade/upsell signal (402) distinct from role denial (403).",
    "Safe, phased rollout that cannot break current behavior.",
])
h("Non-Goals", 2)
bullets([
    "Replacing or redesigning the existing RBAC model.",
    "Changing pricing, billing provider, or the checkout flow.",
    "Per-record data-level security (out of scope; handled by existing app logic).",
])

# ==================================================================== 3
h("3. Current State", 1)
doc.add_paragraph("The platform runs as four .NET Core apps on Azure App Services against one SQL database "
                  "([agone-dev]) with per-product schemas (hire.*, learn.*, work.*) and a shared core.* schema.")
bullets([
    "AG ONE Gateway — login, SSO, JWT issue, and a permissions service keyed by user id.",
    "Hire / Learn / Work — each reads user id from the JWT cookie, calls AG ONE for permissions, "
    "caches them ~10 minutes, and enforces them locally.",
    "core.* — RBAC (Users, Roles, Permissions, RolePermissions, UserRoles) plus the product catalog "
    "(Products, ProductFeatureModules, ProductFeatures with per-tier availability bits, ProductPlanTiers, "
    "prices, terms) and Subscriptions.",
])
img("runtime_component.png", caption="Figure 1 — Component / deployment. Green marks the only new part (AG ONE returns effective permissions).")

# ==================================================================== 4
h("4. Core Concept — RBAC ∩ Entitlement", 1)
doc.add_paragraph("Two orthogonal questions must both be true for access:")
table(["Layer", "Question", "Source"],
      [["RBAC (existing)", "Does the user's role grant this permission?", "core.UserRoles → RolePermissions → Permissions"],
       ["Entitlement (new)", "Is this permission included in the tenant's plan tier?", "Subscription tier → ProductFeatures (tier bits) → ProductFeaturePermissions → Permissions"]],
      widths=[1.7, 2.8, 2.5])
doc.add_paragraph("Because both sides are expressed as the same permission codes, wiring them together needs "
                  "only the feature→permission bridge; the existing permission checks are reused verbatim.")

# ==================================================================== 5
h("5. Architecture Overview", 1)
doc.add_paragraph("The entitlement computation is centralized in AG ONE. The three apps consume the result. "
                  "The engine is a shared .NET library with three adapters bound to core.* tables.")
img("drawio2_solution_architecture.png", caption="Figure 2 — Solution architecture (engine, adapters, data).")

# ==================================================================== 6
h("6. Data Model & Database Injection", 1)
doc.add_paragraph("Mapping the design onto your existing tables:")
table(["Concept", "Existing table", "Notes"],
      [["Product", "core.Products", "Code = hire / learn / work"],
       ["Feature module", "core.ProductFeatureModules", "grouping under a product"],
       ["Feature", "core.ProductFeatures", "availability = per-tier bits (Freemium/Lite/Standard/Enterprise) + IsComingSoon"],
       ["Plan tier", "core.ProductPlanTiers", "PlanTier name"],
       ["Subscription", "core.Subscriptions", "tenant × product, status, licenses, dates"],
       ["RBAC", "core.Users/Roles/Permissions/RolePermissions/UserRoles", "unchanged"]],
      widths=[1.5, 2.6, 2.9])
h("The only injection", 2)
numbered([
    "NEW table core.ProductFeaturePermissions (ProductFeatureId → PermissionId) — the bridge.",
    "Recommended column core.Subscriptions.ProductPlanTierId (FK → core.ProductPlanTiers) so a subscription resolves to a tier.",
    "Read-only views/function: core.vSubscriptionTier, core.vTenantEntitledPermissions, core.fnEffectivePermissions.",
])
img("drawio2_data_model_erd.png", caption="Figure 3 — ERD of existing core.* tables; green = the only new objects.")

# ==================================================================== 7
h("7. Runtime Flows", 1)
h("7.1 Login & JWT", 2)
doc.add_paragraph("Subscription and tier are checked once at login and baked into the JWT as claims "
                  "(products[], per-product tier, ent_version).")
img("runtime_login.png", caption="Figure 4 — Login and JWT issue.")
h("7.2 Permission fetch + Feature intersection", 2)
doc.add_paragraph("On a cache miss, the app calls AG ONE, which now returns effective permissions "
                  "(RBAC ∩ entitlement). The app caches and enforces exactly as today.")
img("runtime_permfetch.png", caption="Figure 5 — Permission fetch; green steps ⑥–⑦ are the new intersection inside AG ONE.")
h("7.3 Data Flow Diagram", 2)
img("runtime_dfd.png", caption="Figure 6 — Data flow across identity, subscription and permission stores.")
h("7.4 Process flow — Permission → Feature decision", 2)
img("runtime_process.png", caption="Figure 7 — How a single permission is decided (with baseline vs strict).")

# ==================================================================== 8
h("8. Database Implementation (SQL)", 1)
doc.add_paragraph("Additive and safe to run on a live database. Full script: "
                  "src/Subscriptions/sql/003_inject_existing_core.sql.")
h("8.1 The bridge table", 2)
code_block(
"""CREATE TABLE core.ProductFeaturePermissions
(
    Id               UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    ProductFeatureId UNIQUEIDENTIFIER NOT NULL,
    PermissionId     UNIQUEIDENTIFIER NOT NULL,
    CreatedAt        DATETIME2(7)     NOT NULL DEFAULT SYSUTCDATETIME(),
    UpdatedAt        DATETIME2(7)     NULL,
    IsDeleted        BIT              NOT NULL DEFAULT (0),
    CONSTRAINT PK_ProductFeaturePermissions PRIMARY KEY (Id),
    CONSTRAINT FK_PFP_ProductFeatures FOREIGN KEY (ProductFeatureId)
        REFERENCES core.ProductFeatures (Id) ON DELETE CASCADE,
    CONSTRAINT FK_PFP_Permissions FOREIGN KEY (PermissionId)
        REFERENCES core.Permissions (Id) ON DELETE CASCADE,
    CONSTRAINT UQ_PFP UNIQUE (ProductFeatureId, PermissionId)
);""")
h("8.2 Entitled permissions per (tenant, product)", 2)
code_block(
"""CREATE OR ALTER VIEW core.vTenantEntitledPermissions AS
SELECT DISTINCT st.TenantId, st.ProductId, fpp.PermissionId, p.Code AS PermissionCode
FROM core.vSubscriptionTier st
JOIN core.ProductFeatureModules m ON m.ProductId = st.ProductId AND m.IsDeleted = 0
JOIN core.ProductFeatures pf ON pf.ProductFeatureModuleId = m.Id
     AND pf.IsDeleted = 0 AND pf.IsComingSoon = 0
     AND CASE st.PlanTier
             WHEN N'Freemium'   THEN pf.AvailableFreemium
             WHEN N'Lite'       THEN pf.AvailableLite
             WHEN N'Standard'   THEN pf.AvailableStandard
             WHEN N'Enterprise' THEN pf.AvailableEnterprise
             ELSE CONVERT(bit,0) END = 1
JOIN core.ProductFeaturePermissions fpp ON fpp.ProductFeatureId = pf.Id AND fpp.IsDeleted = 0
JOIN core.Permissions p ON p.Id = fpp.PermissionId AND p.IsDeleted = 0;""")
h("8.3 Effective permissions (RBAC ∩ entitlement)", 2)
code_block(
"""CREATE OR ALTER FUNCTION core.fnEffectivePermissions(@TenantId uniqueidentifier, @UserId uniqueidentifier)
RETURNS TABLE AS RETURN
(
    SELECT DISTINCT p.Id AS PermissionId, p.Code AS PermissionCode
    FROM core.UserRoles ur
    JOIN core.RolePermissions rp ON rp.RoleId = ur.RoleId AND rp.IsDeleted = 0
    JOIN core.Permissions p ON p.Id = rp.PermissionId AND p.IsDeleted = 0
    JOIN core.vTenantEntitledPermissions ep ON ep.PermissionId = p.Id AND ep.TenantId = @TenantId
    WHERE ur.UserId = @UserId AND ur.TenantId = @TenantId AND ur.IsDeleted = 0
);""")
h("8.4 Seeding the bridge (ongoing authoring)", 2)
code_block(
"""INSERT INTO core.ProductFeaturePermissions (ProductFeatureId, PermissionId)
SELECT pf.Id, p.Id
FROM core.ProductFeatures pf
JOIN core.Permissions p ON p.Code IN (N'hire.employee.read', N'hire.employee.write')
WHERE pf.Name = N'Employee Master Data'
  AND NOT EXISTS (SELECT 1 FROM core.ProductFeaturePermissions x
                  WHERE x.ProductFeatureId = pf.Id AND x.PermissionId = p.Id);""")

# ==================================================================== 9
h("9. Application Implementation (.NET C#)", 1)
doc.add_paragraph("A shared library (AgOne.Entitlements) holds the engine; AG ONE binds three adapters over "
                  "core.* and exposes the effective-permissions endpoint. Reference code: src/Subscriptions.")
h("9.1 Gateway effective-permission service", 2)
code_block(
"""public interface IGatewayEntitlementService
{
    Task<EffectivePermissionResponse> BuildAsync(
        Guid tenantId, Guid userId, IEnumerable<ProductKey> subscribedProducts,
        int cacheTtlSeconds = 600, CancellationToken ct = default);
}

// Per product: effective = RBAC ∩ entitlement; then union across products,
// plus a stable Version for client cache-busting. (See GatewayEntitlementService.cs)""")
h("9.2 Permissions endpoint (drop-in replacement)", 2)
code_block(
"""[ApiController]
[Route("api/permissions")]
public sealed class PermissionsController : ControllerBase
{
    private readonly IGatewayEntitlementService _gateway;
    public PermissionsController(IGatewayEntitlementService gateway) => _gateway = gateway;

    [HttpGet("{userId:guid}")]
    public async Task<ActionResult<EffectivePermissionResponse>> Get(Guid userId, CancellationToken ct)
    {
        var tenantId = Guid.Parse(User.FindFirstValue("tenant_id")!);
        var products = User.FindAll("product").Select(c => Enum.Parse<ProductKey>(c.Value, true));
        return Ok(await _gateway.BuildAsync(tenantId, userId, products, 600, ct));
    }
}""")
h("9.3 JWT enrichment at login", 2)
code_block(
"""var products = await _subscriptions.GetActiveProductsAsync(tenantId);
var claims = new List<Claim> {
    new("user_id", user.Id.ToString()),
    new("tenant_id", tenantId.ToString()),
    new("ent_version", entitlementVersion)      // changes when a plan changes -> busts app caches
};
foreach (var p in products) {
    claims.Add(new Claim("product", p.ProductCode));
    claims.Add(new Claim($"tier:{p.ProductCode}", p.PlanTier));
}""")
h("9.4 App-side cached client (Hire / Learn / Work — minimal change)", 2)
code_block(
"""public async Task<ISet<string>> GetPermissionsAsync(Guid userId, string entVersion, CancellationToken ct)
{
    var key = $"perms:{userId:N}:{entVersion}";        // version in key = automatic invalidation
    if (_cache.TryGetValue(key, out ISet<string>? cached)) return cached!;

    var res = await _http.GetFromJsonAsync<EffectivePermissionResponse>($"api/permissions/{userId}", ct);
    var set = res!.AllPermissionCodes.ToHashSet(StringComparer.OrdinalIgnoreCase);
    _cache.Set(key, set, TimeSpan.FromSeconds(res.CacheTtlSeconds));   // existing ~10 min
    return set;
}""")
doc.add_paragraph("Enforcement stays as-is: the existing [RequirePermission]/handler checks the cached set. "
                  "Since the set is already plan-filtered, lower-tier users simply lack the gated permission.")

# ==================================================================== 10
h("10. Enforcement Model — Baseline vs Strict", 1)
table(["Policy", "Unmapped permission", "Use when"],
      [["Baseline (rollout)", "Stays allowed (ungated). Only permissions mapped to a premium feature are gated.",
        "Initial rollout — cannot break existing behavior."],
       ["Strict (target)", "Denied unless entitled by the tier (deny-by-default).",
        "After every premium permission is mapped."]],
      widths=[1.6, 3.3, 2.1])
doc.add_paragraph("Start baseline, map features incrementally, then switch to strict (core.fnEffectivePermissions).")

# ==================================================================== 11
h("11. Plan × Feature Matrix", 1)
doc.add_paragraph("Availability is stored per feature as tier bits on core.ProductFeatures. Representative HR matrix:")
table(["Feature (Hire)", "Freemium", "Lite", "Standard", "Enterprise"],
      [["Employee Master Data", "✓", "✓", "✓", "✓"],
       ["Personal Profile / Personalized Home", "✓", "✓", "✓", "✓"],
       ["Employment History / Document Mgmt", "", "✓", "✓", "✓"],
       ["Punch Clock / Leave Request", "", "✓", "✓", "✓"],
       ["Org Chart / Geofencing / Leave Balance", "", "", "✓", "✓"],
       ["Onboarding / REST API / M365", "", "", "✓", "✓"],
       ["Preboarding / Webhooks / SSO / SCIM", "", "", "", "✓"],
       ["ISO 27001 / SOC 2", "", "", "", "✓"],
       ["Agentic AI (Coming Soon)", "—", "—", "—", "—"]],
      widths=[3.0, 0.9, 0.8, 1.0, 1.1])

# ==================================================================== 12
h("12. Rollout & Migration Plan", 1)
numbered([
    "Deploy DB injection (003 script). No behavior change.",
    "Backfill Subscriptions.ProductPlanTierId (or map via PricingPlans).",
    "Seed core.ProductFeaturePermissions for the first premium features.",
    "Deploy AG ONE change to return effective permissions in BASELINE mode (log gated denials, don't enforce).",
    "Verify logs vs expectations per tenant cohort.",
    "Enforce baseline; enable upsell (402) UX.",
    "Map remaining premium permissions; switch to STRICT deny-by-default.",
])

# ==================================================================== 13
h("13. Security Considerations", 1)
bullets([
    "Deny-by-default in the target state; baseline only during rollout.",
    "Entitlement can only remove permissions the role already grants — never escalate.",
    "Tenant scoping from the authenticated principal (tenant_id claim), never from request input.",
    "Coming-Soon features are dropped during computation and cannot leak.",
    "Audit every denial with reason (role vs plan vs billing) for support and telemetry.",
])

# ==================================================================== 14
h("14. Testing & Verification", 1)
doc.add_paragraph("The reference engine ships with 19 automated tests (xUnit), all passing:")
bullets([
    "Effective access = RBAC ∩ entitlement (allow / upsell / forbidden).",
    "Cumulative tiers; Coming-Soon never entitles even on Enterprise.",
    "Lifecycle: trial expiry, past-due grace window.",
    "Product isolation: a learn subscription never unlocks hire permissions.",
    "Gateway: per-product intersection + union, version stability/change, inactive subscription yields no permissions.",
])
code_block("cd src/Subscriptions\n"
           "dotnet build   # 0 warnings, 0 errors\n"
           "dotnet test    # Passed! Failed: 0, Passed: 19")
img("drawio2_authorization_decision.png", caption="Figure 8 — Authorization decision outcomes (200 / 402 / 403).")

# ==================================================================== 15
h("15. Effort & Ownership", 1)
table(["Workstream", "Owner", "Scope"],
      [["DB migration + seed", "Data / DBA", "003 script, tier backfill, feature→permission mapping"],
       ["AG ONE gateway", "Platform team", "effective-permissions service, endpoint, JWT claims, cache invalidation"],
       ["Hire / Learn / Work", "Product teams", "cache key by ent_version (1 line); upsell 402 handling"],
       ["Billing webhook", "Platform team", "update subscription + bump entitlement version"],
       ["QA", "QA", "tier matrix validation, regression of existing permissions"]],
      widths=[2.2, 1.8, 2.5])
doc.add_paragraph("Difficulty note: the change is additive and isolated — one DB table, one gateway service, "
                  "and an optional one-line app cache tweak. No existing table or RBAC assignment is modified.")

# ==================================================================== 16
h("16. Appendix", 1)
h("16.1 Repository artifacts", 2)
table(["Path", "What"],
      [["docs/Subscription-Entitlement-Architecture.drawio", "Solution, ERD (real schema), decision — editable"],
       ["docs/AGONE-Entitlement-Runtime-Flow.drawio", "Component, login, permission-fetch, DFD, process — editable"],
       ["docs/Subscription-Entitlement-Architecture.md", "Design doc (incl. schema mapping)"],
       ["docs/AGONE-Entitlement-Runtime.md", "Runtime step-by-step + C#"],
       ["src/Subscriptions/sql/003_inject_existing_core.sql", "DB injection (bridge + views/function)"],
       ["src/Subscriptions/src/AgOne.Entitlements", "Engine + gateway service"],
       ["src/Subscriptions/tests", "xUnit tests (19)"]],
      widths=[4.2, 2.3])
h("16.2 Glossary", 2)
table(["Term", "Meaning"],
      [["RBAC", "Role-based access control (existing permissions)"],
       ["Entitlement", "What a subscription tier includes"],
       ["Effective permission", "RBAC ∩ entitlement — what the user can actually do"],
       ["ent_version", "Version stamp that busts app caches when a plan changes"],
       ["Baseline / Strict", "Rollout policy for unmapped permissions"]],
      widths=[2.0, 4.5])

doc.save(OUT)
print("Saved", OUT)
