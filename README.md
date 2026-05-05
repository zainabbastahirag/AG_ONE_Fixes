# BABA · AI Fun Portal

> A memory-aware AI companion with a 3D lip-syncing avatar, persistent SQLite memory, vector recall, custom personalities, voice interruption, and real-time streaming chat.

This is a full optimization rebuild of the BABA Fun Portal. It is designed around the constraints you listed:

- **Persistent DB memory** — everything lives in SQLite via EF Core. Tables auto-create on startup. No data is held in server RAM.
- **Vector memory** — every memory is embedded (Ollama embedding model when available, deterministic local fallback otherwise) and recalled by cosine similarity.
- **"BABA remembers everything you said"** — facts, preferences, events, summaries are stored per-user. Conversation history is also persisted.
- **Sign-up gate** — guests get a stateless chat. Memory, saved conversations, custom personalities, and avatars are unlocked by registering.
- **Memory-aware Ollama prompt** — top-K relevant memories are injected into the system prompt before each generation, so BABA "feels" like it knows you.
- **Personality adoption** — switch between presets (Classic, Wise Guru, Hype Buddy, Tech Tutor, Stand-up) or build your own.
- **Quick responses** — answers stream token-by-token via Server-Sent Events, so the UI feels live.
- **Real 3D avatar with lip sync** — Three.js robot with viseme-driven mouth shapes, blinking, idle bob, listening pulse, and emotional tilt.
- **Voice in & out** — Web Speech API for STT, browser SpeechSynthesis for TTS, with a continuous-conversation toggle and ChatGPT-style interrupt (Esc, or just speak over BABA).
- **User-created personalities + avatars** — including photo-based billboard avatars from your own picture, GLB model URLs, and color-themed robots.
- **Scalability** — async streaming, indexed SQLite queries, IP rate-limiting, Docker-ready.
- **Pages** — Chat, Auth, Memory, Personalities, Avatars, About, Terms.

## Quick start

### Option A — local dotnet

```bash
# install Ollama and pull models (one-time)
curl -fsSL https://ollama.com/install.sh | sh
ollama pull llama3.2
ollama pull nomic-embed-text

cd src/BabaPortal.Api
dotnet run --urls=http://0.0.0.0:5099
```

Open http://localhost:5099 — sign up, then chat.

### Option B — Docker compose (BABA + Ollama)

```bash
docker compose up --build
# in another terminal, pull models inside the ollama container
docker compose exec ollama ollama pull llama3.2
docker compose exec ollama ollama pull nomic-embed-text
```

Open http://localhost:8080.

## Configuration

`src/BabaPortal.Api/appsettings.json` (override in env vars with `__`):

| Key | Default | Notes |
|-----|---------|-------|
| `Database:Path` | `baba.db` | Single-file SQLite DB |
| `Jwt:Key` | _(replace this)_ | Long random secret used to sign tokens |
| `Ollama:BaseUrl` | `http://localhost:11434` | Ollama API endpoint |
| `Ollama:ChatModel` | `llama3.2` | Any chat model you've pulled |
| `Ollama:EmbeddingModel` | `nomic-embed-text` | Falls back to local hash embeddings if missing |
| `IpRateLimiting.GeneralRules` | guest 20/min, chat 60/min, auth 30/min | Tune for scale |

## Architecture

```
Browser
 ├── Three.js avatar (viseme lip-sync, blink, idle, emotion)
 ├── Web Speech STT  ──► sends user text
 ├── SpeechSynthesis TTS ◄── streams sentence-by-sentence from BABA
 └── SSE client       ◄── streams tokens from /api/chat/stream

ASP.NET Core 8 API
 ├── /api/auth (register, login, me)            JWT
 ├── /api/chat/stream  (SSE, authed, memory-aware)
 ├── /api/chat/guest/stream  (SSE, no memory)
 ├── /api/memory  (list, add, recall, delete)   vector recall
 ├── /api/personalities  (CRUD, presets + custom)
 └── /api/avatars  (CRUD, presets + custom)

EF Core ──► SQLite (single file, auto-migrated on startup)
 ├── Users, Conversations, Messages
 ├── MemoryEntries (with embedding bytes)
 ├── Personalities, Avatars

Ollama (external service)
 ├── /api/chat        (streaming chat completions)
 └── /api/embeddings  (nomic-embed-text by default)
```

## Memory model

Each memory has:

- `Content` — the natural-language statement
- `Kind` — `fact` / `preference` / `event` / `summary`
- `Importance` — 0..1, used for ranking
- `Embedding` — float[] stored as bytes
- `LastUsedAt`, `UseCount` — recency stats

On every reply, BABA recalls the top-6 memories scored by:

```
score = 0.8 * cosine(query, memory) + 0.1 * importance + 0.1 * recencyWeight
```

Memories that match get their `LastUsedAt` and `UseCount` bumped, so frequently relevant memories stay sticky.

## Voice & lip-sync

The frontend pushes the streaming text to two consumers in parallel:

1. **The visible text bubble** (immediate display).
2. **The voice/avatar pipeline** — characters are mapped to phoneme groups (`aa`, `e`, `i`, `o`, `u`, `pp`, `ff`, `ss`, …), each with a target mouth-open value. The avatar consumes that schedule at ~16ms ticks. When the streamed sentence ends with `.`/`!`/`?`/`\n`, the sentence is queued into `SpeechSynthesis` for natural TTS.

ChatGPT-style interrupt: pressing **Esc**, clicking **Stop**, or **speaking over BABA** while continuous mode is on cancels both the SSE stream and the active TTS utterance.

## Pages

- `#chat` — main interactive page with avatar, sidebar, composer.
- `#auth` — sign in / sign up tabs.
- `#memory` — list, add, and delete memories.
- `#personalities` — preset + custom personality cards, system-prompt editor.
- `#avatars` — marketplace + creator (robot color, photo billboard, GLB URL).
- `#about` — explains BABA, memory model, privacy.
- `#terms` — terms & conditions.

## Notes & roadmap

The rendering pipeline is intentionally pluggable so future upgrades fit cleanly:

- **Custom voice cloning** slot exists in the personality `Voice` field; wire it to a TTS service (e.g. Coqui XTTS) by extending `VoiceIO._enqueue`.
- **Photo→3D avatar**: `Avatar.setAvatar({kind:'photo', imageUrl})` already works as a billboard; replace with a face-mesh reconstruction pipeline (e.g. PIFu / DECA) when needed.
- **GLB blendshape lip-sync**: `_buildGlb` loads the model; once a GLB has ARKit blendshapes, swap `mouth.scale.y` for `morphTargetInfluences`.
- **Multi-tenant scale-out**: SQLite is durable up to thousands of concurrent users when run on SSD with WAL; for higher scale, swap `UseSqlite` for `UseNpgsql` — the EF model and queries are unchanged.
