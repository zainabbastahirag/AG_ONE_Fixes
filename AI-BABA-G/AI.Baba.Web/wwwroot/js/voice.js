// ═══════════════════════════════════════════════════════════════════════
//  AI BABA-G — Professional Voice I/O (STT + TTS)  — mobile-ready
//
//  Mobile realities this module accounts for:
//   * iOS Safari has NO usable SpeechRecognition (webkitSpeechRecognition
//     is not exposed). We detect this, mark voice as "tts-only", show a
//     clear hint, and let the user type.
//   * iOS requires a user-gesture-initiated speak() to ever produce
//     sound. We unlock the synth on the first tap with a silent 0-volume
//     utterance.
//   * Android Chrome supports SpeechRecognition but `continuous = true`
//     stops itself after a few seconds. We use single-shot recognition
//     and re-arm on `onend` only when the user is still in continuous
//     mode and we're not currently speaking.
//   * Some browsers (Samsung Internet, older Edge) only fire `final`
//     events; the previous interim-only path would never trigger ASK.
//   * Mic must be opened INSIDE the user-gesture handler, not after an
//     async call — otherwise iOS/Safari blocks the request silently.
// ═══════════════════════════════════════════════════════════════════════

const SR = window.SpeechRecognition || window.webkitSpeechRecognition;

const UA = (navigator.userAgent || '').toLowerCase();
const IS_IOS = /iphone|ipad|ipod/.test(UA) || (UA.includes('mac') && 'ontouchend' in document);
const IS_ANDROID = /android/.test(UA);
const IS_MOBILE = IS_IOS || IS_ANDROID || matchMedia('(pointer: coarse)').matches;
const HAS_SR = !!SR && !IS_IOS;          // iOS Safari falsely exposes SR but it doesn't work
const HAS_TTS = 'speechSynthesis' in window;

// Voice profiles match by name patterns AND `lang` codes from the
// browser's installed voices. We always fall back to a localService voice
// in the same language family if no exact pattern matches, so the right
// regional voice gets picked even when the user's OS exposes a generic
// list.
const VOICE_PROFILES = {
    /* ─── DEFAULT MALE / GURU ────────────────────────────────────────── */
    guru: {
        match: [/old/i, /grandpa/i, /senior/i, /deep/i, /baritone/i,
                /daniel/i, /alex/i, /george/i, /fred/i, /arthur/i,
                /reed/i, /rishi/i, /aaron/i, /james/i],
        langs: ['en-IN', 'en-US', 'en-GB'],
        rate: 0.88, pitch: 0.78, volume: 1.0, gender: 'male', label: 'Old Guru (deep)'
    },
    /* ─── REGIONAL FEMALE ────────────────────────────────────────────── */
    indian_female: {
        match: [/veena/i, /lekha/i, /heera/i, /priya/i, /raveena/i, /isha/i, /neerja/i, /aditi/i, /kalpana/i, /shruti/i, /anjali/i, /microsoft.*kavya/i, /microsoft.*neerja/i],
        langs: ['en-IN', 'hi-IN'],
        rate: 0.95, pitch: 1.05, volume: 1.0, gender: 'female', label: 'Indian (Hindi/English) — Female'
    },
    chinese_female: {
        match: [/tingting/i, /tingt/i, /xiaoxiao/i, /yaoyao/i, /lili/i, /huihui/i, /microsoft.*xiaoyi/i, /microsoft.*hsiaochen/i, /sin\-ji/i],
        langs: ['zh-CN', 'zh-TW', 'zh-HK', 'zh'],
        rate: 0.95, pitch: 1.05, volume: 1.0, gender: 'female', label: 'Chinese (Mandarin) — Female'
    },
    malaysian_female: {
        match: [/yasmin/i, /amira/i, /microsoft.*yasmin/i, /siti/i, /aisha/i, /malay/i],
        langs: ['ms-MY', 'en-MY', 'id-ID'],
        rate: 0.95, pitch: 1.04, volume: 1.0, gender: 'female', label: 'Malaysian — Female'
    },
    bengali_female: {
        match: [/microsoft.*bashkar/i, /microsoft.*tanishaa/i, /tanishaa/i, /pradeep/i, /bashkar/i, /bengali/i],
        langs: ['bn-IN', 'bn-BD', 'bn'],
        rate: 0.95, pitch: 1.04, volume: 1.0, gender: 'female', label: 'Bengali / Bangla — Female'
    },
    pakistani_female: {
        match: [/microsoft.*uzma/i, /uzma/i, /asad/i, /microsoft.*gul/i, /urdu/i, /pakistan/i],
        langs: ['ur-PK', 'ur-IN', 'ur'],
        rate: 0.95, pitch: 1.05, volume: 1.0, gender: 'female', label: 'Pakistani (Urdu) — Female'
    },
    /* ─── GENERIC FALLBACKS ──────────────────────────────────────────── */
    elder_woman: {
        match: [/samantha/i, /victoria/i, /kate/i, /serena/i, /tessa/i, /susan/i, /allison/i, /moira/i],
        langs: ['en-US', 'en-GB', 'en-IN'],
        rate: 0.92, pitch: 0.95, volume: 1.0, gender: 'female', label: 'Elder Woman (warm)'
    },
    expert: {
        match: [/google.*us.*english/i, /microsoft.*aria/i, /microsoft.*guy/i, /natural/i],
        langs: ['en-US', 'en-GB'],
        rate: 0.95, pitch: 0.95, volume: 1.0, gender: 'any', label: 'Expert (neutral)'
    },
    gentle: {
        match: [/karen/i, /moira/i, /tessa/i, /samantha/i, /female/i],
        langs: ['en-US', 'en-GB', 'en-AU'],
        rate: 0.95, pitch: 1.05, volume: 1.0, gender: 'female', label: 'Gentle (soft)'
    },
    deep: {
        match: [/daniel/i, /alex/i, /fred/i, /microsoft david/i, /microsoft mark/i, /male/i],
        langs: ['en-US', 'en-GB'],
        rate: 0.9, pitch: 0.8, volume: 1.0, gender: 'male', label: 'Deep (baritone)'
    }
};

