# MissionControl

An AI-powered workspace where autonomous agents operate continuously — a real-time, modular AI operating system.

**Updated:** March 29, 2026

---

## Tech Stack

| Layer | Technology | Details |
|---|---|---|
| Backend | .NET 10, ASP.NET Core | net10.0, nullable enabled |
| ORM | Entity Framework Core 10 | SQLite (dev), swappable to Postgres |
| Real-time | ASP.NET SignalR | AgentHub `/hubs/agent` |
| Background | IHostedService | BackgroundOrchestrationService, LogTailHostedService |
| Logging | Serilog | Console + rolling file (14 days), enriched with AgentName/Action/CorrelationId |
| API Docs | OpenAPI | Spec at `/openapi/v1.json` (no SwaggerUI) |
| Auth | None (dev) | See Security section |
| Frontend | Angular 21 | Standalone components, Signals API, Angular CLI 21 |
| Styling | TailwindCSS 4 | |
| UI | @angular/cdk 21 | drag-drop, overlays |
| SignalR client | @microsoft/signalr 10 | |
| Testing | Vitest 4 | |
| Agent runner | OpenClaw CLI | Isolated mc-{id} workspaces, log tailing |

---

## Architecture

```
MissionControl/
├── src/
│   ├── MissionControl.Domain/        # Entities, Enums, Events, Interfaces
│   │   ├── Entities/                 # Agent, TaskItem, Project, MemoryEntry, ActivityLog, LogEntry
│   │   ├── Enums/                    # AgentStatus, AgentRole (DBZ), TaskItemStatus, TaskPriority
│   │   ├── Events/                   # Domain events
│   │   └── Interfaces/               # IAgentRepository, IOpenClawRunner, ISignalRNotifier ...
│   ├── MissionControl.Application/   # DTOs, Services, Use Cases
│   │   ├── DTOs/                     # AgentDto, TaskItemDto, SpawnAgentDto ...
│   │   ├── Services/                 # AgentService, OrchestratorService, TaskService, MemoryService ...
│   │   └── Interfaces/               # ISignalRNotifier, ILogService
│   ├── MissionControl.Infrastructure/# EF Core, Repos, Hubs, Background Services
│   │   ├── Data/                     # AppDbContext, AppDbContextFactory
│   │   ├── Hubs/                     # AgentHub (SignalR)
│   │   ├── Migrations/               # EF migrations
│   │   ├── Repositories/             # All IRepository implementations
│   │   └── Services/                 # SignalRNotifier, BackgroundOrchestrationService,
│   │                                 #   OpenClawRunner, LogTailHostedService
│   └── MissionControl.API/           # Controllers, Middleware, Program.cs
└── client-app/                       # Angular 21 frontend
    └── src/app/
        ├── core/                     # Models, Services (API, SignalR, Agent, Memory...)
        └── features/                 # Dashboard, Agents, Tasks, Memory, Projects, Logs, People
```

### Task Flow Diagram

```
User creates Task
      │
      ▼
[Todo] ──(Orchestrator tick)──► [Planning]
                                    │ Whis scores complexity, picks agent
                                    ▼
                               [Ready] ──► agent.Status = Working
                                    │
                                    ▼
                               [Coding] ──(45s sim / real work)──► agent.Status = Idle
                                    │
                                    ▼
                               [Review] ──(Gohan, 25s)
                                    │
                          80% pass ─┤─ 20% fail
                               │               │
                               ▼               ▼
                            [Done]           [Fix] ──(retry)──► [Coding]
                                             (2nd fail → escalate to Vegeta)
```

### Log Routing Diagram

```
OpenClaw Agent (mc-{id})
      │ writes to ~/.openclaw/logs/mc-{id}.log
      ▼
LogTailHostedService (FileSystemWatcher)
      │ tails new lines
      ▼
ISignalRNotifier.NotifyAgentLogLineAsync(agentId, line)
      │                        │
      ▼                        ▼
AgentHub (SignalR)         ILogService (DB)
      │
      ▼
Angular SignalRService.agentLogLines signal
      │
      ▼
AgentLogsComponent (live feed per agent)
```

---

## Log Location

All backend and frontend error logs are stored at:
```
src/MissionControl.API/logs/missioncontrol-YYYYMMDD.log
```
Replace YYYYMMDD with today’s date.

---

## OpenClaw Integration

MissionControl can spawn real OpenClaw agents isolated per workspace via the `IOpenClawRunner` interface.

### Agent Isolation

