import { Avatar } from './avatar.js';
import { VoiceIO } from './voice.js';

const api = {
  async fetch(path, opts = {}){
    const headers = new Headers(opts.headers || {});
    if (state.token) headers.set('Authorization', 'Bearer ' + state.token);
    if (opts.body && !(opts.body instanceof FormData)) headers.set('Content-Type', 'application/json');
    const res = await fetch(path, { ...opts, headers });
    if (res.status === 401){ state.token=null; state.user=null; saveAuth(); renderAuthArea(); }
    return res;
  },
  async json(path, opts){
    const r = await this.fetch(path, opts);
    if (!r.ok){ let msg = r.statusText; try { msg = (await r.json()).error || msg; } catch(_){} throw new Error(msg); }
    return r.json();
  }
};

const state = {
  token: localStorage.getItem('baba_token'),
  user: JSON.parse(localStorage.getItem('baba_user') || 'null'),
  personalities: [],
  avatars: [],
  conversationId: null,
  activePersonalityId: localStorage.getItem('baba_personality') || '',
  activeAvatarId: localStorage.getItem('baba_avatar') || '',
  streamingMessageEl: null,
  abortController: null,
  voice: null,
  avatar: null,
};
function saveAuth(){
  if (state.token) localStorage.setItem('baba_token', state.token); else localStorage.removeItem('baba_token');
  if (state.user) localStorage.setItem('baba_user', JSON.stringify(state.user)); else localStorage.removeItem('baba_user');
}

// ============== Router ==============
const routes = ['chat', 'auth', 'memory', 'personalities', 'avatars', 'about', 'terms'];
function currentRoute(){
  const h = (location.hash || '#chat').replace('#','');
  return routes.includes(h) ? h : 'chat';
}
function navigate(){
  const route = currentRoute();
  document.querySelectorAll('.nav a').forEach(a => a.classList.toggle('active', a.dataset.route === route));
  const view = document.getElementById('view');
  view.innerHTML = '';
  const tpl = document.getElementById('tpl-' + route) || document.getElementById('tpl-chat');
  view.appendChild(tpl.content.cloneNode(true));
  ({ chat: mountChat, auth: mountAuth, memory: mountMemory, personalities: mountPersonalities, avatars: mountAvatars, about: ()=>{}, terms: ()=>{} })[route]?.();
}
window.addEventListener('hashchange', navigate);
document.addEventListener('click', (e) => {
  const a = e.target.closest('a[data-route]');
  if (a){ /* native hash nav */ }
});

// ============== Auth area ==============
function renderAuthArea(){
  const el = document.getElementById('authArea');
  if (state.user){
    el.innerHTML = `<span class="pill">Hi, ${escapeHtml(state.user.displayName || state.user.username)}</span>
      <button class="primary-btn" id="logoutBtn">Sign out</button>`;
    document.getElementById('logoutBtn').onclick = () => {
      state.token = null; state.user = null; saveAuth(); renderAuthArea(); navigate();
    };
  } else {
    el.innerHTML = `<a class="primary-btn" href="#auth" data-route="auth" style="text-decoration:none">Sign in / up</a>`;
  }
}

