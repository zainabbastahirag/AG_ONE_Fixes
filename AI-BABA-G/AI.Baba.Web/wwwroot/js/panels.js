// ═══════════════════════════════════════════════════════════════════════
//  Sidebar mode panels.
//
//  Each sidebar mode (Daily Guidance, Predictions, Compatibility, Future
//  Me, Roast Me, Dream Interpreter, History, Saved) opens a fullscreen
//  panel — NOT a message in the chat feed. The chat stays clean for free
//  conversation; structured insights live in their own dedicated views.
//
//  Architecture:
//  - One shared overlay div (created on demand).
//  - Each mode declares { title, intro, inputs, prompt(state), extras(state) }.
//  - openPanel(mode) builds the form, runs the prompt through the existing
//    /api/chat/* SSE pipeline with mode='panel' (so the server uses
//    num_predict=110 for snappier "report"-sized replies), and renders
//    the result into the panel's report area instead of the chat feed.
//  - Special list-mode panels (history / saved) skip the form and render
//    a scrollable list with Speak / Delete / Share buttons.
// ═══════════════════════════════════════════════════════════════════════

import { unlockTTS } from './voice.js';

const ZODIAC = [
    { value: 'aries',       label: '♈ Aries (Mar 21 – Apr 19)' },
    { value: 'taurus',      label: '♉ Taurus (Apr 20 – May 20)' },
    { value: 'gemini',      label: '♊ Gemini (May 21 – Jun 20)' },
    { value: 'cancer',      label: '♋ Cancer (Jun 21 – Jul 22)' },
    { value: 'leo',         label: '♌ Leo (Jul 23 – Aug 22)' },
    { value: 'virgo',       label: '♍ Virgo (Aug 23 – Sep 22)' },
    { value: 'libra',       label: '♎ Libra (Sep 23 – Oct 22)' },
    { value: 'scorpio',     label: '♏ Scorpio (Oct 23 – Nov 21)' },
    { value: 'sagittarius', label: '♐ Sagittarius (Nov 22 – Dec 21)' },
    { value: 'capricorn',   label: '♑ Capricorn (Dec 22 – Jan 19)' },
    { value: 'aquarius',    label: '♒ Aquarius (Jan 20 – Feb 18)' },
    { value: 'pisces',      label: '♓ Pisces (Feb 19 – Mar 20)' },
];

const PANELS = {
    daily: {
        title: 'Daily Guidance', icon: '☀️',
        intro: 'Today\'s focus and one piece of advice from Baba G.',
        inputs: [],
        prompt: () => `Give me today\'s daily guidance and one specific focus. Speak warmly. 2 short sentences.`,
        cta: 'Reveal today\'s reading',
    },
    predictions: {
        title: 'Predictions', icon: '🔮',
        intro: 'A short look at the road ahead.',
        inputs: [
            { type: 'select', name: 'horizon', label: 'When?', options: [
                { value: 'today',    label: 'Today' },
                { value: 'tomorrow', label: 'Tomorrow' },
                { value: '7 days',   label: 'Next 7 days' },
                { value: '30 days',  label: 'Next 30 days' },
                { value: '1 year',   label: 'Year ahead' },
            ] },
            { type: 'select', name: 'area', label: 'Area of life', options: [
                { value: 'overall',     label: 'Overall' },
                { value: 'career',      label: 'Career & money' },
                { value: 'love',        label: 'Love & relationships' },
                { value: 'family',      label: 'Family & home' },
                { value: 'health',      label: 'Health & body' },
                { value: 'spiritual',   label: 'Spirit & growth' },
            ] },
        ],
        prompt: ({ horizon, area }) =>
            `Predict ${horizon} for the area: ${area}. Be specific and warm. 3 short sentences.`,
        cta: 'See my prediction',
    },
    compatibility: {
        title: 'Compatibility', icon: '❤️',
        intro: 'Pick two zodiac signs and Baba G will read their chemistry.',
        inputs: [
            { type: 'select', name: 'sign1', label: 'You', options: ZODIAC },
            { type: 'select', name: 'sign2', label: 'Them', options: ZODIAC },
            { type: 'select', name: 'kind', label: 'Type of bond', options: [
                { value: 'romantic',   label: 'Romantic partner' },
                { value: 'friendship', label: 'Friendship' },
                { value: 'family',     label: 'Family member' },
                { value: 'work',       label: 'Work / business' },
            ] },
        ],
        prompt: ({ sign1, sign2, kind }) =>
            `Give a compatibility reading between a ${sign1} and a ${sign2} as ${kind}. ` +
            `Cover strengths, friction, and one piece of practical advice. 4 short sentences.`,
        cta: 'Get our chemistry',
        extras: ({ sign1, sign2 }) => compatibilityScoreCard(sign1, sign2),
    },
    'future-me': {
        title: 'Future Me', icon: '🪞',
        intro: 'A message from your future self in 10 years.',
        inputs: [
            { type: 'text', name: 'concern', label: 'What\'s on your mind today?', placeholder: 'e.g. should I switch jobs?' },
        ],
        prompt: ({ concern }) =>
            `Speak as my own future self, 10 years from today, sending advice back about: ` +
            `"${concern || 'life in general'}". Be warm, concrete, and brave. 4 short sentences.`,
        cta: 'Hear from Future Me',
    },
    roast: {
        title: 'Roast Me', icon: '🔥',
        intro: 'A warm, playful Baba G roast — never mean.',
        inputs: [
            { type: 'text', name: 'about', label: 'What should I roast you about? (optional)', placeholder: 'e.g. my screen-time, my coffee, my procrastination' },
        ],
        prompt: ({ about }) =>
            `Roast me with love, Baba G style, about: "${about || 'my procrastination, perfectionism, or scrolling habits'}". ` +
            `4 playful, warm lines. Never mean. End with a wink.`,
        cta: 'Roast me 🔥',
    },
    dream: {
        title: 'Dream Interpreter', icon: '☁️',
        intro: 'Describe a dream and Baba G will read between the symbols.',
        inputs: [
            { type: 'textarea', name: 'dream', label: 'Describe your dream', placeholder: 'I was walking through an empty market and a tiger followed me…', required: true, rows: 4 },
        ],
        prompt: ({ dream }) =>
            `Interpret this dream: "${dream}". Focus on what it might mean for the person\'s ` +
            `life right now, gently. 4 short sentences. Plain prose.`,
        cta: 'Read my dream',
    },
    history: { type: 'list', title: 'History', icon: '🕘', source: 'baba_history' },
    saved:   { type: 'list', title: 'Saved',   icon: '⭐', source: 'baba_saved'   },
};

