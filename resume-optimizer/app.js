const OLLAMA_BASE = "http://localhost:11434";

const SAMPLE_RESUME = `Jane Developer
Software Engineer | jane@email.com | github.com/janedev

EXPERIENCE
Software Engineer — Acme Corp (2021–Present)
- Built web applications using React and Node.js
- Worked on backend APIs and database queries
- Helped improve team processes

Junior Developer — StartupXYZ (2019–2021)
- Fixed bugs and added small features
- Wrote unit tests

EDUCATION
B.S. Computer Science — State University (2019)

SKILLS
JavaScript, Python, SQL, Git, React, Node.js`;

const SYSTEM_PROMPT = `You are an expert resume coach and ATS (Applicant Tracking System) specialist.
Analyze resumes and return ONLY valid JSON with no markdown fences or extra text.

Return this exact JSON structure:
{
  "ats_score": <number 0-100>,
  "ats_summary": "<2-3 sentence summary of ATS compatibility>",
  "score_breakdown": [
    {"label": "<category>", "score": <0-100>, "note": "<brief note>"}
  ],
  "missing_keywords": [
    {"keyword": "<term>", "priority": "high" | "medium" | "low", "reason": "<why it matters>"}
  ],
  "better_bullets": [
    {
      "original": "<original bullet or paraphrased weak bullet>",
      "improved": "<stronger bullet with metrics and action verbs>",
      "reason": "<why this is better>"
    }
  ],
  "interview_questions": [
    {"category": "<behavioral|technical|role-specific>", "question": "<question>"}
  ]
}

Guidelines:
- ATS score should reflect formatting, keyword density, quantified achievements, and clarity.
- Provide 5-10 missing keywords relevant to the role (or general industry if no job description).
- Suggest 3-5 improved bullet points from the weakest bullets in the resume.
- Provide 6-8 likely interview questions based on resume content.
- Use realistic, industry-standard terminology.`;

const elements = {
  resumeInput: document.getElementById("resumeInput"),
  jobDescription: document.getElementById("jobDescription"),
  modelSelect: document.getElementById("modelSelect"),
  analyzeBtn: document.getElementById("analyzeBtn"),
  loadSample: document.getElementById("loadSample"),
  ollamaStatus: document.getElementById("ollamaStatus"),
  errorMessage: document.getElementById("errorMessage"),
  resultsPanel: document.getElementById("resultsPanel"),
  emptyState: document.getElementById("emptyState"),
  atsScore: document.getElementById("atsScore"),
  atsSummary: document.getElementById("atsSummary"),
  scoreRingFill: document.getElementById("scoreRingFill"),
  scoreBreakdown: document.getElementById("scoreBreakdown"),
  missingKeywords: document.getElementById("missingKeywords"),
  betterBullets: document.getElementById("betterBullets"),
  interviewQuestions: document.getElementById("interviewQuestions"),
};

const CIRCUMFERENCE = 2 * Math.PI * 52;

function setLoading(isLoading) {
  elements.analyzeBtn.disabled = isLoading;
  elements.analyzeBtn.querySelector(".btn__label").hidden = isLoading;
  elements.analyzeBtn.querySelector(".btn__spinner").hidden = !isLoading;
}

function showError(message) {
  elements.errorMessage.textContent = message;
  elements.errorMessage.hidden = !message;
}

function scoreColor(score) {
  if (score >= 80) return "var(--success)";
  if (score >= 60) return "var(--warning)";
  return "var(--danger)";
}

function updateScoreRing(score) {
  const offset = CIRCUMFERENCE - (score / 100) * CIRCUMFERENCE;
  elements.scoreRingFill.style.strokeDashoffset = offset;
  elements.scoreRingFill.style.stroke = scoreColor(score);
}

