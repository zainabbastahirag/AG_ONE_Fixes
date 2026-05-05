// ═══════════════════════════════════════════════════════════════
//  AI BABA-G — Frontend Logic (ES module)
//  Integrates persistent memory, vector recall, SSE streaming,
//  3D avatar with viseme lip-sync, custom personalities, and
//  ChatGPT-style interrupt with the existing mystical UI.
// ═══════════════════════════════════════════════════════════════
import { Avatar3D } from './avatar.js';
import { VoiceIO } from './voice.js';

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
    isSpeakingTts: false,
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

    // update central avatar figure
    const figureMap = { sage: '🧙‍♂️', philosopher: '🤔', healer: '🙏', elder: '👳', storyteller: '📖' };
    const fig = document.getElementById('avatarFigure');
    if (fig && !state.use3D) fig.textContent = figureMap[state.currentAvatar] || '🧙‍♂️';
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
    state.voice = new VoiceIO({
        onPartial: (t) => { document.getElementById('chatInput').value = t; },
        onFinal: (t) => {
            document.getElementById('chatInput').value = t;
            if (t.toLowerCase().includes('hey baba')) {
                document.getElementById('chatInput').value = t.replace(/hey baba/gi, '').trim();
            }
            if (document.getElementById('chatInput').value.trim()) askBaba();
        },
        onSpeakChunk: (chunk) => { state.avatar3d?.speakText(chunk); },
        onSpeakingDone: () => {
            showWave(false);
            document.getElementById('listeningText').classList.remove('visible');
            if (state.voice?.continuous) {
                document.getElementById('listeningText').classList.add('visible');
            }
        },
        getRate: () => parseFloat(document.getElementById('speedRange')?.value || '1'),
    });
}

window.toggleVoice = function () {
    if (!state.voice) initVoice();
    if (!state.voice.supported()) {
        alert('Voice input not supported in this browser. Use Chrome.');
        return;
    }
    if (state.voice.listening) {
        state.voice.stopListening();
        document.getElementById('micBtn').classList.remove('recording');
        document.getElementById('listeningText').classList.remove('visible');
    } else {
        state.voice.startListening();
        document.getElementById('micBtn').classList.add('recording');
        document.getElementById('listeningText').classList.add('visible');
        state.avatar3d?.setListening(true);
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
    if (state.abortController) { state.abortController.abort(); state.abortController = null; }
    state.voice?.stopSpeaking();
    state.avatar3d?.stopSpeaking();
    document.getElementById('avatarFigure')?.classList.remove('speaking');
    document.getElementById('askBtn').hidden = false;
    document.getElementById('stopBtn').hidden = true;
    showWave(false);
};

window.onInputKey = function (e) {
    if (e.key === 'Enter') { e.preventDefault(); askBaba(); }
    if (e.key === 'Escape') { window.stopAll(); }
};

// ───────────────────────────────────────────────────────────────
//  Ask Baba  — chooses streaming SSE or legacy /api/ask
// ───────────────────────────────────────────────────────────────
async function askBaba() {
    if (state.abortController) return;
    if (!state.voice) initVoice();
    const input = document.getElementById('chatInput');
    const prompt = input.value.trim();
    if (!prompt) return;

    input.value = '';
    setBabaText('Thinking...');
    showWave(true);
    state.voice?.stopSpeaking();
    state.avatar3d?.stopSpeaking();

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
        if (err.name !== 'AbortError') {
            console.error(err);
            setBabaText('The connection to wisdom was lost. Please try again.');
        }
    } finally {
        state.abortController = null;
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
    const data = await res.json();
    if (data.success) {
        setBabaText(data.response);
        state.voice?.speak(data.response);
    } else {
        setBabaText('Something went wrong... try again, seeker.');
    }
}

async function streamingAsk(prompt) {
    const isAuthed = !!state.user;
    const url = isAuthed ? '/api/chat/stream' : '/api/chat/guest/stream';
    const body = isAuthed
        ? { message: prompt, conversationId: state.conversationId, personalityId: state.currentPersonalityId, avatar: state.currentAvatar, mindset: state.currentMindset }
        : { message: prompt, personalityId: state.currentPersonalityId, avatar: state.currentAvatar, mindset: state.currentMindset };

    state.abortController = new AbortController();
    const headers = { 'Content-Type': 'application/json' };
    if (state.token) headers['Authorization'] = 'Bearer ' + state.token;
    const res = await fetch(url, { method: 'POST', headers, body: JSON.stringify(body), signal: state.abortController.signal });
    if (!res.ok || !res.body) {
        setBabaText(`Connection failed (${res.status}).`);
        return;
    }

    setBabaText('');
    const babaTextEl = document.getElementById('babaText');
    babaTextEl.classList.add('cursor-blink');

    const reader = res.body.getReader();
    const decoder = new TextDecoder();
    let buf = '';
    let full = '';
    while (true) {
        const { value, done } = await reader.read();
        if (done) break;
        buf += decoder.decode(value, { stream: true });
        let idx;
        while ((idx = buf.indexOf('\n\n')) >= 0) {
            const block = buf.slice(0, idx); buf = buf.slice(idx + 2);
            let evt = 'message', data = '';
            for (const line of block.split('\n')) {
                if (line.startsWith('event:')) evt = line.slice(6).trim();
                else if (line.startsWith('data:')) data += line.slice(5).trimStart();
            }
            data = data.replace(/\\n/g, '\n');
            if (evt === 'token') {
                full += data;
                babaTextEl.textContent = full;
                state.voice?.pushStreamingText(data);
            } else if (evt === 'meta') {
                try { const m = JSON.parse(data); if (m.conversationId) { state.conversationId = m.conversationId; loadConversations(); } } catch (_) { }
            } else if (evt === 'error') {
                full += `\n[error: ${data}]`; babaTextEl.textContent = full;
            }
        }
    }
    babaTextEl.classList.remove('cursor-blink');
    state.voice?.flushStreaming();
}

// ───────────────────────────────────────────────────────────────
//  UI helpers
// ───────────────────────────────────────────────────────────────
function setBabaText(text) {
    const el = document.getElementById('babaText');
    if (el) { el.classList.remove('cursor-blink'); el.textContent = text; }
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
        setBabaText(last?.role === 'assistant' ? last.content : 'Continuing our conversation, seeker...');
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
    document.getElementById('speedVal').textContent = parseFloat(e.target.value).toFixed(1) + 'x';
});

document.getElementById('continuousMode')?.addEventListener('change', (e) => {
    if (!state.voice) initVoice();
    state.voice.setContinuous(e.target.checked);
    if (e.target.checked) document.getElementById('listeningText').classList.add('visible');
    else document.getElementById('listeningText').classList.remove('visible');
});

document.getElementById('newChatBtn')?.addEventListener('click', () => {
    state.conversationId = null;
    setBabaText('A fresh canvas, seeker. What is on your mind?');
});

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
});
