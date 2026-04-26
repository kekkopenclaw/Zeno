#!/bin/bash
# ultimate_robocop_watcher.sh

PANE="%1"
STALL_LIMIT=15 # How many cycles to wait before a "Hard Rescue"
IDLE_CYCLES=0
LAST_HASH=""

echo "🤖 Ultimate Watcher Active. Monitoring Pane $PANE..."

while true; do
  # 1. Capture and Hash
  CONTENT=$(tmux capture-pane -pt "$PANE")
  HASH=$(echo "$CONTENT" | md5sum)

  if [ "$HASH" == "$LAST_HASH" ]; then
    ((IDLE_CYCLES++))
    
    # CASE A: Ready to act (Prompt is visible)
    if [[ "$CONTENT" =~ [\$\>\?\)] ]]; then
      if [ "$IDLE_CYCLES" -eq 1 ]; then
        echo "✨ Terminal Idle. Pinging Agent..."
        openclaw chat "Terminal is ready. Last 10 lines:\n$(echo \"$CONTENT\" | tail -n 10)"
      fi
    
    # CASE B: Hard Stalled (No prompt, no movement)
    elif [ "$IDLE_CYCLES" -ge "$STALL_LIMIT" ]; then
      echo "⚠️ STALL DETECTED. Sending Ctrl+C and asking Agent for a reboot..."
      tmux send-keys -t "$PANE" C-c
      openclaw chat "The terminal was stuck for 60s. I sent a Ctrl+C. Check the state."
      IDLE_CYCLES=0
    fi
  else
    # Case C: Active typing is happening
    IDLE_CYCLES=0
    LAST_HASH="$HASH"
  fi

  sleep 4
done
