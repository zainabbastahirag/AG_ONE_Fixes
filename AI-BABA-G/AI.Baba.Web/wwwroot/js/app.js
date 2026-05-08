// ═══════════════════════════════════════════════════════════════
//  AI BABA-G — Frontend Logic (ES module)
//  Persistent memory, vector recall, SSE streaming, 3D avatar with
//  viseme lip-sync, custom personalities, and ChatGPT-style interrupt
//  with the existing mystical UI.
// ═══════════════════════════════════════════════════════════════
import { Avatar3D } from './avatar.js';
import { VoiceIO, unlockTTS } from './voice.js';
import { renderMarkdown, plainText } from './format.js';
import { initWorkspaces, setWorkspace, workspaceForAvatar } from './workspaces.js';

const state = {
    currentAvatar: 'sage',
    currentMindset: 'balanced',
    currentPersonalityId: null,
    token: localStorage.getItem('baba_token'),
    user: JSON.parse(localStorage.getItem('baba_user') || 'null'),
    conversationId: null,
    streaming: true,
    use3D: false,
    avatar3d: null,
    voice: null,
    abortController: null,
    inFlight: false,
    voiceProfile: localStorage.getItem('baba_voice_profile') || 'guru',
    autoSpeak: localStorage.getItem('baba_autospeak') !== '0',
    // Workspaces can redirect the streaming AI reply to their own report
    // panel so users see structured reports instead of just a chat bubble.
    replyTarget: null,                       // DOM element receiving rendered markdown
    replyTokenHandler: null,                 // optional (rawToken, fullText) => void
};

// Used by workspaces to intercept the streamed reply.
window.babaSetReplyTarget = function (target, opts = {}) {
    state.replyTarget = target || null;
    state.replyTokenHandler = (typeof opts.onToken === 'function') ? opts.onToken : null;
};

function saveAuth() {
    if (state.token) localStorage.setItem('baba_token', state.token); else localStorage.removeItem('baba_token');
    if (state.user) localStorage.setItem('baba_user', JSON.stringify(state.user)); else localStorage.removeItem('baba_user');
}

const api = {
    async fetch(path, opts = {}) {
        const headers = new Headers(opts.headers || {});
        if (state.token) headers.set('Authorization', 'Bearer ' + state.token);
        if (opts.body && !(opts.body instanceof FormData) && !headers.has('Content-Type'))
            headers.set('Content-Type', 'application/json');
        const res = await fetch(path, { ...opts, headers });
        if (res.status === 401) {
            state.token = null; state.user = null; saveAuth(); renderAuthPill();
        }
        return res;
    },
    async json(path, opts) {
        const r = await this.fetch(path, opts);
        if (!r.ok) { let msg = r.statusText; try { msg = (await r.json()).error || msg; } catch (_) { } throw new Error(msg); }
        return r.json();
    }
};

// ───────────────────────────────────────────────────────────────
//  Selection (avatar / mindset)  — kept compatible with inline HTML
// ───────────────────────────────────────────────────────────────
window.selectAvatar = function (el) {
    if (!el) return;
    document.querySelectorAll('.avatar-card').forEach(c => {
        c.classList.remove('active');
        c.querySelector('.avatar-check')?.remove();
    });
    el.classList.add('active');
    state.currentAvatar = el.dataset.avatar;
    const img = el.querySelector('.avatar-img');
    const check = document.createElement('div');
    check.className = 'avatar-check';
    check.textContent = '✓';
    img.appendChild(check);

    const figureMap = {
        sage: '🧙‍♂️', philosopher: '🤔', healer: '🙏', elder: '👳', storyteller: '📖',
        designer: '🎨', developer: '💻', pm: '📋', marketing: '📣', sales: '💼', hr: '🤝',
        astrologer: '🔮',
    };
    const fig = document.getElementById('avatarFigure');
    if (fig && !state.use3D) fig.textContent = figureMap[state.currentAvatar] || '🧙‍♂️';

    // Map avatars → recommended voice profile (user can still override).
    const profileMap = {
        sage: 'guru', elder: 'guru', philosopher: 'expert', healer: 'gentle',
        storyteller: 'expert', designer: 'gentle', developer: 'expert',
        pm: 'expert', marketing: 'gentle', sales: 'expert', hr: 'gentle',
        astrologer: 'guru',
    };
    const sel = document.getElementById('voiceSelect');
    if (sel && !sel.dataset.userPicked) {
        const recommended = profileMap[state.currentAvatar];
        if (recommended) {
            sel.value = recommended;
            state.voiceProfile = recommended;
        }
    }

    // Swap the workspace below the bubble to match the role.
    const ws = workspaceForAvatar(state.currentAvatar);
    setWorkspace(ws);
    document.querySelector('.app-grid')?.setAttribute('data-mode', ws);
    document.body.setAttribute('data-mode', ws);
    // When entering chat mode, restore the bubble as the reply target.
    if (ws === 'chat') {
        window.babaSetReplyTarget(null);
    } else if (ws !== 'astrology') {
        // For pro modes (except astrology, which manages its own targets),
        // default the report panel as the streaming reply target so a free-
        // form question typed into the chat input also lands in the report.
        const target = document.querySelector('#workspace .ws-report-target');
        if (target) window.babaSetReplyTarget(target);
    }
};