// ============== Chat ==============
function mountChat(){
  const messages = document.getElementById('messages');
  const composer = document.getElementById('composer');
  const input = document.getElementById('msgInput');
  const sendBtn = document.getElementById('sendBtn');
  const stopBtn = document.getElementById('stopBtn');
  const newChat = document.getElementById('newChatBtn');
  const personalitySelect = document.getElementById('personalitySelect');
  const avatarSelect = document.getElementById('avatarSelect');
  const continuous = document.getElementById('continuousMode');
  const guestHint = document.getElementById('guestHint');
  const micBtn = document.getElementById('micBtn');
  const avatarStatus = document.getElementById('avatarStatus');

  guestHint.hidden = !!state.user;

  // Init avatar (3D)
  const canvas = document.getElementById('avatarCanvas');
  state.avatar = new Avatar(canvas);

  // Init voice
  state.voice = new VoiceIO({
    onPartial: (t) => { input.value = t; },
    onFinal: (t) => { input.value = t; submit(); },
    onSpeakChunk: (chunk) => { state.avatar.speakText(chunk); },
    onSpeakingDone: () => { avatarStatus.textContent = state.voice.continuous ? 'listening…' : 'idle'; },
  });
  if (!state.voice.supported()){
    micBtn.title = 'Voice not supported in this browser';
    micBtn.disabled = true;
  }
  micBtn.addEventListener('click', () => {
    if (state.voice.listening){ state.voice.stopListening(); micBtn.classList.remove('listening'); avatarStatus.textContent='idle'; }
    else { state.voice.startListening(); micBtn.classList.add('listening'); avatarStatus.textContent='listening…'; state.avatar.setListening(true); }
  });
  continuous.checked = localStorage.getItem('baba_continuous') === '1';
  if (continuous.checked) state.voice.setContinuous(true);
  continuous.addEventListener('change', () => {
    localStorage.setItem('baba_continuous', continuous.checked ? '1' : '0');
    state.voice.setContinuous(continuous.checked);
    if (continuous.checked) avatarStatus.textContent = 'listening…';
  });

  // Personality + Avatar selectors
  loadLibraries().then(() => {
    fillSelect(personalitySelect, state.personalities, state.activePersonalityId, p => `${p.name} — ${p.tagline||''}`);
    fillSelect(avatarSelect, state.avatars, state.activeAvatarId, a => `${a.name} (${a.kind})`);
    applyAvatarChoice();
  });
  personalitySelect.addEventListener('change', () => {
    state.activePersonalityId = personalitySelect.value;
    localStorage.setItem('baba_personality', state.activePersonalityId);
  });
  avatarSelect.addEventListener('change', () => {
    state.activeAvatarId = avatarSelect.value;
    localStorage.setItem('baba_avatar', state.activeAvatarId);
    applyAvatarChoice();
  });

  // Conversation list (only for signed-in users)
  loadConversations();
  newChat.addEventListener('click', () => {
    state.conversationId = null;
    messages.innerHTML = '';
    showWelcome();
  });

  // Auto-grow textarea
  input.addEventListener('input', () => {
    input.style.height = 'auto'; input.style.height = Math.min(input.scrollHeight, 160) + 'px';
  });
  input.addEventListener('keydown', (e) => {
    if (e.key === 'Enter' && !e.shiftKey){ e.preventDefault(); submit(); }
    // ChatGPT-style interrupt: pressing Esc stops streaming AND speaking
    if (e.key === 'Escape'){ stopStreaming(); state.voice?.stopSpeaking(); }
  });
  composer.addEventListener('submit', (e) => { e.preventDefault(); submit(); });
  stopBtn.addEventListener('click', () => { stopStreaming(); state.voice?.stopSpeaking(); });

  showWelcome();

  function showWelcome(){
    if (messages.children.length) return;
    const div = document.createElement('div');
    div.className = 'msg system empty-state';
    if (state.user){
      div.innerHTML = `<h2>Hello ${escapeHtml(state.user.displayName || state.user.username)} 👋</h2>
        <p>BABA remembers you. Pick a personality, choose an avatar, and talk or type.<br>
        Press the mic for voice. Press <kbd>Esc</kbd> to interrupt BABA mid-sentence.</p>`;
    } else {
      div.innerHTML = `<h2>BABA doesn't know you yet</h2>
        <p>You're chatting as a guest. To unlock <strong>persistent memory</strong>, saved conversations, custom personalities & avatars,
        please <a href="#auth" data-route="auth">create a free account</a>.</p>`;
    }
    messages.appendChild(div);
  }

  async function submit(){
    const text = input.value.trim();
    if (!text) return;
    if (state.abortController) return; // already streaming
    addMessage('user', text);
    input.value = '';
    input.style.height = 'auto';

    sendBtn.hidden = true; stopBtn.hidden = false;
    avatarStatus.textContent = 'thinking…';
    state.avatar.setEmotion('thinking');

    // Cancel any ongoing TTS so the user's new request takes priority (interrupt).
    state.voice?.stopSpeaking();
    state.avatar.stopSpeaking();

    try {
      const isAuthed = !!state.user;
      const url = isAuthed ? '/api/chat/stream' : '/api/chat/guest/stream';
      const body = isAuthed
        ? { message: text, conversationId: state.conversationId, personalityId: state.activePersonalityId || null }
        : { message: text, personalityId: state.activePersonalityId || null };

      state.abortController = new AbortController();
      const headers = { 'Content-Type': 'application/json' };
      if (state.token) headers['Authorization'] = 'Bearer ' + state.token;
      const res = await fetch(url, { method: 'POST', headers, body: JSON.stringify(body), signal: state.abortController.signal });
      if (!res.ok || !res.body) {
        addMessage('assistant', `[error: ${res.status}]`);
        return;
      }
      const assistantEl = addMessage('assistant', '');
      assistantEl.classList.add('cursor');
      state.streamingMessageEl = assistantEl;

      avatarStatus.textContent = 'speaking…';
      state.avatar.setEmotion('happy');

      const reader = res.body.getReader();
      const decoder = new TextDecoder();
      let buf = '';
      let full = '';
      while (true){
        const { value, done } = await reader.read();
        if (done) break;
        buf += decoder.decode(value, { stream: true });
        // SSE parse: blocks separated by \n\n
        let idx;
        while ((idx = buf.indexOf('\n\n')) >= 0){
          const block = buf.slice(0, idx); buf = buf.slice(idx + 2);
          let evt = 'message', data = '';
          for (const line of block.split('\n')){
            if (line.startsWith('event:')) evt = line.slice(6).trim();
            else if (line.startsWith('data:')) data += line.slice(5).trimStart();
          }
          data = data.replace(/\\n/g, '\n');
          if (evt === 'token'){
            full += data;
            assistantEl.textContent = full;
            messages.scrollTop = messages.scrollHeight;
            state.voice?.pushStreamingText(data);
          } else if (evt === 'meta'){
            try { const m = JSON.parse(data); if (m.conversationId){ state.conversationId = m.conversationId; loadConversations(); } } catch(_){}
          } else if (evt === 'error'){
            full += `\n[error: ${data}]`; assistantEl.textContent = full;
          } else if (evt === 'done'){
            // streaming complete
          }
        }
      }
      assistantEl.classList.remove('cursor');
      state.voice?.flushStreaming();
      avatarStatus.textContent = state.voice?.continuous ? 'listening…' : 'idle';
      state.avatar.setEmotion('neutral');
    } catch(err){
      if (err.name !== 'AbortError'){
        console.error(err);
        addMessage('assistant', '[network error]');
      }
    } finally {
      state.abortController = null;
      sendBtn.hidden = false; stopBtn.hidden = true;
    }
  }

  function stopStreaming(){
    if (state.abortController){ state.abortController.abort(); state.abortController = null; }
    state.streamingMessageEl?.classList.remove('cursor');
    sendBtn.hidden = false; stopBtn.hidden = true;
    avatarStatus.textContent = 'idle';
  }

  function addMessage(role, text){
    // remove the welcome empty-state if present
    const empty = messages.querySelector('.empty-state'); if (empty) empty.remove();
    const div = document.createElement('div');
    div.className = `msg ${role}`;
    div.innerHTML = `<div class="who">${role}</div>`;
    const body = document.createTextNode(text);
    div.appendChild(body);
    messages.appendChild(div);
    messages.scrollTop = messages.scrollHeight;
    return div;
  }

  async function loadConversations(){
    const list = document.getElementById('conversationList');
    list.innerHTML = '';
    if (!state.user) return;
    try {
      const items = await api.json('/api/chat/conversations');
      for (const c of items){
        const li = document.createElement('li');
        li.textContent = c.title;
        if (c.id === state.conversationId) li.classList.add('active');
        li.title = new Date(c.updatedAt).toLocaleString();
        const del = document.createElement('span'); del.className='del'; del.textContent='✕';
        del.onclick = async (e) => {
          e.stopPropagation();
          if (!confirm('Delete this conversation?')) return;
          await api.fetch('/api/chat/conversations/' + c.id, { method: 'DELETE' });
          if (state.conversationId === c.id){ state.conversationId = null; messages.innerHTML=''; showWelcome(); }
          loadConversations();
        };
        li.appendChild(del);
        li.addEventListener('click', () => loadConversation(c.id));
        list.appendChild(li);
      }
    } catch(e){ console.warn(e); }
  }

  async function loadConversation(id){
    try {
      const c = await api.json('/api/chat/conversations/' + id);
      state.conversationId = c.id;
      messages.innerHTML = '';
      for (const m of c.messages){ addMessage(m.role, m.content); }
      loadConversations();
    } catch(e){ console.warn(e); }
  }

  function applyAvatarChoice(){
    const a = state.avatars.find(x => x.id === state.activeAvatarId);
    if (a) state.avatar.setAvatar(a);
  }
}