// ─── TTS unlock (iOS) ────────────────────────────────────────────────────
//   Until a user gesture has explicitly invoked speak(), iOS Safari blocks
//   speech-synth output. We attach a one-shot tap/click listener that
//   queues a silent 0-volume utterance — that single "successful" call
//   permanently unlocks the synth for the rest of the page lifetime.
let _ttsUnlocked = false;
export function unlockTTS() {
    if (_ttsUnlocked || !HAS_TTS) return;
    try {
        // Resume in case it's paused (Chrome will sometimes auto-pause).
        window.speechSynthesis.resume();
        const u = new SpeechSynthesisUtterance(' ');
        u.volume = 0; u.rate = 1; u.pitch = 1;
        window.speechSynthesis.speak(u);
        _ttsUnlocked = true;
    } catch (_) { /* ignore */ }
}
function attachTtsUnlocker() {
    if (!HAS_TTS) return;
    const fire = () => { unlockTTS(); detach(); };
    const detach = () => {
        document.removeEventListener('touchstart', fire, true);
        document.removeEventListener('touchend', fire, true);
        document.removeEventListener('click', fire, true);
        document.removeEventListener('keydown', fire, true);
    };
    document.addEventListener('touchstart', fire, { capture: true, once: true, passive: true });
    document.addEventListener('touchend',   fire, { capture: true, once: true, passive: true });
    document.addEventListener('click',      fire, { capture: true, once: true });
    document.addEventListener('keydown',    fire, { capture: true, once: true });
}
attachTtsUnlocker();

// Some Chrome versions silently stop speaking after ~15s. Periodically
// pump resume() during long replies so the queue doesn't stall on mobile.
if (HAS_TTS) {
    setInterval(() => {
        if (window.speechSynthesis.speaking && !window.speechSynthesis.paused) {
            try { window.speechSynthesis.resume(); } catch (_) { }
        }
    }, 8000);
}