window.selectMindset = function (el) {
    document.querySelectorAll('.mindset-card').forEach(c => {
        c.classList.remove('active');
        c.querySelector('.mindset-check')?.remove();
    });
    el.classList.add('active');
    state.currentMindset = el.dataset.mindset;
    const check = document.createElement('div');
    check.className = 'mindset-check';
    check.textContent = '✓';
    el.appendChild(check);
};

// ───────────────────────────────────────────────────────────────
//  Voice (STT + TTS)
// ───────────────────────────────────────────────────────────────
function initVoice() {
    if (state.voice) return;
    state.voice = new VoiceIO({
        onPartial: (t) => {
            const inp = document.getElementById('chatInput');
            if (inp) inp.value = t;
        },
        onFinal: (t) => {
            const inp = document.getElementById('chatInput');
            if (!inp) return;
            inp.value = t;
            if (inp.value.trim()) askBaba();
        },
        onSpeakChunk: (chunk) => { state.avatar3d?.speakText(chunk); },
        onSpeakingStart: () => {
            document.getElementById('listeningText')?.classList.remove('visible');
        },
        onSpeakingDone: () => {
            showWave(false);
            if (state.voice?.continuous && !state.voice?.userMutedMic) {
                document.getElementById('listeningText')?.classList.add('visible');
            }
        },
        onUnsupported: (msg) => {
            const hint = document.getElementById('voiceHint');
            if (hint) { hint.textContent = msg; hint.classList.add('visible'); }
            const micBtn = document.getElementById('micBtn');
            if (micBtn) {
                micBtn.classList.add('disabled');
                micBtn.setAttribute('aria-disabled', 'true');
                micBtn.title = msg;
            }
        },
        getRate: () => parseFloat(document.getElementById('speedRange')?.value || '1'),
        getProfile: () => localStorage.getItem('baba_voice_profile') || state.voiceProfile,
    });
    // If recognition isn't supported, mark mic disabled but keep TTS available.
    if (!state.voice.supported()) {
        const micBtn = document.getElementById('micBtn');
        if (micBtn) {
            micBtn.classList.add('disabled');
            micBtn.title = state.voice.isMobile()
                ? 'Voice input unavailable on this browser. You can still type — the bot will speak its reply.'
                : 'Voice input is not supported in this browser.';
        }
    }
    // Expose for baba.js (voice-call modal, replay buttons, etc.)
    window._babaVoice = state.voice;
}

window.toggleVoice = function (ev) {
    // CRITICAL: this must run synchronously inside the user-gesture handler.
    // No async work before .startListening() — Safari/iOS will silently
    // reject mic permission otherwise.
    initVoice();
    unlockTTS();
    if (!state.voice.supported()) {
        const hint = document.getElementById('voiceHint');
        const msg = state.voice.isMobile()
            ? 'Voice input is unavailable on this browser. Please type — the bot will speak its reply.'
            : 'Voice input is not supported in this browser. Please use Chrome, Edge, or Samsung Internet.';
        if (hint) { hint.textContent = msg; hint.classList.add('visible'); }
        return;
    }
    if (state.voice.listening) {
        state.voice.stopListening();
        document.getElementById('micBtn')?.classList.remove('recording');
        document.getElementById('listeningText')?.classList.remove('visible');
    } else {
        const ok = state.voice.startListening();
        if (ok) {
            document.getElementById('micBtn')?.classList.add('recording');
            document.getElementById('listeningText')?.classList.add('visible');
            state.avatar3d?.setListening(true);
        }
    }
};

