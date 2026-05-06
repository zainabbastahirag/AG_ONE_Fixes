// ═══════════════════════════════════════════════════════════════════════
//  Avatar-driven workspaces.
//
//  Pro avatars take over the whole center area. Each workspace is built
//  from three blocks:
//
//   1. INPUT panel  — role-specific form inputs (e.g. code editor, brand
//      brief, birthday). Persists in localStorage.
//
//   2. ACTIONS rail — one-tap buttons + a free-form prompt input. Each
//      button builds a structured prompt and routes it through the
//      existing /api/chat/* SSE endpoint, with the streamed reply
//      redirected (via window.babaSetReplyTarget) into …
//
//   3. REPORT panel — a rich, scrollable region where the AI's markdown
//      reply lands as a formatted report (headings, lists, code, tables,
//      images), instead of just a chat bubble. For Astrology this is a
//      multi-section dashboard with deterministic visual cards on top
//      of the streamed narrative.
// ═══════════════════════════════════════════════════════════════════════

import {
    SIGNS, westernSign, chineseSign, buildBirthChart, moodGauges, dateKey,
    fetchFamousBirthdayMatch, fetchFunFacts, buildAdvicePrompt, parseSections
} from './astrology.js';

const AVATAR_TO_WORKSPACE = {
    sage: 'chat', philosopher: 'chat', healer: 'chat', elder: 'chat', storyteller: 'chat',
    designer: 'design', developer: 'dev', pm: 'pm', marketing: 'marketing',
    sales: 'sales', hr: 'hr', astrologer: 'astrology',
};

let _ask = null;
let _setText = null;
let _setMarkdown = null;
let _root = null;

export function initWorkspaces({ root, ask, setBubbleText, setBubbleMarkdown }) {
    _root = root;
    _ask = ask;
    _setText = setBubbleText || (() => { });
    _setMarkdown = setBubbleMarkdown || (() => { });
    setWorkspace('chat');
}

export function workspaceForAvatar(avatarKey) {
    return AVATAR_TO_WORKSPACE[avatarKey] || 'chat';
}

export function setWorkspace(key) {
    if (!_root) return;
    _root.innerHTML = '';
    _root.dataset.ws = key;
    // Reset the streaming target whenever we leave a workspace; per-workspace
    // builders may set it again immediately if they have their own report panel.
    if (window.babaSetReplyTarget) window.babaSetReplyTarget(null);
    const builder = WORKSPACES[key] || WORKSPACES.chat;
    builder(_root);
    _root.classList.toggle('hidden', key === 'chat');
    updateRightPaneContext(key);
}

// ─── Right-panel context ────────────────────────────────────────────────
function updateRightPaneContext(key) {
    document.querySelectorAll('.context-pane').forEach(p => {
        p.classList.toggle('active', p.dataset.context === key);
    });
    document.querySelectorAll('.mindset-section').forEach(p => {
        p.style.display = key === 'chat' ? '' : 'none';
    });
}

// ─── Helpers ─────────────────────────────────────────────────────────────
function el(tag, props = {}, ...children) {
    const e = document.createElement(tag);
    for (const [k, v] of Object.entries(props || {})) {
        if (k === 'class') e.className = v;
        else if (k === 'style') e.style.cssText = v;
        else if (k.startsWith('on') && typeof v === 'function') e.addEventListener(k.slice(2), v);
        else if (k === 'dataset') Object.assign(e.dataset, v);
        else if (v != null) e.setAttribute(k, v);
    }
    for (const c of children.flat()) {
        if (c == null) continue;
        if (typeof c === 'string' || typeof c === 'number') e.appendChild(document.createTextNode(String(c)));
        else e.appendChild(c);
    }
    return e;
}

function send(prompt) { if (_ask && prompt) _ask(prompt); }

function reportPanel(extraClass = '') {
    const empty = el('p', { class: 'ws-empty' }, 'Pick an action above and the AI report will appear here.');
    const target = el('div', { class: 'ws-report-target' }, empty);
    const pane = el('section', { class: `ws-report ${extraClass}` },
        el('header', { class: 'ws-report-head' }, el('h3', {}, 'AI Report'), el('span', { class: 'ws-report-tag' }, 'live')),
        target
    );
    return { pane, target, reset: () => { target.innerHTML = ''; target.appendChild(empty); } };
}

