#!/usr/bin/env python3
"""
AG ONE — 2026 Team & Resource Management Tracker
Director-ready Excel workbook with 6 polished sheets.
"""

import openpyxl
from openpyxl.styles import (
    Font, PatternFill, Alignment, Border, Side, numbers
)
from openpyxl.utils import get_column_letter
from openpyxl.chart import BarChart, Reference
from copy import copy

# ─── colour palette ───────────────────────────────────────────────────
NAVY      = "1E3A5F"
BLUE      = "3B82F6"
LIGHT_BLUE= "DBEAFE"
WHITE     = "FFFFFF"
DARK_GRAY = "374151"
MED_GRAY  = "6B7280"
LIGHT_GRAY= "F3F4F6"
BORDER_CLR= "E2E8F0"
GREEN     = "10B981"
AMBER     = "F59E0B"
RED       = "EF4444"
LIGHT_GREEN  = "D1FAE5"
LIGHT_AMBER  = "FEF3C7"
LIGHT_RED    = "FEE2E2"

thin_border = Border(
    left=Side(style="thin", color=BORDER_CLR),
    right=Side(style="thin", color=BORDER_CLR),
    top=Side(style="thin", color=BORDER_CLR),
    bottom=Side(style="thin", color=BORDER_CLR),
)

# ─── teams data ───────────────────────────────────────────────────────
TEAMS = {
    "OneWork":  {"lead": "Abdullah",  "members": ["Abdullah", "Nastaran", "Jawad", "Geena"]},
    "Learn":    {"lead": "Ricky",     "members": ["Ricky", "Loc", "Than"]},
    "Safe":     {"lead": "Faisal",    "members": ["Faisal", "Surya", "Kiritini"]},
    "OneHire":  {"lead": "Logesh",    "members": ["Logesh", "Hanis", "Fatin", "Sharuti"]},
    "Spot":     {"lead": "Ricky",     "members": ["Ricky", "Loc", "Than", "Majed", "Hema"]},
    "Pulse":    {"lead": "Hanis",     "members": ["Hanis", "Fatin", "Majed", "Hema", "Rahmya", "Umeshawar"]},
}

TEAM_COLORS = {
    "OneWork": "3B82F6",
    "Learn":   "8B5CF6",
    "Safe":    "10B981",
    "OneHire": "F59E0B",
    "Spot":    "EF4444",
    "Pulse":   "EC4899",
}

TEAM_LIGHT = {
    "OneWork": "DBEAFE",
    "Learn":   "EDE9FE",
    "Safe":    "D1FAE5",
    "OneHire": "FEF3C7",
    "Spot":    "FEE2E2",
    "Pulse":   "FCE7F3",
}

SPRINTS_2026 = []
from datetime import date, timedelta
sprint_start = date(2026, 1, 5)
sprint_num = 1
while sprint_start.year == 2026:
    sprint_end = sprint_start + timedelta(days=13)
    if sprint_end.year > 2026:
        sprint_end = date(2026, 12, 31)
    SPRINTS_2026.append({
        "name": f"Sprint {sprint_num}",
        "start": sprint_start.strftime("%d %b"),
        "end": sprint_end.strftime("%d %b"),
    })
    sprint_start = sprint_end + timedelta(days=1)
    sprint_num += 1


def style_header_row(ws, row, max_col, fill_color=NAVY):
    """Apply header styling to a row."""
    for col in range(1, max_col + 1):
        cell = ws.cell(row=row, column=col)
        cell.font = Font(name="Aptos", bold=True, color=WHITE, size=11)
        cell.fill = PatternFill(start_color=fill_color, end_color=fill_color, fill_type="solid")
        cell.alignment = Alignment(horizontal="center", vertical="center", wrap_text=True)
        cell.border = thin_border


def style_data_cell(cell, row_idx, wrap=True, center=True):
    """Alternate-row styling."""
    fill = PatternFill(start_color=LIGHT_GRAY, end_color=LIGHT_GRAY, fill_type="solid") if row_idx % 2 == 0 else PatternFill(start_color=WHITE, end_color=WHITE, fill_type="solid")
    cell.fill = fill
    cell.font = Font(name="Aptos", size=10, color=DARK_GRAY)
    cell.border = thin_border
    cell.alignment = Alignment(
        horizontal="center" if center else "left",
        vertical="center",
        wrap_text=wrap,
    )


