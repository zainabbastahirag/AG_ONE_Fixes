// ═══════════════════════════════════════════════════════════════════════
//  Avatar-driven workspaces.
//  When the user picks a "professional" avatar, the center area swaps in
//  a role-specific board. Each workspace is small, self-contained, and
//  produces a structured prompt that is sent through the existing
//  /api/chat/* SSE endpoint — so all replies still stream into the
//  speech bubble (markdown-formatted) and speak out loud (plain text).
// ═══════════════════════════════════════════════════════════════════════

import { westernSign, chineseSign, fetchFamousBirthdayMatch, buildAdvicePrompt } from './astrology.js';

const AVATAR_TO_WORKSPACE = {
    sage: 'chat', philosopher: 'chat', healer: 'chat', elder: 'chat', storyteller: 'chat',
    designer: 'design', developer: 'dev', pm: 'pm', marketing: 'marketing',
    sales: 'sales', hr: 'hr', astrologer: 'astrology',
};

let _ask = null;        // injected: (prompt) => Promise — sends a chat turn
let _setText = null;    // injected: (text) => void — write into the bubble (formatted)
let _root = null;       // workspace container element

/**
 * Wire the workspaces into the page. `ask(promptText)` triggers a chat
 * roundtrip so the bot's reply streams into the existing speech bubble.
 */
export function initWorkspaces({ root, ask, setBubbleText, setBubbleMarkdown }) {
    _root = root;
    _ask = ask;
    _setText = setBubbleText || (() => { });
    setWorkspace('chat');
}

export function workspaceForAvatar(avatarKey) {
    return AVATAR_TO_WORKSPACE[avatarKey] || 'chat';
}

export function setWorkspace(key) {
    if (!_root) return;
    _root.innerHTML = '';
    _root.dataset.ws = key;
    const builder = WORKSPACES[key] || WORKSPACES.chat;
    builder(_root);
    // Keep the chat input visible regardless of workspace.
    _root.classList.toggle('hidden', key === 'chat');
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
        if (typeof c === 'string') e.appendChild(document.createTextNode(c));
        else e.appendChild(c);
    }
    return e;
}

function send(prompt) { if (_ask && prompt) _ask(prompt); }

