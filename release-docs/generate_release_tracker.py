#!/usr/bin/env python3
"""
Single-file release tracker (Excel) for the TeamPulse products.

Produces ONE workbook, `release-tracker-<year>.xlsx`, that all 7 projects use to
update their release documentation for every sprint of the year:

  - "Read Me"         : how to use + status legend + register links
  - "Release Tracker" : one row per project x sprint (the sheet teams fill in)
  - "Dashboard"       : live status counts per project (formulas)

Projects, year and sprint calendar are shared with `generate_release_docs.py`
(same CONFIG), so this Excel file stays in sync with the per-project markdown
docs. Edit the CONFIG in that file, then re-run:

    python3 generate_release_tracker.py

Requires openpyxl (`pip install openpyxl`). Standard library otherwise.
"""

from __future__ import annotations

from pathlib import Path

from openpyxl import Workbook
from openpyxl.formatting.rule import CellIsRule, DataBarRule
from openpyxl.styles import Alignment, Border, Font, PatternFill, Side
from openpyxl.utils import get_column_letter
from openpyxl.worksheet.datavalidation import DataValidation

from generate_release_docs import PROJECTS, RELEASE_STATUSES, YEAR, build_sprints

ROOT = Path(__file__).resolve().parent
OUTPUT = ROOT / f"release-tracker-{YEAR}.xlsx"

# Palette (matches the repo's existing evaluation workbook style)
NAVY = "1F4E79"
BLUE = "2E75B6"
LIGHT_BLUE = "D9E2F3"
ALT = "F2F2F2"
WHITE = "FFFFFF"

STATUS_COLORS = {
    "Planned": "D9D9D9",       # grey
    "In Progress": "BDD7EE",   # blue
    "Testing": "FFEB9C",       # yellow
    "Released": "C6EFCE",      # green
    "On Hold": "FCE4D6",       # orange
    "Cancelled": "FFC7CE",     # red
}

HEADER_FILL = PatternFill("solid", fgColor=NAVY)
SUBHEADER_FILL = PatternFill("solid", fgColor=LIGHT_BLUE)
ALT_FILL = PatternFill("solid", fgColor=ALT)
WHITE_FILL = PatternFill("solid", fgColor=WHITE)
THIN = Side(style="thin", color="B4B4B4")
BORDER = Border(left=THIN, right=THIN, top=THIN, bottom=THIN)


def style_cell(cell, *, bold=False, fill=None, align="left", size=11, color="000000", wrap=True):
    cell.font = Font(name="Calibri", size=size, bold=bold, color=color)
    cell.alignment = Alignment(horizontal=align, vertical="center", wrap_text=wrap)
    cell.border = BORDER
    if fill:
        cell.fill = fill


def set_col_widths(ws, widths):
    for idx, width in enumerate(widths, start=1):
        ws.column_dimensions[get_column_letter(idx)].width = width


# --------------------------------------------------------------------------- #
# Release Tracker sheet (the main one teams fill in)
# --------------------------------------------------------------------------- #

TRACKER_HEADERS = [
    "Project", "Sprint", "Quarter", "Sprint start", "Sprint end",
    "Release # / version", "Release name", "Status", "Target date",
    "Released date", "Progress %", "Release owner", "Tech lead",
    "Summary / highlights", "Register link", "Detailed doc", "Notes",
]
# 0-based column indexes used for validation / formatting
COL_STATUS = TRACKER_HEADERS.index("Status") + 1
COL_PROGRESS = TRACKER_HEADERS.index("Progress %") + 1


