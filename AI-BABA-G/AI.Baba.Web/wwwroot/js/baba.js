// ═══════════════════════════════════════════════════════════════════════
//  AI BABA G — UI shell module
//  Owns: sidebar mode buttons, voice-call fullscreen modal, message-feed
//  bubble rendering, daily-streak counter, premium / BuyMeACoffee modal,
//  mobile drawer behavior. The streaming AI + STT/TTS plumbing lives in
//  app.js / voice.js.
// ═══════════════════════════════════════════════════════════════════════

import { unlockTTS } from './voice.js';

// ─── Streak ───────────────────────────────────────────────────────────
//   On first paint each calendar day we increment the streak; if more
//   than one day was missed we reset to 1.
function updateStreak() {
    const todayKey = new Date().toISOString().slice(0, 10);
    let streak = parseInt(localStorage.getItem('baba_streak') || '0', 10);
    const last = localStorage.getItem('baba_streak_last');
    if (last !== todayKey) {
        if (last) {
            const prev = new Date(last);
            const today = new Date(todayKey);
            const diffDays = Math.round((today - prev) / 86_400_000);
            streak = diffDays === 1 ? streak + 1 : 1;
        } else {
            streak = 1;
        }
        localStorage.setItem('baba_streak', String(streak));
        localStorage.setItem('baba_streak_last', todayKey);
    } else if (!streak) {
        streak = 1; localStorage.setItem('baba_streak', '1');
    }
    const el = document.getElementById('streakCount');
    if (el) el.textContent = streak;
}

// ─── Modes ────────────────────────────────────────────────────────────
//   Each mode is a structured prompt that goes through the existing
//   /api/chat/* SSE pipeline. Reply renders into a message bubble in
//   the feed (and is spoken by the TTS).
const MODE_PROMPTS = {
    chat: null,       // open-ended chat, no preset
    call: '__OPEN_CALL__',
    daily:        'Give me today\'s daily guidance and one specific focus to work on for today. Speak warmly, like an old friend. 3 short sentences.',
    predictions:  'Tell me what the next 7 days look like for my energy, work, and relationships. Speak like a wise seer in 4 short sentences.',
    compatibility:'I want a compatibility reading. Ask me my zodiac sign and a partner / friend\'s zodiac sign first, then once I share, give a short compatibility analysis. 3 short sentences when you analyze.',
    'future-me':  'Speak as my future self in 10 years from now, sending a message back in time to me today. Be wise, kind, and concrete. 4 short sentences.',
    history:      '__HISTORY__',
    saved:        '__SAVED__',
    roast:        'Roast me with love, Baba G style. 4 short playful lines about my procrastination, perfectionism, or scrolling habits — keep it warm and funny, never mean.',
    dream:        'Interpret a dream for me. Ask me to describe my recent dream first, then once I share, give a short interpretation focused on what it might mean for my life right now. 4 short sentences when you interpret.',
};

const MODE_TITLES = {
    chat: 'Chat with Baba G',
    daily: 'Daily Guidance',
    predictions: 'Predictions',
    compatibility: 'Compatibility',
    'future-me': 'Future Me',
    roast: 'Roast Me',
    dream: 'Dream Interpreter',
};

const MODE_OPENERS = {
    daily: 'Daily Guidance — let me see what the universe whispers for you today, mere dost…',
    predictions: 'Predictions — closing my eyes for a moment, let me see the road ahead…',
    compatibility: 'Compatibility — first tell me your sign, and the sign of the one you\'re curious about.',
    'future-me': 'Future Me — let me dial through to your future self, 10 years on…',
    roast: 'Roast mode — beta, ready ho? Don\'t cry now 😄',
    dream: 'Dream Interpreter — describe what you remember and Baba will read between the symbols.',
};

function applyModeSelection(modeCmd) {
    if (!modeCmd || modeCmd === 'history' || modeCmd === 'saved') return;  // navigated separately
    document.querySelectorAll('.sb-nav-item').forEach(b => {
        b.classList.toggle('active', b.dataset.modeCmd === modeCmd);
    });
}

