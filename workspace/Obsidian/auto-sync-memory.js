#!/usr/bin/env node
// Auto-discover agent memory folders, summarize, sync with Obsidian and vault_facts.db
const fs = require('fs');
const path = require('path');
const { spawnSync } = require('child_process');
const sqlite3 = require('sqlite3').verbose();

const OBSIDIAN_ROOT = __dirname;
const DB_PATH = path.join(OBSIDIAN_ROOT, 'vault_facts.db');

// Recursively find all Agent-* folders from multiple roots
function getAgentFolders() {
  const roots = [
    OBSIDIAN_ROOT,
    path.join(OBSIDIAN_ROOT, '..'),
    path.join(OBSIDIAN_ROOT, '../projects'),
    path.resolve(OBSIDIAN_ROOT, '../../workspaces'), // main agents location
    path.resolve(OBSIDIAN_ROOT, '../../workspaces/obsidian'),
    path.resolve(OBSIDIAN_ROOT, '../../workspaces/projects'),
  ];
  const agentFolders = new Set();
  function findAgents(dir) {
    if (!fs.existsSync(dir)) return;
    fs.readdirSync(dir).forEach(f => {
      const fullPath = path.join(dir, f);
      if (fs.statSync(fullPath).isDirectory()) {
        if (f.startsWith('Agent-')) {
          agentFolders.add(fullPath);
        } else {
          findAgents(fullPath);
        }
      }
    });
  }
  roots.forEach(findAgents);
  return Array.from(agentFolders);
}

function summarizeAgent(agentDir) {
  const logs = [];
  // Recursively collect all .md files in agentDir and subfolders
  function collectMdFiles(dir) {
    let files = [];
    fs.readdirSync(dir).forEach(f => {
      const fullPath = path.join(dir, f);
      if (fs.statSync(fullPath).isDirectory()) {
        files = files.concat(collectMdFiles(fullPath));
      } else if (f.endsWith('.md')) {
        files.push(fullPath);
      }
    });
    return files;
  }
  collectMdFiles(agentDir).forEach(file => {
    logs.push(fs.readFileSync(file, 'utf8'));
  });
  const rawLog = logs.join('\n');
  // Summarize using OpenClaw agent (replace with actual call)
  const result = spawnSync('openclaw', ['agent', 'summarize', '--stdin'], { input: rawLog, encoding: 'utf8' });
  let summary = result && result.stdout ? result.stdout.trim() : '';
  if (!summary) summary = rawLog.trim();
  // Write summary to lessons/YYYY-MM-DD.md
  const lessonsDir = path.join(agentDir, 'lessons');
  if (!fs.existsSync(lessonsDir)) fs.mkdirSync(lessonsDir);
  const date = new Date().toISOString().slice(0, 10);
  fs.writeFileSync(path.join(lessonsDir, `${date}.md`), summary + '\n', { flag: 'a' });
  // Also update vault_facts.db
  // Create facts table if not exists
  const db = new sqlite3.Database(DB_PATH);
  db.serialize(() => {
    db.run('CREATE TABLE IF NOT EXISTS facts (id INTEGER PRIMARY KEY AUTOINCREMENT, date TEXT, agent TEXT, content TEXT)');
    summary.split('\n').forEach(line => {
      if (line.trim()) {
        db.run('INSERT INTO facts (date, agent, content) VALUES (?, ?, ?)', [date, path.basename(agentDir), line]);
      }
    });
  });
  db.close();
}

function syncAllAgents() {
  getAgentFolders().forEach(summarizeAgent);
}

syncAllAgents();
