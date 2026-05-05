# AI BABA-G — Memory-Aware Mystical AI Companion

> An optimization rebuild of the existing **AI-BABA-G** portal (in `AI-BABA-G/AI.Baba.Web/`). Adds persistent SQLite memory, vector recall, real-time SSE streaming, custom personalities, an avatar marketplace, and an optional 3D Three.js avatar with viseme lip-sync — all on top of the original gold/dark mystical UI.

## Highlights

- **Persistent SQLite memory** via EF Core (`AI.Baba.Web/Data/BabaDbContext.cs`). Tables auto-create at startup. Server RAM stays low — long-term state lives on disk.
- **Vector recall** — every memory is embedded (Ollama `nomic-embed-text` when available, deterministic local fallback otherwise) and recalled by cosine similarity weighted by importance and recency.
- **Memory-aware Ollama prompt** — top-K relevant memories are injected silently into the system prompt before each generation, so BABA "feels" like it knows you.
- **Sign-up gate** — guests still get a beautiful chat (legacy `/api/ask` endpoint preserved). Persistent memory, saved chats, custom personalities, and custom avatars unlock by registering.
- **Real-time SSE streaming** chat (`/api/chat/stream` and `/api/chat/guest/stream`) for ChatGPT-like token-by-token delivery.
- **3D viseme avatar** — toggle the "robot avatar" switch in settings to swap the emoji for a Three.js robot with phoneme-driven mouth shapes, blink, idle motion, and listening pulse. Photo billboards from your own picture and GLB model URLs are also supported.
- **Voice in & out + interrupt** — Web Speech API for STT, browser TTS, continuous-conversation mode, and ChatGPT-style interrupt with **Esc** or **Stop** button.
- **5 preset personalities** (The Sage, The Philosopher, The Healer, The Elder, The Storyteller) plus a custom personality builder. Each maps to a preferred avatar and mindset.
- **5 preset avatars** (the original mystical emoji line-up) + the 3D Robot, plus a creator for emoji / 3D / photo / GLB.
- **Pages** — Chat, Auth, Memory, Personalities, Avatars, About, Terms.
- **Scalable** — async streaming end-to-end, indexed SQLite queries, IP rate limiting, Docker-ready, drop-in PostgreSQL upgrade path (`UseSqlite` → `UseNpgsql`).

## Quick start

### Local dotnet

```bash
# install Ollama (one-time)
curl -fsSL https://ollama.com/install.sh | sh
ollama pull llama3.2
ollama pull nomic-embed-text

cd AI-BABA-G/AI.Baba.Web
dotnet run --urls=http://0.0.0.0:5099
```

Open http://localhost:5099. The mystical chat page loads as guest. Click the **Sign in / up** pill in the top-right to register.

### Docker compose (BABA + Ollama in one stack)

```bash
docker compose up --build
docker compose exec ollama ollama pull llama3.2
docker compose exec ollama ollama pull nomic-embed-text
```

Open http://localhost:8080.

## Configuration

`AI-BABA-G/AI.Baba.Web/appsettings.json` (override with env vars using `__`):

| Key | Default | Notes |
|-----|---------|-------|
| `Database:Path` | `baba.db` | Single-file SQLite DB |
| `Jwt:Key` | _(replace this)_ | Long random secret used to sign tokens |
| `Ollama:BaseUrl` | `http://localhost:11434` | Ollama API endpoint |
| `Ollama:ChatModel` | `llama3.2` | Any chat model you've pulled |
| `Ollama:EmbeddingModel` | `nomic-embed-text` | Falls back to local hash embeddings if missing |
| `IpRateLimiting.GeneralRules` | guest 20/min, chat 60/min, auth 30/min, ask 60/min | Tune for scale |

## Architecture