// ─── chat (default) ──────────────────────────────────────────────────────
const WORKSPACES = {
    chat(root) { /* nothing — the avatar+bubble are the workspace */ },

    // ─── DEVELOPER ───────────────────────────────────────────────────────
    dev(root) {
        const langs = ['javascript', 'typescript', 'python', 'csharp', 'go', 'rust', 'sql', 'html', 'css', 'bash'];
        const stored = JSON.parse(localStorage.getItem('baba_dev_state') || '{}');
        const langSel = el('select', { class: 'ws-input', style: 'min-width: 130px' },
            ...langs.map(l => {
                const o = el('option', { value: l }, l);
                if ((stored.lang || 'javascript') === l) o.selected = true;
                return o;
            })
        );
        const editor = el('textarea', {
            class: 'ws-code', placeholder: '// Paste or write your code here...',
            spellcheck: 'false', autocapitalize: 'off', autocomplete: 'off'
        });
        editor.value = stored.code || '';

        const persist = () => localStorage.setItem('baba_dev_state',
            JSON.stringify({ lang: langSel.value, code: editor.value }));
        editor.addEventListener('input', persist);
        langSel.addEventListener('change', persist);

        const action = (label, build) => el('button', {
            class: 'ws-btn', type: 'button',
            onclick: () => {
                const code = editor.value.trim();
                if (!code) { _setText('Paste some code in the editor first, then I will help.'); return; }
                send(build({ lang: langSel.value, code }));
            }
        }, label);

        const customPrompt = el('input', { class: 'ws-input', placeholder: 'Ask BABA-G about this code...' });
        const askCustom = el('button', {
            class: 'ws-btn primary', type: 'button',
            onclick: () => {
                const q = customPrompt.value.trim();
                if (!q) return;
                const code = editor.value.trim();
                send(`I am working in ${langSel.value}. Here is the code:\n\`\`\`${langSel.value}\n${code}\n\`\`\`\n${q}`);
                customPrompt.value = '';
            }
        }, 'Ask');

        root.appendChild(el('div', { class: 'ws-card dev' },
            el('div', { class: 'ws-head' },
                el('div', { class: 'ws-title' }, '\uD83D\uDCBB Code Canvas'),
                el('div', { class: 'ws-tools' }, langSel)
            ),
            editor,
            el('div', { class: 'ws-actions' },
                action('Explain', ({ lang, code }) => `Explain this ${lang} code clearly. Walk through the important parts in 3-5 short sentences. End with one improvement suggestion.\n\n\`\`\`${lang}\n${code}\n\`\`\``),
                action('Find bugs', ({ lang, code }) => `Review this ${lang} code for bugs and edge cases. List the most likely issues, then show a corrected version.\n\n\`\`\`${lang}\n${code}\n\`\`\``),
                action('Optimize', ({ lang, code }) => `Suggest performance and readability improvements for this ${lang} code. Provide a refactored version.\n\n\`\`\`${lang}\n${code}\n\`\`\``),
                action('Add tests', ({ lang, code }) => `Write idiomatic unit tests for this ${lang} code. Pick the standard testing framework for the language.\n\n\`\`\`${lang}\n${code}\n\`\`\``),
                action('Document', ({ lang, code }) => `Add concise doc comments / JSDoc / docstrings to this ${lang} code. Return the documented version.\n\n\`\`\`${lang}\n${code}\n\`\`\``),
            ),
            el('div', { class: 'ws-row' }, customPrompt, askCustom)
        ));
    },

    // ─── DESIGNER ────────────────────────────────────────────────────────
    design(root) {
        const stored = JSON.parse(localStorage.getItem('baba_design_state') || '{}');
        const brand = el('input', { class: 'ws-input', placeholder: 'Brand / product name', value: stored.brand || '' });
        const audience = el('input', { class: 'ws-input', placeholder: 'Target audience', value: stored.audience || '' });
        const vibe = el('input', { class: 'ws-input', placeholder: 'Vibe (e.g. "calm, premium, mystical")', value: stored.vibe || '' });
        const swatches = ['#D4A853', '#4779F7', '#6C3AED', '#22c55e', '#ef4444', '#f472b6'];
        const palette = el('div', { class: 'ws-swatches' });
        const selectedColors = new Set(stored.colors || ['#D4A853', '#6C3AED']);
        function refreshPalette() {
            palette.innerHTML = '';
            for (const c of swatches) {
                const dot = el('button', {
                    class: 'ws-swatch' + (selectedColors.has(c) ? ' active' : ''),
                    style: `background:${c}`, type: 'button', title: c,
                    onclick: () => {
                        if (selectedColors.has(c)) selectedColors.delete(c); else selectedColors.add(c);
                        persist(); refreshPalette();
                    }
                });
                palette.appendChild(dot);
            }
        }
        refreshPalette();

        const persist = () => localStorage.setItem('baba_design_state', JSON.stringify({
            brand: brand.value, audience: audience.value, vibe: vibe.value, colors: [...selectedColors]
        }));
        [brand, audience, vibe].forEach(i => i.addEventListener('input', persist));

        const buildBrief = () => `Brand: ${brand.value || '—'}\nAudience: ${audience.value || '—'}\nVibe: ${vibe.value || '—'}\nPalette: ${[...selectedColors].join(', ')}`;
        const action = (label, build) => el('button', {
            class: 'ws-btn', type: 'button',
            onclick: () => send(build())
        }, label);

        root.appendChild(el('div', { class: 'ws-card design' },
            el('div', { class: 'ws-head' }, el('div', { class: 'ws-title' }, '\uD83C\uDFA8 Designer Board')),
            el('div', { class: 'ws-grid-2' }, brand, audience),
            vibe,
            el('div', { class: 'ws-label' }, 'Palette'),
            palette,
            el('div', { class: 'ws-actions' },
                action('Critique my palette', () => `${buildBrief()}\n\nReview this color palette for the brand and audience. Suggest 2 specific improvements and one accessibility check (contrast).`),
                action('Type system', () => `${buildBrief()}\n\nSuggest a 2-font type system (display + body) with web-safe fallbacks and 3 size/weight pairings for hero, body, caption.`),
                action('Layout ideas', () => `${buildBrief()}\n\nSuggest 3 distinct landing-page hero layouts that fit the vibe. Be concrete about hierarchy and whitespace.`),
                action('Microcopy', () => `${buildBrief()}\n\nWrite 5 short on-brand microcopy lines: hero headline, subhead, primary CTA, secondary CTA, error toast.`),
            )
        ));
    },

    // ─── PROJECT MANAGER ─────────────────────────────────────────────────
    pm(root) {
        const stored = JSON.parse(localStorage.getItem('baba_pm_state') || '{}');
        const goal = el('input', { class: 'ws-input', placeholder: 'Project goal / outcome', value: stored.goal || '' });
        const constraints = el('input', { class: 'ws-input', placeholder: 'Constraints (deadline, team size, tech)', value: stored.constraints || '' });
        const horizon = el('select', { class: 'ws-input' },
            ...['1 week', '2 weeks', '1 month', '1 quarter'].map(t => {
                const o = el('option', { value: t }, t); if ((stored.horizon || '2 weeks') === t) o.selected = true; return o;
            })
        );
        const persist = () => localStorage.setItem('baba_pm_state', JSON.stringify({
            goal: goal.value, constraints: constraints.value, horizon: horizon.value
        }));
        [goal, constraints].forEach(i => i.addEventListener('input', persist));
        horizon.addEventListener('change', persist);

        const brief = () => `Goal: ${goal.value || '—'}\nConstraints: ${constraints.value || '—'}\nHorizon: ${horizon.value}`;
        const action = (label, build) => el('button', { class: 'ws-btn', type: 'button', onclick: () => send(build()) }, label);

        root.appendChild(el('div', { class: 'ws-card pm' },
            el('div', { class: 'ws-head' }, el('div', { class: 'ws-title' }, '\uD83D\uDCCB PM Workspace')),
            el('div', { class: 'ws-grid-2' }, goal, horizon),
            constraints,
            el('div', { class: 'ws-actions' },
                action('Sprint plan', () => `${brief()}\n\nDraft a sprint plan: 5–8 prioritized tasks with rough owners (PM, Eng, Design), dependencies, and one risk per task.`),
                action('Risks register', () => `${brief()}\n\nList the top 5 risks with likelihood / impact and one mitigation each.`),
                action('Stakeholder update', () => `${brief()}\n\nWrite a 4-line stakeholder status: progress, next, risks, ask.`),
                action('Backlog grooming', () => `${brief()}\n\nSuggest acceptance criteria for the 3 most ambiguous items in the goal above.`),
            )
        ));
    },

    // ─── MARKETING ───────────────────────────────────────────────────────
    marketing(root) {
        const stored = JSON.parse(localStorage.getItem('baba_mkt_state') || '{}');
        const product = el('input', { class: 'ws-input', placeholder: 'Product / offering', value: stored.product || '' });
        const audience = el('input', { class: 'ws-input', placeholder: 'Target audience', value: stored.audience || '' });
        const channel = el('select', { class: 'ws-input' },
            ...['LinkedIn', 'Twitter / X', 'Email', 'Instagram', 'TikTok', 'Landing Page', 'YouTube'].map(t => {
                const o = el('option', { value: t }, t); if ((stored.channel || 'LinkedIn') === t) o.selected = true; return o;
            })
        );
        const persist = () => localStorage.setItem('baba_mkt_state', JSON.stringify({ product: product.value, audience: audience.value, channel: channel.value }));
        [product, audience].forEach(i => i.addEventListener('input', persist));
        channel.addEventListener('change', persist);

        const brief = () => `Product: ${product.value || '—'}\nAudience: ${audience.value || '—'}\nChannel: ${channel.value}`;
        const action = (label, build) => el('button', { class: 'ws-btn', type: 'button', onclick: () => send(build()) }, label);

        root.appendChild(el('div', { class: 'ws-card marketing' },
            el('div', { class: 'ws-head' }, el('div', { class: 'ws-title' }, '\uD83D\uDCE3 Marketing Studio')),
            el('div', { class: 'ws-grid-2' }, product, channel),
            audience,
            el('div', { class: 'ws-actions' },
                action('Hook lines', () => `${brief()}\n\nWrite 5 attention-grabbing first lines tailored to ${channel.value}.`),
                action('3-post series', () => `${brief()}\n\nDraft a 3-post sequence for ${channel.value}: educate → demonstrate → convert. Each post < 80 words.`),
                action('Email subject + body', () => `${brief()}\n\nWrite a cold email: subject line, one-paragraph body, single clear CTA.`),
                action('Positioning', () => `${brief()}\n\nWrite a one-sentence positioning statement and 3 differentiators vs the obvious competitors.`),
            )
        ));
    },

    // ─── SALES ───────────────────────────────────────────────────────────
    sales(root) {
        const stored = JSON.parse(localStorage.getItem('baba_sales_state') || '{}');
        const prospect = el('input', { class: 'ws-input', placeholder: 'Prospect / company', value: stored.prospect || '' });
        const pain = el('input', { class: 'ws-input', placeholder: 'Pain point you solve', value: stored.pain || '' });
        const stage = el('select', { class: 'ws-input' },
            ...['Discovery', 'Demo', 'Proposal', 'Negotiation', 'Closing'].map(t => {
                const o = el('option', { value: t }, t); if ((stored.stage || 'Discovery') === t) o.selected = true; return o;
            })
        );
        const persist = () => localStorage.setItem('baba_sales_state', JSON.stringify({ prospect: prospect.value, pain: pain.value, stage: stage.value }));
        [prospect, pain].forEach(i => i.addEventListener('input', persist));
        stage.addEventListener('change', persist);

        const brief = () => `Prospect: ${prospect.value || '—'}\nPain: ${pain.value || '—'}\nStage: ${stage.value}`;
        const action = (label, build) => el('button', { class: 'ws-btn', type: 'button', onclick: () => send(build()) }, label);

        root.appendChild(el('div', { class: 'ws-card sales' },
            el('div', { class: 'ws-head' }, el('div', { class: 'ws-title' }, '\uD83D\uDCBC Sales Coach')),
            el('div', { class: 'ws-grid-2' }, prospect, stage),
            pain,
            el('div', { class: 'ws-actions' },
                action('Discovery questions', () => `${brief()}\n\nWrite 6 strong discovery questions tailored to this prospect and pain.`),
                action('Pitch (60 sec)', () => `${brief()}\n\nWrite a confident, friendly 60-second pitch suitable for a video call.`),
                action('Objection handling', () => `${brief()}\n\nList the 3 most likely objections at the ${stage.value} stage and a calm one-paragraph response to each.`),
                action('Follow-up email', () => `${brief()}\n\nDraft a short follow-up email after a ${stage.value} meeting. Recap, value, single next step.`),
            )
        ));
    },

    // ─── HR ──────────────────────────────────────────────────────────────
    hr(root) {
        const stored = JSON.parse(localStorage.getItem('baba_hr_state') || '{}');
        const role = el('input', { class: 'ws-input', placeholder: 'Role / title', value: stored.role || '' });
        const skills = el('input', { class: 'ws-input', placeholder: 'Key skills (comma separated)', value: stored.skills || '' });
        const seniority = el('select', { class: 'ws-input' },
            ...['Intern', 'Junior', 'Mid', 'Senior', 'Lead', 'Manager'].map(t => {
                const o = el('option', { value: t }, t); if ((stored.seniority || 'Mid') === t) o.selected = true; return o;
            })
        );
        const persist = () => localStorage.setItem('baba_hr_state', JSON.stringify({ role: role.value, skills: skills.value, seniority: seniority.value }));
        [role, skills].forEach(i => i.addEventListener('input', persist));
        seniority.addEventListener('change', persist);

        const brief = () => `Role: ${role.value || '—'}\nSeniority: ${seniority.value}\nKey skills: ${skills.value || '—'}`;
        const action = (label, build) => el('button', { class: 'ws-btn', type: 'button', onclick: () => send(build()) }, label);

        root.appendChild(el('div', { class: 'ws-card hr' },
            el('div', { class: 'ws-head' }, el('div', { class: 'ws-title' }, '\uD83E\uDD1D HR Studio')),
            el('div', { class: 'ws-grid-2' }, role, seniority),
            skills,
            el('div', { class: 'ws-actions' },
                action('Job description', () => `${brief()}\n\nWrite a concise, inclusive job description: summary, responsibilities (5), requirements (5), nice-to-have (3).`),
                action('Interview kit', () => `${brief()}\n\nDesign a 45-min interview: 3 behavioral and 3 role-specific questions, plus what a strong vs. weak answer looks like.`),
                action('Offer letter', () => `${brief()}\n\nDraft a warm, professional offer letter outline (no specific numbers). Include start date, role, manager, intro to culture.`),
                action('PIP draft', () => `${brief()}\n\nDraft a fair, supportive 30-day performance-improvement plan with 3 measurable goals.`),
            )
        ));
    },

    // ─── ASTROLOGY ───────────────────────────────────────────────────────
    astrology(root) {
        const stored = JSON.parse(localStorage.getItem('baba_astro_state') || '{}');
        const name = el('input', { class: 'ws-input', placeholder: 'Your name (optional)', value: stored.name || '' });
        const dob = el('input', { class: 'ws-input', type: 'date', value: stored.dob || '' });
        const photo = el('input', { class: 'ws-input', type: 'file', accept: 'image/*' });
        const photoPreview = el('div', { class: 'ws-photo-preview' });
        if (stored.photoData) {
            photoPreview.appendChild(el('img', { src: stored.photoData, alt: 'You', class: 'ws-photo' }));
        }
        photo.addEventListener('change', () => {
            const f = photo.files?.[0];
            if (!f) return;
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

        const result = el('div', { class: 'ws-astro-result' });

        const reveal = el('button', {
            class: 'ws-btn primary', type: 'button',
            onclick: async () => {
                if (!dob.value) { result.innerHTML = '<p class="ws-msg err">Please pick your birthday first.</p>'; return; }
                result.innerHTML = '<p class="ws-msg">\u2728 Consulting the stars...</p>';
                const [y, m, d] = dob.value.split('-').map(Number);
                const sign = westernSign(m, d);
                const chinese = chineseSign(y);

                let famous = [];
                try { famous = await fetchFamousBirthdayMatch(m, d, 3); }
                catch (e) { console.warn('Wikipedia fetch failed', e); }

                renderAstroResult(result, { name: name.value, dob: dob.value, sign, chinese, famous });

                const prompt = buildAdvicePrompt({
                    name: name.value, birthday: dob.value, sign, chinese, famous
                });
                send(prompt);
            }
        }, '\u2728 Reveal my reading');

        root.appendChild(el('div', { class: 'ws-card astrology' },
            el('div', { class: 'ws-head' }, el('div', { class: 'ws-title' }, '\uD83D\uDD2E Astrologer Sage')),
            el('div', { class: 'ws-grid-2' }, name, dob),
            el('label', { class: 'ws-file-label' }, 'Optional selfie ', photo),
            photoPreview,
            el('div', { class: 'ws-actions' }, reveal),
            result
        ));
    },
};

function renderAstroResult(target, { name, dob, sign, chinese, famous }) {
    target.innerHTML = '';
    const card = el('div', { class: 'astro-card' });

    const head = el('div', { class: 'astro-head' });
    head.appendChild(el('div', { class: 'astro-sign' },
        el('div', { class: 'astro-emoji' }, sign.emoji),
        el('div', { class: 'astro-meta' },
            el('div', { class: 'astro-name' }, sign.name),
            el('div', { class: 'astro-sub' }, `${sign.element} \u00B7 ${chinese.emoji} Year of the ${chinese.animal}`)
        )
    ));
    head.appendChild(el('div', { class: 'astro-traits' }, sign.traits));
    card.appendChild(head);

    if (famous && famous.length) {
        const fameTitle = el('div', { class: 'astro-section-title' }, '\u2605 Born on the same day');
        card.appendChild(fameTitle);
        const list = el('div', { class: 'astro-fame-list' });
        for (const f of famous.slice(0, 3)) {
            const node = el('a', { class: 'astro-fame', href: f.url, target: '_blank', rel: 'noopener noreferrer' },
                el('img', { src: f.thumb, alt: f.title, class: 'astro-photo', loading: 'lazy' }),
                el('div', { class: 'astro-fame-meta' },
                    el('div', { class: 'astro-fame-name' }, f.title),
                    el('div', { class: 'astro-fame-year' }, String(f.year || '')),
                    el('div', { class: 'astro-fame-extract' }, (f.extract || '').slice(0, 160) + ((f.extract || '').length > 160 ? '\u2026' : ''))
                )
            );
            list.appendChild(node);
        }
        card.appendChild(list);
    }

    target.appendChild(card);
}