Each agent gets its own:
- **ID**: `mc-{agentId}` (e.g. `mc-1`)
- **Workspace**: `~/.openclaw/workspaces/mc-{agentId}/`
- **Model**: configurable per agent (defaults to `ollama/llama3`)
- **Log file**: `~/.openclaw/logs/mc-{agentId}.log`

### Spawn / Pause / Resume

```
POST /api/agents/{id}/spawn   body: { "model": "ollama/llama3" }
POST /api/agents/{id}/pause
POST /api/agents/{id}/resume
```

The `OpenClawRunner` calls the OpenClaw CLI:
```bash
openclaw agents add mc-{id}
openclaw config set --agent mc-{id} workspace.root ~/.openclaw/workspaces/mc-{id}
openclaw config set --agent mc-{id} model.default {model}
# On pause:
openclaw agents pause mc-{id}
```

Degrades gracefully if OpenClaw CLI is not installed — DB state is still updated.

### Live Log Tailing

`LogTailHostedService` watches each active agent's log file via `FileSystemWatcher` and broadcasts new lines in real-time via SignalR (`AgentLogLine` event). The Angular `AgentLogsComponent` subscribes and appends lines live per-agent.

### Config

```json
"OpenClaw": {
  "ExecutablePath": "openclaw",
  "WorkspaceRoot": "~/.openclaw/workspaces",
  "LogRoot": "~/.openclaw/logs",
  "AgentPrefix": "mc-",
  "DefaultModel": "ollama/llama3"
}
```

---

## Agent Roles (Dragon Ball Z)

| Role | Responsibility |
|---|---|
| Whis | Orchestrator — task routing, state machine, retries, escalation |
| Beerus | Architect — high-level design decisions |
| Kakarot | Standard coder — moderate complexity |
| Vegeta | Advanced coder — high complexity / critical / escalations |
| Piccolo | Refactorer — cleanup, structure improvements |
| Gohan | Reviewer — inspects output, approves or rejects |
| Trunks | Memory & learning — stores summaries, updates routing weights |
| Bulma | Tooling — context injection, prompt construction |

---

## Security

> ⚠️ **Development config — not production-ready as-is.**

- **Auth:** None — all API and SignalR endpoints are open
- **CORS:** Any origin with credentials (`SetIsOriginAllowed(_ => true)`)
- **JWT/Roles:** Not implemented
- **Rate limiting:** Not implemented

### Production Hardening Checklist

- [ ] Add JWT bearer auth (`Microsoft.AspNetCore.Authentication.JwtBearer`)
- [ ] Restrict CORS to known origins
- [ ] Add rate limiting middleware
- [ ] Set `AllowedHosts` to your domain
- [ ] Enable HTTPS / HSTS
- [ ] Move secrets to environment variables / Azure Key Vault
- [ ] Enable EF query logging only in development
- [ ] Lock down SignalR hub authorization with `[Authorize]`

---

## Background Jobs & 24/7 Ops

| Service | Purpose | Interval |
|---|---|---|
| `BackgroundOrchestrationService` | Ticks orchestrator for all projects | 2s (configurable) |
| `LogTailHostedService` | Tails OpenClaw log files, broadcasts via SignalR | Real-time (FileSystemWatcher) |

Both are registered as `IHostedService` and start/stop with the app.

---

## Scaling & Deployment

- **Docker:** Mount volumes for `missioncontrol.db` and `logs/`
- **Database:** Change connection string to Postgres — just swap the EF provider
- **SignalR scale-out:** Add Redis backplane via `AddStackExchangeRedis`
- **Reverse proxy:** Nginx/Traefik in front of both API (:5083) and Angular (:4200) or serve Angular as static files from the API
- **Monitoring:** Serilog extendable to Application Insights, Seq, Grafana/Loki

---

## Complete Dependency Listing

### Backend (NuGet)

- `Microsoft.AspNetCore.OpenApi` 10.0.5
- `Microsoft.AspNetCore.SignalR.Core` 1.2.9
- `Microsoft.EntityFrameworkCore.Sqlite` 10.0.5
- `Microsoft.EntityFrameworkCore.Design` 10.0.5
- `Microsoft.Extensions.Hosting` 10.0.0
- `Microsoft.Extensions.Logging.Abstractions` 10.0.0
- `Serilog.AspNetCore` 9.0.0
- `Serilog.Sinks.File` 6.0.0
- `Serilog.Enrichers.Environment` 3.0.0

### Frontend (npm)

- `@angular/core` ^21.2.0
- `@angular/cdk` ^21.2.4
- `@angular/animations` ^21.2.6
- `@microsoft/signalr` ^10.0.0
- `tailwindcss` ^4.2.2
- `typescript` ~5.9.2
- `vitest` ^4.0.8
- `prettier` ^3.8.1

