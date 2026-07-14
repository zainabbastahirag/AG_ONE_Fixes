#!/usr/bin/env python3
"""
Release documentation generator for the TeamPulse products.

Creates one folder per project, and inside it a per-year folder holding a
detailed release-notes document for every sprint of the year, plus a
per-project year index that links each sprint doc to your release-number
register.

WHY A SCRIPT?
- Your 7 projects run the same 2-week sprint cadence, so the whole year of
  release docs can be scaffolded consistently and re-generated at any time.
- Edit the CONFIG block below (projects, year, sprint cadence, register links)
  and re-run: `python3 generate_release_docs.py`
- Re-running is SAFE: existing sprint docs are never overwritten (your team's
  edits are preserved). Only missing docs and the index/templates are written.
  Use `--force` to overwrite everything (fresh templates).

Standard library only. No dependencies.
"""

from __future__ import annotations

import argparse
import datetime as dt
from pathlib import Path

# --------------------------------------------------------------------------- #
# CONFIG — edit this block, then re-run the script.
# --------------------------------------------------------------------------- #

YEAR = 2026

# First sprint of the year: number + start date. The cadence anchors the whole
# calendar. Defaults match the TeamPulse seed data (Sprint 7 = 14–28 Apr 2026,
# 2-week sprints), so Sprint 1 starts 20 Jan 2026.
FIRST_SPRINT_NUMBER = 1
FIRST_SPRINT_START = dt.date(2026, 1, 20)
SPRINT_LENGTH_DAYS = 14

# Generate sprints until (and including) the last one that STARTS on/before this
# date. Set to the end of the year for a full-year plan.
LAST_SPRINT_START_ON_OR_BEFORE = dt.date(2026, 12, 31)

# Your 7 projects. `register_link` should point at the "release number" document
# you already maintain for that project (a spreadsheet, Confluence page, the
# TeamPulse Releases screen, etc.). Leave the placeholder if you don't have the
# URL yet — every generated doc links to it in one place.
PROJECTS = [
    {"name": "AG ONE",  "slug": "ag-one",  "version_prefix": "1.",
     "register_link": "<link to the AG ONE release-number register>"},
    {"name": "OneWork", "slug": "onework", "version_prefix": "1.",
     "register_link": "<link to the OneWork release-number register>"},
    {"name": "Learn",   "slug": "learn",   "version_prefix": "1.",
     "register_link": "<link to the Learn release-number register>"},
    {"name": "Safe",    "slug": "safe",    "version_prefix": "0.",
     "register_link": "<link to the Safe release-number register>"},
    {"name": "OneFlow", "slug": "oneflow", "version_prefix": "1.",
     "register_link": "<link to the OneFlow release-number register>"},
    {"name": "Spot",    "slug": "spot",    "version_prefix": "0.",
     "register_link": "<link to the Spot release-number register>"},
    {"name": "Pulse",   "slug": "pulse",   "version_prefix": "0.",
     "register_link": "<link to the Pulse release-number register>"},
]

RELEASE_STATUSES = ["Planned", "In Progress", "Testing", "Released", "On Hold", "Cancelled"]

# --------------------------------------------------------------------------- #
# Internals
# --------------------------------------------------------------------------- #

ROOT = Path(__file__).resolve().parent


def build_sprints():
    """Return the full-year list of sprint dicts based on the CONFIG cadence."""
    sprints = []
    number = FIRST_SPRINT_NUMBER
    start = FIRST_SPRINT_START
    while start <= LAST_SPRINT_START_ON_OR_BEFORE:
        end = start + dt.timedelta(days=SPRINT_LENGTH_DAYS - 1)
        quarter = (start.month - 1) // 3 + 1
        sprints.append({
            "number": number,
            "quarter": quarter,
            "start": start,
            "end": end,
        })
        number += 1
        start = start + dt.timedelta(days=SPRINT_LENGTH_DAYS)
    return sprints


def sprint_doc_name(sprint) -> str:
    return f"sprint-{sprint['number']:02d}-release.md"