window.toggle3D = function () {
    const box = document.getElementById('toggle3DBox');
    state.use3D = box ? box.checked : !state.use3D;
    if (box) box.checked = state.use3D;
    const canvas = document.getElementById('avatar3d');
    const fig = document.getElementById('avatarFigure');
    if (state.use3D) {
        canvas.hidden = false; fig.style.opacity = '0';
        if (!state.avatar3d) state.avatar3d = new Avatar3D(canvas);
    } else {
        canvas.hidden = true; fig.style.opacity = '1';
    }
};

window.stopAll = function () {
    if (state.abortController) {
        try { state.abortController.abort(); } catch (_) { }
        state.abortController = null;
    }
    state.voice?.stopSpeaking();
    state.avatar3d?.stopSpeaking();
    document.getElementById('avatarFigure')?.classList.remove('speaking');
    document.getElementById('askBtn').hidden = false;
    document.getElementById('stopBtn').hidden = true;
    showWave(false);
    state.inFlight = false;
};

window.onInputKey = function (e) {
    if (e.key === 'Enter') { e.preventDefault(); askBaba(); }
    if (e.key === 'Escape') { window.stopAll(); }
};

// ───────────────────────────────────────────────────────────────
//  Ask Baba  — chooses streaming SSE or legacy /api/ask
// ───────────────────────────────────────────────────────────────
async function askBaba() {
    if (state.inFlight) return;                 // hard guard against double-submits
    initVoice();
    const input = document.getElementById('chatInput');
    const prompt = input.value.trim();
    if (!prompt) return;

    input.value = '';
    state.inFlight = true;

    // Render the user's question + create a bot bubble for the streaming reply.
    if (window.babaUi) {
        try { window.babaUi.onUserSend(prompt); } catch (_) { }
        try {
            const target = window.babaUi.startBabaTurn();
            if (target) window.babaSetReplyTarget(target);
        } catch (_) { }
        try { window.babaUi.setBubble('Hmm…'); } catch (_) { }
    }

    // Stop any in-flight TTS so the new question takes priority (interrupt).
    state.voice?.stopSpeaking();
    state.avatar3d?.stopSpeaking();

    showTypingDots();
    showWave(true);

    state.streaming = document.getElementById('streamMode')?.checked ?? true;

    document.getElementById('askBtn').hidden = true;
    document.getElementById('stopBtn').hidden = false;

    try {
        if (state.streaming) {
            await streamingAsk(prompt);
        } else {
            await legacyAsk(prompt);
        }
    } catch (err) {
        if (err?.name !== 'AbortError') {
            console.error(err);
            const msg = String(err?.message || '').slice(0, 200);
            setBabaText(msg
                ? `I'm having trouble reaching the wisdom servers (${msg}). Please try again in a moment.`
                : 'The connection to wisdom was lost. Please try again.');
        }
    } finally {
        state.abortController = null;
        state.inFlight = false;
        document.getElementById('askBtn').hidden = false;
        document.getElementById('stopBtn').hidden = true;
        showWave(false);
    }
}
window.askBaba = askBaba;

async function legacyAsk(prompt) {
    const res = await api.fetch('/api/ask', {
        method: 'POST',
        body: JSON.stringify({
            prompt,
            avatar: state.currentAvatar,
            mindset: state.currentMindset
        })
    });
    let data; try { data = await res.json(); } catch (_) { data = {}; }
    if (res.ok && data.success) {
        setBabaMarkdown(data.response);
        if (state.autoSpeak) state.voice?.speak(plainText(data.response));
    } else {
        const err = data?.error || `Server returned ${res.status}.`;
        setBabaText(err);
    }
}

