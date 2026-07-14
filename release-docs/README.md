# Release Documentation

A single home for the **sprint release documentation** of all
7 products, for the whole of **2026**. Two ways to work:

- **One Excel file** — `release-tracker-2026.xlsx`: every project x sprint on
  one sheet with status dropdowns, colour-coding, progress bars and a live
  Dashboard tab. Best when you want a single file to share and update.
- **Per-project markdown docs** — a folder per project with a detailed
  release-notes document per sprint, for long-form detail. The Excel tracker's
  "Detailed doc" column links to these.

Both share the same projects, sprint calendar and **release-number register**
links, and are generated from the same config.

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
├─ release-tracker-2026.xlsx     ← the single-file Excel tracker
├─ generate_release_docs.py      ← regenerate the markdown scaffold
├─ generate_release_tracker.py   ← regenerate the Excel tracker
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
python3 release-docs/generate_release_docs.py      # markdown docs + indexes
python3 release-docs/generate_release_tracker.py    # release-tracker-2026.xlsx
```

Re-running the markdown generator never overwrites a sprint doc your team has
already edited (use `--force` to reset to blank templates). The Excel generator
rewrites the whole workbook, so regenerate it before teams start filling it in.
Requires `openpyxl` (`pip install openpyxl`).

## Release status legend

`Planned` → `In Progress` → `Testing` → `Released` (or `On Hold` / `Cancelled`)
— matches the TeamPulse **Releases** module.