export class VoiceIO {
    constructor({ onPartial, onFinal, onSpeakingDone, onSpeakingStart, onSpeakChunk, getRate, getProfile, onUnsupported } = {}) {
        this.onPartial = onPartial || (() => { });
        this.onFinal = onFinal || (() => { });
        this.onSpeakingDone = onSpeakingDone || (() => { });
        this.onSpeakingStart = onSpeakingStart || (() => { });
        this.onSpeakChunk = onSpeakChunk || (() => { });
        this.getRate = getRate || (() => 1.0);
        this.getProfile = getProfile || (() => 'guru');
        this.onUnsupported = onUnsupported || (() => { });

        this.recog = null;
        this.listening = false;
        this.continuous = false;
        this.userMutedMic = false;
        this.suspendForSpeak = false;
        this._lastFinalAt = 0;
        this._restartLock = false;
        this._buffer = '';
        this._wakeRequired = false;
        this._isMobile = IS_MOBILE;
        this._isIOS = IS_IOS;

        if (HAS_SR) {
            const recog = new SR();
            // On Android Chrome, continuous=true is unreliable and often
            // produces `audio-capture` errors after ~10s. We use single-shot
            // recognition and explicitly re-arm on `onend`. Desktop Chrome
            // works fine either way; choose the mobile-safe default.
            recog.continuous = !this._isMobile;
            recog.interimResults = true;
            recog.lang = navigator.language || 'en-US';
            recog.maxAlternatives = 1;

            recog.onstart = () => { this.listening = true; };

            recog.onresult = (e) => {
                if (this.suspendForSpeak) return;
                let interim = '', finalText = '';
                for (let i = e.resultIndex; i < e.results.length; i++) {
                    const t = e.results[i][0].transcript;
                    if (e.results[i].isFinal) finalText += t; else interim += t;
                }
                if (interim) this.onPartial(interim);
                if (finalText) this._handleFinal(finalText);
            };

            recog.onend = () => {
                this.listening = false;
                if (this.userMutedMic || this.suspendForSpeak) return;
                if (!this.continuous) return;
                if (this._restartLock) return;
                this._restartLock = true;
                setTimeout(() => {
                    this._restartLock = false;
                    this._safeStart();
                }, this._isMobile ? 600 : 350);
            };

            recog.onerror = (e) => {
                const code = e?.error || '';
                if (code === 'not-allowed' || code === 'service-not-allowed') {
                    this.continuous = false;
                    this.userMutedMic = true;
                    this.onUnsupported('Microphone permission denied. Please allow microphone access in your browser settings.');
                } else if (code === 'audio-capture') {
                    this.onUnsupported('No microphone detected.');
                }
                // 'no-speech' and 'aborted' are normal — ignore.
            };

            this.recog = recog;
        }
    }

    /// True when STT is supported on this device.
    supported() { return !!this.recog; }
    /// True when TTS is supported on this device.
    canSpeak() { return HAS_TTS; }
    isMobile() { return this._isMobile; }

    _handleFinal(finalText) {
        const txt = finalText.trim();
        if (!txt) return;
        const now = performance.now();
        if (now - this._lastFinalAt < 600) return;
        this._lastFinalAt = now;

        if (this.isSpeaking()) this.stopSpeaking();

        if (this._wakeRequired) {
            const lower = txt.toLowerCase();
            const idx = Math.max(lower.indexOf('hey baba'), lower.indexOf('baba'));
            if (idx < 0) return;
            const stripped = txt.replace(/hey baba[,.! ]*/i, '').replace(/^baba[,.! ]*/i, '').trim();
            if (!stripped) return;
            this.onFinal(stripped);
            return;
        }
        this.onFinal(txt);
    }

    _safeStart() {
        if (!this.recog) return;
        if (this.listening || this.suspendForSpeak || this.userMutedMic) return;
        try {
            this.recog.start();
        } catch (e) {
            // 'InvalidStateError' is thrown when called while already starting;
            // back off and try again on the next tick.
            setTimeout(() => {
                if (!this.listening && !this.suspendForSpeak && !this.userMutedMic) {
                    try { this.recog.start(); } catch (_) { }
                }
            }, 250);
        }
    }

