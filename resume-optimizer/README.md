# AI Resume Optimizer

A local-first resume analysis tool powered by [Ollama](https://ollama.com). Paste your resume and get:

- **ATS score** — how well your resume passes applicant tracking systems
- **Missing keywords** — terms to add for better matching
- **Better bullet points** — stronger, quantified rewrites
- **Interview questions** — likely questions based on your experience

All processing runs on your machine. No API keys, no cloud uploads.

## Prerequisites

1. Install [Ollama](https://ollama.com/download)
2. Pull a model (recommended):

```bash
ollama pull llama3.2
```

3. Make sure Ollama is running:

```bash
ollama serve
```

## Run the app

Because the browser calls Ollama at `http://localhost:11434`, serve the files with a local HTTP server (opening `index.html` directly may hit CORS issues in some browsers).

**Option A — Python:**

```bash
cd resume-optimizer
python3 -m http.server 8080
```

Then open http://localhost:8080

**Option B — Node:**

```bash
cd resume-optimizer
npx serve .
```

**Option C — VS Code / Cursor:** use the "Live Server" extension on `index.html`.

## Usage

1. Paste your resume in the text area
2. Optionally paste a target job description for tailored keyword analysis
3. Select your Ollama model from the dropdown
4. Click **Analyze Resume**

Results appear on the right: ATS score ring, keyword tags, improved bullets, and interview questions.

## Project structure

```
resume-optimizer/
├── index.html   # UI layout
├── styles.css   # Styling
├── app.js       # Ollama API integration
└── README.md
```

## Troubleshooting

| Issue | Fix |
|-------|-----|
| "Ollama offline" | Run `ollama serve` in a terminal |
| No models in dropdown | Run `ollama pull llama3.2` (or any model you prefer) |
| Invalid JSON error | Try a larger model (`llama3.1`, `mistral`) or click Analyze again |
| CORS errors | Serve via HTTP (see above), don't open as `file://` |

## Customization

- **Ollama URL:** edit `OLLAMA_BASE` in `app.js` if Ollama runs on a different host/port
- **Default model:** change the fallback in `index.html` or pull your preferred model first