function fillSelect(sel, items, activeId, label){
  sel.innerHTML = '';
  for (const it of items){
    const o = document.createElement('option');
    o.value = it.id; o.textContent = label(it);
    if (it.id === activeId) o.selected = true;
    sel.appendChild(o);
  }
  if (!sel.value && items[0]) sel.value = items[0].id;
}

async function loadLibraries(){
  try {
    const [p, a] = await Promise.all([
      api.json('/api/personalities'),
      api.json('/api/avatars'),
    ]);
    state.personalities = p;
    state.avatars = a;
    if (!state.activePersonalityId && p[0]) state.activePersonalityId = p[0].id;
    if (!state.activeAvatarId && a[0]) state.activeAvatarId = a[0].id;
    localStorage.setItem('baba_personality', state.activePersonalityId);
    localStorage.setItem('baba_avatar', state.activeAvatarId);
  } catch(e){ console.warn(e); }
}

// ============== Auth page ==============
function mountAuth(){
  const tabs = document.querySelectorAll('.auth-tabs button');
  const forms = { login: document.getElementById('loginForm'), register: document.getElementById('registerForm') };
  tabs.forEach(t => t.addEventListener('click', () => {
    tabs.forEach(x => x.classList.remove('active')); t.classList.add('active');
    Object.values(forms).forEach(f => f.classList.remove('active'));
    forms[t.dataset.tab].classList.add('active');
  }));
  forms.login.addEventListener('submit', async (e) => {
    e.preventDefault();
    const fd = new FormData(forms.login);
    const msgEl = document.getElementById('loginMsg');
    msgEl.textContent = ''; msgEl.classList.remove('ok');
    try {
      const data = await api.json('/api/auth/login', { method: 'POST', body: JSON.stringify(Object.fromEntries(fd)) });
      state.token = data.token; state.user = data.user; saveAuth();
      msgEl.textContent = 'Welcome back!'; msgEl.classList.add('ok');
      renderAuthArea();
      setTimeout(() => location.hash = '#chat', 300);
    } catch(err){ msgEl.textContent = err.message; }
  });
  forms.register.addEventListener('submit', async (e) => {
    e.preventDefault();
    const fd = new FormData(forms.register);
    const msgEl = document.getElementById('registerMsg');
    msgEl.textContent = ''; msgEl.classList.remove('ok');
    try {
      const data = await api.json('/api/auth/register', { method: 'POST', body: JSON.stringify(Object.fromEntries(fd)) });
      state.token = data.token; state.user = data.user; saveAuth();
      msgEl.textContent = 'Account created. BABA will remember you now.'; msgEl.classList.add('ok');
      renderAuthArea();
      setTimeout(() => location.hash = '#chat', 400);
    } catch(err){ msgEl.textContent = err.message; }
  });
}

