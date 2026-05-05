// ═══════════════════════════════════════════════════════════════
// AI BABA-G — Frontend Logic
// ═══════════════════════════════════════════════════════════════

let currentAvatar = 'sage';
let currentMindset = 'balanced';
let isRecording = false;
let recognition = null;
let isSpeaking = false;

// ─── AVATAR SELECTION ──────────────────────────────────────
function selectAvatar(el) {
    document.querySelectorAll('.avatar-card').forEach(c => {
        c.classList.remove('active');
        c.querySelector('.avatar-check')?.remove();
    });
    el.classList.add('active');
    currentAvatar = el.dataset.avatar;
    const img = el.querySelector('.avatar-img');
    const check = document.createElement('div');
    check.className = 'avatar-check';
    check.textContent = '✓';
    img.appendChild(check);
}

// ─── MINDSET SELECTION ─────────────────────────────────────
function selectMindset(el) {
    document.querySelectorAll('.mindset-card').forEach(c => {
        c.classList.remove('active');
        c.querySelector('.mindset-check')?.remove();
    });
    el.classList.add('active');
    currentMindset = el.dataset.mindset;
    const check = document.createElement('div');
    check.className = 'mindset-check';
    check.textContent = '✓';
    el.appendChild(check);
}

// ─── ASK BABA ──────────────────────────────────────────────
async function askBaba() {
    const input = document.getElementById('chatInput');
    const prompt = input.value.trim();
    if (!prompt) return;

    input.value = '';
    setBabaText('Thinking...');
    showWave(true);

    try {
        const res = await fetch('/api/ask', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                prompt,
                avatar: currentAvatar,
                mindset: currentMindset
            })
        });

        const data = await res.json();

        if (data.success) {
            setBabaText(data.response);
            speak(data.response);
        } else {
            setBabaText('Something went wrong... try again, seeker.');
        }
    } catch (err) {
        console.error(err);
        setBabaText('The connection to wisdom was lost. Please try again.');
    }

    showWave(false);
}

// ─── TEXT-TO-SPEECH ────────────────────────────────────────
function speak(text) {
    if (!('speechSynthesis' in window)) return;

    window.speechSynthesis.cancel();
    const utterance = new SpeechSynthesisUtterance(text);

    const speedSlider = document.getElementById('speedRange');
    utterance.rate = parseFloat(speedSlider?.value || '1');
    utterance.pitch = 0.9;
    utterance.volume = 1;

    const voices = window.speechSynthesis.getVoices();
    const preferred = voices.find(v => v.lang.startsWith('en') && v.name.toLowerCase().includes('male'))
        || voices.find(v => v.lang.startsWith('en'))
        || voices[0];
    if (preferred) utterance.voice = preferred;

    utterance.onstart = () => {
        isSpeaking = true;
        document.querySelector('.avatar-figure')?.classList.add('speaking');
        showWave(true);
    };

    utterance.onend = () => {
        isSpeaking = false;
        document.querySelector('.avatar-figure')?.classList.remove('speaking');
        showWave(false);
    };

    window.speechSynthesis.speak(utterance);
}

// ─── SPEECH-TO-TEXT (VOICE INPUT) ──────────────────────────
function toggleVoice() {
    if (isRecording) {
        stopRecording();
    } else {
        startRecording();
    }
}

function startRecording() {
    if (!('webkitSpeechRecognition' in window || 'SpeechRecognition' in window)) {
        alert('Voice input not supported in this browser. Use Chrome.');
        return;
    }

    const SpeechRecognition = window.SpeechRecognition || window.webkitSpeechRecognition;
    recognition = new SpeechRecognition();
    recognition.continuous = false;
    recognition.interimResults = true;
    recognition.lang = 'en-US';

    recognition.onstart = () => {
        isRecording = true;
        document.getElementById('micBtn').classList.add('recording');
        document.getElementById('listeningText').classList.add('visible');
    };

    recognition.onresult = (event) => {
        let transcript = '';
        for (let i = event.resultIndex; i < event.results.length; i++) {
            transcript += event.results[i][0].transcript;
        }
        document.getElementById('chatInput').value = transcript;

        if (event.results[event.resultIndex].isFinal) {
            // Check for wake word
            if (transcript.toLowerCase().includes('hey baba')) {
                document.getElementById('chatInput').value = transcript.replace(/hey baba/gi, '').trim();
            }
            stopRecording();
            if (document.getElementById('chatInput').value.trim()) {
                askBaba();
            }
        }
    };

    recognition.onerror = () => stopRecording();
    recognition.onend = () => stopRecording();

    recognition.start();
}

function stopRecording() {
    isRecording = false;
    document.getElementById('micBtn').classList.remove('recording');
    document.getElementById('listeningText').classList.remove('visible');
    if (recognition) {
        try { recognition.stop(); } catch (e) { }
        recognition = null;
    }
}

// ─── UI HELPERS ────────────────────────────────────────────
function setBabaText(text) {
    document.getElementById('babaText').innerText = text;
}

function showWave(show) {
    document.getElementById('voiceWave').classList.toggle('active', show);
}

// ─── SPEED SLIDER ──────────────────────────────────────────
document.getElementById('speedRange')?.addEventListener('input', (e) => {
    document.getElementById('speedVal').textContent = parseFloat(e.target.value).toFixed(1) + 'x';
});

// ─── TOPIC TAGS ────────────────────────────────────────────
document.querySelectorAll('.tag').forEach(tag => {
    tag.addEventListener('click', () => {
        const topic = tag.textContent.replace(/^[^\s]+\s/, '');
        document.getElementById('chatInput').value = `Tell me about ${topic}`;
        document.getElementById('chatInput').focus();
    });
});

// ─── WAKE WORD LISTENER (background) ──────────────────────
function startWakeWordListener() {
    if (!('webkitSpeechRecognition' in window || 'SpeechRecognition' in window)) return;

    // Passive wake word detection runs when not actively recording or speaking
    setInterval(() => {
        if (!isRecording && !isSpeaking) {
            // Ready state — user can say "Hey Baba" or click mic
        }
    }, 5000);
}

// ─── INIT ──────────────────────────────────────────────────
window.addEventListener('load', () => {
    // Preload voices
    if ('speechSynthesis' in window) {
        window.speechSynthesis.getVoices();
        window.speechSynthesis.onvoiceschanged = () => window.speechSynthesis.getVoices();
    }
    startWakeWordListener();
});