def add_title_block(ws, title, subtitle, max_col):
    """Frozen title band at rows 1-2."""
    ws.merge_cells(start_row=1, start_column=1, end_row=1, end_column=max_col)
    title_cell = ws.cell(row=1, column=1, value=title)
    title_cell.font = Font(name="Aptos", bold=True, size=16, color=WHITE)
    title_cell.fill = PatternFill(start_color=NAVY, end_color=NAVY, fill_type="solid")
    title_cell.alignment = Alignment(horizontal="left", vertical="center")
    for c in range(2, max_col + 1):
        ws.cell(row=1, column=c).fill = PatternFill(start_color=NAVY, end_color=NAVY, fill_type="solid")
    ws.row_dimensions[1].height = 36

    ws.merge_cells(start_row=2, start_column=1, end_row=2, end_column=max_col)
    sub_cell = ws.cell(row=2, column=1, value=subtitle)
    sub_cell.font = Font(name="Aptos", size=10, italic=True, color=MED_GRAY)
    sub_cell.fill = PatternFill(start_color=LIGHT_BLUE, end_color=LIGHT_BLUE, fill_type="solid")
    sub_cell.alignment = Alignment(horizontal="left", vertical="center")
    for c in range(2, max_col + 1):
        ws.cell(row=2, column=c).fill = PatternFill(start_color=LIGHT_BLUE, end_color=LIGHT_BLUE, fill_type="solid")
    ws.row_dimensions[2].height = 22


def auto_width(ws, min_w=10, max_w=32):
    for col_cells in ws.columns:
        max_len = 0
        col_letter = get_column_letter(col_cells[0].column)
        for cell in col_cells:
            if cell.value:
                max_len = max(max_len, len(str(cell.value)))
        ws.column_dimensions[col_letter].width = min(max(max_len + 3, min_w), max_w)


# ═══════════════════════════════════════════════════════════════════════
# BUILD WORKBOOK
# ═══════════════════════════════════════════════════════════════════════
wb = openpyxl.Workbook()

# ───────────────────────────────────────────────────────────────────────
# SHEET 1 — TEAM OVERVIEW DASHBOARD
# ───────────────────────────────────────────────────────────────────────
ws1 = wb.active
ws1.title = "Team Overview"
ws1.sheet_properties.tabColor = NAVY

COLS1 = ["Team / Product", "Tech Lead", "Team Size", "Members", "Current Sprint", "Sprint Goal", "Status", "Key Blocker"]
max_c1 = len(COLS1)
add_title_block(ws1, "AG ONE — Team Overview Dashboard 2026", "All teams at a glance  •  Updated: ___________", max_c1)
style_header_row(ws1, 3, max_c1, BLUE)
for i, h in enumerate(COLS1, 1):
    ws1.cell(row=3, column=i, value=h)
    style_header_row(ws1, 3, max_c1, BLUE)

r = 4
for team, info in TEAMS.items():
    ws1.cell(row=r, column=1, value=team)
    ws1.cell(row=r, column=2, value=info["lead"])
    ws1.cell(row=r, column=3, value=len(info["members"]))
    ws1.cell(row=r, column=4, value=", ".join(info["members"]))
    ws1.cell(row=r, column=5, value="Sprint 1")
    ws1.cell(row=r, column=6, value="")
    ws1.cell(row=r, column=7, value="On Track")
    ws1.cell(row=r, column=8, value="")

    tc = TEAM_COLORS[team]
    for c in range(1, max_c1 + 1):
        cell = ws1.cell(row=r, column=c)
        if c == 1:
            cell.font = Font(name="Aptos", bold=True, size=11, color=WHITE)
            cell.fill = PatternFill(start_color=tc, end_color=tc, fill_type="solid")
        else:
            style_data_cell(cell, r, center=(c != 4))
        if c == 4:
            cell.alignment = Alignment(horizontal="left", vertical="center", wrap_text=True)
    # Status conditional colour placeholder
    status_cell = ws1.cell(row=r, column=7)
    status_cell.fill = PatternFill(start_color=LIGHT_GREEN, end_color=LIGHT_GREEN, fill_type="solid")
    status_cell.font = Font(name="Aptos", bold=True, size=10, color="065F46")
    r += 1