```
Browser
 ├── Existing mystical UI (gold/dark, Cinzel font, 5 emoji avatars + 5 mindsets)
 ├── Optional Three.js avatar (viseme lip-sync, blink, listening pulse)
 ├── Web Speech STT  ──► sends user text
 ├── SpeechSynthesis TTS ◄── streams sentence-by-sentence from BABA
 └── SSE client       ◄── streams tokens from /api/chat/stream

ASP.NET Core 8 API (single project: AI-BABA-G/AI.Baba.Web/)
 ├── /api/auth (register, login, me)            JWT
 ├── /api/chat/stream  (SSE, authed, memory-aware)
 ├── /api/chat/guest/stream  (SSE, no memory)
 ├── /api/ask  (legacy, kept for backward compatibility)
 ├── /api/memory  (list, add, recall, delete)   vector recall
 ├── /api/personalities  (CRUD, presets + custom)
 └── /api/avatars  (CRUD, presets + custom)

EF Core ──► SQLite (single file, auto-created on startup)
 ├── Users, Conversations, ChatMessages
 ├── MemoryEntries (with embedding bytes + dim)
 └── Personalities, Avatars

Ollama (external service)
 ├── /api/chat        (streaming chat completions)
 └── /api/embeddings  (nomic-embed-text by default)
```

## File map

```
AI-BABA-G/AI.Baba.Web/
  Program.cs                       # DI, JWT, CORS, rate limit, EnsureCreated, seed presets
  AI.Baba.Web.csproj               # net8.0 with EF Core / JWT / BCrypt / RateLimit deps
  Models/
    ChatModels.cs                  # AskRequest/AskResponse/UserMemory + new DTOs
    Entities.cs                    # User, Conversation, ChatMessage, MemoryEntry, Personality, Avatar
  Data/BabaDbContext.cs            # EF Core ctx + indices
  Services/
    AuthService.cs                 # JWT + BCrypt
    EmbeddingService.cs            # Ollama embeddings + local hash fallback
    MemoryService.cs               # legacy session memory + persistent vector memory
    OllamaService.cs               # legacy Generate + new streaming chat
  Controllers/
    HomeController.cs              # Index, Auth, Memory, Personalities, Avatars, About, Terms
    ApiController.cs               # legacy /api/ask, now memory-aware when authed
    AuthController.cs              # register, login, me
    ChatController.cs              # SSE stream for guest + authed, conversation CRUD
    MemoryController.cs            # list/add/recall/delete
    PersonalityController.cs
    AvatarController.cs
  Views/
    Home/Index.cshtml              # original mystical UI + new auth pill, conv list, 3D toggle, footer links
    Home/Auth.cshtml               # sign in / sign up
    Home/Memory.cshtml             # add/list/delete persistent memories
    Home/Personalities.cshtml      # preset + custom personality cards
    Home/Avatars.cshtml            # marketplace + creator (emoji/robot/photo/glb)
    Home/About.cshtml              # explains memory model & privacy
    Home/Terms.cshtml              # T&Cs
    Shared/_PortalLayout.cshtml    # shared layout for the new pages
  wwwroot/
    css/site.css                   # original gold theme + portal additions
    js/app.js                      # ES module: SSE streaming, auth, conv history, interrupt
    js/avatar.js                   # Three.js avatar with viseme lip-sync
    js/voice.js                    # Web Speech STT + TTS with sentence buffering
Dockerfile
docker-compose.yml                 # BABA + Ollama
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

The frontend pushes streaming text to two consumers in parallel:

1. **The speech bubble** (immediate display).
2. **The voice/avatar pipeline** — characters are mapped to phoneme groups (`aa`, `e`, `i`, `o`, `u`, `pp`, `ff`, `ss`, …), each with a target mouth-open value. The Three.js avatar consumes that schedule at ~16 ms ticks. When the streamed sentence ends with `.`/`!`/`?`/`\n`, the sentence is queued into `SpeechSynthesis` for natural TTS.

ChatGPT-style interrupt: pressing **Esc**, clicking **Stop**, or **speaking over BABA** while continuous mode is on cancels both the SSE stream and the active TTS utterance.
