# Release Documentation

A single home for the **sprint release documentation** of all
7 products, for the whole of **2026**. Each project has its own
folder with a detailed release-notes document per sprint, so every team updates
their own docs in one predictable place, and each doc links back to your
existing **release-number register**.

## The 7 projects

| Project | Folder | Release-number register |
|---------|--------|-------------------------|
| AG ONE | [`projects/ag-one/`](projects/ag-one/README.md) | [register](<link to the AG ONE release-number register>) |
| OneWork | [`projects/onework/`](projects/onework/README.md) | [register](<link to the OneWork release-number register>) |
| Learn | [`projects/learn/`](projects/learn/README.md) | [register](<link to the Learn release-number register>) |
| Safe | [`projects/safe/`](projects/safe/README.md) | [register](<link to the Safe release-number register>) |
| OneFlow | [`projects/oneflow/`](projects/oneflow/README.md) | [register](<link to the OneFlow release-number register>) |
| Spot | [`projects/spot/`](projects/spot/README.md) | [register](<link to the Spot release-number register>) |
| Pulse | [`projects/pulse/`](projects/pulse/README.md) | [register](<link to the Pulse release-number register>) |

## Folder layout

```
release-docs/
├─ README.md                     ← you are here
├─ generate_release_docs.py      ← regenerate / extend the scaffold
├─ templates/
│  ├─ sprint-release-notes.template.md
│  └─ project-year-index.template.md
└─ projects/
   ├─ ag-one/
   │  ├─ README.md                 ← year index (table of all sprints)
   │  └─ 2026/
   │     ├─ sprint-01-release.md
   │     ├─ sprint-02-release.md
   │     └─ ...
   └─ ... (one folder per project)
```

## How your teams use it

1. Open your project folder, e.g. `projects/ag-one/README.md` — it lists every
   sprint of 2026 with a link to that sprint's detailed release doc.
2. Open the sprint doc (e.g. `projects/ag-one/2026/sprint-01-release.md`) and
   fill in every section with your team before the sprint review.
3. Record the final **release number / version** in your release-number register
   (linked at the top of every doc and index), so the register stays the single
   source of truth for versions and these docs hold the detail.

## Sprint calendar (2026)

- Cadence: **14-day sprints**.
- 25 sprints scaffolded: Sprint 01
  (20 Jan 2026) → Sprint 25
  (22 Dec 2026).

## Regenerating / changing projects or dates

Edit the `CONFIG` block in `generate_release_docs.py` (projects, year, cadence,
register links) and run:

```bash
python3 release-docs/generate_release_docs.py
```

Re-running never overwrites a sprint doc your team has already edited. To reset
everything back to blank templates, run with `--force`.

## Release status legend

`Planned` → `In Progress` → `Testing` → `Released` (or `On Hold` / `Cancelled`)
— matches the TeamPulse **Releases** module.