ws1.freeze_panes = "A4"
auto_width(ws1)
ws1.column_dimensions["D"].width = 38
ws1.column_dimensions["F"].width = 28
ws1.column_dimensions["H"].width = 28

# ───────────────────────────────────────────────────────────────────────
# SHEET 2 — RESOURCE ALLOCATION MATRIX (per sprint)
# ───────────────────────────────────────────────────────────────────────
ws2 = wb.create_sheet("Resource Allocation")
ws2.sheet_properties.tabColor = BLUE

all_members_set = set()
for info in TEAMS.values():
    all_members_set.update(info["members"])
all_members = sorted(all_members_set, key=str.lower)

COLS2 = ["#", "Member"] + [t for t in TEAMS.keys()] + ["Total Teams", "Availability %", "Notes"]
max_c2 = len(COLS2)
add_title_block(ws2, "Resource Allocation Matrix — 2026", "Shows which member belongs to which team  •  Shared resources highlighted", max_c2)
style_header_row(ws2, 3, max_c2, BLUE)
for i, h in enumerate(COLS2, 1):
    ws2.cell(row=3, column=i, value=h)

r = 4
for idx, member in enumerate(all_members, 1):
    ws2.cell(row=r, column=1, value=idx)
    ws2.cell(row=r, column=2, value=member)
    team_count = 0
    for ti, team in enumerate(TEAMS.keys()):
        col = ti + 3
        if member in TEAMS[team]["members"]:
            ws2.cell(row=r, column=col, value="✓")
            ws2.cell(row=r, column=col).font = Font(name="Aptos", bold=True, size=11, color=TEAM_COLORS[team])
            ws2.cell(row=r, column=col).fill = PatternFill(start_color=TEAM_LIGHT[team], end_color=TEAM_LIGHT[team], fill_type="solid")
            team_count += 1
        else:
            ws2.cell(row=r, column=col, value="")
    ws2.cell(row=r, column=max_c2 - 2, value=team_count)
    avail = 100 if team_count <= 1 else round(100 / team_count)
    ws2.cell(row=r, column=max_c2 - 1, value=f"{avail}%")
    ws2.cell(row=r, column=max_c2, value="Shared" if team_count > 1 else "")

    for c in range(1, max_c2 + 1):
        cell = ws2.cell(row=r, column=c)
        if cell.fill == PatternFill():
            style_data_cell(cell, r, center=True)
        else:
            cell.border = thin_border
            cell.alignment = Alignment(horizontal="center", vertical="center")
    # Highlight shared resources
    if team_count > 1:
        ws2.cell(row=r, column=2).font = Font(name="Aptos", bold=True, size=10, color=RED)
        ws2.cell(row=r, column=max_c2).font = Font(name="Aptos", bold=True, size=10, color=RED)
    r += 1

ws2.freeze_panes = "C4"
auto_width(ws2)

# ───────────────────────────────────────────────────────────────────────
# SHEET 3 — PROJECT ROADMAP & TARGETS
# ───────────────────────────────────────────────────────────────────────
ws3 = wb.create_sheet("Project Roadmap")
ws3.sheet_properties.tabColor = "8B5CF6"

COLS3 = ["Team / Product", "Project Name", "Start Date", "Target End", "Current Phase",
         "Achieved So Far", "Next Target", "Delivery Aligned?", "Risk / Blocker", "Owner"]
max_c3 = len(COLS3)
add_title_block(ws3, "Project Roadmap & Delivery Targets — 2026", "Track start/end, achievements, next targets, and delivery alignment", max_c3)
style_header_row(ws3, 3, max_c3, "8B5CF6")
for i, h in enumerate(COLS3, 1):
    ws3.cell(row=3, column=i, value=h)