function runMode(modeCmd) {
    if (modeCmd === 'call') { openCall(); return; }
    if (modeCmd === 'history') { showHistory(); return; }
    if (modeCmd === 'saved') { showSaved(); return; }
    applyModeSelection(modeCmd);
    closeMobileDrawers();

    const prompt = MODE_PROMPTS[modeCmd];
    if (!prompt) return;
    // Show an opener bubble + send the prompt.
    const opener = MODE_OPENERS[modeCmd];
    if (opener) addBabaMessage(opener, { mode: modeCmd, opener: true });
    setBubbleText('Hmm… let Baba G think for a moment.');
    const inp = document.getElementById('chatInput');
    if (inp) inp.value = prompt;
    if (typeof window.askBaba === 'function') window.askBaba();
}

// ─── Message feed ─────────────────────────────────────────────────────
function timeNow() {
    const d = new Date();
    return d.toLocaleTimeString(undefined, { hour: '2-digit', minute: '2-digit' });
}

function addUserMessage(text) {
    const feed = document.getElementById('msgFeed');
    if (!feed) return;
    const node = document.createElement('article');
    node.className = 'msg msg-user';
    node.innerHTML = `
        <div class="msg-bubble">
            <button class="msg-play" type="button" aria-label="Play voice"><span>▶</span></button>
            <div class="msg-wave" aria-hidden="true">${'<span></span>'.repeat(28)}</div>
            <span class="msg-time">${timeNow()}</span>
            <p class="msg-text"></p>
        </div>
        <img class="msg-avatar" src="/img/avatar-user.svg" alt="You" />`;
    node.querySelector('.msg-text').textContent = text;
    feed.appendChild(node);
    feed.scrollTop = feed.scrollHeight;
    saveHistory(text, 'user');
}

function addBabaMessage(text, opts = {}) {
    const feed = document.getElementById('msgFeed');
    if (!feed) return null;
    const node = document.createElement('article');
    node.className = 'msg msg-baba' + (opts.opener ? ' opener' : '');
    node.innerHTML = `
        <img class="msg-avatar" src="/img/baba-g.png" alt="Baba G" />
        <div class="msg-bubble">
            <button class="msg-play" type="button" aria-label="Play voice"><span>▶</span></button>
            <div class="msg-wave" aria-hidden="true">${'<span></span>'.repeat(28)}</div>
            <span class="msg-time">${timeNow()}</span>
            <p class="msg-text"></p>
        </div>`;
    const textEl = node.querySelector('.msg-text');
    textEl.textContent = text;
    feed.appendChild(node);
    feed.scrollTop = feed.scrollHeight;
    if (!opts.opener) saveHistory(text, 'baba');
    // Allow user to "play" any prior reply through TTS again.
    node.querySelector('.msg-play')?.addEventListener('click', () => {
        unlockTTS();
        if (window._babaVoice && typeof window._babaVoice.speak === 'function') {
            window._babaVoice.speak(textEl.textContent);
        }
    });
    return textEl;
}

function setBubbleText(text) {
    const bubble = document.getElementById('heroBubble');
    const t = document.getElementById('bubbleText');
    if (!bubble || !t) return;
    bubble.hidden = false;
    t.textContent = text;
}

function setHeroStatus(text) {
    const el = document.getElementById('heroStatusText');
    if (el) el.textContent = text;
}

// ─── Light-weight history (localStorage) ──────────────────────────────
function saveHistory(text, role) {
    try {
        const arr = JSON.parse(localStorage.getItem('baba_history') || '[]');
        arr.push({ role, text, at: Date.now() });
        if (arr.length > 200) arr.splice(0, arr.length - 200);
        localStorage.setItem('baba_history', JSON.stringify(arr));
    } catch (_) { }
}

function loadHistory() {
    try { return JSON.parse(localStorage.getItem('baba_history') || '[]'); }
    catch (_) { return []; }
}