async function streamingAsk(prompt) {
    const isAuthed = !!state.user;
    const url = isAuthed ? '/api/chat/stream' : '/api/chat/guest/stream';
    // Tell the server which mode this turn is in:
    //   'voice' — call modal is open; replies must be 1–2 sentences (num_predict=64).
    //   'panel' — fired from one of the sidebar panels; 2–3 sentences (num_predict=110).
    //   undefined — normal chat (current Ollama defaults).
    const mode = window.babaCall?.isOpen?.() ? 'voice' :
                 (state.activePanelMode || undefined);
    const body = isAuthed
        ? { message: prompt, conversationId: state.conversationId, personalityId: state.currentPersonalityId, avatar: state.currentAvatar, mindset: state.currentMindset, mode }
        : { message: prompt, personalityId: state.currentPersonalityId, avatar: state.currentAvatar, mindset: state.currentMindset, mode };

    state.abortController = new AbortController();
    const headers = { 'Content-Type': 'application/json' };
    if (state.token) headers['Authorization'] = 'Bearer ' + state.token;

    showTypingDots();

    let res;
    try {
        res = await fetch(url, { method: 'POST', headers, body: JSON.stringify(body), signal: state.abortController.signal });
    } catch (e) {
        if (e?.name === 'AbortError') return;
        setBabaText('Could not reach the server. Please check your connection and try again.');
        return;
    }

    if (!res.ok || !res.body) {
        // Try to surface a friendly error from JSON-shaped responses.
        let friendly = `Connection failed (${res.status}).`;
        try {
            const j = await res.clone().json();
            if (j?.error) friendly = j.error;
        } catch (_) { }
        if (res.status === 429) friendly = 'You\'re asking very quickly. Please wait a moment and try again.';
        if (res.status === 503) friendly = 'The service is starting up. Please try again in a few seconds.';
        setBabaText(friendly);
        return;
    }

    const babaTextEl = getReplyEl();
    let firstToken = true;

    const reader = res.body.getReader();
    const decoder = new TextDecoder();
    let buf = '';
    let full = '';            // raw markdown accumulated
    let spokenSoFar = '';     // plain text already pushed to TTS

    let renderScheduled = false;
    const scheduleRender = () => {
        if (renderScheduled) return;
        renderScheduled = true;
        requestAnimationFrame(() => {
            renderScheduled = false;
            babaTextEl.innerHTML = renderMarkdown(full);
            babaTextEl.classList.add('cursor-blink');
            scrollBubbleToEnd();
        });
    };

    let serverDone = false;
    while (!serverDone) {
        let chunk;
        try {
            chunk = await reader.read();
        } catch (e) {
            if (e?.name === 'AbortError') break;
            throw e;
        }
        const { value, done } = chunk;
        if (done) break;
        buf += decoder.decode(value, { stream: true });
        let idx;
        while ((idx = buf.indexOf('\n\n')) >= 0) {
            const block = buf.slice(0, idx); buf = buf.slice(idx + 2);
            let evt = 'message', dataLines = [];
            for (const line of block.split('\n')) {
                if (line.startsWith('event:')) {
                    // 'event:' field — trim a single optional leading space + trailing CR
                    evt = line.slice(6).replace(/^ /, '').replace(/\r$/, '');
                } else if (line.startsWith('data:')) {
                    // 'data:' field — per the SSE spec strip EXACTLY ONE leading space.
                    // (The previous code used trimStart() which ate every leading space,
                    // welding tokens like ' the' / ' however' / a literal ' ' to the
                    // previous word and producing 'That'sverykindofyou,butIdon't...'.)
                    let v = line.slice(5);
                    if (v.startsWith(' ')) v = v.slice(1);
                    if (v.endsWith('\r')) v = v.slice(0, -1);
                    dataLines.push(v);
                }
            }
            // Per spec: when multiple 'data:' lines appear in one event, join with '\n'.
            let data = dataLines.join('\n');
            data = data.replace(/\\n/g, '\n');
            if (evt === 'ack') {
                showTypingDots();
            } else if (evt === 'token') {
                if (firstToken) {
                    firstToken = false;
                    babaTextEl.innerHTML = '';
                    babaTextEl.classList.add('formatted');
                }
                full += data;
                scheduleRender();
                if (state.replyTokenHandler) {
                    try { state.replyTokenHandler(data, full); } catch (_) { }
                }
                // Pipe into the voice-call modal bubble too, when it's open.
                if (window.babaCall?.isOpen?.()) {
                    try { window.babaCall.onTokenChunk(full); } catch (_) { }
                }
                // Update the centered hero bubble with the latest line.
                if (window.babaUi?.setBubble) {
                    try { window.babaUi.setBubble(full.slice(-180)); } catch (_) { }
                }

                if (localStorage.getItem('baba_autospeak') !== '0') {
                    // Feed the TTS only NEW plain-text since last push, so it
                    // never speaks markdown punctuation like '**', '###', '`'.
                    const plainAll = plainText(full);
                    if (plainAll.length > spokenSoFar.length && plainAll.startsWith(spokenSoFar)) {
                        const delta = plainAll.slice(spokenSoFar.length);
                        spokenSoFar = plainAll;
                        if (delta) state.voice?.pushStreamingText(delta);
                    } else if (plainAll !== spokenSoFar) {
                        // Rare: plainText collapsed earlier whitespace; resync
                        // without re-speaking what we already spoke.
                        spokenSoFar = plainAll;
                    }
                }
            } else if (evt === 'meta') {
                try { const m = JSON.parse(data); if (m.conversationId) { state.conversationId = m.conversationId; loadConversations(); } } catch (_) { }
            } else if (evt === 'error') {
                full += `\n\n_[error: ${data}]_`; scheduleRender();
            } else if (evt === 'done') {
                serverDone = true;
                try { reader.cancel(); } catch (_) { }
                break;
            }
        }
    }
    // Final render without the typing cursor.
    babaTextEl.innerHTML = renderMarkdown(full) || babaTextEl.innerHTML;
    babaTextEl.classList.remove('cursor-blink');
    scrollBubbleToEnd();
    if (localStorage.getItem('baba_autospeak') !== '0') state.voice?.flushStreaming();
}