let _overlayEl = null;

export function openPanel(mode) {
    const cfg = PANELS[mode];
    if (!cfg) return;
    closeAnyMobileDrawer();
    const ov = ensureOverlay();
    ov.dataset.mode = mode;
    ov.innerHTML = renderPanel(mode, cfg);
    ov.hidden = false;
    document.body.classList.add('panel-open');
    bindPanel(ov, mode, cfg);
    // Make sure the chat reply target points back at the bubble whenever we
    // close — clear it now so a panel run can install its own.
    if (window.babaSetReplyTarget) window.babaSetReplyTarget(null);
}

export function closePanel() {
    if (!_overlayEl) return;
    _overlayEl.hidden = true;
    document.body.classList.remove('panel-open');
    if (window.babaSetReplyTarget) window.babaSetReplyTarget(null);
    if (window.state) window.state.activePanelMode = null;
    if (window._babaState) window._babaState.activePanelMode = null;
}

function ensureOverlay() {
    if (_overlayEl) return _overlayEl;
    _overlayEl = document.createElement('div');
    _overlayEl.id = 'panelOverlay';
    _overlayEl.className = 'panel-overlay';
    _overlayEl.hidden = true;
    document.body.appendChild(_overlayEl);
    return _overlayEl;
}

function renderPanel(mode, cfg) {
    if (cfg.type === 'list') return renderListPanel(mode, cfg);
    return `
      <div class="panel-modal">
        <header class="panel-head">
          <button class="panel-back" type="button" data-panel-close aria-label="Back">⌄</button>
          <div class="panel-title">
            <span class="panel-icon">${cfg.icon}</span>
            <strong>${cfg.title}</strong>
          </div>
          <span class="panel-spacer"></span>
        </header>
        <p class="panel-intro">${cfg.intro || ''}</p>
        ${(cfg.inputs && cfg.inputs.length) ? `<form class="panel-form">${cfg.inputs.map(renderInput).join('')}</form>` : ''}
        <button class="btn-primary big panel-cta" type="button" data-panel-go>${cfg.cta || 'Reveal'}</button>
        <div class="panel-extras"></div>
        <article class="panel-result" hidden>
          <header class="panel-result-head">
            <span class="ws-report-tag">Baba G</span>
            <span class="panel-result-time"></span>
          </header>
          <p class="panel-result-text"></p>
          <div class="panel-result-actions">
            <button class="msg-action panel-replay" type="button">▶ Replay</button>
            <button class="msg-action panel-save" type="button">⭐ Save</button>
            <button class="msg-action panel-copy" type="button">📋 Copy</button>
            <button class="msg-action panel-close-2" type="button" data-panel-close>✕ Close</button>
          </div>
        </article>
      </div>`;
}

