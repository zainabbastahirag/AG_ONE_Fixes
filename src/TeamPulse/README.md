# TeamPulse — Team & Resource Command Center

TeamPulse is a full-stack ASP.NET Core 8 MVC application that turns the "Team Overview / Resource Tracker"
spreadsheet into a living product. It combines five command-center modules in one place:

| Module | What it does |
|--------|--------------|
| **Teams** | Manage teams/products, tech leads, status (On Track / At Risk / On Hold / Blocked), focus & blockers |
| **Resources** | Resource manager — people, roles, team allocation %, capacity, linked login accounts |
| **Work Tracker** | Kanban board + list of work items across statuses, priorities, types, sprints |
| **Sprints** | Plan iterations, set the active sprint, track completion |
| **Releases** | Release manager with status, progress %, target & released dates |
| **Performance** | Sprint & quarterly reviews across 5 dimensions, reviewer assignments, ratings |

## Key capabilities

- **Invite tech leads & reviewers** — Admins generate invite links (Invitations). The invitee sets up their
  own account and is auto-assigned a role (and optionally made lead of a team).
- **Reviewer assignments** — Admins assign specific reviewers to specific members so they can submit
  sprint/quarterly performance reviews ("My Review Queue").
- **Role-based access** — `Admin`, `TechLead`, `Member`. Management actions are restricted; reviewers can only
  review the members assigned to them or on teams they lead.
- **Seeded from the real tracker** — Teams, tech leads, members, sprints, work items, releases and a sample
  review are seeded on first run.

## Tech stack

- ASP.NET Core 8 MVC
- Entity Framework Core 8 (SQLite) — **code-first migrations**
- ASP.NET Core Identity (roles + cookie auth)
- Bootstrap 5 + Bootstrap Icons, custom design system (`wwwroot/css/site.css`)

## Run it

```bash
cd src/TeamPulse
dotnet run
```

The database is created/migrated and seeded automatically on first launch.

Open the app and sign in with the seeded admin:

- **Email:** `admin@teampulse.app`
- **Password:** `Admin#2026`

Seeded tech-lead accounts (e.g. `abdullah@teampulse.app`, `majed@teampulse.app`) use password `Pulse#2026`.

## Database / migrations

```bash
# add a new migration after model changes
dotnet ef migrations add <Name> -o Data/Migrations
# apply migrations (also done automatically at startup)
dotnet ef database update
```
