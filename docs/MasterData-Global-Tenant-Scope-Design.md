# Solution Design: Adding Global + Tenant Scope to Master Data

**Status:** Draft for review
**Author:** (you)
**Last updated:** 2026-07-14

---

## 0. How to read this document

The Master Data system described here is **not** part of the code in this
repository, so this design is written from your description plus standard
multi-tenant master-data practice. Before implementing, please confirm the
**Assumptions** in section 2 — if any are wrong, the "minimal change" surface
in section 5 shifts.

Wherever I had to interpret an unclear term, I state the interpretation inline
in **[interpretation]** notes.

---

## 1. Context & current state

### 1.1 What exists today

Master Data is split into two layers that live in the **same physical database
but under different schemas**:

| Layer | Schema | Responsibility |
|-------|--------|----------------|
| **Metadata layer** — *Definitions & Fields* | `ag` **[interpretation: "AG one" = the `ag` schema/service that owns metadata]** | Describes *what* master data types exist and *which fields* each one has. |
| **Data layer** — *actual master data records* | `products` | Stores the concrete master-data rows (the actual values) for each definition. |

So there are (at least) three logical tables:

- `ag.MasterDataDefinition` — one row per master-data type (e.g. "Country",
  "Currency", "ProductCategory").
- `ag.MasterDataField` — the fields that belong to a definition (name, data
  type, required, etc.).
- `products.MasterDataRecord` (+ per-field values) — the actual data rows for a
  definition.

### 1.2 Current scoping model

Every master-data record — and today likely every definition too — is scoped by
a **`TenantId`**. All reads and writes are implicitly filtered by the current
tenant. There is **no** notion of data shared across all tenants.

```
Tenant A  ── sees ──▶  master data where TenantId = A
Tenant B  ── sees ──▶  master data where TenantId = B
```

---

## 2. Problem statement, goals, assumptions

### 2.1 Problem

We need master data that is **Global** (owned by the platform, shared and
visible to every tenant) in addition to the existing **Tenant** master data
(owned by and visible to a single tenant). Today only Tenant scope exists.

Typical driver: reference/lookup data (countries, currencies, units, standard
product categories) should be maintained **once, centrally**, while tenants
retain the ability to add/override their own entries.

### 2.2 Goals

- **G1** — Support two scopes: **Global** and **Tenant**.
- **G2** — A tenant sees **Global data + its own Tenant data** in one merged
  view.
- **G3** — Existing tenant behaviour is unchanged (backward compatible).
- **G4** — **Minimal blast radius**: smallest possible change to schema, code,
  and APIs.

### 2.3 Non-goals

- Cross-tenant sharing between *specific* tenants (only "all tenants" = Global).
- Restructuring the two-schema (`ag` / `products`) layout.
- Reworking the field/definition model itself.

### 2.4 Assumptions (please confirm)

1. **A1** — The DB engine supports schemas within one database (SQL Server /
   PostgreSQL style). Schema is `ag` for metadata, `products` for data.
2. **A2** — `TenantId` is a non-null column today with a foreign key or logical
   link to a tenant.
3. **A3** — A single query path resolves "which master data does this tenant
   see" (a repository / service method we can centralize the change in).
4. **A4** — Global data is written by a **platform/admin** role, not by ordinary
   tenant users.
5. **A5** — When a tenant defines an entry with the same natural key as a global
   entry, the **tenant entry should win (override)** for that tenant.
   *(If instead you want global to be immutable and collisions rejected, see
   section 6.3 — this is a key decision.)*

---

## 3. Design principle: "Scope" as a first-class, minimal addition

The core idea that keeps the change small:

> Introduce a single **`Scope`** concept and make **`TenantId` nullable**.
> `Scope = Global` ⇒ `TenantId = NULL`. `Scope = Tenant` ⇒ `TenantId = <id>`.

Everything else (merge rules, uniqueness, security) derives from those two
columns. We do **not** create parallel "global" tables — that would double the
surface area and split the read path.

`Scope` is technically derivable from `TenantId IS NULL`, but we store it as an
explicit column anyway for readability, indexing, and to leave room for future
scope values (e.g. `Region`) without another migration.

```
Scope enum:
  1 = Global   -> TenantId IS NULL
  2 = Tenant   -> TenantId = <tenant>
```

---

## 4. Which layers get a scope?

Two valid options — pick based on whether **definitions** are global or only
**data** is.

### Option A (recommended, most minimal): Definitions stay global, only data is scoped

- `ag.MasterDataDefinition` / `ag.MasterDataField` remain **global/shared** as
  they effectively are today (the *shape* of "Country" is the same for everyone).
