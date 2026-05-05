// ═══════════════════════════════════════════════════════════════════════
//  AI BABA-G — Professional Voice I/O (STT + TTS)
//  Goals (vs the previous version that "kept talking with no pauses"):
//    1. Bot speaks one full SENTENCE at a time (never mid-clause), so it
//       sounds like a real guru/old-man/girl — not a robot machine-gun.
//    2. While the bot is speaking, the microphone is fully muted/paused so
//       the bot does NOT hear itself and re-trigger an answer loop.
//    3. Continuous mode requires either a real silence pause OR a wake
//       word ("hey baba"); it never restarts mid-speech.
//    4. The voice ("old man / girl / guru / expert") is selectable and is
//       matched against the browser's installed voices on first speak,
//       not on construction (voices load asynchronously in Chrome).
//    5. STT auto-restart is throttled so it can't loop forever, and the
//       final-result handler is debounced so a single utterance is sent
//       to the backend exactly once.
// ═══════════════════════════════════════════════════════════════════════

const SR = window.SpeechRecognition || window.webkitSpeechRecognition;

const VOICE_PROFILES = {
    guru: {
        // Old, deep, calm man — "old guru / sage" feel
        match: [/old/i, /grandpa/i, /senior/i, /deep/i, /baritone/i,
                /daniel/i, /alex/i, /george/i, /fred/i, /arthur/i,
                /reed/i, /rishi/i, /aaron/i, /james/i],
        rate: 0.88, pitch: 0.78, volume: 1.0,
        gender: 'male'
    },
    elder_woman: {
        // Warm older female — Hindi/Indian-style "expert" voice if available
        match: [/lekha/i, /veena/i, /heera/i, /priya/i, /samantha/i,
                /victoria/i, /kate/i, /serena/i, /tessa/i, /susan/i],
        rate: 0.92, pitch: 0.95, volume: 1.0,
        gender: 'female'
    },
    expert: {
        match: [/google.*us.*english/i, /microsoft.*aria/i, /microsoft.*guy/i, /natural/i],
        rate: 0.95, pitch: 0.95, volume: 1.0,
        gender: 'any'
    },
    gentle: {
        match: [/karen/i, /moira/i, /tessa/i, /samantha/i, /female/i],
        rate: 0.95, pitch: 1.05, volume: 1.0,
        gender: 'female'
    },
    deep: {
        match: [/daniel/i, /alex/i, /fred/i, /microsoft david/i, /microsoft mark/i, /male/i],
        rate: 0.9, pitch: 0.8, volume: 1.0,
        gender: 'male'
    }
};

export class VoiceIO {
    constructor({ onPartial, onFinal, onSpeakingDone, onSpeakingStart, onSpeakChunk, getRate, getProfile } = {}) {
        this.onPartial = onPartial || (() => { });
        this.onFinal = onFinal || (() => { });
        this.onSpeakingDone = onSpeakingDone || (() => { });
        this.onSpeakingStart = onSpeakingStart || (() => { });
        this.onSpeakChunk = onSpeakChunk || (() => { });
        this.getRate = getRate || (() => 1.0);
        this.getProfile = getProfile || (() => 'guru');

        this.recog = null;
        this.listening = false;
        this.continuous = false;
        this.userMutedMic = false;          // user-toggled mute
        this.suspendForSpeak = false;       // pause STT while bot speaks
        this._lastFinalAt = 0;              // dedupe rapid finals
        this._restartLock = false;          // throttle onend → start

        this._buffer = '';
        this._wakeRequired = false;         // optional wake-word for continuous

        if (SR) {
            const recog = new SR();
            recog.continuous = true;
            recog.interimResults = true;
            recog.lang = navigator.language || 'en-US';

            recog.onresult = (e) => {
                if (this.suspendForSpeak) return;       // ignore self-echo
                let interim = '', finalText = '';
                for (let i = e.resultIndex; i < e.results.length; i++) {
                    const t = e.results[i][0].transcript;
                    if (e.results[i].isFinal) finalText += t; else interim += t;
                }
                if (interim) this.onPartial(interim);

                if (finalText) {
                    const txt = finalText.trim();
                    if (!txt) return;
                    const now = performance.now();
                    if (now - this._lastFinalAt < 600) return;  // dedupe
                    this._lastFinalAt = now;

                    // If a TTS is playing, treat the new utterance as an interrupt.
                    if (this.isSpeaking()) this.stopSpeaking();

                    // Wake-word gating in continuous mode (optional)
                    if (this._wakeRequired) {
                        const lower = txt.toLowerCase();
                        const idx = lower.indexOf('hey baba');
                        const idx2 = lower.indexOf('baba');
                        if (idx < 0 && idx2 < 0) return;
                        const stripped = txt.replace(/hey baba[,.! ]*/i, '')
                                            .replace(/^baba[,.! ]*/i, '')
                                            .trim();
                        if (!stripped) return;
                        this.onFinal(stripped);
                        return;
                    }
                    this.onFinal(txt);
                }
            };

            recog.onend = () => {
                this.listening = false;
                if (!this.continuous || this.userMutedMic) return;
                if (this.suspendForSpeak) return;       // will resume on speak-end
                if (this._restartLock) return;
                this._restartLock = true;
                setTimeout(() => {
                    this._restartLock = false;
                    if (this.continuous && !this.userMutedMic && !this.suspendForSpeak) {
                        try { recog.start(); this.listening = true; } catch (_) { }
                    }
                }, 350);
            };

            recog.onerror = (e) => {
                // Common harmless errors: 'no-speech', 'aborted', 'audio-capture'
                if (e?.error === 'not-allowed' || e?.error === 'service-not-allowed') {
                    this.continuous = false;
                }
            };

            this.recog = recog;
        }
    }

