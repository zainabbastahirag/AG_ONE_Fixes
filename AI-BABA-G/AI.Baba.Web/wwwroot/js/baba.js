// ═══════════════════════════════════════════════════════════════════════
//  AI BABA G — UI shell module
//  Owns: sidebar mode buttons, voice-call fullscreen modal, message-feed
//  bubble rendering, daily-streak counter, premium / BuyMeACoffee modal,
//  mobile drawer behavior. The streaming AI + STT/TTS plumbing lives in
//  app.js / voice.js.
// ═══════════════════════════════════════════════════════════════════════

import { unlockTTS } from './voice.js';

// ─── Premium / paywall ────────────────────────────────────────────────
const FREE_CALL_LIMIT_SEC = 20;
function isPremium() {
    return localStorage.getItem('baba_premium') === '1';
}
function setPremium(on) {
    if (on) localStorage.setItem('baba_premium', '1');
    else localStorage.removeItem('baba_premium');
    refreshPremiumUi();
}
function refreshPremiumUi() {
    const badge = document.getElementById('premiumStatusBadge');
    const body  = document.getElementById('premiumSidebarBody');
    if (badge) badge.hidden = !isPremium();
    if (body) body.textContent = isPremium()
        ? 'Premium active. Enjoy unlimited calls, memory, and personalities.'
        : 'Unlock unlimited chats, voice calls, and premium features.';
    // Hide AdSense slots for premium users.
    document.querySelectorAll('.ad-slot').forEach(a => a.classList.toggle('hidden', isPremium()));
    // Unlock pills on premium-only nav items.
    document.querySelectorAll('.sb-nav-pill.lock').forEach(p => p.classList.toggle('hidden', isPremium()));
}

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
            ${opts.opener ? '' : `
            <div class="msg-actions">
                <button class="msg-action save-btn" type="button">⭐ <span class="lbl">Save</span></button>
                <button class="msg-action copy-btn" type="button">📋 Copy</button>
            </div>`}
        </div>`;
    const textEl = node.querySelector('.msg-text');
    textEl.textContent = text;
    feed.appendChild(node);
    feed.scrollTop = feed.scrollHeight;
    if (!opts.opener) saveHistory(text, 'baba');
    node.querySelector('.msg-play')?.addEventListener('click', () => {
        unlockTTS();
        if (window._babaVoice && typeof window._babaVoice.speak === 'function') {
            window._babaVoice.speak(textEl.textContent);
        }
    });
    const saveBtn = node.querySelector('.save-btn');
    if (saveBtn) {
        const refresh = () => {
            const cur = textEl.textContent;
            const on = isStarred(cur);
            saveBtn.classList.toggle('saved', on);
            saveBtn.querySelector('.lbl').textContent = on ? 'Saved' : 'Save';
        };
        refresh();
        saveBtn.addEventListener('click', () => {
            const cur = textEl.textContent;
            if (isStarred(cur)) unsaveStarred(cur); else saveStarred(cur);
            refresh();
        });
    }
    node.querySelector('.copy-btn')?.addEventListener('click', () => {
        navigator.clipboard?.writeText(textEl.textContent).catch(() => { });
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
    feed.innerHTML = '';
    let arr = []; try { arr = JSON.parse(localStorage.getItem('baba_saved') || '[]'); } catch (_) { }
    if (!arr.length) {
        feed.innerHTML = '<p class="feed-empty">⭐ Saved replies — tap the ⭐ Save button on any of Baba G\u2019s replies to keep it forever. Your starred list will appear here.</p>';
        return;
    }
    for (const m of arr) addBabaMessage(m.text, {});
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
    const banner = document.getElementById('callFreeBanner');
    const premium = isPremium();
    if (banner) banner.classList.toggle('hidden', premium);
    const tick = () => {
        const s = Math.floor((Date.now() - _callStartTs) / 1000);
        if (premium) {
            const mm = String(Math.floor(s / 60)).padStart(2, '0');
            const ss = String(s % 60).padStart(2, '0');
            if (timer) {
                timer.textContent = `${mm}:${ss}`;
                timer.classList.remove('free-warn', 'free-end');
            }
        } else {
            // Free trial: count DOWN from FREE_CALL_LIMIT_SEC.
            const left = Math.max(0, FREE_CALL_LIMIT_SEC - s);
            if (timer) {
                timer.textContent = `Free trial · 0:${String(left).padStart(2, '0')}`;
                timer.classList.toggle('free-warn', left <= 10 && left > 5);
                timer.classList.toggle('free-end', left <= 5);
            }
            if (left <= 0) {
                stopCallTimer();
                triggerFreeCallEnd();
            }
        }
    };
    tick();
    _callTimerH = setInterval(tick, 1000);
}
function stopCallTimer() { if (_callTimerH) { clearInterval(_callTimerH); _callTimerH = null; } }

function triggerFreeCallEnd() {
    setCallState('Trial ended');
    setCallBubble('Your 20-second free call is up, mere dost. Upgrade to Premium for unlimited voice calls with Baba G.');
    if (window._babaVoice) {
        try { window._babaVoice.stopSpeaking(); } catch (_) { }
        try { window._babaVoice.setContinuous(false); } catch (_) { }
    }
    // Auto-close the call after 1.5s and open the premium modal.
    setTimeout(() => { closeCall(); openPremium('Your 20-second free voice-call trial just ended. Upgrade to keep talking with Baba G.'); }, 1500);
}

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
function openPremium(reason) {
    const ov = document.getElementById('premiumOverlay');
    if (!ov) return;
    const reasonEl = ov.querySelector('.premium-reason');
    if (reasonEl) {
        if (reason) {
            reasonEl.textContent = reason;
            reasonEl.hidden = false;
        } else {
            reasonEl.hidden = true;
        }
    }
    ov.hidden = false;
}
function closePremium() {
    const ov = document.getElementById('premiumOverlay');
    if (ov) ov.hidden = true;
}

// ─── Saved messages ───────────────────────────────────────────────────
function saveStarred(text) {
    try {
        const arr = JSON.parse(localStorage.getItem('baba_saved') || '[]');
        arr.push({ text, at: Date.now() });
        if (arr.length > 100) arr.splice(0, arr.length - 100);
        localStorage.setItem('baba_saved', JSON.stringify(arr));
    } catch (_) { }
}
function unsaveStarred(text) {
    try {
        const arr = JSON.parse(localStorage.getItem('baba_saved') || '[]');
        const next = arr.filter(m => m.text !== text);
        localStorage.setItem('baba_saved', JSON.stringify(next));
    } catch (_) { }
}
function isStarred(text) {
    try {
        const arr = JSON.parse(localStorage.getItem('baba_saved') || '[]');
        return arr.some(m => m.text === text);
    } catch (_) { return false; }
}

// ─── Bind everything on load ─────────────────────────────────────────
window.addEventListener('load', () => {
    refreshPremiumUi();
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
    document.getElementById('btnIveSubscribed')?.addEventListener('click', () => {
        setPremium(true); closePremium();
    });
    document.getElementById('btnRevokePremium')?.addEventListener('click', () => {
        setPremium(false); closePremium();
    });

    // Voice profile + auto-speak — persist to localStorage so app.js picks it up.
    const voiceSel = document.getElementById('voiceSelect');
    if (voiceSel) {
        const saved = localStorage.getItem('baba_voice_profile');
        if (saved) voiceSel.value = saved;
        voiceSel.addEventListener('change', () => {
            localStorage.setItem('baba_voice_profile', voiceSel.value);
            // app.js reads this on each utterance through the live profile getter.
        });
    }
    const autoSpeak = document.getElementById('autoSpeak');
    if (autoSpeak) {
        autoSpeak.checked = localStorage.getItem('baba_autospeak') !== '0';
        autoSpeak.addEventListener('change', () => {
            localStorage.setItem('baba_autospeak', autoSpeak.checked ? '1' : '0');
        });
    }

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