- Only `products.MasterDataRecord` gains `Scope` + nullable `TenantId`.

This is the smallest change: one table, one read path.

### Option B: Definitions are also scopable

- Add `Scope` + nullable `TenantId` to `ag.MasterDataDefinition` too, so a
  tenant can define its *own* master-data types on top of global ones.
- Larger surface (metadata read path, definition uniqueness, admin UI).

**Recommendation:** Start with **Option A**. Adopt B later only if tenants must
create their own definitions. The rest of this document assumes **Option A**
(and calls out where B differs).

---

## 5. Detailed design (Option A)

### 5.1 Data model change

Only `products.MasterDataRecord` changes.

```sql
-- 1. Add the scope discriminator (default = existing behaviour = Tenant)
ALTER TABLE products.MasterDataRecord
    ADD Scope TINYINT NOT NULL CONSTRAINT DF_MasterDataRecord_Scope DEFAULT (2); -- 2 = Tenant

-- 2. Make TenantId nullable so Global rows can have no tenant
ALTER TABLE products.MasterDataRecord
    ALTER COLUMN TenantId UNIQUEIDENTIFIER NULL;

-- 3. Integrity: Global <=> TenantId NULL, Tenant <=> TenantId NOT NULL
ALTER TABLE products.MasterDataRecord
    ADD CONSTRAINT CK_MasterDataRecord_Scope_Tenant CHECK (
        (Scope = 1 AND TenantId IS NULL) OR
        (Scope = 2 AND TenantId IS NOT NULL)
    );
```

### 5.2 EF Core model (if this is the stack — this repo is .NET/EF Core)

```csharp
public enum MasterDataScope : byte
{
    Global = 1,
    Tenant = 2
}

public class MasterDataRecord
{
    public Guid Id { get; set; }
    public int DefinitionId { get; set; }

    public MasterDataScope Scope { get; set; } = MasterDataScope.Tenant;
    public Guid? TenantId { get; set; }   // NULL when Scope == Global

    // ... existing field-value columns unchanged ...
}
```

```csharp
// OnModelCreating
modelBuilder.Entity<MasterDataRecord>(e =>
{
    e.Property(x => x.Scope).HasDefaultValue(MasterDataScope.Tenant);
    e.HasIndex(x => new { x.DefinitionId, x.Scope, x.TenantId });
    e.ToTable("MasterDataRecord", "products", t =>
        t.HasCheckConstraint("CK_MasterDataRecord_Scope_Tenant",
            "([Scope] = 1 AND [TenantId] IS NULL) OR ([Scope] = 2 AND [TenantId] IS NOT NULL)"));
});
```

### 5.3 The read path — the one place real logic changes

Today (conceptually):

```csharp
// BEFORE
var data = db.MasterDataRecord
    .Where(r => r.DefinitionId == defId && r.TenantId == currentTenantId);
```

After — a tenant sees **Global ∪ its own Tenant** rows:

```csharp
// AFTER
var data = db.MasterDataRecord
    .Where(r => r.DefinitionId == defId &&
                (r.Scope == MasterDataScope.Global || r.TenantId == currentTenantId));
```

If you use EF Core **global query filters** for tenancy, update the filter once
and every query inherits the new behaviour — this is the true "minimal change"
path:

```csharp
modelBuilder.Entity<MasterDataRecord>().HasQueryFilter(r =>
    r.Scope == MasterDataScope.Global || r.TenantId == _tenantProvider.CurrentTenantId);
```

> **[interpretation]** "how minimal effect I use to change" → centralize the
> change in the single query filter / resolver so no per-controller changes are
> needed.

### 5.4 Merge & override precedence (Assumption A5: tenant overrides global)

When Global and Tenant both have a row with the same **natural key** (the
business key, e.g. `Code`), the tenant row must win for that tenant.

```csharp
var merged = records
    .GroupBy(r => r.NaturalKey)                       // e.g. r.Code
    .Select(g => g.OrderBy(r => r.Scope)              // Tenant(2) sorts after Global(1)
                  .Last())                            // -> pick Tenant if present, else Global
    .ToList();
```

Precedence rule (single sentence): **Tenant-scoped row for key K hides the
Global row for key K, for that tenant only.**

### 5.5 Write path

- **Create Tenant record:** unchanged — `Scope = Tenant`, `TenantId = current`.
- **Create Global record:** `Scope = Global`, `TenantId = NULL`; allowed only
  for the platform/admin role (see section 7).
- Default of `Scope = Tenant` means any existing create code that doesn't set
  Scope keeps working.

### 5.6 Uniqueness