function renderInput(f) {
    if (f.type === 'select') {
        return `<label class="panel-field">
          <span>${f.label}</span>
          <select name="${f.name}">
            ${f.options.map(o => `<option value="${escapeAttr(o.value)}">${escapeHtml(o.label)}</option>`).join('')}
          </select>
        </label>`;
    }
    if (f.type === 'textarea') {
        return `<label class="panel-field">
          <span>${f.label}</span>
          <textarea name="${f.name}" rows="${f.rows || 3}" placeholder="${escapeAttr(f.placeholder || '')}" ${f.required ? 'required' : ''}></textarea>
        </label>`;
    }
    return `<label class="panel-field">
      <span>${f.label}</span>
      <input type="${f.type || 'text'}" name="${f.name}" placeholder="${escapeAttr(f.placeholder || '')}" ${f.required ? 'required' : ''} />
    </label>`;
}

function renderListPanel(mode, cfg) {
    const arr = (() => {
        try { return JSON.parse(localStorage.getItem(cfg.source) || '[]'); }
        catch (_) { return []; }
    })();
    const items = arr.slice().reverse();   // newest first
    return `
      <div class="panel-modal">
        <header class="panel-head">
          <button class="panel-back" type="button" data-panel-close aria-label="Back">⌄</button>
          <div class="panel-title">
            <span class="panel-icon">${cfg.icon}</span>
            <strong>${cfg.title}</strong>
            <small class="panel-count">${items.length}</small>
          </div>
          <button class="panel-clear" type="button" data-panel-clear title="Clear all">🗑</button>
        </header>
        ${items.length === 0 ? `<p class="panel-empty">${cfg.title === 'Saved'
            ? '⭐ No saved replies yet. Tap the Save button on any of Baba G\u2019s answers and they\u2019ll appear here.'
            : 'No conversation history yet. Say hi to Baba G and your chats will appear here.'}</p>`
        : `<ul class="panel-list">
            ${items.map(m => `
              <li class="panel-list-item">
                <div class="pli-meta">
                  <span class="pli-role">${m.role === 'user' ? 'You' : 'Baba G'}</span>
                  <span class="pli-time">${formatTime(m.at)}</span>
                </div>
                <p class="pli-text">${escapeHtml(m.text)}</p>
                <div class="pli-actions">
                  <button class="msg-action pli-speak" data-text="${escapeAttr(m.text)}" type="button">🔊 Speak</button>
                  <button class="msg-action pli-copy" data-text="${escapeAttr(m.text)}" type="button">📋 Copy</button>
                  <button class="msg-action pli-delete" data-text="${escapeAttr(m.text)}" type="button">🗑 Delete</button>
                </div>
              </li>`).join('')}
          </ul>`}
      </div>`;
}

function bindPanel(ov, mode, cfg) {
    ov.querySelectorAll('[data-panel-close]').forEach(b => b.addEventListener('click', closePanel));

    if (cfg.type === 'list') {
        bindListPanel(ov, mode, cfg);
        return;
    }

    const form = ov.querySelector('.panel-form');
    const cta = ov.querySelector('[data-panel-go]');
    const result = ov.querySelector('.panel-result');
    const resultText = result.querySelector('.panel-result-text');
    const resultTime = result.querySelector('.panel-result-time');
    const extrasHost = ov.querySelector('.panel-extras');

    cta?.addEventListener('click', () => {
        // Validate required fields.
        if (form) {
            for (const el of form.elements) {
                if (el.required && !el.value) { el.focus(); return; }
            }
        }
        const fields = {};
        if (form) for (const el of form.elements) fields[el.name] = el.value.trim();

        // Visual extras (compatibility score, etc.) before AI.
        if (typeof cfg.extras === 'function') {
            try { extrasHost.innerHTML = ''; const node = cfg.extras(fields); if (node) extrasHost.appendChild(node); }
            catch (_) { }
        }

        // Stream the AI reply into the result panel — NOT into the chat feed.
        result.hidden = false;
        resultText.textContent = '';
        resultText.innerHTML = '<span class="typing-dots"><span></span><span></span><span></span></span>';
        resultTime.textContent = formatTime(Date.now());

        const target = resultText;
        // Mark this turn as a 'panel' run so app.js sends mode=panel and the
        // backend uses num_predict=110 for shorter, snappier outputs.
        window.state = window.state || {};
        window.state.activePanelMode = 'panel';

        if (window.babaSetReplyTarget) window.babaSetReplyTarget(target, {
            onToken: (_chunk, full) => {
                target.textContent = full.trim();
            }
        });
        unlockTTS();
        const inp = document.getElementById('chatInput');
        if (inp) inp.value = cfg.prompt(fields);
        if (typeof window.askBaba === 'function') window.askBaba();
    });

    result.querySelector('.panel-replay')?.addEventListener('click', () => {
        unlockTTS();
        if (window._babaVoice && resultText.textContent) window._babaVoice.speak(resultText.textContent);
    });
    result.querySelector('.panel-save')?.addEventListener('click', (e) => {
        const text = resultText.textContent;
        if (!text) return;
        let arr = []; try { arr = JSON.parse(localStorage.getItem('baba_saved') || '[]'); } catch (_) { }
        const exists = arr.some(m => m.text === text);
        if (!exists) {
            arr.push({ text, at: Date.now() });
            localStorage.setItem('baba_saved', JSON.stringify(arr));
        }
        e.target.classList.add('saved');
        e.target.textContent = '⭐ Saved';
    });
    result.querySelector('.panel-copy')?.addEventListener('click', () => {
        navigator.clipboard?.writeText(resultText.textContent || '').catch(() => { });
    });
}