// ─── Code language auto-detect ──────────────────────────────────────────
//   Heuristic — checks distinctive syntax patterns. Not a parser; good
//   enough that pasting code "just works" without the user picking a lang.
function detectLanguage(code) {
    if (!code) return { lang: 'text', confidence: 0 };
    const t = code;
    const score = {};
    const bump = (k, n = 1) => (score[k] = (score[k] || 0) + n);
    if (/<\/?(html|body|div|span|h[1-6]|p|a|img)\b/i.test(t)) bump('html', 3);
    if (/<!DOCTYPE\s+html/i.test(t)) bump('html', 5);
    if (/^\s*(import|from)\s+\S+\s+import\b|^\s*def\s+\w+\(.*\):/m.test(t)) bump('python', 5);
    if (/^\s*(class|def)\s+\w+|^\s*if\s+__name__\s*==\s*['"]__main__['"]/m.test(t)) bump('python', 3);
    if (/\binterface\s+\w+\s*\{|:\s*(string|number|boolean|any)\b/.test(t)) bump('typescript', 4);
    if (/\b(const|let|var)\b|\bfunction\b|=>|\bconsole\.log\b/.test(t)) bump('javascript', 2);
    if (/\busing\s+\w+;|\bnamespace\s+\w+|\bpublic\s+(class|static|void)/.test(t)) bump('csharp', 5);
    if (/\bpackage\s+main\b|\bfunc\s+\w+\(/.test(t)) bump('go', 5);
    if (/\bfn\s+\w+\(|let\s+mut\s+|impl\s+\w+/.test(t)) bump('rust', 5);
    if (/\b(SELECT|INSERT|UPDATE|DELETE)\b.*\b(FROM|INTO|SET|WHERE)\b/is.test(t)) bump('sql', 5);
    if (/^\s*[#@.][\w-]+\s*\{|\bcolor\s*:\s*#[0-9a-f]/im.test(t)) bump('css', 4);
    if (/^#!\s*\/(usr\/)?bin\/(ba)?sh\b|^\s*echo\s+/m.test(t) && /\b(sudo|apt|yum|cd|grep)\b/.test(t)) bump('bash', 5);
    if (/^\s*<\?php\b/.test(t)) bump('php', 6);
    if (/^\s*(public|private|protected)\s+\w+\s+\w+\s*\(/m.test(t) && /import\s+java\./.test(t)) bump('java', 6);
    let best = 'text', bestN = 0;
    for (const [k, v] of Object.entries(score)) if (v > bestN) { best = k; bestN = v; }
    return { lang: best, confidence: bestN };
}

function codeMetrics(code) {
    if (!code) return { lines: 0, chars: 0, tokens: 0 };
    const lines = code.split(/\r?\n/).length;
    const chars = code.length;
    const tokens = (code.match(/\w+|[^\s\w]/g) || []).length;
    return { lines, chars, tokens };
}

// ─── Workspaces ─────────────────────────────────────────────────────────
const WORKSPACES = {
    chat(root) { /* the avatar+bubble are the workspace */ },

    // ─── DEVELOPER ───────────────────────────────────────────────────────
    dev(root) {
        const stored = JSON.parse(localStorage.getItem('baba_dev_state') || '{}');
        const editor = el('textarea', {
            class: 'ws-code', placeholder: '// Paste your code here — the language will auto-detect.',
            spellcheck: 'false', autocapitalize: 'off', autocomplete: 'off'
        });
        editor.value = stored.code || '';

        const langTag = el('span', { class: 'ws-lang' }, 'auto');
        const linesTag = el('span', { class: 'ws-metric' }, '0 lines');
        const charsTag = el('span', { class: 'ws-metric' }, '0 chars');
        const overrideSel = el('select', { class: 'ws-input ws-lang-override', title: 'Override detection' },
            ...['auto', 'javascript', 'typescript', 'python', 'csharp', 'go', 'rust', 'java', 'sql', 'html', 'css', 'bash', 'php', 'text']
                .map(l => { const o = el('option', { value: l }, l); if ((stored.langOverride || 'auto') === l) o.selected = true; return o; })
        );

        let detected = 'text';
        function refreshTags() {
            const m = codeMetrics(editor.value);
            linesTag.textContent = `${m.lines} lines`;
            charsTag.textContent = `${m.chars} chars`;
            const override = overrideSel.value;
            if (override === 'auto') {
                const d = detectLanguage(editor.value);
                detected = d.lang;
                langTag.textContent = `auto · ${detected}`;
            } else {
                detected = override;
                langTag.textContent = override;
            }
        }
        refreshTags();
        const persist = () => {
            localStorage.setItem('baba_dev_state', JSON.stringify({ code: editor.value, langOverride: overrideSel.value }));
            refreshTags();
        };
        editor.addEventListener('input', persist);
        overrideSel.addEventListener('change', persist);

        const { pane: report, target: reportTarget, reset: resetReport } = reportPanel('dev');

        const action = (label, build) => el('button', {
            class: 'ws-btn', type: 'button',
            onclick: () => {
                const code = editor.value.trim();
                if (!code) { reportTarget.innerHTML = '<p class="ws-msg err">Paste some code in the editor first.</p>'; return; }
                window.babaSetReplyTarget(reportTarget);
                send(build({ lang: detected, code }));
            }
        }, label);

        const customPrompt = el('input', {
            class: 'ws-input', placeholder: 'Ask BABA-G about this code...',
            onkeydown: (e) => { if (e.key === 'Enter') askCustom.click(); }
        });
        const askCustom = el('button', {
            class: 'ws-btn primary', type: 'button',
            onclick: () => {
                const q = customPrompt.value.trim();
                if (!q) return;
                const code = editor.value.trim();
                window.babaSetReplyTarget(reportTarget);
                send(`I am working in ${detected}. Here is the code:\n\`\`\`${detected}\n${code}\n\`\`\`\n${q}`);
                customPrompt.value = '';
            }
        }, 'Ask');

        root.appendChild(el('div', { class: 'ws-shell dev' },
            el('div', { class: 'ws-card dev' },
                el('div', { class: 'ws-head' },
                    el('div', { class: 'ws-title' }, '\uD83D\uDCBB Code Canvas'),
                    el('div', { class: 'ws-tools' }, langTag, linesTag, charsTag, overrideSel)
                ),
                editor,
                el('div', { class: 'ws-actions' },
                    action('Explain',     ({ lang, code }) => `Explain this ${lang} code clearly. Walk through the important parts in 3–5 short sentences. End with one improvement suggestion.\n\n\`\`\`${lang}\n${code}\n\`\`\``),
                    action('Find bugs',   ({ lang, code }) => `Review this ${lang} code for bugs and edge cases. List the most likely issues, then show a corrected version in a code block.\n\n\`\`\`${lang}\n${code}\n\`\`\``),
                    action('Optimize',    ({ lang, code }) => `Suggest performance and readability improvements for this ${lang} code. Provide a refactored version.\n\n\`\`\`${lang}\n${code}\n\`\`\``),
                    action('Add tests',   ({ lang, code }) => `Write idiomatic unit tests for this ${lang} code using the standard testing framework for the language.\n\n\`\`\`${lang}\n${code}\n\`\`\``),
                    action('Document',    ({ lang, code }) => `Add concise doc comments / JSDoc / docstrings to this ${lang} code. Return the documented version in a code block.\n\n\`\`\`${lang}\n${code}\n\`\`\``),
                    action('Convert to…', ({ lang, code }) => `Convert this ${lang} code to TypeScript (or, if it is already TypeScript, to Python). Show the conversion in a code block and note any behavioral differences.\n\n\`\`\`${lang}\n${code}\n\`\`\``),
                ),
                el('div', { class: 'ws-row' }, customPrompt, askCustom)
            ),
            report
        ));
    },

    // ─── DESIGNER ────────────────────────────────────────────────────────
    design(root) {
        const stored = JSON.parse(localStorage.getItem('baba_design_state') || '{}');
        const brand = el('input', { class: 'ws-input', placeholder: 'Brand / product name', value: stored.brand || '' });
        const audience = el('input', { class: 'ws-input', placeholder: 'Target audience', value: stored.audience || '' });
        const vibe = el('input', { class: 'ws-input', placeholder: 'Vibe (e.g. "calm, premium, mystical")', value: stored.vibe || '' });
        const swatches = ['#D4A853', '#4779F7', '#6C3AED', '#22c55e', '#ef4444', '#f472b6', '#0ea5e9', '#f59e0b'];
        const palette = el('div', { class: 'ws-swatches' });
        const selectedColors = new Set(stored.colors || ['#D4A853', '#6C3AED']);
        function refreshPalette() {
            palette.innerHTML = '';
            for (const c of swatches) {
                palette.appendChild(el('button', {
                    class: 'ws-swatch' + (selectedColors.has(c) ? ' active' : ''),
                    style: `background:${c}`, type: 'button', title: c,
                    onclick: () => { selectedColors.has(c) ? selectedColors.delete(c) : selectedColors.add(c); persist(); refreshPalette(); }
                }));
            }
        }
        refreshPalette();
        const persist = () => localStorage.setItem('baba_design_state', JSON.stringify({
            brand: brand.value, audience: audience.value, vibe: vibe.value, colors: [...selectedColors]
        }));
        [brand, audience, vibe].forEach(i => i.addEventListener('input', persist));

        const { pane: report, target: reportTarget } = reportPanel('design');
        const buildBrief = () => `Brand: ${brand.value || '—'}\nAudience: ${audience.value || '—'}\nVibe: ${vibe.value || '—'}\nPalette: ${[...selectedColors].join(', ')}`;
        const action = (label, build) => el('button', {
            class: 'ws-btn', type: 'button',
            onclick: () => { window.babaSetReplyTarget(reportTarget); send(build()); }
        }, label);

        root.appendChild(el('div', { class: 'ws-shell design' },
            el('div', { class: 'ws-card design' },
                el('div', { class: 'ws-head' }, el('div', { class: 'ws-title' }, '\uD83C\uDFA8 Designer Board')),
                el('div', { class: 'ws-grid-2' }, brand, audience),
                vibe,
                el('div', { class: 'ws-label' }, 'Palette'),
                palette,
                el('div', { class: 'ws-actions' },
                    action('Critique my palette', () => `${buildBrief()}\n\nReview this color palette for the brand and audience. Suggest 2 specific improvements and one accessibility check (contrast).`),
                    action('Type system',         () => `${buildBrief()}\n\nSuggest a 2-font type system (display + body) with web-safe fallbacks and 3 size/weight pairings for hero, body, caption.`),
                    action('Layout ideas',        () => `${buildBrief()}\n\nSuggest 3 distinct landing-page hero layouts that fit the vibe. Be concrete about hierarchy and whitespace.`),
                    action('Microcopy',           () => `${buildBrief()}\n\nWrite 5 short on-brand microcopy lines: hero headline, subhead, primary CTA, secondary CTA, error toast.`),
                    action('Brand mood',          () => `${buildBrief()}\n\nWrite a 4-line brand mood description and suggest 3 inspirational reference brands (no logos, just names + why).`),
                )
            ),
            report
        ));
    },

    // ─── PROJECT MANAGER ─────────────────────────────────────────────────
    pm(root)        { simpleWorkspace(root, 'pm', '\uD83D\uDCCB PM Workspace', [
        { key: 'goal',        label: 'Project goal / outcome' },
        { key: 'horizon',     label: 'Horizon', kind: 'select', options: ['1 week', '2 weeks', '1 month', '1 quarter'] },
        { key: 'constraints', label: 'Constraints (deadline, team size, tech)' },
    ], (b) => [
        ['Sprint plan',         `${b}\n\nDraft a sprint plan: 5–8 prioritized tasks with rough owners (PM, Eng, Design), dependencies, and one risk per task. Use a markdown table.`],
        ['Risks register',      `${b}\n\nList the top 5 risks as a markdown table with columns: risk, likelihood, impact, mitigation.`],
        ['Stakeholder update',  `${b}\n\nWrite a 4-line stakeholder status: progress, next, risks, ask.`],
        ['Backlog grooming',    `${b}\n\nSuggest acceptance criteria for the 3 most ambiguous items in the goal above.`],
        ['Kickoff agenda',      `${b}\n\nDraft a 30-minute project kickoff agenda with timing per item.`],
    ]); },
    marketing(root) { simpleWorkspace(root, 'marketing', '\uD83D\uDCE3 Marketing Studio', [
        { key: 'product',  label: 'Product / offering' },
        { key: 'channel',  label: 'Channel', kind: 'select', options: ['LinkedIn', 'Twitter / X', 'Email', 'Instagram', 'TikTok', 'Landing Page', 'YouTube'] },
        { key: 'audience', label: 'Target audience' },
    ], (b, st) => [
        ['Hook lines',          `${b}\n\nWrite 5 attention-grabbing first lines tailored to ${st.channel}. Format as a numbered list.`],
        ['3-post series',       `${b}\n\nDraft a 3-post sequence for ${st.channel}: educate → demonstrate → convert. Each post under 80 words. Use ## headers per post.`],
        ['Email sequence',      `${b}\n\nDraft a 3-email nurture sequence: subject + short body for each. Use ## headers per email.`],
        ['Positioning',         `${b}\n\nWrite a one-sentence positioning statement and 3 differentiators vs the obvious competitors. Use a markdown table for differentiators.`],
        ['Audience persona',    `${b}\n\nWrite a one-page persona card for the target audience. Use a markdown table with: who, pains, gains, day-in-life, channels they trust.`],
    ]); },
    sales(root)     { simpleWorkspace(root, 'sales', '\uD83D\uDCBC Sales Coach', [
        { key: 'prospect', label: 'Prospect / company' },
        { key: 'pain',     label: 'Pain point you solve' },
        { key: 'stage',    label: 'Stage', kind: 'select', options: ['Discovery', 'Demo', 'Proposal', 'Negotiation', 'Closing'] },
    ], (b, st) => [
        ['Discovery questions', `${b}\n\nWrite 6 strong discovery questions tailored to this prospect and pain. Format as a numbered list.`],
        ['Pitch (60 sec)',      `${b}\n\nWrite a confident, friendly 60-second pitch suitable for a video call.`],
        ['Objection handling',  `${b}\n\nList the 3 most likely objections at the ${st.stage} stage and a calm one-paragraph response to each. Use ## headers per objection.`],
        ['Follow-up email',     `${b}\n\nDraft a short follow-up email after a ${st.stage} meeting: recap, value, single next step.`],
        ['Battle card',         `${b}\n\nWrite a sales battle card: our strengths, common objections, ROI proof points. Use markdown sections.`],
    ]); },
    hr(root)        { simpleWorkspace(root, 'hr', '\uD83E\uDD1D HR Studio', [
        { key: 'role',      label: 'Role / title' },
        { key: 'seniority', label: 'Seniority', kind: 'select', options: ['Intern', 'Junior', 'Mid', 'Senior', 'Lead', 'Manager'] },
        { key: 'skills',    label: 'Key skills (comma separated)' },
    ], (b) => [
        ['Job description',     `${b}\n\nWrite a concise, inclusive JD using these markdown sections: ## Summary ## Responsibilities ## Requirements ## Nice-to-have`],
        ['Interview kit',       `${b}\n\nDesign a 45-minute interview: 3 behavioral and 3 role-specific questions. Format each question with a markdown sub-heading and a "strong answer" / "weak answer" block.`],
        ['Offer letter',        `${b}\n\nDraft a warm, professional offer-letter outline. Include start date, role, manager, intro to culture. Use a markdown structure.`],
        ['PIP draft',           `${b}\n\nDraft a fair, supportive 30-day performance-improvement plan with 3 measurable goals. Use a markdown table for goals.`],
        ['Onboarding plan',     `${b}\n\nWrite a 30-60-90 day onboarding plan as a markdown table.`],
    ]); },

    // ─── ASTROLOGY (fullscreen dashboard) ────────────────────────────────
    astrology(root) { astrologyWorkspace(root); },
};

// ─── Generic role workspace (PM / Marketing / Sales / HR) ───────────────
function simpleWorkspace(root, key, title, fields, actions) {
    const stored = JSON.parse(localStorage.getItem('baba_' + key + '_state') || '{}');
    const inputs = {};
    const formGrid = el('div', { class: 'ws-form-grid' });
    for (const f of fields) {
        let input;
        if (f.kind === 'select') {
            input = el('select', { class: 'ws-input' },
                ...f.options.map(o => {
                    const opt = el('option', { value: o }, o);
                    if ((stored[f.key] || f.options[0]) === o) opt.selected = true;
                    return opt;
                })
            );
        } else {
            input = el('input', { class: 'ws-input', placeholder: f.label, value: stored[f.key] || '' });
        }
        inputs[f.key] = input;
        formGrid.appendChild(input);
    }
    const persist = () => {
        const out = {};
        for (const f of fields) out[f.key] = inputs[f.key].value;
        localStorage.setItem('baba_' + key + '_state', JSON.stringify(out));
    };
    Object.values(inputs).forEach(i => i.addEventListener('input', persist));

    const { pane: report, target: reportTarget } = reportPanel(key);

    const brief = () => fields.map(f => `${f.label}: ${inputs[f.key].value || '—'}`).join('\n');
    const stateView = () => {
        const o = {}; for (const f of fields) o[f.key] = inputs[f.key].value; return o;
    };

    const actionBtns = el('div', { class: 'ws-actions' });
    for (const [label, build] of actions(brief(), stateView())) {
        actionBtns.appendChild(el('button', {
            class: 'ws-btn', type: 'button',
            onclick: () => {
                window.babaSetReplyTarget(reportTarget);
                send(typeof build === 'function' ? build() : build);
            }
        }, label));
    }

    root.appendChild(el('div', { class: 'ws-shell ' + key },
        el('div', { class: 'ws-card ' + key },
            el('div', { class: 'ws-head' }, el('div', { class: 'ws-title' }, title)),
            formGrid,
            actionBtns
        ),
        report
    ));
}

// ─── ASTROLOGY DASHBOARD ────────────────────────────────────────────────
function astrologyWorkspace(root) {
    const stored = JSON.parse(localStorage.getItem('baba_astro_state') || '{}');

    // Form
    const name = el('input', { class: 'ws-input', placeholder: 'Your name (optional)', value: stored.name || '' });
    const dob = el('input', { class: 'ws-input', type: 'date', value: stored.dob || '' });
    const photo = el('input', { class: 'ws-input', type: 'file', accept: 'image/*' });
    const photoPreview = el('div', { class: 'ws-photo-preview' });
    if (stored.photoData) photoPreview.appendChild(el('img', { src: stored.photoData, alt: 'You', class: 'ws-photo' }));
    photo.addEventListener('change', () => {
        const f = photo.files?.[0]; if (!f) return;
        const r = new FileReader();
        r.onload = () => {
            photoPreview.innerHTML = '';
            photoPreview.appendChild(el('img', { src: r.result, alt: 'You', class: 'ws-photo' }));
            stored.photoData = r.result;
            localStorage.setItem('baba_astro_state', JSON.stringify(stored));
        };
        r.readAsDataURL(f);
    });
    [name, dob].forEach(i => i.addEventListener('input', () => {
        stored.name = name.value; stored.dob = dob.value;
        localStorage.setItem('baba_astro_state', JSON.stringify(stored));
    }));

    // Dashboard host (deterministic visuals + Ollama narrative)
    const dash = el('div', { class: 'astro-dash' });

    // Right-pane zodiac wheel (fed by selecting a sign)
    function paintWheel(activeSign) {
        const wheel = document.querySelector('.context-pane.astrology .zodiac-wheel');
        if (!wheel) return;
        wheel.innerHTML = '';
        SIGNS.forEach((s, idx) => {
            const angle = (idx / 12) * 360 - 90;
            const node = el('button', {
                class: 'zw-sign' + (activeSign?.name === s.name ? ' active' : ''),
                style: `--a:${angle}deg`,
                title: `${s.name} (${s.element})`,
                type: 'button',
                onclick: () => {
                    const today = new Date();
                    paintWheel(s);
                    showQuickInsight(s, today);
                }
            }, s.emoji);
            wheel.appendChild(node);
        });
    }
    paintWheel(null);

    function showQuickInsight(sign, date) {
        const target = document.querySelector('.context-pane.astrology .zw-insight');
        if (!target) return;
        const gauges = moodGauges(sign.name, dateKey(date));
        target.innerHTML = '';
        target.appendChild(el('h4', {}, `${sign.emoji} ${sign.name}`));
        target.appendChild(el('p', { class: 'zw-traits' }, sign.traits));
        target.appendChild(gaugeStack(gauges));
    }

    const reveal = el('button', {
        class: 'ws-btn primary', type: 'button',
        onclick: async () => {
            if (!dob.value) { dash.innerHTML = '<p class="ws-msg err">Please pick your birthday first.</p>'; return; }
            await renderAstroDashboard(dash, { name: name.value, dob: dob.value });
        }
    }, '\u2728 Reveal my full reading');

    root.appendChild(el('div', { class: 'ws-shell astrology' },
        el('div', { class: 'ws-card astrology' },
            el('div', { class: 'ws-head' },
                el('div', { class: 'ws-title' }, '\uD83D\uDD2E Astrologer Sage')),
            el('div', { class: 'ws-grid-2' }, name, dob),
            el('label', { class: 'ws-file-label' }, 'Optional selfie ', photo),
            photoPreview,
            el('div', { class: 'ws-actions' }, reveal)
        ),
        dash
    ));

    // Auto-render the dashboard on entry if a birthday is already saved.
    if (stored.dob) renderAstroDashboard(dash, { name: stored.name, dob: stored.dob });
}

async function renderAstroDashboard(dash, { name, dob }) {
    dash.innerHTML = '';
    const [y, m, d] = dob.split('-').map(Number);
    const chart = buildBirthChart({ name, month: m, day: d, year: y });
    const today = new Date();

    // 1) Birth chart hero
    dash.appendChild(birthChartCard(chart, dob));

    // 2) Today + Mood gauges
    const todayCard = el('section', { class: 'astro-card' },
        el('header', { class: 'astro-section-head' },
            el('h3', {}, `\u2728 Today, ${today.toDateString()}`),
            el('span', { class: 'astro-tag' }, chart.luckyDay === DAY_NAME(today) ? 'Your lucky day' : '')
        ),
        gaugeStack(moodGauges(chart.sign.name, dateKey(today))),
        el('div', { class: 'astro-section narrative', dataset: { sec: 'today' } })
    );
    dash.appendChild(todayCard);

    // 3) Next 3 days mini-cards
    const next3 = el('div', { class: 'astro-3-grid' });
    for (let i = 1; i <= 3; i++) {
        const dt = new Date(today); dt.setDate(today.getDate() + i);
        next3.appendChild(el('div', { class: 'astro-mini' },
            el('div', { class: 'astro-mini-day' }, dt.toLocaleDateString(undefined, { weekday: 'short', month: 'short', day: 'numeric' })),
            gaugeStack(moodGauges(chart.sign.name, dateKey(dt)), { mini: true })
        ));
    }
    dash.appendChild(el('section', { class: 'astro-card' },
        el('header', { class: 'astro-section-head' }, el('h3', {}, '\uD83D\uDDD3\uFE0F The next 3 days')),
        next3,
        el('div', { class: 'astro-section narrative', dataset: { sec: 'next_3_days' } })
    ));

    // 4) Next month + Year ahead + Love + Career + Magic
    const sectionGrid = el('div', { class: 'astro-section-grid' });
    sectionGrid.appendChild(narrativeCard('\uD83C\uDF15 The month ahead',  'next_month'));
    sectionGrid.appendChild(narrativeCard('\uD83C\uDF1F The year ahead',   'year_ahead'));
    sectionGrid.appendChild(narrativeCard('\u2764\uFE0F Love & family',     'love_family'));
    sectionGrid.appendChild(narrativeCard('\uD83D\uDCBC Career & money',   'career_money'));
    sectionGrid.appendChild(narrativeCard('\uD83D\uDD2E Magic & spirit',   'magic_spirit'));
    dash.appendChild(sectionGrid);

    // 5) Famous match (Wikipedia)
    const fameHost = el('section', { class: 'astro-card' },
        el('header', { class: 'astro-section-head' }, el('h3', {}, '\u2605 Born on the same day')),
        el('div', { class: 'astro-fame-list' }, el('p', { class: 'ws-msg' }, 'Looking up famous birthday twins…'))
    );
    dash.appendChild(fameHost);

    // 6) Funny historical facts (Wikipedia events)
    const factsHost = el('section', { class: 'astro-card' },
        el('header', { class: 'astro-section-head' }, el('h3', {}, '\uD83C\uDF89 What else happened on this day')),
        el('div', { class: 'astro-facts' }, el('p', { class: 'ws-msg' }, 'Loading historical events…'))
    );
    dash.appendChild(factsHost);

    // 7) Free-form follow-up at the bottom
    const followInput = el('input', { class: 'ws-input', placeholder: 'Ask the Astrologer Sage anything…', onkeydown: (e) => { if (e.key === 'Enter') followBtn.click(); } });
    const followBtn = el('button', {
        class: 'ws-btn primary', type: 'button',
        onclick: () => {
            const q = followInput.value.trim(); if (!q) return;
            const followCard = el('section', { class: 'astro-card md formatted' });
            const followTarget = el('div', {});
            followCard.appendChild(el('header', { class: 'astro-section-head' }, el('h3', {}, '\uD83D\uDCAC Your question')));
            followCard.appendChild(el('p', { class: 'astro-q' }, q));
            followCard.appendChild(followTarget);
            dash.appendChild(followCard);
            window.babaSetReplyTarget(followTarget);
            send(`As the Astrologer Sage speaking to a ${chart.sign.name} (${chart.chinese.animal}): ${q}\n\nReply in 3-5 short sentences. Plain prose. No markdown.`);
            followInput.value = '';
        }
    }, 'Ask');
    dash.appendChild(el('div', { class: 'astro-followup' }, followInput, followBtn));

    // ─── Wikipedia fetches ──────────────────────────────────────────────
    let famous = [];
    try {
        famous = await fetchFamousBirthdayMatch(m, d, 3);
        const list = fameHost.querySelector('.astro-fame-list');
        list.innerHTML = '';
        for (const f of famous) {
            list.appendChild(el('a', { class: 'astro-fame', href: f.url, target: '_blank', rel: 'noopener noreferrer' },
                el('img', { src: f.thumb, alt: f.title, class: 'astro-photo', loading: 'lazy' }),
                el('div', { class: 'astro-fame-meta' },
                    el('div', { class: 'astro-fame-name' }, f.title),
                    el('div', { class: 'astro-fame-year' }, String(f.year || '')),
                    el('div', { class: 'astro-fame-extract' }, (f.extract || '').slice(0, 180) + ((f.extract || '').length > 180 ? '\u2026' : ''))
                )
            ));
        }
        if (!famous.length) list.appendChild(el('p', { class: 'ws-msg' }, 'No famous birthday twins found for this date — you are unique!'));
    } catch (e) {
        fameHost.querySelector('.astro-fame-list').innerHTML = '<p class="ws-msg">(Wikipedia is unreachable from this device.)</p>';
    }

    try {
        const facts = await fetchFunFacts(m, d, 5);
        const factsEl = factsHost.querySelector('.astro-facts');
        factsEl.innerHTML = '';
        for (const f of facts) {
            factsEl.appendChild(el('div', { class: 'astro-fact' },
                el('span', { class: 'astro-fact-year' }, String(f.year || '?')),
                el('span', { class: 'astro-fact-text' }, f.text)
            ));
        }
        if (!facts.length) factsEl.appendChild(el('p', { class: 'ws-msg' }, 'A quiet day in history.'));
    } catch (_) {
        factsHost.querySelector('.astro-facts').innerHTML = '<p class="ws-msg">(Wikipedia is unreachable from this device.)</p>';
    }

    // ─── Ollama narrative — single batched call, sectioned ──────────────
    const sectionTargets = {};
    dash.querySelectorAll('.astro-section.narrative').forEach(el2 => {
        sectionTargets[el2.dataset.sec] = el2;
        el2.innerHTML = '<span class="typing-dots"><span></span><span></span><span></span></span>';
    });
    // Stream into a hidden buffer; on every token, re-parse and route into
    // the right section card.
    const buffer = el('div', { style: 'display:none' });
    dash.appendChild(buffer);
    window.babaSetReplyTarget(buffer, {
        onToken: (_chunk, full) => {
            const sec = parseSections(full);
            for (const [key, txt] of Object.entries(sec)) {
                const target = sectionTargets[key];
                if (target) target.textContent = txt;
            }
        }
    });
    send(buildAdvicePrompt({ chart, famous }));
}

function birthChartCard(chart, dob) {
    return el('section', { class: 'astro-card hero' },
        el('div', { class: 'astro-hero-left' },
            el('div', { class: 'astro-emoji huge', style: `background:${chart.luckyColor}33;color:${chart.luckyColor}` }, chart.sign.emoji),
            el('div', {},
                el('h2', {}, `${chart.sign.name}`),
                el('div', { class: 'astro-sub' }, `${chart.sign.element} \u00B7 ${chart.modality} \u00B7 ruled by ${chart.ruler}`),
                el('div', { class: 'astro-sub' }, `${chart.chinese.emoji} Year of the ${chart.chinese.animal} \u00B7 ${chart.chinese.traits}`),
                el('p', { class: 'astro-mantra' }, `\u201C${chart.mantra}\u201D`)
            )
        ),
        el('div', { class: 'astro-hero-right' },
            metric('\u2728 Spirit animal', chart.spirit),
            metric('\uD83C\uDFAF Lucky number', String(chart.luckyNumber)),
            metric('\uD83D\uDDD3\uFE0F Lucky day', chart.luckyDay),
            metric('\uD83D\uDC8E Crystal', chart.crystal),
            metricColor('\uD83C\uDFA8 Lucky color', chart.luckyColor, chart.luckyColorName),
            metric('\uD83C\uDF82 Birthday', dob),
        )
    );
}
function metric(label, value) {
    return el('div', { class: 'astro-metric' }, el('span', { class: 'm-label' }, label), el('span', { class: 'm-value' }, value));
}
function metricColor(label, color, name) {
    return el('div', { class: 'astro-metric' },
        el('span', { class: 'm-label' }, label),
        el('span', { class: 'm-value' }, el('span', { class: 'm-swatch', style: `background:${color}` }), name)
    );
}
function gaugeStack(gauges, opts = {}) {
    const wrap = el('div', { class: 'astro-gauges' + (opts.mini ? ' mini' : '') });
    for (const g of gauges) {
        const bar = el('div', { class: 'astro-gauge' },
            el('span', { class: 'g-name' }, g.name),
            el('span', { class: 'g-track' }, el('span', { class: 'g-fill', style: `width:${g.value}%` })),
            el('span', { class: 'g-num' }, `${g.value}`)
        );
        wrap.appendChild(bar);
    }
    return wrap;
}
function narrativeCard(title, secKey) {
    return el('section', { class: 'astro-card' },
        el('header', { class: 'astro-section-head' }, el('h3', {}, title)),
        el('div', { class: 'astro-section narrative', dataset: { sec: secKey } },
            el('span', { class: 'typing-dots' }, el('span', {}), el('span', {}), el('span', {}))
        )
    );
}
function DAY_NAME(d) { return ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday'][d.getDay()]; }