Enforce natural-key uniqueness **within a scope bucket** so Global and each
Tenant can independently own key `K`:

```sql
-- Unique per tenant (Tenant scope). Filtered index ignores Global rows.
CREATE UNIQUE INDEX UX_MDR_Tenant_Key
    ON products.MasterDataRecord (DefinitionId, TenantId, [Code])
    WHERE Scope = 2;

-- Unique globally (Global scope, TenantId is NULL).
CREATE UNIQUE INDEX UX_MDR_Global_Key
    ON products.MasterDataRecord (DefinitionId, [Code])
    WHERE Scope = 1;
```

---

## 6. Key decisions to lock down

### 6.1 Explicit `Scope` column vs. inferring from `TenantId IS NULL`
Storing `Scope` explicitly (chosen) costs one small column but improves
readability, indexing, and future extensibility. Inferring is marginally
smaller but less clear and harder to extend.

### 6.2 One table vs. separate "global" table
Chosen: **one table**. A separate `GlobalMasterDataRecord` table would fork the
read path, duplicate indexes/constraints, and complicate the merge — the
opposite of minimal.

### 6.3 Collision policy (needs your call)
- **(A5, default) Tenant overrides Global** — flexible, matches most SaaS
  lookup-data needs.
- **Alternative: Global is authoritative** — reject a tenant row whose key
  collides with a global key. Simpler mental model, less tenant flexibility.
  If you choose this, drop the merge step (5.4) and add a create-time check.

---

## 7. Security & access control

- Add a permission such as `MasterData.ManageGlobal`.
- **Write to Global** requires this permission (platform/admin only).
- **Write to Tenant** requires the existing per-tenant permission.
- **Read** is unchanged for tenant users — the merged view is served
  transparently; they need no new permission to *see* global data.
- Guard on the server, not just the UI: reject `Scope = Global` writes from
  non-platform principals.

---

## 8. Migration & rollout

### 8.1 Data migration
1. Deploy schema change with `Scope DEFAULT 2 (Tenant)` → **all existing rows
   become Tenant scope automatically**, so current behaviour is preserved (G3).
2. No backfill of `TenantId` needed (existing rows already have it).
3. Seed Global rows separately (admin action or seed script) after deploy.

### 8.2 Deployment order (backward compatible)
1. Migration: add `Scope` (default Tenant), make `TenantId` nullable, add check
   constraint + filtered unique indexes.
2. Deploy code with the updated read filter + merge and the Global write path
   (behind a feature flag if desired).
3. Enable Global authoring for admins; seed initial global data.

### 8.3 Rollback
- Code rollback is safe: old read path (`TenantId == current`) simply won't
  return Global rows; nothing breaks for tenant data.
- Schema rollback: only needed if Global rows exist; remove/relocate them first,
  then revert nullable/check-constraint changes.

---

## 9. Impact summary (the "how little changes" answer)

| Area | Change | Size |
|------|--------|------|
| `products.MasterDataRecord` schema | +`Scope` col, `TenantId` nullable, 1 check + 2 filtered indexes | Small |
| `ag.*` metadata schema | **none** (Option A) | None |
| Read path | 1 predicate in the central query filter/resolver | Small |
| Merge/override | 1 grouping step (only if A5) | Small |
| Write path | default keeps tenant writes as-is; add guarded global create | Small |
| Security | 1 new permission + server-side guard | Small |
| API/DTO | optional `scope` field (default `Tenant`) | Small |
| Data migration | column default = auto backfill to Tenant | Trivial |

Net: **one table touched, one read predicate changed** — the rest is
additive and backward compatible.

---

## 10. Edge cases & tests

- Tenant with no own rows → sees Global only.
- Tenant overrides a global key → sees its own row, not the global one (A5).
- Two tenants override the same global key independently → isolated (5.6).
- Non-admin attempts Global write → rejected server-side.
- Global row deleted while a tenant relied on it → tenant loses that key unless
  it had its own; decide whether to warn admins on global delete.
- Check constraint blocks illegal states (Global with a TenantId, Tenant with
  NULL).

Recommended automated tests: read-merge with/without override, uniqueness per
scope, constraint rejects illegal combos, authorization on global writes.

---

## 11. Open questions

1. Confirm "AG one" = the `ag` metadata schema. **[interpretation]**
2. Collision policy: tenant-overrides-global (A5) or global-authoritative?
   (section 6.3)
3. Do definitions need to be scopable too (Option B), or only data (Option A)?
4. Who exactly may author Global data (role/permission name)?
5. Is there an existing central tenant query filter we can amend, or are tenant
   filters written per query?