r = 4
sample_projects = [
    ("OneWork", "Core Platform v2", "05 Jan 2026", "30 Jun 2026"),
    ("Learn",   "LMS Integration",  "05 Jan 2026", "30 Apr 2026"),
    ("Safe",    "Compliance Engine", "19 Jan 2026", "30 Sep 2026"),
    ("OneHire", "Recruitment Portal","02 Feb 2026", "31 Jul 2026"),
    ("Spot",    "City Module",       "02 Mar 2026", "30 Nov 2026"),
    ("Pulse",   "Survey Analytics",  "16 Feb 2026", "31 Aug 2026"),
]
for team, proj, sd, ed in sample_projects:
    tc = TEAM_COLORS[team]
    ws3.cell(row=r, column=1, value=team)
    ws3.cell(row=r, column=1).font = Font(name="Aptos", bold=True, color=WHITE, size=10)
    ws3.cell(row=r, column=1).fill = PatternFill(start_color=tc, end_color=tc, fill_type="solid")
    ws3.cell(row=r, column=2, value=proj)
    ws3.cell(row=r, column=3, value=sd)
    ws3.cell(row=r, column=4, value=ed)
    ws3.cell(row=r, column=5, value="In Progress")
    ws3.cell(row=r, column=6, value="")
    ws3.cell(row=r, column=7, value="")
    ws3.cell(row=r, column=8, value="Yes")
    ws3.cell(row=r, column=9, value="")
    ws3.cell(row=r, column=10, value=TEAMS[team]["lead"])

    for c in range(1, max_c3 + 1):
        cell = ws3.cell(row=r, column=c)
        if c != 1:
            style_data_cell(cell, r, center=(c not in [2, 6, 7, 9]))
        else:
            cell.border = thin_border
            cell.alignment = Alignment(horizontal="center", vertical="center")
    # blank rows for adding more projects per team
    r += 1
    for c in range(1, max_c3 + 1):
        cell = ws3.cell(row=r, column=c)
        style_data_cell(cell, r)
    r += 1

ws3.freeze_panes = "A4"
auto_width(ws3)
ws3.column_dimensions["F"].width = 32
ws3.column_dimensions["G"].width = 32
ws3.column_dimensions["I"].width = 28

# ───────────────────────────────────────────────────────────────────────
# SHEET 4 — SPRINT TRACKER (per team per sprint)
# ───────────────────────────────────────────────────────────────────────
ws4 = wb.create_sheet("Sprint Tracker")
ws4.sheet_properties.tabColor = GREEN

COLS4 = ["Team", "Sprint", "Dates", "Sprint Goal", "Achieved", "Tech Lead Feedback",
         "Achievement / Win", "Failure / Miss", "Blocker", "Status"]
max_c4 = len(COLS4)
add_title_block(ws4, "Sprint-by-Sprint Tracker — 2026", "Record goals, outcomes, tech lead feedback, and blockers every sprint", max_c4)
style_header_row(ws4, 3, max_c4, GREEN)
for i, h in enumerate(COLS4, 1):
    ws4.cell(row=3, column=i, value=h)

r = 4
for team in TEAMS:
    tc = TEAM_COLORS[team]
    for sp in SPRINTS_2026[:26]:
        ws4.cell(row=r, column=1, value=team)
        ws4.cell(row=r, column=1).font = Font(name="Aptos", bold=True, color=WHITE, size=10)
        ws4.cell(row=r, column=1).fill = PatternFill(start_color=tc, end_color=tc, fill_type="solid")
        ws4.cell(row=r, column=2, value=sp["name"])
        ws4.cell(row=r, column=3, value=f"{sp['start']} – {sp['end']}")
        for c in range(4, max_c4 + 1):
            ws4.cell(row=r, column=c, value="")

        for c in range(1, max_c4 + 1):
            cell = ws4.cell(row=r, column=c)
            if c != 1:
                style_data_cell(cell, r, center=(c in [2, 3, 10]))
            else:
                cell.border = thin_border
                cell.alignment = Alignment(horizontal="center", vertical="center")
        r += 1

