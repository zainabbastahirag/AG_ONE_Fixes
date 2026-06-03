# AG ONE Design System Reference

The canonical **full CSS framework** (tokens, typography, header, sidebar, buttons, marketplace cards) is defined in the AG ONE product specification.

This repository ships a **focused subset** in:

`Src/AgoneSentimentSales.DesignSystem/wwwroot/css/agone.css`

For parity with other AG ONE apps (AI Hub, Marketplace), copy the complete `agone.css` from `AgoneDesignSystem.Styles` into that path and rebuild the DesignSystem project.

### Usage in Blazor WASM

```html
<link href="_content/AgoneSentimentSales.DesignSystem/css/agone.css" rel="stylesheet" />
```

### Key classes used by Sentiment Sales
- Layout: `agone-app-shell`, `agone-sidebar-container`, `agone-header`, `agone-page-container`
- Actions: `agone-btn-primary-medium`, `agone-btn-secondary-medium`
- Data: `agone-data-table`, `agone-mp-card-metrics-type1`
- Status: `agone-status-confirmed`, `agone-status-partial`, `agone-ai-badge`
