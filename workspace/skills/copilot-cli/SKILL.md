---
name: copilot-cli
description: Direct tmux-based controller for GitHub Copilot CLI. Optimized for hash-watcher triggers.
entry: index.js
user-invocable: true
---

# Copilot CLI Master Skill

**IMPORTANT:**
ALWAYS append `Enter` after every `tmux send-keys` command in agent actions. Every Copilot command, answer, or script sent must be followed by Enter—or Copilot will not process the input!

## Available Tools:
- **execute**: Sends any string as a command to the Copilot tmux pane (with Enter).
- **confirm**: Sends 'y' + Enter (for y/n prompts).
- **rescue**: Sends Ctrl+C (to break terminal hangs).
- **read_state**: Captures the current terminal buffer for analysis (returns last 100 lines).

## Usage Example:
```js
copilot.run({ action: "execute", payload: "npm run lint", pane: "copilot.0" });
copilot.run({ action: "read_state", pane: "copilot.0" });
```

- The `pane` argument defaults to `%1` (inject as needed).