ws4.freeze_panes = "D4"
auto_width(ws4)
ws4.column_dimensions["D"].width = 30
ws4.column_dimensions["E"].width = 30
ws4.column_dimensions["F"].width = 30
ws4.column_dimensions["G"].width = 26
ws4.column_dimensions["H"].width = 26
ws4.column_dimensions["I"].width = 26

# ───────────────────────────────────────────────────────────────────────
# SHEET 5 — INDIVIDUAL MEMBER PERFORMANCE
# ───────────────────────────────────────────────────────────────────────
ws5 = wb.create_sheet("Member Performance")
ws5.sheet_properties.tabColor = AMBER

COLS5 = ["#", "Member", "Team(s)", "Role", "Current Sprint Task",
         "Progress %", "Tech Lead Rating", "Feedback / Notes", "Achievements (YTD)", "Areas to Improve"]
max_c5 = len(COLS5)
add_title_block(ws5, "Individual Member Performance — 2026", "Track each member's progress, ratings, and feedback across sprints", max_c5)
style_header_row(ws5, 3, max_c5, AMBER)
for i, h in enumerate(COLS5, 1):
    ws5.cell(row=3, column=i, value=h)

r = 4
for idx, member in enumerate(all_members, 1):
    teams_for = [t for t, info in TEAMS.items() if member in info["members"]]
    is_lead = any(TEAMS[t]["lead"] == member for t in teams_for)
    ws5.cell(row=r, column=1, value=idx)
    ws5.cell(row=r, column=2, value=member)
    ws5.cell(row=r, column=3, value=", ".join(teams_for))
    ws5.cell(row=r, column=4, value="Tech Lead" if is_lead else "Developer")
    for c in range(5, max_c5 + 1):
        ws5.cell(row=r, column=c, value="")

    for c in range(1, max_c5 + 1):
        cell = ws5.cell(row=r, column=c)
        style_data_cell(cell, r, center=(c in [1, 4, 6, 7]))
        if c == 2:
            cell.font = Font(name="Aptos", bold=True, size=10, color=DARK_GRAY)
    r += 1

ws5.freeze_panes = "C4"
auto_width(ws5)
ws5.column_dimensions["E"].width = 30
ws5.column_dimensions["H"].width = 32
ws5.column_dimensions["I"].width = 28
ws5.column_dimensions["J"].width = 28

# ───────────────────────────────────────────────────────────────────────
# SHEET 6 — YEARLY TARGETS & MILESTONES
# ───────────────────────────────────────────────────────────────────────
ws6 = wb.create_sheet("2026 Targets")
ws6.sheet_properties.tabColor = RED

COLS6 = ["Team", "Q1 Target (Jan–Mar)", "Q1 Status", "Q2 Target (Apr–Jun)", "Q2 Status",
         "Q3 Target (Jul–Sep)", "Q3 Status", "Q4 Target (Oct–Dec)", "Q4 Status", "Year-End Goal"]
max_c6 = len(COLS6)
add_title_block(ws6, "2026 Yearly Targets & Quarterly Milestones", "High-level view of what each team must deliver per quarter", max_c6)
style_header_row(ws6, 3, max_c6, RED)
for i, h in enumerate(COLS6, 1):
    ws6.cell(row=3, column=i, value=h)

r = 4
for team in TEAMS:
    tc = TEAM_COLORS[team]
    ws6.cell(row=r, column=1, value=team)
    ws6.cell(row=r, column=1).font = Font(name="Aptos", bold=True, color=WHITE, size=11)
    ws6.cell(row=r, column=1).fill = PatternFill(start_color=tc, end_color=tc, fill_type="solid")
    for c in range(2, max_c6 + 1):
        ws6.cell(row=r, column=c, value="")
    # Status columns get green fill placeholder
    for sc in [3, 5, 7, 9]:
        cell = ws6.cell(row=r, column=sc)
        cell.value = "Not Started"
        cell.fill = PatternFill(start_color=LIGHT_GRAY, end_color=LIGHT_GRAY, fill_type="solid")
        cell.font = Font(name="Aptos", size=10, italic=True, color=MED_GRAY)

    for c in range(1, max_c6 + 1):
        cell = ws6.cell(row=r, column=c)
        if c != 1:
            cell.border = thin_border
            cell.alignment = Alignment(horizontal="center" if c in [3,5,7,9] else "left", vertical="center", wrap_text=True)
            if cell.font == Font():
                cell.font = Font(name="Aptos", size=10, color=DARK_GRAY)
        else:
            cell.border = thin_border
            cell.alignment = Alignment(horizontal="center", vertical="center")
    ws6.row_dimensions[r].height = 48
    r += 1

