#!/usr/bin/env node
// Parse Obsidian Layer 3 markdown for lessons, decisions, mistakes, milestones, and write to obsidian/vault_facts.db (SQLite)
// Fields: id, type, date, source_file, content
// Usage: node obsidian/l3-to-sqlite.js [vaultDir]

const fs = require('fs');
const path = require('path');
const sqlite3 = require('sqlite3').verbose();

const VAULT = process.argv[2] || path.resolve(__dirname);
const DB_PATH = path.join(__dirname, 'vault_facts.db');

const RE_ENTRY = /^(lesson|decision|mistake|milestone):\s*(.*)/i;
const RE_DATE = /^(\d{4}-\d{2}-\d{2})/;

function findAllMdFiles(root) {
  let files = [];
  for (const f of fs.readdirSync(root)) {
    const fp = path.join(root, f);
    if (fs.statSync(fp).isDirectory()) files = files.concat(findAllMdFiles(fp));
    else if (f.endsWith('.md')) files.push(fp);
  }
  return files;
}

function extractEntries(file) {
  const entries = [];
  const dateMatch = file.match(RE_DATE);
  const defaultDate = dateMatch ? dateMatch[1] : null;
  const lines = fs.readFileSync(file, 'utf8').split(/\r?\n/);
  lines.forEach(line => {
    const m = line.match(RE_ENTRY);
    if (m) {
      entries.push({
        type: m[1].toLowerCase(),
        content: m[2].trim(),
        date: defaultDate,
        source_file: path.relative(VAULT, file)
      });
    }
  });
  return entries;
}

// Setup DB and table
const db = new sqlite3.Database(DB_PATH);
db.serialize(() => {
  db.run(`CREATE TABLE IF NOT EXISTS facts (\n  id INTEGER PRIMARY KEY AUTOINCREMENT,\n  type TEXT,\n  date TEXT,\n  source_file TEXT,\n  content TEXT,\n  UNIQUE(type, date, source_file, content)\n)`);

  const files = findAllMdFiles(VAULT);
  let inserted = 0;
  files.forEach(f => {
    const entries = extractEntries(f);
    entries.forEach(e => {
      db.run(`INSERT OR IGNORE INTO facts (type, date, source_file, content) VALUES (?, ?, ?, ?)`,
        [e.type, e.date, e.source_file, e.content],
        err => { if (!err) inserted++; }
      );
    });
  });
  db.close(() => {
    console.log(`Scan complete. Inserted ${inserted} new facts into ${DB_PATH}.`);
  });
});