def sprint_release_doc(project, sprint) -> str:
    """The detailed per-sprint release-notes document teams fill in."""
    n = sprint["number"]
    window = f"{sprint['start']:%d %b %Y} \u2192 {sprint['end']:%d %b %Y}"
    statuses = " / ".join(RELEASE_STATUSES)
    return f"""# {project['name']} \u2014 Sprint {n:02d} Release Notes ({YEAR})

> Fill in every section before the sprint review. Replace all `<...>` placeholders.
> Keep entries factual and link out to PRs / tickets for the detail.
> Release-number register: [{project['name']} release register]({project['register_link']})

## Release at a glance

| Field | Value |
|-------|-------|
| Project / Product | {project['name']} |
| Sprint | Sprint {n:02d} (Q{sprint['quarter']} {YEAR}) |
| Sprint window | {window} |
| Release number / version | `{project['version_prefix']}x.y` &nbsp;\u2190 record this in the release register |
| Release name | <short human-friendly name> |
| Release status | {statuses} |
| Target date | <YYYY-MM-DD> |
| Released date | <YYYY-MM-DD or \u2014> |
| Progress % | <0\u2013100> |
| Release owner | <name> |
| Tech lead | <name> |

## 1. Release summary

<2\u20134 sentences: what this release delivers and why it matters to users/business.>

## 2. What's included

### \u2728 Features
- <new capability> \u2014 <ticket / PR link>

### \u2699\ufe0f Improvements
- <enhancement> \u2014 <ticket / PR link>

### \U0001f41b Bug fixes
- <fix> \u2014 <ticket / PR link>

### \U0001f9f0 Technical / chore
- <infra, refactor, dependency bump> \u2014 <ticket / PR link>

## 3. Work items in this release

| ID | Title | Type | Status | Story pts | Owner |
|----|-------|------|--------|-----------|-------|
| <#123> | <title> | Feature/Bug/Task | Done | <pts> | <name> |

## 4. Deployment

- **Environments:** <dev / staging / prod>
- **Deployment steps / runbook:** <link or steps>
- **Database / config / migration changes:** <yes+detail / none>
- **Feature flags:** <flags toggled, default state>

## 5. Testing & QA

- **Test summary:** <what was tested, coverage, sign-off>
- **Known issues:** <open issues shipped with this release, or none>
- **Rollback plan:** <how to roll back if needed>

## 6. Risks, dependencies & blockers

- <risk or dependency, owner, mitigation>

## 7. Documentation & links

- Release-number register: [{project['name']} release register]({project['register_link']})
- Related design / PRD: <link>
- Sprint board: <link>

## 8. Sign-off

| Role | Name | Approved (Y/N) | Date |
|------|------|----------------|------|
| Release owner | <name> | | |
| Tech lead | <name> | | |
| QA | <name> | | |
| Product / Stakeholder | <name> | | |
"""


def project_year_index(project, sprints) -> str:
    """Per-project year overview linking each sprint doc + the register."""
    lines = [
        f"# {project['name']} \u2014 Release Docs {YEAR}",
        "",
        f"Release-number register: [{project['name']} release register]({project['register_link']})",
        "",
        "Detailed release notes for every sprint this year. Open the sprint's",
        "document, fill it in with your team, and record the final release number",
        "in the register linked above.",
        "",
        f"| Sprint | Window | Release # / version | Status | Detailed notes |",
        f"|--------|--------|---------------------|--------|----------------|",
    ]
    for s in sprints:
        window = f"{s['start']:%d %b} \u2013 {s['end']:%d %b}"
        doc = f"{YEAR}/{sprint_doc_name(s)}"
        lines.append(
            f"| Sprint {s['number']:02d} (Q{s['quarter']}) | {window} | "
            f"<`{project['version_prefix']}x.y`> | <Planned> | [Open]({doc}) |"
        )
    lines.append("")
    lines.append("> Update the **Release #** and **Status** columns as each sprint progresses,")
    lines.append("> then keep the authoritative version list in your release register.")
    lines.append("")
    return "\n".join(lines)


def sprint_notes_template(sprints) -> str:
    """Standalone copy of the detailed template (project/sprint-agnostic)."""
    sample = {"name": "<PROJECT>", "version_prefix": "<x>.",
              "register_link": "<link to this project's release-number register>"}
    fake_sprint = {"number": 0, "quarter": 1,
                   "start": dt.date(YEAR, 1, 1), "end": dt.date(YEAR, 1, 14)}
    body = sprint_release_doc(sample, fake_sprint)
    body = body.replace("Sprint 00", "Sprint <NN>")
    header = (
        "<!-- TEMPLATE: copy this file into <project>/<year>/sprint-<NN>-release.md\n"
        "     and replace every <...> placeholder. -->\n\n"
    )
    return header + body