def build_tracker_sheet(ws, sprints):
    ws.title = "Release Tracker"

    last_col = len(TRACKER_HEADERS)
    ws.merge_cells(start_row=1, start_column=1, end_row=1, end_column=last_col)
    ws.cell(1, 1, f"RELEASE TRACKER {YEAR} \u2014 all projects, every sprint")
    style_cell(ws.cell(1, 1), bold=True, fill=HEADER_FILL, align="center", size=16, color=WHITE)

    ws.merge_cells(start_row=2, start_column=1, end_row=2, end_column=last_col)
    ws.cell(2, 1, "Fill in one row per sprint release. Status colours the row; record the final version in your release register too.")
    style_cell(ws.cell(2, 1), fill=SUBHEADER_FILL, align="center", size=10)

    header_row = 3
    for c, h in enumerate(TRACKER_HEADERS, 1):
        ws.cell(header_row, c, h)
        style_cell(ws.cell(header_row, c), bold=True, fill=SUBHEADER_FILL, align="center", size=10)

    reg_link = {p["name"]: p["register_link"] for p in PROJECTS}
    slug_by_name = {p["name"]: p["slug"] for p in PROJECTS}
    vprefix = {p["name"]: p["version_prefix"] for p in PROJECTS}

    row = header_row + 1
    first_data = row
    for project in PROJECTS:
        for s in sprints:
            fill = ALT_FILL if (row % 2 == 0) else WHITE_FILL
            values = [
                project["name"],
                f"Sprint {s['number']:02d}",
                f"Q{s['quarter']}",
                s["start"],
                s["end"],
                None,                    # Release # / version
                None,                    # Release name
                "Planned",               # Status default
                None,                    # Target date
                None,                    # Released date
                None,                    # Progress %
                None,                    # Release owner
                None,                    # Tech lead
                None,                    # Summary
                reg_link[project["name"]],  # Register link
                f"projects/{slug_by_name[project['name']]}/{YEAR}/sprint-{s['number']:02d}-release.md",
                None,                    # Notes
            ]
            for c, v in enumerate(values, 1):
                cell = ws.cell(row, c, v)
                align = "left" if c in (1, 6, 7, 14, 15, 16, 17) else "center"
                style_cell(cell, fill=fill, align=align, size=10)
                if c in (4, 5):  # date columns
                    cell.number_format = "yyyy-mm-dd"
            ws.cell(row, COL_PROGRESS).number_format = "0"
            ws.cell(row, TRACKER_HEADERS.index("Target date") + 1).number_format = "yyyy-mm-dd"
            ws.cell(row, TRACKER_HEADERS.index("Released date") + 1).number_format = "yyyy-mm-dd"
            row += 1
    last_data = row - 1

    # Status dropdown
    status_dv = DataValidation(type="list", formula1='"' + ",".join(RELEASE_STATUSES) + '"',
                               allow_blank=True)
    status_dv.error = "Pick a status from the list"
    status_dv.errorTitle = "Invalid status"
    ws.add_data_validation(status_dv)
    status_letter = get_column_letter(COL_STATUS)
    status_dv.add(f"{status_letter}{first_data}:{status_letter}{last_data}")

    # Progress 0-100 validation
    prog_dv = DataValidation(type="whole", operator="between", formula1="0", formula2="100",
                             allow_blank=True)
    prog_dv.error = "Enter a whole number 0-100"
    prog_dv.errorTitle = "Invalid progress"
    ws.add_data_validation(prog_dv)
    prog_letter = get_column_letter(COL_PROGRESS)
    prog_dv.add(f"{prog_letter}{first_data}:{prog_letter}{last_data}")

    # Colour whole row by Status (applied to the Status column cell)
    status_range = f"{status_letter}{first_data}:{status_letter}{last_data}"
    for status, color in STATUS_COLORS.items():
        ws.conditional_formatting.add(
            status_range,
            CellIsRule(operator="equal", formula=[f'"{status}"'],
                       fill=PatternFill("solid", fgColor=color)),
        )

    # Progress data bar
    ws.conditional_formatting.add(
        f"{prog_letter}{first_data}:{prog_letter}{last_data}",
        DataBarRule(start_type="num", start_value=0, end_type="num", end_value=100,
                    color=BLUE, showValue=True),
    )

    ws.auto_filter.ref = f"A{header_row}:{get_column_letter(last_col)}{last_data}"
    widths = [16, 11, 8, 12, 12, 16, 22, 13, 12, 12, 10, 16, 16, 34, 26, 34, 30]
    set_col_widths(ws, widths)
    ws.freeze_panes = f"C{first_data}"
    ws.sheet_view.zoomScale = 90
    return first_data, last_data


# --------------------------------------------------------------------------- #
# Dashboard sheet (live counts)
# --------------------------------------------------------------------------- #

