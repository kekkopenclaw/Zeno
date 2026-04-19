#!/usr/bin/env node
// Agent-LLM-driven summarizer for Obsidian vault logs
// Reads memory/*.md logs, calls OpenClaw agent/LLM to extract lessons, decisions, mistakes, milestones, and writes them to Agent-Omniking/auto-lessons/YYYY-MM-DD.md

const fs = require('fs');
const path = require('path');
const { spawnSync } = require('child_process');

const MEMORY_DIR = path.resolve(__dirname, '../memory');
const OUTPUT_DIR = path.resolve(__dirname, 'Agent-Omniking/auto-lessons');
const DATE = new Date().toISOString().slice(0, 10);
const OUT_FILE = path.join(OUTPUT_DIR, `${DATE}.md`);

function gatherMemoryLines(limit = 200) {
  let lines = [];
  if (!fs.existsSync(MEMORY_DIR)) return lines;
  for (const fname of fs.readdirSync(MEMORY_DIR)) {
    if (fname.endsWith('.md')) {
      const fileLines = fs.readFileSync(path.join(MEMORY_DIR, fname), 'utf8').split(/\r?\n/).filter(Boolean);
      lines = lines.concat(fileLines.slice(-limit));
    }
  }
  return lines.slice(-limit);
}

function agentSummarize(rawLog) {
  // Call OpenClaw LLM via CLI subagent if available (replace with correct call if needed for your stack)
  const prompt = `You are OpenClaw’s memory summarizer. Given raw memory lines, extract the most important new lessons, decisions, mistakes, and milestones as one lesson/decision/mistake/milestone per line, labeled ('lesson:', 'decision:', etc.), never duplicating existing lessons. Only emit new, relevant items.\n\nRecent memory:\n${rawLog}\n`;
  const result = spawnSync('openclaw', ['agent', 'summarize', '--stdin'], {
    input: prompt,
    encoding: 'utf8',
    maxBuffer: 1024 * 512
  });
  if (result.error) {
    console.error(result.error);
    return [];
  }
  return result.stdout.split(/\r?\n/).map(s => s.trim()).filter(Boolean);
}

fs.mkdirSync(OUTPUT_DIR, { recursive: true });
const memLines = gatherMemoryLines(200);
if (memLines.length === 0) {
  console.log('No memory logs found to summarize.');
  process.exit(0);
}
const lessons = agentSummarize(memLines.join('\n'));
if (lessons.length > 0) {
  fs.appendFileSync(OUT_FILE, lessons.join('\n') + '\n');
  console.log(`Agent summarizer wrote ${lessons.length} lessons to ${OUT_FILE}`);
  lessons.forEach(l => console.log('> ' + l));
} else {
  console.log('No new lessons extracted from memory logs.');
}
