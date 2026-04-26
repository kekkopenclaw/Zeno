---
name: cli-copilot-watcher
description: Watches tmux Copilot output file for new actionable content; triggers OpenClaw agent logic instantly (event-driven, no polling).
entry: index.js
user-invocable: false
---

# cli-copilot-watcher Skill

This skill provides instant, event-based integration between your Copilot CLI session (via tmux/pipe-pane) and your OpenClaw agent.

- Listens for changes to /tmp/copilot_output_ready.txt, which is written by your pipe-copilot.sh script after each Copilot step/output.
- When new (complete) output is detected, immediately calls your OpenClaw agent's workflow for analysis and next prompt generation.
- Zero polling/cron overhead. Only fires on real events.

## Usage
1. Start your Copilot CLI via tmux.
2. Run your pipe-copilot.sh script, integrated with tmux pipe-pane, to write Copilot responses to /tmp/copilot_output_ready.txt.
3. Register this skill in OpenClaw and launch the watcher (it will persist as long as OpenClaw session runs).
4. Replace the console.log in index.js with your agent handler logic (e.g. queue next Copilot instruction or analyze output).

## Integration
- Can be combined with manager-level agent skills that drive complex, autonomous CLI goal execution.
- Suitable for any agent or automation requiring instant, token-efficient CLI feedback.
