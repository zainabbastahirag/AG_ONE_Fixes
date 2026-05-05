// Voice in/out: Web Speech API STT + SpeechSynthesis TTS, streaming-aware.
const SR = window.SpeechRecognition || window.webkitSpeechRecognition;

export class VoiceIO {
    constructor({ onPartial, onFinal, onSpeakingDone, onSpeakChunk, getRate } = {}) {
        this.onPartial = onPartial || (() => { });
        this.onFinal = onFinal || (() => { });
        this.onSpeakingDone = onSpeakingDone || (() => { });
        this.onSpeakChunk = onSpeakChunk || (() => { });
        this.getRate = getRate || (() => 1.0);

        this.recog = null;
        this.listening = false;
        this.continuous = false;
        this._buffer = '';

        if (SR) {
            this.recog = new SR();
            this.recog.continuous = true;
            this.recog.interimResults = true;
            this.recog.lang = navigator.language || 'en-US';
            this.recog.onresult = (e) => {
                let interim = '', finalText = '';
                for (let i = e.resultIndex; i < e.results.length; i++) {
                    const t = e.results[i][0].transcript;
                    if (e.results[i].isFinal) finalText += t; else interim += t;
                }
                if (interim) this.onPartial(interim);
                if (finalText) {
                    if (this.isSpeaking()) this.stopSpeaking();
                    this.onFinal(finalText.trim());
                }
            };
            this.recog.onend = () => {
                this.listening = false;
                if (this.continuous && !this.isSpeaking()) {
                    try { this.recog.start(); this.listening = true; } catch (_) { }
                }
            };
            this.recog.onerror = () => { /* swallow no-speech etc */ };
        }
    }

    supported() { return !!this.recog; }

    startListening() {
        if (!this.recog || this.listening) return;
        try { this.recog.start(); this.listening = true; } catch (_) { }
    }
    stopListening() {
        if (!this.recog) return;
        try { this.recog.stop(); } catch (_) { }
        this.listening = false;
    }
    setContinuous(b) {
        this.continuous = !!b;
        if (b) this.startListening(); else this.stopListening();
    }

    isSpeaking() { return window.speechSynthesis && (window.speechSynthesis.speaking || window.speechSynthesis.pending); }
    stopSpeaking() {
        this._buffer = '';
        try { window.speechSynthesis.cancel(); } catch (_) { }
    }

    /// Speak a *complete* string (used by legacy /api/ask flow).
    speak(text) {
        this.stopSpeaking();
        this.onSpeakChunk(text);
        this._enqueue(text);
    }

    /// Streaming: tokens arrive incrementally; flush by sentence so TTS pace stays natural.
    pushStreamingText(piece) {
        if (!piece) return;
        this._buffer += piece;
        this.onSpeakChunk(piece);
        const m = this._buffer.match(/[^.!?\n]*[.!?\n]+/);
        if (m) {
            const sentence = m[0];
            this._buffer = this._buffer.slice(sentence.length);
            this._enqueue(sentence.trim());
        }
    }

    flushStreaming() {
        const tail = this._buffer.trim();
        this._buffer = '';
        if (tail) this._enqueue(tail);
    }

    _enqueue(text) {
        if (!text || !window.speechSynthesis) return;
        const u = new SpeechSynthesisUtterance(text);
        const voices = window.speechSynthesis.getVoices();
        const preferred = voices.find(v => v.lang.startsWith('en') && v.name.toLowerCase().includes('male'))
            || voices.find(v => v.lang.startsWith('en'))
            || voices[0];
        if (preferred) u.voice = preferred;
        u.rate = this.getRate();
        u.pitch = 0.95; u.volume = 1.0;
        u.onstart = () => document.querySelector('.avatar-figure')?.classList.add('speaking');
        u.onend = () => {
            if (!window.speechSynthesis.pending && !window.speechSynthesis.speaking) {
                document.querySelector('.avatar-figure')?.classList.remove('speaking');
                this.onSpeakingDone();
            }
        };
        window.speechSynthesis.speak(u);
    }
}
