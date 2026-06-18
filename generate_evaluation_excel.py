#!/usr/bin/env python3
"""Generate candidate evaluation Excel workbook."""

from openpyxl import Workbook
from openpyxl.styles import Alignment, Border, Font, PatternFill, Side
from openpyxl.utils import get_column_letter

PHASE1_CANDIDATES = [
    "Mohammad Kashif Mohiuddin",
    "Shahid",
    "Hadeef",
]

PHASE2_CANDIDATES = [
    "Baizid Yaldram",
    "MUHAMMAD HADIF AMAR BIN RAZALI",
    "CITA WAFA ATIAH",
    "MUHAMMAD MAIRAJ",
    "Mohammad Shahid Akhtar",
    "Aurneela Ghosh Riddhi",
]

PHASE1_CRITERIA = [
    ("Presentation", "How well they present — structure, confidence, slides, delivery"),
    ("Technical Observation", "How well they spot and explain technical points"),
]

PHASE2_CRITERIA = [
    ("Clear Thinking", "How easy their ideas are to follow (clarity of thought)"),
    ("Deep vs Wide", "Goes deep on topics vs covers many areas (depth vs breadth)"),
    ("Build on Ideas", "Lists points and builds on them step by step (listing & building)"),
    ("Business Link", "Connects ideas to real business value (business linkage)"),
    ("Positive Manner", "Respectful, calm, professional conduct (conduct with benefit)"),
    ("Fresh Ideas", "Shows own thinking, not just textbook answers (original thinking)"),
    ("Speaking Style", "Tone, pace, and how clearly they communicate (communication style)"),
]

RATING_SCALE = "1 = Weak | 2 = Below avg | 3 = Average | 4 = Good | 5 = Excellent"

HEADER_FILL = PatternFill("solid", fgColor="1F4E79")
SUBHEADER_FILL = PatternFill("solid", fgColor="D9E2F3")
ALT_FILL = PatternFill("solid", fgColor="F2F2F2")
WHITE_FILL = PatternFill("solid", fgColor="FFFFFF")
THIN = Side(style="thin", color="B4B4B4")
BORDER = Border(left=THIN, right=THIN, top=THIN, bottom=THIN)


def style_cell(cell, *, bold=False, fill=None, align="left", size=11):
    cell.font = Font(name="Calibri", size=size, bold=bold, color="000000")
    cell.alignment = Alignment(horizontal=align, vertical="center", wrap_text=True)
    cell.border = BORDER
    if fill:
        cell.fill = fill


def set_col_widths(ws, widths):
    for idx, width in enumerate(widths, start=1):
        ws.column_dimensions[get_column_letter(idx)].width = width


def build_overview_sheet(wb):
    ws = wb.active
    ws.title = "Overview"
    ws.merge_cells("A1:F1")
    ws["A1"] = "Candidate Evaluation — Phase 1 & Phase 2"
    style_cell(ws["A1"], bold=True, fill=HEADER_FILL, align="center", size=16)
    ws["A1"].font = Font(name="Calibri", size=16, bold=True, color="FFFFFF")

    rows = [
        ("", ""),
        ("Session 1 — Phase 1", "Presentation + Technical Observation"),
        ("Candidates", ", ".join(PHASE1_CANDIDATES)),
        ("", ""),
        ("Session 2 — Phase 2", "Clarity & discussion criteria (simplified labels)"),
        ("Candidates", ", ".join(PHASE2_CANDIDATES)),
        ("", ""),
        ("Rating scale", RATING_SCALE),
        ("", ""),
        ("Sheets", "Phase 1 Scores | Phase 2 Scores | Phase 1 Notes | Phase 2 Notes | Summary"),
    ]
    for r, (a, b) in enumerate(rows, start=3):
        ws.cell(r, 1, a)
        ws.cell(r, 2, b)
        style_cell(ws.cell(r, 1), bold=bool(a))
        style_cell(ws.cell(r, 2))
    set_col_widths(ws, [22, 90])