// ============== Memory page ==============
async function mountMemory(){
  const list = document.getElementById('memoryList');
  const input = document.getElementById('memoryInput');
  const kind = document.getElementById('memoryKind');
  const btn = document.getElementById('memoryAddBtn');
  const gate = document.getElementById('memoryGate');
  if (!state.user){
    gate.hidden = false; input.disabled = true; btn.disabled = true; return;
  }
  async function refresh(){
    list.innerHTML = '';
    const items = await api.json('/api/memory');
    if (!items.length){
      const li = document.createElement('li'); li.className=''; li.textContent = 'No memories yet. Tell BABA about yourself in chat — or add one above.';
      li.style.color = 'var(--fg-2)'; list.appendChild(li); return;
    }
    for (const m of items){
      const li = document.createElement('li');
      const kind = document.createElement('span'); kind.className='kind'; kind.textContent = m.kind;
      const body = document.createElement('div'); body.className='body';
      const text = document.createElement('div'); text.textContent = m.content;
      const meta = document.createElement('div'); meta.className='meta';
      meta.textContent = `added ${new Date(m.createdAt).toLocaleDateString()} · used ${m.useCount}x · importance ${(m.importance*100|0)}%`;
      body.appendChild(text); body.appendChild(meta);
      const del = document.createElement('button'); del.className='del'; del.textContent='✕';
      del.onclick = async () => { if (!confirm('Forget this?')) return; await api.fetch('/api/memory/' + m.id, { method: 'DELETE' }); refresh(); };
      li.appendChild(kind); li.appendChild(body); li.appendChild(del);
      list.appendChild(li);
    }
  }
  btn.addEventListener('click', async () => {
    const v = input.value.trim(); if (!v) return;
    await api.fetch('/api/memory', { method: 'POST', body: JSON.stringify({ content: v, kind: kind.value, importance: 0.7 }) });
    input.value=''; refresh();
  });
  refresh();
}

