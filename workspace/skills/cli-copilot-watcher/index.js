const fs = require('fs');
const path = '/tmp/copilot_output_ready.txt';

// Handler to send output to your agent's processing function (replace console.log with actual logic)
function handleCopilotOutput(output) {
  // TODO: Call your agent skill/handler. For now, just log:
  console.log('[Copilot Output Event]:\n' + output);
  // Example for OpenClaw: you might queue a job or call another module
}

// Setup file watcher (low overhead)
fs.watchFile(path, { interval: 500 }, (curr, prev) => {
  if (curr.mtime !== prev.mtime) {
    const output = fs.readFileSync(path, 'utf8');
    handleCopilotOutput(output);
  }
});

console.log('cli-copilot-watcher started, watching ' + path);