ws6.freeze_panes = "B4"
auto_width(ws6)
for col_letter in ["B", "D", "F", "H", "J"]:
    ws6.column_dimensions[col_letter].width = 30
for col_letter in ["C", "E", "G", "I"]:
    ws6.column_dimensions[col_letter].width = 14

# ───────────────────────────────────────────────────────────────────────
# SHEET 7 — LEGEND & INSTRUCTIONS
# ───────────────────────────────────────────────────────────────────────
ws7 = wb.create_sheet("How To Use")
ws7.sheet_properties.tabColor = MED_GRAY

max_c7 = 4
add_title_block(ws7, "How To Use This Tracker", "Quick guide for all stakeholders", max_c7)

instructions = [
    ("Sheet", "Purpose", "Update Frequency", "Who Updates"),
    ("Team Overview", "See all teams, current sprint, status at a glance", "Every sprint", "Engineering Manager"),
    ("Resource Allocation", "View who belongs to which team, spot shared resources", "When team changes", "Engineering Manager"),
    ("Project Roadmap", "Track project start/end, achievements, next targets, alignment", "Every sprint", "Tech Leads"),
    ("Sprint Tracker", "Detailed sprint goals, outcomes, tech lead feedback, blockers", "Every sprint", "Tech Leads"),
    ("Member Performance", "Individual progress, ratings, achievements", "Every sprint", "Tech Leads"),
    ("2026 Targets", "Quarterly milestones and year-end goals per team", "Quarterly", "Engineering Manager"),
]

style_header_row(ws7, 3, max_c7, NAVY)
for i, h in enumerate(instructions[0], 1):
    ws7.cell(row=3, column=i, value=h)

for ri, row_data in enumerate(instructions[1:], 4):
    for ci, val in enumerate(row_data, 1):
        cell = ws7.cell(row=ri, column=ci, value=val)
        style_data_cell(cell, ri, center=(ci != 2))

# Status legend
r_leg = len(instructions) + 4
ws7.cell(row=r_leg, column=1, value="STATUS LEGEND")
ws7.cell(row=r_leg, column=1).font = Font(name="Aptos", bold=True, size=12, color=NAVY)
legend = [
    ("On Track", GREEN, LIGHT_GREEN),
    ("At Risk", AMBER, LIGHT_AMBER),
    ("Blocked", RED, LIGHT_RED),
    ("Not Started", MED_GRAY, LIGHT_GRAY),
    ("Completed", NAVY, LIGHT_BLUE),
]
for li, (label, fg, bg) in enumerate(legend, r_leg + 1):
    cell = ws7.cell(row=li, column=1, value=f"  ●  {label}")
    cell.font = Font(name="Aptos", bold=True, size=10, color=fg)
    cell.fill = PatternFill(start_color=bg, end_color=bg, fill_type="solid")
    cell.border = thin_border
    ws7.merge_cells(start_row=li, start_column=1, end_row=li, end_column=2)

auto_width(ws7)
ws7.column_dimensions["B"].width = 52

# ─── PRINT SETTINGS & FINAL TOUCHES ──────────────────────────────────
for ws in wb.worksheets:
    ws.sheet_view.showGridLines = False
    ws.page_setup.orientation = "landscape"
    ws.page_setup.fitToWidth = 1

# ─── SAVE ─────────────────────────────────────────────────────────────
output_path = "/workspace/AG_ONE_Team_Tracker_2026.xlsx"
wb.save(output_path)
print(f"✅ Saved: {output_path}")