function showHistory() {
    closeMobileDrawers();
    const feed = document.getElementById('msgFeed');
    if (!feed) return;
    feed.innerHTML = '';
    const arr = loadHistory();
    if (!arr.length) {
        feed.innerHTML = '<p class="feed-empty">No conversation history yet. Say hi to Baba G and your chats will appear here.</p>';
        return;
    }
    for (const m of arr) {
        if (m.role === 'user') addUserMessage(m.text);
        else addBabaMessage(m.text, {});
    }
}

function showSaved() {
    closeMobileDrawers();
    const feed = document.getElementById('msgFeed');
    if (!feed) return;
    feed.innerHTML = '<p class="feed-empty">⭐ Saved conversations — long-press a Baba G reply to save it. Coming soon.</p>';
}

// ─── Voice Call modal ─────────────────────────────────────────────────
let _callTimerH = null;
let _callStartTs = 0;
let _callMuted = false;
let _callSpeaker = true;

function openCall() {
    closeMobileDrawers();
    const ov = document.getElementById('callOverlay');
    if (!ov) return;
    ov.hidden = false;
    document.body.classList.add('call-open');
    unlockTTS();

    // Reset state
    document.getElementById('callState').textContent = 'Connecting…';
    document.getElementById('callTimer').hidden = true;
    document.getElementById('callBubble').textContent = 'Baba G is getting ready to talk with you. Get comfy, take a deep breath and let\'s have a real conversation.';
    document.getElementById('callEq').hidden = true;
    document.getElementById('callListen').hidden = true;

    setTimeout(() => {
        document.getElementById('callState').textContent = 'Listening…';
        document.getElementById('callTimer').hidden = false;
        document.getElementById('callEq').hidden = false;
        document.getElementById('callListen').hidden = false;
        startCallTimer();

        // Kick the bot off with a warm greeting, and arm continuous voice.
        const inp = document.getElementById('chatInput');
        if (inp) inp.value = 'Greet me warmly in two short sentences as if we just connected on a phone call. Then ask me how I am feeling.';
        if (typeof window.askBaba === 'function') window.askBaba();

        if (window._babaVoice && typeof window._babaVoice.setContinuous === 'function') {
            window._babaVoice.setContinuous(true, { wakeWord: false });
        }
    }, 800);
}

function closeCall() {
    const ov = document.getElementById('callOverlay');
    if (!ov) return;
    ov.hidden = true;
    document.body.classList.remove('call-open');
    stopCallTimer();
    if (window._babaVoice) {
        try { window._babaVoice.setContinuous(false); } catch (_) { }
        try { window._babaVoice.stopSpeaking(); } catch (_) { }
    }
    if (typeof window.stopAll === 'function') window.stopAll();
}

function startCallTimer() {
    _callStartTs = Date.now();
    const timer = document.getElementById('callTimer');
    const tick = () => {
        const s = Math.floor((Date.now() - _callStartTs) / 1000);
        const mm = String(Math.floor(s / 60)).padStart(2, '0');
        const ss = String(s % 60).padStart(2, '0');
        if (timer) timer.textContent = `${mm}:${ss}`;
    };
    tick();
    _callTimerH = setInterval(tick, 1000);
}
function stopCallTimer() { if (_callTimerH) { clearInterval(_callTimerH); _callTimerH = null; } }

function setCallBubble(text) {
    const el = document.getElementById('callBubble');
    if (el) el.textContent = text;
}
function setCallState(text) {
    const el = document.getElementById('callState');
    if (el) el.textContent = text;
}

// Expose hooks app.js can call when streaming starts/ends.
window.babaCall = {
    isOpen: () => !document.getElementById('callOverlay')?.hidden,
    onTokenStart: () => setCallState('Speaking…'),
    onTokenChunk: (full) => {
        if (full) setCallBubble(full.length > 220 ? '…' + full.slice(-220) : full);
    },
    onTokenEnd: () => setCallState('Listening…'),
};

