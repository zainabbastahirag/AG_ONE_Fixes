# Product Requirements Document — AG ONE Sentiment Sales

## 1. Objective
Develop an agentic AI solution for market research and lead generation to support marketplace programmes and digital sales efforts.

Profile the **top 100 London Stock Exchange (LSE)** listed companies (by market capitalisation), grouped by industry, with focus on IT offshoring to India/Asia, IT budget allocation, technology strategy, and executive contacts. Export to **Microsoft Excel** with professional dashboard formatting.

## 2. Research Scope

### 2.1 Company Identification
- Top 100 LSE firms by market cap, grouped by industry
- Offshoring status (India/Asia/other) with countries where available

### 2.2 IT Budget Breakdown
- Total IT budget (latest fiscal year)
- Capex vs Opex
- Categories: offshore/onshore resources, cloud (AWS/Azure/GCP), licensing, app support, data & AI, EUC, cyber security, managed services, other

### 2.3 Technology Strategy
- Data & AI adoption programmes
- Digital transformation vs traditional IT
- Automation, analytics, AI initiatives

### 2.4 Executive Contacts
- CIO/CTO, CDO, Head of IT Infrastructure, VP Applications/Cloud, other decision-makers
- LinkedIn URLs or verified email patterns

### 2.5 Additional Lead Generation
- Asia subsidiaries, outsourcing partners, IT transformation announcements, hiring trends

## 3. Output Requirements
Multi-sheet Excel workbook matching KLSE reference format (adapted for LSE/GBP):
- Dashboard Summary
- Company Profiles
- IT Budget Breakdown
- Technology Strategy
- Executive Contacts
- Outsourcing Partners
- Lead Generation Data

## 4. Data Sources
Public domain: annual reports, company websites, LinkedIn, job boards, press releases. Phase 2+: licensed market data APIs.

## 5. Technical Alignment
- .NET 8 layered architecture (AG ONE AI Hub pattern)
- Blazor WebAssembly UI with AG ONE Design System
- Azure OpenAI for agentic enrichment (Phase 2)