    /// startListening must be called synchronously from inside a user-gesture
    /// handler (click / touchend). On iOS/Safari, async work BEFORE this call
    /// causes the gesture token to expire and the mic permission silently
    /// fails. The caller in app.js wires this directly off the mic-button
    /// click event.
    startListening() {
        if (!this.recog) {
            this.onUnsupported(this._isIOS
                ? 'Voice input is not supported on iOS Safari. Please type your question, the bot can still speak its replies.'
                : 'Voice input is not supported in this browser. Please use Chrome, Edge, or Samsung Internet on Android.');
            return false;
        }
        if (this.listening) return true;
        // Also unlock TTS so the bot's reply can speak immediately.
        unlockTTS();
        this._safeStart();
        return true;
    }
    stopListening() {
        if (!this.recog) return;
        try { this.recog.abort(); } catch (_) { }
        this.listening = false;
    }
    setContinuous(b, { wakeWord = false } = {}) {
        this.continuous = !!b;
        this._wakeRequired = !!wakeWord;
        this.userMutedMic = false;
        if (b) this._safeStart(); else this.stopListening();
    }
    setMuted(b) {
        this.userMutedMic = !!b;
        if (b) this.stopListening();
        else if (this.continuous) this._safeStart();
    }

    // ─── TTS ────────────────────────────────────────────────────────────
    isSpeaking() {
        return !!(HAS_TTS && (window.speechSynthesis.speaking || window.speechSynthesis.pending));
    }

    stopSpeaking() {
        this._buffer = '';
        if (HAS_TTS) { try { window.speechSynthesis.cancel(); } catch (_) { } }
        this._releaseMicAfterSpeak();
    }

    speak(text) {
        this.stopSpeaking();
        if (!text) return;
        unlockTTS();
        this.onSpeakChunk(text);
        this._splitIntoSentences(text).forEach(s => this._enqueue(s));
    }

    pushStreamingText(piece) {
        if (!piece) return;
        this._buffer += piece;
        this.onSpeakChunk(piece);

        while (true) {
            const text = this._buffer;
            let m = /[.!?\n](?:\s|$)/.exec(text);
            if (!m) {
                m = /[.!?]([A-Z])/.exec(text);
                if (m) {
                    const cut = m.index + 1;
                    const sentence = text.slice(0, cut).trim();
                    this._buffer = text.slice(cut);
                    if (sentence) this._enqueue(sentence);
                    continue;
                }
            }
            if (!m) {
                if (text.length > 220) {
                    const c = text.search(/[,;—–]\s/);
                    if (c >= 60) {
                        const clause = text.slice(0, c + 1).trim();
                        this._buffer = text.slice(c + 1);
                        if (clause) this._enqueue(clause);
                        continue;
                    }
                    if (text.length > 320) {
                        const sp = text.lastIndexOf(' ', 220);
                        if (sp > 80) {
                            const piece = text.slice(0, sp).trim();
                            this._buffer = text.slice(sp + 1);
                            if (piece) this._enqueue(piece);
                            continue;
                        }
                    }
                }
                break;
            }
            const cut = m.index + 1;
            const sentence = text.slice(0, cut).trim();
            this._buffer = text.slice(cut);
            if (sentence) this._enqueue(sentence);
        }
    }

    flushStreaming() {
        const tail = this._buffer.trim();
        this._buffer = '';
        if (tail) this._enqueue(tail);
    }

    _splitIntoSentences(text) {
        const out = [];
        const re = /[^.!?\n]+[.!?\n]?/g;
        let m;
        while ((m = re.exec(text)) !== null) {
            const s = m[0].trim();
            if (s) out.push(s);
        }
        return out.length ? out : [text];
    }