def top_readme(sprints) -> str:
    proj_rows = "\n".join(
        f"| {p['name']} | [`projects/{p['slug']}/`](projects/{p['slug']}/README.md) | "
        f"[register]({p['register_link']}) |"
        for p in PROJECTS
    )
    first = sprints[0]
    last = sprints[-1]
    return f"""# Release Documentation

A single home for the **sprint release documentation** of all
{len(PROJECTS)} products, for the whole of **{YEAR}**. Two ways to work:

- **One Excel file** \u2014 `release-tracker-{YEAR}.xlsx`: every project x sprint on
  one sheet with status dropdowns, colour-coding, progress bars and a live
  Dashboard tab. Best when you want a single file to share and update.
- **Per-project markdown docs** \u2014 a folder per project with a detailed
  release-notes document per sprint, for long-form detail. The Excel tracker's
  "Detailed doc" column links to these.

Both share the same projects, sprint calendar and **release-number register**
links, and are generated from the same config.

## The 7 projects

| Project | Folder | Release-number register |
|---------|--------|-------------------------|
{proj_rows}

## Folder layout

```
release-docs/
\u251c\u2500 README.md                     \u2190 you are here
\u251c\u2500 release-tracker-{YEAR}.xlsx     \u2190 the single-file Excel tracker
\u251c\u2500 generate_release_docs.py      \u2190 regenerate the markdown scaffold
\u251c\u2500 generate_release_tracker.py   \u2190 regenerate the Excel tracker
\u251c\u2500 templates/
\u2502  \u251c\u2500 sprint-release-notes.template.md
\u2502  \u2514\u2500 project-year-index.template.md
\u2514\u2500 projects/
   \u251c\u2500 ag-one/
   \u2502  \u251c\u2500 README.md                 \u2190 year index (table of all sprints)
   \u2502  \u2514\u2500 {YEAR}/
   \u2502     \u251c\u2500 sprint-01-release.md
   \u2502     \u251c\u2500 sprint-02-release.md
   \u2502     \u2514\u2500 ...
   \u2514\u2500 ... (one folder per project)
```

## How your teams use it

1. Open your project folder, e.g. `projects/ag-one/README.md` \u2014 it lists every
   sprint of {YEAR} with a link to that sprint's detailed release doc.
2. Open the sprint doc (e.g. `projects/ag-one/{YEAR}/sprint-01-release.md`) and
   fill in every section with your team before the sprint review.
3. Record the final **release number / version** in your release-number register
   (linked at the top of every doc and index), so the register stays the single
   source of truth for versions and these docs hold the detail.

## Sprint calendar ({YEAR})

- Cadence: **{SPRINT_LENGTH_DAYS}-day sprints**.
- {len(sprints)} sprints scaffolded: Sprint {first['number']:02d}
  ({first['start']:%d %b %Y}) \u2192 Sprint {last['number']:02d}
  ({last['start']:%d %b %Y}).

## Regenerating / changing projects or dates

Edit the `CONFIG` block in `generate_release_docs.py` (projects, year, cadence,
register links) and run:

```bash
python3 release-docs/generate_release_docs.py      # markdown docs + indexes
python3 release-docs/generate_release_tracker.py    # release-tracker-{YEAR}.xlsx
```

Re-running the markdown generator never overwrites a sprint doc your team has
already edited (use `--force` to reset to blank templates). The Excel generator
rewrites the whole workbook, so regenerate it before teams start filling it in.
Requires `openpyxl` (`pip install openpyxl`).

## Release status legend

`Planned` \u2192 `In Progress` \u2192 `Testing` \u2192 `Released` (or `On Hold` / `Cancelled`)
\u2014 matches the TeamPulse **Releases** module.
"""


def write(path: Path, content: str, force: bool, protect: bool = False):
    """Write content. If protect and file exists, skip unless force."""
    if path.exists() and protect and not force:
        return "skip"
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(content, encoding="utf-8")
    return "write"


def main():
    parser = argparse.ArgumentParser(description="Generate release documentation scaffold.")
    parser.add_argument("--force", action="store_true",
                        help="Overwrite existing per-sprint docs (loses team edits).")
    args = parser.parse_args()

    sprints = build_sprints()

    written = 0
    skipped = 0

    # Top-level README + templates
    write(ROOT / "README.md", top_readme(sprints), force=True)
    write(ROOT / "templates" / "sprint-release-notes.template.md",
          sprint_notes_template(sprints), force=True)
    # A tiny index template for reference.
    write(ROOT / "templates" / "project-year-index.template.md",
          project_year_index({"name": "<PROJECT>", "slug": "<slug>",
                               "version_prefix": "<x>.",
                               "register_link": "<link to release-number register>"},
                              sprints),
          force=True)

    for project in PROJECTS:
        pdir = ROOT / "projects" / project["slug"]
        write(pdir / "README.md", project_year_index(project, sprints), force=True)
        for s in sprints:
            result = write(pdir / str(YEAR) / sprint_doc_name(s),
                           sprint_release_doc(project, s),
                           force=args.force, protect=True)
            if result == "write":
                written += 1
            else:
                skipped += 1

    total_docs = len(PROJECTS) * len(sprints)
    print(f"Projects: {len(PROJECTS)} | Sprints/project: {len(sprints)} | "
          f"Total sprint docs: {total_docs}")
    print(f"Sprint docs written: {written} | preserved (already existed): {skipped}")
    print("Indexes, README and templates refreshed.")


if __name__ == "__main__":
    main()