function bindListPanel(ov, mode, cfg) {
    ov.querySelectorAll('.pli-speak').forEach(b => b.addEventListener('click', () => {
        unlockTTS();
        if (window._babaVoice) window._babaVoice.speak(b.dataset.text || '');
    }));
    ov.querySelectorAll('.pli-copy').forEach(b => b.addEventListener('click', () => {
        navigator.clipboard?.writeText(b.dataset.text || '').catch(() => { });
    }));
    ov.querySelectorAll('.pli-delete').forEach(b => b.addEventListener('click', () => {
        const t = b.dataset.text || '';
        try {
            const arr = JSON.parse(localStorage.getItem(cfg.source) || '[]')
                .filter(m => m.text !== t);
            localStorage.setItem(cfg.source, JSON.stringify(arr));
        } catch (_) { }
        b.closest('.panel-list-item')?.remove();
    }));
    ov.querySelector('[data-panel-clear]')?.addEventListener('click', () => {
        if (!confirm('Clear all ' + cfg.title.toLowerCase() + '?')) return;
        localStorage.removeItem(cfg.source);
        ov.innerHTML = renderListPanel(mode, cfg);
        bindPanel(ov, mode, cfg);
    });
}

function compatibilityScoreCard(sign1, sign2) {
    if (!sign1 || !sign2) return null;
    // Deterministic score in [40..98] from the two signs.
    const seed = (sign1 + '|' + sign2).split('').reduce((a, c) => ((a << 5) - a + c.charCodeAt(0)) | 0, 0);
    const score = 40 + Math.abs(seed) % 59;
    const card = document.createElement('div');
    card.className = 'compat-score-card';
    card.innerHTML = `
        <div class="compat-score">
            <div class="compat-score-num">${score}<small>/100</small></div>
            <div class="compat-score-label">${chemistryLabel(score)}</div>
        </div>
        <div class="compat-bar"><span style="width:${score}%"></span></div>
        <div class="compat-pair"><span>${capitalize(sign1)}</span> ✦ <span>${capitalize(sign2)}</span></div>`;
    return card;
}
function chemistryLabel(n) {
    if (n >= 90) return 'Soulmates';
    if (n >= 75) return 'Strong chemistry';
    if (n >= 60) return 'Promising';
    if (n >= 50) return 'Workable';
    return 'Needs effort';
}
function capitalize(s) { return s ? s.charAt(0).toUpperCase() + s.slice(1) : ''; }

function escapeHtml(s) {
    return String(s).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;').replace(/'/g, '&#39;');
}
function escapeAttr(s) { return escapeHtml(String(s)); }

function formatTime(ts) {
    const d = new Date(ts);
    const same = (new Date()).toDateString() === d.toDateString();
    return same ? d.toLocaleTimeString(undefined, { hour: '2-digit', minute: '2-digit' })
                : d.toLocaleDateString(undefined, { month: 'short', day: 'numeric' }) + ' · ' +
                  d.toLocaleTimeString(undefined, { hour: '2-digit', minute: '2-digit' });
}
function closeAnyMobileDrawer() {
    document.querySelector('.sb-left')?.classList.remove('open');
    document.querySelector('.sb-right')?.classList.remove('open');
    document.querySelector('.panel-backdrop')?.classList.remove('open');
}