    _pickVoice(profileKey) {
        if (!HAS_TTS) return null;
        const synth = window.speechSynthesis;
        const voices = synth.getVoices();
        if (!voices || !voices.length) return null;
        const profile = VOICE_PROFILES[profileKey] || VOICE_PROFILES.guru;

        // 1. Strong match: exact name pattern within the profile's preferred langs.
        const inLangs = (v) => !profile.langs || profile.langs.some(l => (v.lang || '').toLowerCase().startsWith(l.toLowerCase()));
        for (const re of profile.match) {
            const v = voices.find(v => re.test(v.name || '') && inLangs(v));
            if (v) return v;
            // Fall back to any name match, even if the lang differs.
            const any = voices.find(v => re.test(v.name || ''));
            if (any) return any;
        }
        // 2. Lang-only match — pick a voice in the requested language with the
        //    right gender hint when possible.
        for (const lang of (profile.langs || [])) {
            const sameLang = voices.filter(v => (v.lang || '').toLowerCase().startsWith(lang.toLowerCase()));
            if (!sameLang.length) continue;
            if (profile.gender === 'female') {
                const fem = sameLang.find(v => /female|woman|girl|samantha|karen|victoria|lekha|veena|priya|tingting|xiaoxiao|yaoyao|yasmin|tanishaa|uzma|gul|aisha|raveena/i.test(v.name));
                if (fem) return fem;
            }
            if (profile.gender === 'male') {
                const m = sameLang.find(v => /male|man|boy|david|daniel|alex|fred|mark|rishi|aaron/i.test(v.name) && !/female/i.test(v.name));
                if (m) return m;
            }
            const local = sameLang.find(v => v.localService);
            if (local) return local;
            return sameLang[0];
        }
        // 3. Fall back to English then anything localService.
        const enVoices = voices.filter(v => (v.lang || '').toLowerCase().startsWith('en'));
        const pool = enVoices.length ? enVoices : voices;
        if (profile.gender === 'female') {
            const v = pool.find(v => /female|woman|girl|samantha|karen|victoria/i.test(v.name));
            if (v) return v;
        }
        if (profile.gender === 'male') {
            const v = pool.find(v => /male/i.test(v.name) && !/female/i.test(v.name));
            if (v) return v;
        }
        const local = pool.find(v => v.localService);
        return local || pool[0];
    }

    _suspendMicForSpeak() {
        if (this.suspendForSpeak) return;
        this.suspendForSpeak = true;
        if (this.listening) {
            try { this.recog.abort(); } catch (_) { }
            this.listening = false;
        }
    }

    _releaseMicAfterSpeak() {
        if (!this.suspendForSpeak) return;
        this.suspendForSpeak = false;
        if (this.continuous && !this.userMutedMic) {
            const delay = this._isMobile ? 500 : 250;
            setTimeout(() => this._safeStart(), delay);
        }
    }

    _enqueue(text) {
        if (!text || !HAS_TTS) return;
        const synth = window.speechSynthesis;
        const profileKey = this.getProfile();
        const profile = VOICE_PROFILES[profileKey] || VOICE_PROFILES.guru;

        this._suspendMicForSpeak();

        const u = new SpeechSynthesisUtterance(text);
        const voice = this._pickVoice(profileKey);
        if (voice) u.voice = voice;
        const userRate = parseFloat(this.getRate() || 1) || 1;
        u.rate = Math.min(2.0, Math.max(0.5, profile.rate * userRate));
        u.pitch = profile.pitch;
        u.volume = profile.volume;
        u.lang = (voice && voice.lang) || navigator.language || 'en-US';

        u.onstart = () => {
            document.querySelector('.avatar-figure')?.classList.add('speaking');
            this.onSpeakingStart();
        };
        u.onend = () => {
            if (!synth.pending && !synth.speaking) {
                document.querySelector('.avatar-figure')?.classList.remove('speaking');
                this._releaseMicAfterSpeak();
                this.onSpeakingDone();
            }
        };
        u.onerror = () => {
            if (!synth.pending && !synth.speaking) this._releaseMicAfterSpeak();
        };

        try {
            synth.speak(u);
        } catch (e) {
            // Some Android WebViews throw when the queue is busy — wait then retry.
            setTimeout(() => { try { synth.speak(u); } catch (_) { } }, 80);
        }
    }
}