def build_score_sheet(ws, title, candidates, criteria):
    ws.title = title
    headers = ["Candidate"] + [c[0] for c in criteria] + ["Total", "Average", "Overall Notes"]
    ws.append(headers)
    for col in range(1, len(headers) + 1):
        style_cell(ws.cell(1, col), bold=True, fill=HEADER_FILL, align="center")
        ws.cell(1, col).font = Font(name="Calibri", size=11, bold=True, color="FFFFFF")

    for i, name in enumerate(candidates, start=2):
        row_fill = ALT_FILL if i % 2 == 0 else WHITE_FILL
        ws.cell(i, 1, name)
        style_cell(ws.cell(i, 1), fill=row_fill)
        for c in range(2, len(criteria) + 2):
            style_cell(ws.cell(i, c), fill=row_fill, align="center")
        total_col = len(criteria) + 2
        avg_col = len(criteria) + 3
        notes_col = len(criteria) + 4
        first_score = get_column_letter(2)
        last_score = get_column_letter(len(criteria) + 1)
        ws.cell(i, total_col, f"=SUM({first_score}{i}:{last_score}{i})")
        ws.cell(i, avg_col, f"=IF(COUNT({first_score}{i}:{last_score}{i})>0,AVERAGE({first_score}{i}:{last_score}{i}),\"\")")
        style_cell(ws.cell(i, total_col), fill=row_fill, align="center", bold=True)
        style_cell(ws.cell(i, avg_col), fill=row_fill, align="center", bold=True)
        style_cell(ws.cell(i, notes_col), fill=row_fill)

    legend_row = len(candidates) + 3
    ws.cell(legend_row, 1, "Criteria guide")
    style_cell(ws.cell(legend_row, 1), bold=True, fill=SUBHEADER_FILL)
    for idx, (label, desc) in enumerate(criteria, start=2):
        ws.cell(legend_row, idx, f"{label}: {desc}")
        style_cell(ws.cell(legend_row, idx), fill=SUBHEADER_FILL)
    ws.cell(legend_row + 1, 1, RATING_SCALE)
    ws.merge_cells(start_row=legend_row + 1, start_column=1, end_row=legend_row + 1, end_column=len(headers))
    style_cell(ws.cell(legend_row + 1, 1), fill=SUBHEADER_FILL)

    widths = [34] + [14] * len(criteria) + [10, 10, 40]
    set_col_widths(ws, widths)
    ws.freeze_panes = "B2"


def build_notes_sheet(ws, title, candidates, criteria):
    ws.title = title
    ws.merge_cells("A1:D1")
    ws["A1"] = title
    style_cell(ws["A1"], bold=True, fill=HEADER_FILL, align="center", size=14)
    ws["A1"].font = Font(name="Calibri", size=14, bold=True, color="FFFFFF")

    row = 3
    for name in candidates:
        ws.merge_cells(start_row=row, start_column=1, end_row=row, end_column=4)
        ws.cell(row, 1, name)
        style_cell(ws.cell(row, 1), bold=True, fill=SUBHEADER_FILL)
        row += 1
        for label, desc in criteria:
            ws.cell(row, 1, label)
            ws.merge_cells(start_row=row, start_column=2, end_row=row, end_column=4)
            ws.cell(row, 2, f"({desc})")
            style_cell(ws.cell(row, 1), bold=True)
            style_cell(ws.cell(row, 2))
            row += 2
        row += 1
    set_col_widths(ws, [24, 30, 30, 30])


def build_summary_sheet(wb):
    ws = wb.create_sheet("Summary")
    ws.merge_cells("A1:G1")
    ws["A1"] = "All Candidates — Quick Comparison"
    style_cell(ws["A1"], bold=True, fill=HEADER_FILL, align="center", size=14)
    ws["A1"].font = Font(name="Calibri", size=14, bold=True, color="FFFFFF")

    headers = ["Phase", "Candidate", "Avg Score", "Strengths", "Areas to improve", "Recommend?", "Final notes"]
    ws.append(headers)
    for col in range(1, len(headers) + 1):
        style_cell(ws.cell(2, col), bold=True, fill=SUBHEADER_FILL, align="center")

    row = 3
    for name in PHASE1_CANDIDATES:
        ws.append(["Phase 1", name, "", "", "", "", ""])
        for col in range(1, len(headers) + 1):
            fill = ALT_FILL if row % 2 == 0 else WHITE_FILL
            style_cell(ws.cell(row, col), fill=fill)
        row += 1
    for name in PHASE2_CANDIDATES:
        ws.append(["Phase 2", name, "", "", "", "", ""])
        for col in range(1, len(headers) + 1):
            fill = ALT_FILL if row % 2 == 0 else WHITE_FILL
            style_cell(ws.cell(row, col), fill=fill)
        row += 1

    set_col_widths(ws, [10, 34, 12, 28, 28, 14, 30])
    ws.freeze_panes = "A3"


def main():
    wb = Workbook()
    build_overview_sheet(wb)

    ws1 = wb.create_sheet("Phase 1 Scores")
    build_score_sheet(ws1, "Phase 1 Scores", PHASE1_CANDIDATES, PHASE1_CRITERIA)

    ws2 = wb.create_sheet("Phase 2 Scores")
    build_score_sheet(ws2, "Phase 2 Scores", PHASE2_CANDIDATES, PHASE2_CRITERIA)

    ws3 = wb.create_sheet("Phase 1 Notes")
    build_notes_sheet(ws3, "Phase 1 — Detailed Notes", PHASE1_CANDIDATES, PHASE1_CRITERIA)

    ws4 = wb.create_sheet("Phase 2 Notes")
    build_notes_sheet(ws4, "Phase 2 — Detailed Notes", PHASE2_CANDIDATES, PHASE2_CRITERIA)

    build_summary_sheet(wb)
    wb.save("candidate_evaluation.xlsx")
    print("Created candidate_evaluation.xlsx")


if __name__ == "__main__":
    main()
