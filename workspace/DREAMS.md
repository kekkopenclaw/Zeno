# DREAMS.md – Automated L3 Memory (QMD Workflow)

## Nightly & Event-Driven Agent Memory Consolidation (ACTIVE)

1. **QMD Vault Refresh:**
   - Refreshes all Obsidian markdown for new/changed files:
     ```
     qmd embed
     ```
   - Run nightly, at agent compaction, or at session end.
   - Keeps the semantic and keyword index always in sync with your vault additions.

2. **Agent Search Recap & Context Injection:**
   - Example QMD context queries for nightly injection or session start. Add/search for as many topics as matter:
     ```
     qmd search "critical decision"
     qmd vsearch "how did we handle failures"
     qmd query "all lessons learned from onboarding"
     ```
   - Outputs are logged or passed into session context buffer for reasoning/agent recall/improvement.
   - Tweak queries as your workflow evolves—add/remove as priorities shift!

3. **Fully Automated Agent Memory:**
   - No manual triggers required. Just keep DREAMS.md policies current—agent will always:
     - Consolidate and embed nightly
     - Run smart context queries for prioritized topics
     - Inject new lessons and summaries at next session/compaction/start

---

## Example DREAMS.md Wiring

- Nightly cron/automation (add to agent task scheduler):
  - `qmd embed` (re-index/semantic embed vault)
  - For each topic: `qmd search "..."` / `vsearch` / `query`
  - Cache or inject agent/human context as needed

- Agent startup/session-warm:
  - For every context cue in DREAMS.md, shell out to QMD and inject top results

---

**Edit DREAMS.md any time** to modify search focus, update recap topics, or set injection cadence. QMD handles all record-keeping and semantic indexing: Your “forever memory” is now fully automated and agent-empowered.