function showTypingDots() {
    const el = getReplyEl();
    if (!el) return;
    el.innerHTML = '<span class="typing-dots"><span></span><span></span><span></span></span>';
}

function scrollBubbleToEnd() {
    const bubble = document.getElementById('speechBubble');
    if (!bubble) return;
    bubble.scrollTop = bubble.scrollHeight;
}

// ───────────────────────────────────────────────────────────────
//  UI helpers
// ───────────────────────────────────────────────────────────────
function getReplyEl() {
    return state.replyTarget || document.getElementById('babaText');
}

function setBabaText(text) {
    const el = getReplyEl();
    if (el) {
        el.classList.remove('cursor-blink', 'formatted');
        el.textContent = text;
    }
    scrollBubbleToEnd();
}

function setBabaMarkdown(text) {
    const el = getReplyEl();
    if (el) {
        el.classList.remove('cursor-blink');
        el.innerHTML = renderMarkdown(text);
        el.classList.add('formatted');
    }
    scrollBubbleToEnd();
}

function showWave(show) {
    document.getElementById('voiceWave')?.classList.toggle('active', show);
}

// ───────────────────────────────────────────────────────────────
//  Auth pill in nav
// ───────────────────────────────────────────────────────────────
function renderAuthPill() {
    const el = document.getElementById('authPill');
    if (!el) return;
    if (state.user) {
        el.textContent = state.user.displayName || state.user.username;
        el.title = 'Click to sign out';
        el.onclick = () => {
            if (!confirm('Sign out?')) return;
            state.token = null; state.user = null; saveAuth(); renderAuthPill();
            state.conversationId = null;
            loadConversations();
            updateGuestHint();
        };
    } else {
        el.textContent = 'Sign in / up';
        el.title = 'Sign in or create an account';
        el.onclick = () => { window.location.href = '/Home/Auth'; };
    }
    updateGuestHint();
}

function updateGuestHint() {
    const hint = document.getElementById('guestHint');
    if (hint) hint.style.display = state.user ? 'none' : 'block';
}

// ───────────────────────────────────────────────────────────────
//  Conversations sidebar (signed-in only)
// ───────────────────────────────────────────────────────────────
async function loadConversations() {
    const list = document.getElementById('conversationList');
    if (!list) return;
    list.innerHTML = '';
    if (!state.user) {
        const li = document.createElement('li'); li.className = 'conv-empty';
        li.textContent = 'Sign in to save chats';
        list.appendChild(li); return;
    }
    try {
        const items = await api.json('/api/chat/conversations');
        if (!items.length) {
            const li = document.createElement('li'); li.className = 'conv-empty';
            li.textContent = 'No chats yet';
            list.appendChild(li); return;
        }
        for (const c of items) {
            const li = document.createElement('li');
            li.textContent = c.title;
            if (c.id === state.conversationId) li.classList.add('active');
            li.title = new Date(c.updatedAt).toLocaleString();
            const del = document.createElement('span'); del.className = 'del'; del.textContent = '✕';
            del.onclick = async (e) => {
                e.stopPropagation();
                if (!confirm('Delete this conversation?')) return;
                await api.fetch('/api/chat/conversations/' + c.id, { method: 'DELETE' });
                if (state.conversationId === c.id) state.conversationId = null;
                loadConversations();
            };
            li.appendChild(del);
            li.addEventListener('click', () => loadConversation(c.id));
            list.appendChild(li);
        }
    } catch (e) { console.warn(e); }
}