// ─── Mobile drawer plumbing ──────────────────────────────────────────
function closeMobileDrawers() {
    document.getElementById('sbLeft')?.classList.remove('open');
    document.getElementById('sbRight')?.classList.remove('open');
    document.getElementById('panelBackdrop')?.classList.remove('open');
}

// ─── Premium / Buy Me A Coffee modal ─────────────────────────────────
function openPremium() {
    const ov = document.getElementById('premiumOverlay');
    if (ov) ov.hidden = false;
}
function closePremium() {
    const ov = document.getElementById('premiumOverlay');
    if (ov) ov.hidden = true;
}

// ─── Bind everything on load ─────────────────────────────────────────
window.addEventListener('load', () => {
    updateStreak();

    // Sidebar nav + popular features + right-panel "Start Voice Call" all share
    // the same data-mode-cmd hook.
    document.querySelectorAll('[data-mode-cmd]').forEach(btn => {
        btn.addEventListener('click', (e) => {
            e.preventDefault();
            runMode(btn.dataset.modeCmd);
        });
    });

    // Quick chips
    document.querySelectorAll('.qc').forEach(b => {
        b.addEventListener('click', () => {
            const inp = document.getElementById('chatInput');
            if (inp) inp.value = b.dataset.prompt || b.textContent;
            if (typeof window.askBaba === 'function') window.askBaba();
        });
    });

    // Hero mic button (large central) — duplicates the small one in the composer.
    const heroMic = document.getElementById('heroMicBtn');
    if (heroMic) {
        const fire = (e) => { e.preventDefault(); window.toggleVoice && window.toggleVoice(e); };
        heroMic.addEventListener('click', fire);
        heroMic.addEventListener('touchend', fire, { passive: false });
    }

    // Voice call modal
    document.getElementById('callClose')?.addEventListener('click', closeCall);
    document.getElementById('callEnd')?.addEventListener('click', closeCall);
    document.getElementById('callMute')?.addEventListener('click', () => {
        _callMuted = !_callMuted;
        document.getElementById('callMute')?.classList.toggle('active', _callMuted);
        if (window._babaVoice) window._babaVoice.setMuted(_callMuted);
    });
    document.getElementById('callSpeaker')?.addEventListener('click', () => {
        _callSpeaker = !_callSpeaker;
        document.getElementById('callSpeaker')?.classList.toggle('active', _callSpeaker);
        if (window._babaVoice && !_callSpeaker) window._babaVoice.stopSpeaking();
    });

    // Premium modal
    document.querySelectorAll('[data-open-premium]').forEach(b => b.addEventListener('click', (e) => { e.preventDefault(); openPremium(); }));
    document.querySelectorAll('[data-close-modal]').forEach(b => b.addEventListener('click', closePremium));
    document.getElementById('premiumOverlay')?.addEventListener('click', (e) => { if (e.target.id === 'premiumOverlay') closePremium(); });

    // Mobile drawers
    const left = document.getElementById('sbLeft');
    const right = document.getElementById('sbRight');
    const backdrop = document.getElementById('panelBackdrop');
    document.getElementById('menuLeftBtn')?.addEventListener('click', () => {
        right?.classList.remove('open');
        left?.classList.toggle('open');
        backdrop?.classList.toggle('open', left?.classList.contains('open'));
    });
    document.getElementById('menuRightBtn')?.addEventListener('click', () => {
        left?.classList.remove('open');
        right?.classList.toggle('open');
        backdrop?.classList.toggle('open', right?.classList.contains('open'));
    });
    backdrop?.addEventListener('click', closeMobileDrawers);

    // Render any saved history on first load (so users see their last chats).
    showHistory();
});

// Expose helpers app.js will call when a new turn streams.
window.babaUi = {
    onUserSend: addUserMessage,
    startBabaTurn: () => addBabaMessage('', {}),
    setBubble: setBubbleText,
    setStatus: setHeroStatus,
    closeMobileDrawers,
};
