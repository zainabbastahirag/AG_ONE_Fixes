## Cursor Cloud specific instructions

This workspace contains standalone files for a Blazor WebAssembly + .NET 8 Clean Architecture project (AG ONE). Files are authored here and copy-pasted into the actual multi-project solution.

### Project structure
- **Shared layer** (`AGOne.Shared`): DTOs, interfaces, authorization. Builds with `Shared.csproj` (net8.0, no ASP.NET framework ref).
- **API layer** (`AGOne.Api`): ASP.NET Core controllers. Needs `Microsoft.AspNetCore.App` framework reference.
- **Infrastructure layer** (`AGOne.Infrastructure`): EF Core services. Needs `Microsoft.EntityFrameworkCore.Relational`.
- **UI layer**: Blazor WASM `.razor` pages. Requires full Blazor WASM project context to compile.

### Build verification
- Only `Shared.csproj` exists here. It can verify DTO/interface files directly: `dotnet build Shared.csproj`
- Controller and service `.cs` files target different project layers and need their own `.csproj` (ASP.NET Web SDK or EF Core packages) to compile — create temp projects as needed.
- `.razor` files cannot be compiled standalone; they need a Blazor WASM project with `_Imports.razor`, layouts, etc.

### Schema
- Database schema is `[core]` (e.g. `core.Users`, `core.Roles`, `core.UserRoles`, `core.Products`, `core.Tenants`).
- See `assign-user-roles.sql` and `verify-user-roles.sql` for real-world schema and data references.

### Design system
- Primary blue: `#3b82f6`, dark navy: `#1e3a5f`
- Font: `'Inter', 'Segoe UI', sans-serif`
- Rounded corners: 8–12px, white cards with `#e2e8f0` borders
- Table headers use `background: #3b82f6; color: #fff`