// ============== Personalities page ==============
async function mountPersonalities(){
  const grid = document.getElementById('personalityGrid');
  const gate = document.getElementById('pGate');
  const createBtn = document.getElementById('pCreateBtn');
  if (!state.user) { gate.hidden = false; createBtn.disabled = true; }
  async function refresh(){
    const items = await api.json('/api/personalities');
    state.personalities = items;
    grid.innerHTML = '';
    for (const p of items){
      const card = document.createElement('div');
      card.className = 'lib-card ' + (p.preset ? 'preset' : (p.mine ? 'mine' : ''));
      card.innerHTML = `
        <div class="title">${escapeHtml(p.name)}</div>
        <div class="tag">${escapeHtml(p.tagline || '')}</div>
        <div class="tag">voice: ${escapeHtml(p.voice||'default')} ${p.preset?'· preset':p.mine?'· yours':'· community'}</div>
        <div class="actions">
          <button class="use">Use</button>
          ${p.mine ? '<button class="del">Delete</button>' : ''}
        </div>`;
      card.querySelector('.use').onclick = () => {
        state.activePersonalityId = p.id;
        localStorage.setItem('baba_personality', p.id);
        location.hash = '#chat';
      };
      const delBtn = card.querySelector('.del');
      if (delBtn) delBtn.onclick = async () => { await api.fetch('/api/personalities/' + p.id, { method: 'DELETE' }); refresh(); };
      grid.appendChild(card);
    }
  }
  createBtn.addEventListener('click', async () => {
    if (!state.user) return;
    const body = {
      name: pName.value.trim(),
      tagline: pTagline.value.trim(),
      voice: pVoice.value,
      systemPrompt: pPrompt.value.trim(),
      isPublic: pPublic.checked,
    };
    if (!body.name || !body.systemPrompt){ alert('Name + system prompt required.'); return; }
    await api.fetch('/api/personalities', { method:'POST', body: JSON.stringify(body) });
    pName.value=''; pTagline.value=''; pPrompt.value=''; pPublic.checked=false;
    refresh();
  });
  refresh();
}

// ============== Avatars page ==============
async function mountAvatars(){
  const grid = document.getElementById('avatarGrid');
  const gate = document.getElementById('aGate');
  const createBtn = document.getElementById('aCreateBtn');
  const kind = document.getElementById('aKind');
  const imageRow = document.getElementById('aImageRow');
  const modelRow = document.getElementById('aModelRow');
  const fileInput = document.getElementById('aImageFile');
  if (!state.user) { gate.hidden = false; createBtn.disabled = true; }

  function syncFields(){
    imageRow.style.display = (kind.value === 'photo') ? '' : 'none';
    modelRow.hidden = (kind.value !== 'glb');
  }
  kind.addEventListener('change', syncFields); syncFields();

  // photo upload → embed as data: URL (no server file storage required)
  let dataUrl = '';
  fileInput.addEventListener('change', () => {
    const f = fileInput.files?.[0]; if (!f) return;
    const reader = new FileReader();
    reader.onload = () => { dataUrl = reader.result; aImageUrl.value = dataUrl; };
    reader.readAsDataURL(f);
  });

  async function refresh(){
    const items = await api.json('/api/avatars');
    state.avatars = items;
    grid.innerHTML = '';
    for (const a of items){
      const card = document.createElement('div');
      card.className = 'lib-card ' + (a.preset ? 'preset' : (a.mine ? 'mine' : ''));
      card.innerHTML = `
        <div class="swatch" style="background:${a.imageUrl ? `center/cover url('${a.imageUrl}')` : a.primaryColor}"></div>
        <div class="title">${escapeHtml(a.name)}</div>
        <div class="tag">${escapeHtml(a.kind)} ${a.preset?'· preset':a.mine?'· yours':'· community'}</div>
        <div class="actions">
          <button class="use">Use</button>
          ${a.mine ? '<button class="del">Delete</button>' : ''}
        </div>`;
      card.querySelector('.use').onclick = () => {
        state.activeAvatarId = a.id;
        localStorage.setItem('baba_avatar', a.id);
        location.hash = '#chat';
      };
      const delBtn = card.querySelector('.del');
      if (delBtn) delBtn.onclick = async () => { await api.fetch('/api/avatars/' + a.id, { method: 'DELETE' }); refresh(); };
      grid.appendChild(card);
    }
  }
  createBtn.addEventListener('click', async () => {
    if (!state.user) return;
    const body = {
      name: aName.value.trim(),
      kind: kind.value,
      imageUrl: kind.value === 'photo' ? (aImageUrl.value || dataUrl) : null,
      modelUrl: kind.value === 'glb' ? aModelUrl.value.trim() : null,
      primaryColor: aColor.value,
      isPublic: aPublic.checked,
    };
    if (!body.name){ alert('Name required'); return; }
    await api.fetch('/api/avatars', { method:'POST', body: JSON.stringify(body) });
    aName.value=''; aImageUrl.value=''; aModelUrl.value=''; aPublic.checked=false; dataUrl='';
    refresh();
  });
  refresh();
}

// ============== utils ==============
function escapeHtml(s){ return (s ?? '').toString().replace(/[&<>"']/g, c => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c])); }

// boot
renderAuthArea();
navigate();