function renderResults(data) {
  elements.emptyState.hidden = true;
  elements.resultsPanel.hidden = false;

  const score = Math.min(100, Math.max(0, Number(data.ats_score) || 0));
  elements.atsScore.textContent = Math.round(score);
  elements.atsSummary.textContent = data.ats_summary || "Analysis complete.";
  updateScoreRing(score);

  elements.scoreBreakdown.innerHTML = (data.score_breakdown || [])
    .map(
      (item) =>
        `<li><strong>${escapeHtml(item.label)}</strong> — ${item.score}/100: ${escapeHtml(item.note || "")}</li>`
    )
    .join("");

  const keywords = data.missing_keywords || [];
  elements.missingKeywords.innerHTML = keywords.length
    ? keywords
        .map(
          (kw) =>
            `<span class="keyword-tag${kw.priority === "high" ? " keyword-tag--priority" : ""}" title="${escapeHtml(kw.reason || "")}">${escapeHtml(kw.keyword)}</span>`
        )
        .join("")
    : '<p class="placeholder">No missing keywords identified.</p>';

  const bullets = data.better_bullets || [];
  elements.betterBullets.innerHTML = bullets.length
    ? bullets
        .map(
          (b) => `
        <div class="bullet-item">
          <p class="bullet-item__original">Before: <span>${escapeHtml(b.original)}</span></p>
          <p class="bullet-item__improved">${escapeHtml(b.improved)}</p>
          ${b.reason ? `<p class="bullet-item__reason">${escapeHtml(b.reason)}</p>` : ""}
        </div>`
        )
        .join("")
    : '<p class="placeholder">No bullet improvements suggested.</p>';

  const questions = data.interview_questions || [];
  elements.interviewQuestions.innerHTML = questions.length
    ? questions
        .map(
          (q) =>
            `<li><span class="question-category">${escapeHtml(q.category || "general")}</span>${escapeHtml(q.question)}</li>`
        )
        .join("")
    : '<li class="placeholder">No interview questions generated.</li>';
}

function escapeHtml(text) {
  const div = document.createElement("div");
  div.textContent = text ?? "";
  return div.innerHTML;
}

function parseOllamaJson(raw) {
  const trimmed = raw.trim();
  const jsonMatch = trimmed.match(/\{[\s\S]*\}/);
  if (!jsonMatch) {
    throw new Error("Model did not return valid JSON. Try again or use a different model.");
  }
  return JSON.parse(jsonMatch[0]);
}

async function checkOllama() {
  const statusEl = elements.ollamaStatus;
  try {
    const res = await fetch(`${OLLAMA_BASE}/api/tags`, { signal: AbortSignal.timeout(4000) });
    if (!res.ok) throw new Error("Ollama unreachable");

    const data = await res.json();
    const models = (data.models || []).map((m) => m.name.split(":")[0]);
    const uniqueModels = [...new Set(models)];

    elements.modelSelect.innerHTML = uniqueModels.length
      ? uniqueModels.map((m) => `<option value="${escapeHtml(m)}">${escapeHtml(m)}</option>`).join("")
      : '<option value="llama3.2">llama3.2 (pull with: ollama pull llama3.2)</option>';

    statusEl.className = "header__status is-online";
    statusEl.querySelector("span:last-child").textContent =
      uniqueModels.length ? `Ollama online · ${uniqueModels.length} model(s)` : "Ollama online · no models pulled yet";
    return true;
  } catch {
    statusEl.className = "header__status is-offline";
    statusEl.querySelector("span:last-child").textContent = "Ollama offline";
    return false;
  }
}

async function analyzeResume() {
  const resume = elements.resumeInput.value.trim();
  if (!resume) {
    showError("Please paste your resume before analyzing.");
    return;
  }

  const online = await checkOllama();
  if (!online) {
    showError("Cannot reach Ollama. Start it with: ollama serve");
    return;
  }

  showError("");
  setLoading(true);

  const jobDesc = elements.jobDescription.value.trim();
  const userPrompt = jobDesc
    ? `Analyze this resume against the target job description.\n\n--- RESUME ---\n${resume}\n\n--- JOB DESCRIPTION ---\n${jobDesc}`
    : `Analyze this resume for general ATS optimization.\n\n--- RESUME ---\n${resume}`;

  try {
    const res = await fetch(`${OLLAMA_BASE}/api/chat`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        model: elements.modelSelect.value,
        stream: false,
        format: "json",
        messages: [
          { role: "system", content: SYSTEM_PROMPT },
          { role: "user", content: userPrompt },
        ],
        options: {
          temperature: 0.4,
          num_predict: 2048,
        },
      }),
    });

    if (!res.ok) {
      const err = await res.text();
      throw new Error(err || `Ollama request failed (${res.status})`);
    }

    const payload = await res.json();
    const content = payload.message?.content;
    if (!content) throw new Error("Empty response from Ollama.");

    const data = parseOllamaJson(content);
    renderResults(data);
  } catch (err) {
    showError(err.message || "Analysis failed. Check that your model is pulled and try again.");
  } finally {
    setLoading(false);
  }
}

elements.analyzeBtn.addEventListener("click", analyzeResume);
elements.loadSample.addEventListener("click", () => {
  elements.resumeInput.value = SAMPLE_RESUME;
});

checkOllama();
setInterval(checkOllama, 30000);