def build_dashboard_sheet(wb, first_data, last_data):
    ws = wb.create_sheet("Dashboard")
    tracker = "'Release Tracker'"
    proj_col = f"{tracker}!$A${first_data}:$A${last_data}"
    status_col = f"{tracker}!$H${first_data}:$H${last_data}"

    headers = ["Project", "Total sprints"] + RELEASE_STATUSES
    last_col = len(headers)

    ws.merge_cells(start_row=1, start_column=1, end_row=1, end_column=last_col)
    ws.cell(1, 1, f"RELEASE DASHBOARD {YEAR} \u2014 live counts by status")
    style_cell(ws.cell(1, 1), bold=True, fill=HEADER_FILL, align="center", size=14, color=WHITE)

    for c, h in enumerate(headers, 1):
        ws.cell(2, c, h)
        style_cell(ws.cell(2, c), bold=True, fill=SUBHEADER_FILL, align="center", size=10)

    row = 3
    for i, project in enumerate(PROJECTS):
        fill = ALT_FILL if i % 2 == 1 else WHITE_FILL
        name = project["name"]
        ws.cell(row, 1, name)
        style_cell(ws.cell(row, 1), fill=fill, bold=True)
        ws.cell(row, 2, f'=COUNTIF({proj_col},$A{row})')
        style_cell(ws.cell(row, 2), fill=fill, align="center")
        for j, status in enumerate(RELEASE_STATUSES):
            col = 3 + j
            ws.cell(row, col,
                    f'=COUNTIFS({proj_col},$A{row},{status_col},"{status}")')
            style_cell(ws.cell(row, col), fill=PatternFill("solid", fgColor=STATUS_COLORS[status]),
                       align="center")
        row += 1

    # Totals row
    ws.cell(row, 1, "All projects")
    style_cell(ws.cell(row, 1), bold=True, fill=SUBHEADER_FILL)
    for c in range(2, last_col + 1):
        letter = get_column_letter(c)
        ws.cell(row, c, f"=SUM({letter}3:{letter}{row-1})")
        style_cell(ws.cell(row, c), bold=True, fill=SUBHEADER_FILL, align="center")

    set_col_widths(ws, [16, 14] + [13] * len(RELEASE_STATUSES))
    ws.freeze_panes = "B3"


# --------------------------------------------------------------------------- #
# Read Me sheet
# --------------------------------------------------------------------------- #

def build_readme_sheet(wb):
    ws = wb.active
    ws.title = "Read Me"
    ws.merge_cells("A1:D1")
    ws["A1"] = f"RELEASE DOCUMENTATION \u2014 {YEAR}"
    style_cell(ws["A1"], bold=True, fill=HEADER_FILL, align="center", size=18, color=WHITE)

    lines = [
        ("How to use", True),
        ("1. Go to the 'Release Tracker' tab. Every project already has a row for each sprint of the year.", False),
        ("2. Your team fills in: release number/version, name, status, dates, progress %, owners, summary and notes.", False),
        ("3. 'Status' and 'Progress %' are dropdown / validated. The row is colour-coded by status.", False),
        ("4. The 'Detailed doc' column points to the matching markdown release notes for long-form detail.", False),
        ("5. Record the final version in your project's release-number register (links below and in each row).", False),
        ("6. The 'Dashboard' tab updates automatically with counts by status per project.", False),
        ("", False),
        ("Status legend", True),
    ]
    row = 3
    for text, is_header in lines:
        ws.merge_cells(start_row=row, start_column=1, end_row=row, end_column=4)
        ws.cell(row, 1, text)
        if is_header:
            style_cell(ws.cell(row, 1), bold=True, fill=SUBHEADER_FILL)
        else:
            style_cell(ws.cell(row, 1))
        row += 1

    for status in RELEASE_STATUSES:
        ws.cell(row, 1, status)
        style_cell(ws.cell(row, 1), fill=PatternFill("solid", fgColor=STATUS_COLORS[status]),
                   align="center", bold=True)
        ws.merge_cells(start_row=row, start_column=2, end_row=row, end_column=4)
        ws.cell(row, 2, "")
        style_cell(ws.cell(row, 2))
        row += 1

    row += 1
    ws.merge_cells(start_row=row, start_column=1, end_row=row, end_column=4)
    ws.cell(row, 1, "Projects & release-number registers")
    style_cell(ws.cell(row, 1), bold=True, fill=SUBHEADER_FILL)
    row += 1
    ws.cell(row, 1, "Project")
    ws.merge_cells(start_row=row, start_column=2, end_row=row, end_column=4)
    ws.cell(row, 2, "Release-number register link")
    style_cell(ws.cell(row, 1), bold=True, fill=ALT_FILL)
    style_cell(ws.cell(row, 2), bold=True, fill=ALT_FILL)
    row += 1
    for p in PROJECTS:
        ws.cell(row, 1, p["name"])
        style_cell(ws.cell(row, 1), bold=True)
        ws.merge_cells(start_row=row, start_column=2, end_row=row, end_column=4)
        ws.cell(row, 2, p["register_link"])
        style_cell(ws.cell(row, 2))
        row += 1

    set_col_widths(ws, [22, 30, 30, 30])
    ws.sheet_view.zoomScale = 100


def main():
    sprints = build_sprints()
    wb = Workbook()
    build_readme_sheet(wb)
    tracker = wb.create_sheet("Release Tracker")
    first_data, last_data = build_tracker_sheet(tracker, sprints)
    build_dashboard_sheet(wb, first_data, last_data)
    wb.save(OUTPUT)
    print(f"Created {OUTPUT.name}: {len(PROJECTS)} projects x {len(sprints)} sprints "
          f"= {len(PROJECTS) * len(sprints)} release rows.")


if __name__ == "__main__":
    main()
