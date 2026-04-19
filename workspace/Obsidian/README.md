# Obsidian Agent Memory System – Layer 3 (QMD-powered, Lossless, Portable)

**v2026-04-19 / System Confirmed on Node.js 22.x / OpenClaw mainline / Official QMD Workflow**

---

## 🎯 What You Have (As Confirmed)
- Robust, portable, **multi-layer agent memory system** for OpenClaw, powered by official QMD (Queryable Markdown Database).
- All logs, lessons, context, and decision records are written in markdown (obsidian/Agent-Omniking/, etc). No proprietary lock-in—future-proof and universally human/agent-readable.
- Fast, semantic, hybrid local search for any lesson/decision/aha/project via:
    - `qmd search "keyword"` (instant, no model load)
    - `qmd vsearch "your question"` (vector/semantic search)
    - `qmd query "full-context complex query"` (hybrid w/ LLM re-ranking—trigger model download the first time)
- Automated vault indexing: obisidan/ is fully indexed as the QMD collection 'obsidian'. New files are auto-detected for embedding/search refresh.
- Completely replaces and upgrades all legacy scripts (layer3-search.js, lossless-claw-query.js, etc.)—your setup is now standard, future-proof OpenClaw.

---

## 🚀 Agent Workflow (Detailed)

### 1. Startup/Bootstrap
- On session start, the agent reads SOUL.md, AGENTS.md, USER.md, and DREAMS.md for operational rules, user info, and context.
- Layer 2 logs (memory/*.md) provide recent short-term memory.
- Layer 3 (Obsidian vault, QMD-indexed): all lessons, context, and “checkpointed” facts.

### 2. Indexing & Embeddings (One-time and Automated)
- To (re)add a vault:
    ```
    qmd collection add ~/.openclaw/workspace/obsidian --name obsidian
    qmd embed
    ```
- Collection is called 'obsidian'; all markdown files are included.
- Embeddings are automatically refreshed via `qmd embed` (run nightly or on-demand).
- All indexing and model cache is stored locally in ~/.cache/qmd/ (never sent to third parties).

### 3. Agent & Human Recall
- Search the vault instantly for:
    - "project reboot", "mistake", "aha", etc.—any keyword, lesson, or project
    - Use QMD queries in your agent pipeline/DREAMS.md for nightly recap/context injection:
      ```
      qmd search "critical decision"
      qmd vsearch "how did we handle X"
      qmd query "all lessons learned from Y"
      ```
- Results are scored/resorted by relevance and provenance (filename+context).
- Agent/human can instantly answer “What did I decide about X?”, “Show all lessons on Y” regardless of age/recency.

### 4. Context Injection & Automation
- Add or tune any topic/phrase in DREAMS.md for nightly/periodic agent recall (memory refresh, decision context, checkpoint review, etc.).
- To automate embedding, search, and recall for continuous improvement—just add QMD shell calls to DREAMS.md.
- Full history/context is always available to agent prompts—standard in OpenClaw SOUL.md/DREAMS.md flows.

---

## 🛠️ Key `qmd` Commands
- Add your Obsidian vault:
    qmd collection add ~/.openclaw/workspace/obsidian --name obsidian
- Embed for semantic search:
    qmd embed
- Fast keyword search:
    qmd search "keywords"
- Semantic search:
    qmd vsearch "your question"
- Best-quality rerank/hybrid:
    qmd query "complex context query"
- Check QMD status/collections:
    qmd status

---

## 🧠 Guarantees & Best Practice
- All important memory, lessons, and context logs checkpoint to markdown—never lost to token bloat or forgotten context.
- QMD guarantees fast, lossless recall—whether your data is 1 day or 4 years old.
- Portable, auditable, flexible: works on all UNIX/macOS and under WSL2 on Windows.
- No npm or plugin headaches. One QMD upgrade = instant new features/semantic search.
- Complies with OpenClaw, agent, and SOUL prompt conventions.

---

## How to Onboard a New Agent
1. Clone obsidian/ to their workspace.
2. Run `qmd collection add ...` and `qmd embed` (see above).
3. Add their favorite queries/topics to DREAMS.md for nightly recall.
4. Done—future/agent/human proofed memory forever.

---

For detailed QMD options: see [tobi/qmd on GitHub](https://github.com/tobi/qmd) or run `qmd --help`.

Simplicity, reliability, and instant recall are now yours. If you need a new workflow or agent wiring, add a script/call to DREAMS.md or ask your agent for automation help!