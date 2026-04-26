// ALWAYS append Enter after every tmux send-keys command!
const { execSync } = require('child_process');

function tmuxExecCommand(pane, cmd) {
  // Ensures Enter is always sent after the command
  execSync(`tmux send-keys -t ${pane} "${cmd}" Enter`);
}

module.exports = {
  async run(input) {
    const { action, payload, pane = '%1' } = input;

    try {
      switch(action) {
        case 'execute':
          tmuxExecCommand(pane, payload);
          return `Executed: ${payload}`;
        case 'confirm':
          tmuxExecCommand(pane, 'y');
          return "Sent confirmation (y).";
        case 'rescue':
          execSync(`tmux send-keys -t ${pane} C-c`);
          return "Sent Rescue Signal (Ctrl+C).";
        case 'read_state':
          const out = execSync(`tmux capture-pane -pt ${pane} -S -100`).toString();
          return out;
        default:
          return "Unknown action. Use execute, confirm, rescue, or read_state.";
      }
    } catch (e) {
      return `Error: ${e.message}`;
    }
  }
}
