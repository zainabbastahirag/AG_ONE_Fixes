# Tech Team Monthly Sync — 2027 Workbook

A full-year Excel template for running monthly sync sessions with your tech team.

## Files

| File | Use |
|------|-----|
| `tech_team_monthly_sync_2027.xlsx` | Main workbook — share with team and fill monthly |
| `generate_monthly_sync_excel.py` | Regenerate if you change team size or year |

---

## Workbook sheets

| Sheet | Purpose |
|-------|---------|
| **Start Here** | Instructions for managers and team members |
| **Team Roster** | Master list of names, roles, squads — edit first |
| **Year Dashboard** | 12-month calendar: sync dates, themes, status |
| **Jan–Dec 2027** | One tab per month with agenda + per-member updates |
| **Action Items Tracker** | Cross-month follow-ups with owners and due dates |
| **Feedback Log** | Team feedback, concerns, and manager responses |
| **Gap & Growth Tracker** | Skills, process, and resource gaps |
| **Agenda Library** | Reusable agenda items to copy into monthly tabs |

---

## Each monthly tab includes

Per team member:

- **Last Month Highlights / Updates**
- **New Items / Learnings / Deliverables**
- **What's Next (30 days)**
- **Gaps / Blockers / Where We Need Help**
- **Feedback Needed** (from manager or team)
- **Support Requested**
- **Confidence (1–5)** and **Status**

Plus: meeting agenda, month summary, decisions, and next-month preview.

---

## Recommended workflow

### Before the sync (manager)

1. Update **Team Roster** with current members.
2. Set sync date and theme on **Year Dashboard**.
3. Share the month's tab (e.g. **Mar 2027**) with the team.
4. Ask everyone to fill their row **2 business days before** the meeting.

### During the sync (60 min)

1. Review last month's **Action Items Tracker**.
2. Walk through each member's updates (2–3 min each).
3. Deep dive on top gaps from **Gap & Growth Tracker**.
4. Collect feedback — log in **Feedback Log**.
5. Confirm new action items and draft next month's agenda.

### After the sync

1. Mark agenda items and member statuses as Done.
2. Add action items to **Action Items Tracker**.
3. Update **Year Dashboard** (sync done, key wins, top gaps).
4. Fill **Month Summary & Decisions** at the bottom of the month tab.

---

## Customization

Edit `DEFAULT_TEAM` and `YEAR` in `generate_monthly_sync_excel.py`, then run:

```bash
python3 generate_monthly_sync_excel.py
```
