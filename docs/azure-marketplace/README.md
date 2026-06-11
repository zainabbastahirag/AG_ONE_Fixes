# AG ONE Azure Marketplace Architecture Documents

## Files

| File | Description |
|------|-------------|
| `AG-ONE-Marketplace-Architecture.drawio` | 9-page draw.io diagram set (open in [diagrams.net](https://app.diagrams.net) or draw.io desktop) |
| `AG-ONE-Marketplace-Architecture.docx` | Full Word architecture document with tables, flows, SQL DDL, and diagram index |
| `generate_docs.py` | Regenerates both files from source |

## Draw.io pages

1. **1-High-Level-Architecture** — Gateway, products, Marketplace, Azure services
2. **2-End-to-End-Sequence** — Subscribe → resolve → activate → provision → login → access
3. **3-SaaS-Onboarding-Flow** — Landing page, token exchange, tenant provisioning
4. **4-Database-ER-Diagram** — core.* tables and relationships
5. **5-Integration-Design** — Fulfillment API, webhooks, services
6. **6-Authentication-Flow** — JWT, SSO middleware, tenant context
7. **7-System-Flow-Steps** — 16-step numbered flow
8. **8-Deployment-Architecture** — Azure App Services, SQL, Service Bus, optional AKS
9. **9-Subscription-Lifecycle** — Subscribe, suspend, reinstate, unsubscribe states

## How to open

### draw.io
1. Go to https://app.diagrams.net (or open draw.io desktop)
2. **File → Open from → Device**
3. Select `AG-ONE-Marketplace-Architecture.drawio`
4. Use the page tabs at the bottom to switch between diagrams

### Word
Open `AG-ONE-Marketplace-Architecture.docx` in Microsoft Word, Google Docs, or LibreOffice.

## Regenerate

```bash
pip install python-docx
python3 generate_docs.py
```