async function loadConversation(id) {
    try {
        const c = await api.json('/api/chat/conversations/' + id);
        state.conversationId = c.id;
        if (c.avatarKey) {
            const card = document.querySelector(`.avatar-card[data-avatar="${c.avatarKey}"]`);
            if (card) window.selectAvatar(card);
        }
        if (c.mindsetKey) {
            const card = document.querySelector(`.mindset-card[data-mindset="${c.mindsetKey}"]`);
            if (card) window.selectMindset(card);
        }
        const last = c.messages?.[c.messages.length - 1];
        if (last?.role === 'assistant') setBabaMarkdown(last.content);
        else setBabaText('Continuing our conversation, seeker...');
        loadConversations();
    } catch (e) { console.warn(e); }
}

// ───────────────────────────────────────────────────────────────
//  Topic tags & speed slider (legacy hooks)
// ───────────────────────────────────────────────────────────────
function wireTopicTags() {
    document.querySelectorAll('.tag').forEach(tag => {
        tag.addEventListener('click', () => {
            const topic = tag.textContent.replace(/^[^\s]+\s/, '');
            const input = document.getElementById('chatInput');
            if (input) { input.value = `Tell me about ${topic}`; input.focus(); }
        });
    });
}

document.getElementById('speedRange')?.addEventListener('input', (e) => {
    const v = parseFloat(e.target.value).toFixed(1);
    const el = document.getElementById('speedVal');
    if (el) el.textContent = v + 'x';
});

document.getElementById('continuousMode')?.addEventListener('change', (e) => {
    initVoice();
    state.voice.setContinuous(e.target.checked, { wakeWord: false });
    document.getElementById('listeningText')?.classList.toggle('visible', !!e.target.checked);
});

document.getElementById('voiceSelect')?.addEventListener('change', (e) => {
    state.voiceProfile = e.target.value;
    e.target.dataset.userPicked = '1';
    localStorage.setItem('baba_voice_profile', state.voiceProfile);
});

document.getElementById('autoSpeak')?.addEventListener('change', (e) => {
    state.autoSpeak = !!e.target.checked;
    localStorage.setItem('baba_autospeak', state.autoSpeak ? '1' : '0');
    if (!state.autoSpeak) state.voice?.stopSpeaking();
});

document.getElementById('newChatBtn')?.addEventListener('click', () => {
    state.conversationId = null;
    setBabaText('A fresh canvas, seeker. What is on your mind?');
});

// Drawer behavior is now owned by baba.js (which targets the new .sb-left /
// .sb-right sidebars). Kept as a no-op for backward compatibility.
function setupMobileDrawers() { /* baba.js owns this now */ }

// ───────────────────────────────────────────────────────────────
//  Init
// ───────────────────────────────────────────────────────────────
window.addEventListener('load', () => {
    if ('speechSynthesis' in window) {
        window.speechSynthesis.getVoices();
        window.speechSynthesis.onvoiceschanged = () => window.speechSynthesis.getVoices();
    }
    initVoice();
    wireTopicTags();
    renderAuthPill();
    loadConversations();
    setupMobileDrawers();

    // Mic button — bind synchronously to both pointer/touch events so iOS
    // and Android receive the user-gesture token when permission is asked.
    const micBtn = document.getElementById('micBtn');
    if (micBtn) {
        const handler = (e) => { e.preventDefault(); window.toggleVoice(e); };
        micBtn.addEventListener('click', handler);
        micBtn.addEventListener('touchend', handler, { passive: false });
    }
    // Make the ASK button also unlock TTS so the first reply speaks on iOS.
    document.getElementById('askBtn')?.addEventListener('click', () => unlockTTS());

    // Workspaces — driven by avatar selection. Each workspace's "send"
    // routes through the existing chat flow so replies stream into the
    // bubble and speak out loud just like a normal turn.
    const wsRoot = document.getElementById('workspace');
    if (wsRoot) {
        initWorkspaces({
            root: wsRoot,
            ask: (prompt) => {
                const inp = document.getElementById('chatInput');
                if (inp) inp.value = prompt;
                askBaba();
            },
            setBubbleText: setBabaText,
            setBubbleMarkdown: setBabaMarkdown,
        });
    }

    // Restore persisted UI prefs
    const sel = document.getElementById('voiceSelect');
    if (sel && state.voiceProfile) sel.value = state.voiceProfile;
    const auto = document.getElementById('autoSpeak');
    if (auto) auto.checked = state.autoSpeak;
});
