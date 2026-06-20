# AG ONE Experion — Architecture Documentation

Architecture diagrams and documentation for **AG ONE Experion** within the AG ONE product series.

## Files

| File | Description |
|------|-------------|
| `ag-one-experion-architecture.drawio` | Draw.io diagram — open with [diagrams.net](https://app.diagrams.net) or Draw.io desktop |
| `AG_ONE_Experion_Architecture.docx` | Full Word architecture document |
| `generate_architecture_doc.py` | Regenerates the Word doc if you edit content |

## Quick summary

- **Experion JS SDK** — single blob-hosted `<script>` used by all AG ONE products (.NET apps included)
- **AG ONE Gateway** — central orchestrator; every request enters here first
- **Experion API** — shared intelligence (embed, cache, intent classify, recommendations)
- **Product teams** — own Action Handlers and Generation/KB services; integrate with AG ONE only

## Open the diagram

1. Go to https://app.diagrams.net
2. **File → Open from → Device**
3. Select `ag-one-experion-architecture.drawio`

Or use the Draw.io VS Code extension / desktop app.

## Regenerate Word doc

```bash
pip install python-docx
python docs/ag-one-experion/generate_architecture_doc.py
```

## Architecture layers

```
Presentation   → Experion JS SDK (Blob)
Platform       → AG ONE Gateway + Orchestrator
Intelligence   → Experion API (.NET 8)
Execution      → Per-product Action + Generation backends
Data           → Blob, SQL, AI Search, OpenAI, Service Bus, SignalR
```
