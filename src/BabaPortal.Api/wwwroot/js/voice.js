// Voice in/out: Web Speech API STT + SpeechSynthesis TTS.
// Streams words to the avatar for viseme lip-sync and supports interruption.

const SR = window.SpeechRecognition || window.webkitSpeechRecognition;

export class VoiceIO {
  constructor({ onPartial, onFinal, onSpeakingDone, onSpeakChunk } = {}){
    this.onPartial = onPartial || (()=>{});
    this.onFinal = onFinal || (()=>{});
    this.onSpeakingDone = onSpeakingDone || (()=>{});
    this.onSpeakChunk = onSpeakChunk || (()=>{}); // (text) => avatar.speakText(text)
    this.recog = null;
    this.listening = false;
    this.continuous = false;
    this.utteranceQueue = [];
    this.currentUtter = null;
    this._buffer = '';
    if (SR){
      this.recog = new SR();
      this.recog.continuous = true;
      this.recog.interimResults = true;
      this.recog.lang = navigator.language || 'en-US';
      this.recog.onresult = (e) => {
        let interim = '', finalText = '';
        for (let i = e.resultIndex; i < e.results.length; i++){
          const t = e.results[i][0].transcript;
          if (e.results[i].isFinal) finalText += t; else interim += t;
        }
        if (interim) this.onPartial(interim);
        if (finalText){
          // If TTS is currently speaking and user interrupts → stop speaking.
          if (this.isSpeaking()) this.stopSpeaking();
          this.onFinal(finalText.trim());
        }
      };
      this.recog.onend = () => {
        this.listening = false;
        // If continuous mode, keep restarting once TTS has settled
        if (this.continuous && !this.isSpeaking()){
          try { this.recog.start(); this.listening = true; } catch(_){}
        }
      };
      this.recog.onerror = (e) => {
        // browsers throw 'no-speech' frequently; ignore
      };
    }
  }

  supported(){ return !!this.recog; }

  startListening(){
    if (!this.recog) return;
    if (this.listening) return;
    try { this.recog.start(); this.listening = true; } catch(_){}
  }
  stopListening(){
    if (!this.recog) return;
    try { this.recog.stop(); } catch(_){}
    this.listening = false;
  }

  setContinuous(b){
    this.continuous = !!b;
    if (b) this.startListening(); else this.stopListening();
  }

  isSpeaking(){ return window.speechSynthesis && (window.speechSynthesis.speaking || window.speechSynthesis.pending); }

  stopSpeaking(){
    this.utteranceQueue = [];
    this._buffer = '';
    try { window.speechSynthesis.cancel(); } catch(_){}
  }

  // Stream-friendly speak: accept incremental tokens, flush at sentence boundaries.
  pushStreamingText(piece){
    if (!piece) return;
    this._buffer += piece;
    // Send chunk to avatar immediately so lip-sync starts even before TTS plays.
    this.onSpeakChunk(piece);
    // Flush by sentence
    const m = this._buffer.match(/[^.!?\n]*[.!?\n]+/);
    if (m){
      const sentence = m[0];
      this._buffer = this._buffer.slice(sentence.length);
      this._enqueue(sentence.trim());
    }
  }

  flushStreaming(){
    const tail = this._buffer.trim();
    this._buffer = '';
    if (tail) this._enqueue(tail);
  }

  _enqueue(text){
    if (!text || !window.speechSynthesis) return;
    const u = new SpeechSynthesisUtterance(text);
    const voices = window.speechSynthesis.getVoices();
    // Pick a friendlier voice if available
    const preferred = voices.find(v => /female|samantha|jenny|google.*english|en-?us/i.test(v.name + ' ' + v.lang)) || voices[0];
    if (preferred) u.voice = preferred;
    u.rate = 1.05; u.pitch = 1.0; u.volume = 1.0;
    u.onend = () => {
      if (!window.speechSynthesis.pending && !window.speechSynthesis.speaking){
        this.onSpeakingDone();
      }
    };
    window.speechSynthesis.speak(u);
  }
}
