# AG ONE — Subscription / Entitlement engine

Reference implementation that layers **subscription plans** (Starter / Lite / Standard / Enterprise)
on top of your **existing RBAC** (`core.Permissions` / `core.Roles` / `core.Users`) and reuses it across
`hire`, `learn`, and `work`.

> Full design: [`docs/Subscription-Entitlement-Architecture.md`](../../docs/Subscription-Entitlement-Architecture.md)

## The core rule

```
Effective access = RBAC permission  AND  plan entitlement
```

You keep every existing role/permission. A single new mapping — `Feature → existing permission codes` —
lets a plan *gate* the permissions you already have. No changes to your RBAC tables.

## Projects

| Project | Purpose |
|---|---|
| `src/AgOne.Entitlements` | Pure engine (no ASP.NET dep): domain, catalog, `EntitlementService`, `AccessResolver` |
| `src/AgOne.Entitlements.AspNetCore` | `[RequirePermission]` / `[RequireFeature]`, dynamic policy provider, handler, tenant middleware, caching, DI |
| `tests/AgOne.Entitlements.Tests` | xUnit tests (16) |

## Build & test

```bash
cd src/Subscriptions
dotnet build
dotnet test
```

## Use it in an app

```csharp
builder.Services.AddAgOneEntitlements();
builder.Services.AddScoped<IUserPermissionProvider, EfUserPermissionProvider>(); // over core.*
builder.Services.AddScoped<ISubscriptionStore,     EfSubscriptionStore>();        // over billing.*

app.UseAuthentication();
app.UseAgOneTenantResolution();
app.UseAuthorization();
```

```csharp
[RequirePermission("hire.leave.approve")]      // RBAC + plan, drop-in for a role-only attribute
public IActionResult Approve(int id) => ...;

[RequireFeature(FeatureKeys.DocumentManagement)] // pure plan gate for a module
public class DocumentsController : Controller { }
```

## SQL

- [`sql/001_billing_schema.sql`](sql/001_billing_schema.sql) — `billing` schema + `vEntitledPermissions` view + `fnEffectivePermissions` function
- [`sql/002_seed_catalog.sql`](sql/002_seed_catalog.sql) — catalog seed that mirrors `PlanCatalog.cs`