    supported() { return !!this.recog; }

    startListening() {
        if (!this.recog || this.listening || this.suspendForSpeak) return;
        try { this.recog.start(); this.listening = true; } catch (_) { /* already started */ }
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
        if (b) this.startListening(); else this.stopListening();
    }
    setMuted(b) {
        this.userMutedMic = !!b;
        if (b) this.stopListening();
        else if (this.continuous) this.startListening();
    }

    // ─── TTS ────────────────────────────────────────────────────────────
    isSpeaking() {
        return !!(window.speechSynthesis && (window.speechSynthesis.speaking || window.speechSynthesis.pending));
    }

    stopSpeaking() {
        this._buffer = '';
        try { window.speechSynthesis.cancel(); } catch (_) { }
        this._releaseMicAfterSpeak();
    }

    /// Speak a complete string immediately (used by the legacy /api/ask flow).
    speak(text) {
        this.stopSpeaking();
        if (!text) return;
        this.onSpeakChunk(text);
        this._splitIntoSentences(text).forEach(s => this._enqueue(s));
    }

    /// Streaming: tokens arrive incrementally during SSE.
    /// We ONLY flush a full sentence to the TTS engine — never half-clauses —
    /// so the bot speaks with natural cadence and pauses, not a stutter.
    pushStreamingText(piece) {
        if (!piece) return;
        this._buffer += piece;
        this.onSpeakChunk(piece);

        while (true) {
            const text = this._buffer;
            // Match end-of-sentence (., !, ?, newline, paragraph break).
            const m = /[.!?\n](?:\s|$)/.exec(text);
            if (!m) {
                // Failsafe: if the buffer grows huge without a terminator,
                // flush at the next clause boundary so we don't hold forever.
                if (text.length > 220) {
                    const c = text.search(/[,;—–]\s/);
                    if (c >= 60) {
                        const clause = text.slice(0, c + 1).trim();
                        this._buffer = text.slice(c + 1);
                        if (clause) this._enqueue(clause);
                        continue;
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
        const synth = window.speechSynthesis;
        if (!synth) return null;
        const voices = synth.getVoices();
        if (!voices || !voices.length) return null;
        const profile = VOICE_PROFILES[profileKey] || VOICE_PROFILES.guru;
        const lang = (navigator.language || 'en-US').toLowerCase();
        const enVoices = voices.filter(v => v.lang && v.lang.toLowerCase().startsWith('en'));
        const pool = enVoices.length ? enVoices : voices;

        // Prefer name-pattern matches in the profile.
        for (const re of profile.match) {
            const v = pool.find(v => re.test(v.name || ''));
            if (v) return v;
        }
        // Then by gender hint.
        if (profile.gender === 'male') {
            const v = pool.find(v => /male/i.test(v.name) && !/female/i.test(v.name));
            if (v) return v;
        }
        if (profile.gender === 'female') {
            const v = pool.find(v => /female|woman|girl|samantha|karen|victoria/i.test(v.name));
            if (v) return v;
        }
        // Local voices tend to sound more natural than network ones.
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
            // Small delay so the speech-synth tail doesn't echo back.
            setTimeout(() => {
                if (this.continuous && !this.userMutedMic && !this.suspendForSpeak) {
                    try { this.recog?.start(); this.listening = true; } catch (_) { }
                }
            }, 250);
        }
    }

    _enqueue(text) {
        if (!text || !window.speechSynthesis) return;
        const synth = window.speechSynthesis;
        const profileKey = this.getProfile();
        const profile = VOICE_PROFILES[profileKey] || VOICE_PROFILES.guru;

        // Mute the mic the first time we speak in a turn so the AI doesn't
        // listen to itself and create an infinite reply loop.
        this._suspendMicForSpeak();

        const u = new SpeechSynthesisUtterance(text);
        const voice = this._pickVoice(profileKey);
        if (voice) u.voice = voice;
        const userRate = parseFloat(this.getRate() || 1) || 1;
        u.rate = Math.min(2.0, Math.max(0.5, profile.rate * userRate));
        u.pitch = profile.pitch;
        u.volume = profile.volume;

        u.onstart = () => {
            document.querySelector('.avatar-figure')?.classList.add('speaking');
            this.onSpeakingStart();
        };
        u.onend = () => {
            // Speaking is "done" only when nothing else is queued.
            if (!synth.pending && !synth.speaking) {
                document.querySelector('.avatar-figure')?.classList.remove('speaking');
                this._releaseMicAfterSpeak();
                this.onSpeakingDone();
            }
        };
        u.onerror = () => {
            // Don't get stuck with mic suspended on a synth failure.
            if (!synth.pending && !synth.speaking) this._releaseMicAfterSpeak();
        };

        synth.speak(u);
    }
}