---

## Run Instructions

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js 20+](https://nodejs.org/)
- (Optional) [OpenClaw CLI](https://openclaw.ai) for real agent spawning

### 1. Start the Backend API

```bash
cd src/MissionControl.API
dotnet run
```

- API: http://localhost:5083
- OpenAPI spec: http://localhost:5083/openapi/v1.json
- SignalR Hub: `ws://localhost:5083/hubs/agent`
- Database (`missioncontrol.db`) is created and migrated automatically on first run

### 2. Start the Angular Frontend

```bash
cd client-app
npm install
npm start -- --host 0.0.0.0
```

- App: http://localhost:4200
- Remote access (phone/Tailscale): `http://<your-IP>:4200`

> Ensure your firewall/Tailscale ACLs allow access to port 4200.

---

## What Changed (March 29, 2026)

- **OpenClaw integration**: `IOpenClawRunner`, `OpenClawRunner`, `LogTailHostedService`
- **Agent lifecycle**: Spawn / Pause / Resume endpoints + UI buttons
- **Live log tailing**: FileSystemWatcher → SignalR → `AgentLogsComponent`
- **New Agent fields**: `OpenClawAgentId`, `IsPaused` (migration included)
- **Angular bug fixed**: `ExpressionChangedAfterItHasBeenCheckedError` on Memory and Agents pages — all `computed()` inputs converted to signals
- **Full signal hygiene**: `[ngModel]="sig()" (ngModelChange)="sig.set($event)"` pattern throughout

---

## What Changed (April 3, 2026) — Chroma Vectors, Reflection Loop, Semantic Search

### Vector Memory — Chroma at `./chroma_data` port 8000

MissionControl now embeds every distilled memory into a local [Chroma](https://www.trychroma.com/) vector store for dense semantic search.

**Setup (no Docker):**
```bash
pip install chromadb
chroma run --path ./chroma_data --port 8000
```

Config in `appsettings.json`:
```json
"Chroma": {
  "BaseUrl": "http://localhost:8000",
  "EmbeddingModel": "llama3"
}
```

- Collection: `mission_memories`
- Embeddings: Ollama `/api/embeddings` (same model as generation)
- **Graceful degradation**: if Chroma is not running, the rest of the pipeline continues unaffected — all Chroma methods log a warning and no-op.

**Semantic search endpoint:**
```
GET /api/memory/semantic?query=your+natural+language+query&topK=6
```
Returns top-K nearest neighbours by cosine similarity.

**Angular UI:** Memory tab → **🔍 Semantic** button → type a natural-language query → live results ranked by cosine distance. Results are served directly from the Chroma vector store via SignalR-less REST.

### Reflection Loop → `LESSONS.md`

After every task distillation (nightly KAIROS daemon or Pipeline Test), `ReflectionLoopService` asks Ollama:

> "Based on this outcome, what ONE new general rule should be applied to ALL future tasks?"

If Ollama returns an actionable rule, it is appended to `LESSONS.md` (configurable path):
```
- [2026-04-03 02:14 UTC | task 42] Always validate input schema before persisting to the vector store.
```

Config:
```json
"Reflection": {
  "Enabled": true,
  "Model": "llama3",
  "LessonsFile": "./LESSONS.md"
}
```

Rules from `LESSONS.md` can be loaded via `ReflectionLoopService.LoadLessonsAsync()` and injected into future agent prompts.

### KAIROS Daemon — Layer 4 (Vectors)

The nightly KAIROS 2 AM daemon now has a **4-layer memory pipeline**:

| Layer | Store | Written by |
|---|---|---|
| 1 | Session (in-process) | Every agent loop run |
| 2 | MemorySummary (SQLite) | KairosMemoryService |
| 3 | MemoryEntry (SQLite) | KairosMemoryService |
| 4 | Chroma vector store | ChromaMemoryVectorService |

After saving to SQLite (layers 2+3), the daemon now also embeds the distilled content into Chroma and runs the reflection loop.

### Pipeline Test — Expanded Steps

`POST /api/pipeline-test` now runs 9 steps (was 7):

| Step | Description |
|---|---|
| VectorStore | Embeds memory → Chroma, verifies top-K retrieval |
| Reflection | Derives new rule via Ollama → appends to LESSONS.md |

The response payload now includes `vectorId`, `chromaOk`, and `reflectionRule`.

### New Package

- `Microsoft.SemanticKernel` 1.71.0 — added to `MissionControl.Infrastructure`
  (patched version ≥1.71.0; earlier versions had a critical Arbitrary File Write CVE)
