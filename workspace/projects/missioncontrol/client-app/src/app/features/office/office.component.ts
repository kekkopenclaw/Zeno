import { Component, OnInit, inject, signal } from '@angular/core';
import { AgentService } from '../../core/services/agent.service';
import type { Agent } from '../../core/models';

interface OfficeAgent {
  name: string;
  emoji: string;
  role: string;
  x: number;
  y: number;
  color: string;
  deskColor: string;
  busy: boolean;
  tooltip?: string;
}

@Component({
  selector: 'app-office',
  standalone: true,
  imports: [],
  template: `
<div class="office-page" data-testid="office-page">
  <div class="office-header">
    <button class="btn btn-ghost">Start Chat</button>
    <div>
      <h1 class="page-title">AI Lab</h1>
      <p class="page-subtitle">Live agent workspace · 8 agents online</p>
    </div>
  </div>

  <div class="office-wrap">
    <div class="office-room">
      <!-- Checkered floor -->
      <div class="floor-grid">
        @for (cell of floorCells; track $index) {
          <div class="floor-cell" [class.dark]="cell"></div>
        }
      </div>

      <!-- Conference table -->
      <div class="conf-table">
        <div class="conf-table-top"></div>
      </div>

      <!-- Agent desks + sprites -->
      @for (agent of officeAgents(); track agent.name) {
        <div class="desk-group" [style.left.px]="agent.x" [style.top.px]="agent.y">
          <!-- Desk -->
          <div class="desk" [style.background]="agent.deskColor">
            <div class="monitor"></div>
          </div>
          <!-- Sprite -->
          <div class="sprite" [style.background]="agent.color"
               [class.bounce]="agent.busy"
               [title]="agent.tooltip ?? ''">
            <span class="sprite-face">{{agent.busy ? '😤' : '😐'}}</span>
          </div>
          <!-- Name badge -->
          <div class="agent-badge" [style.background]="agent.color + '22'" [style.border-color]="agent.color + '55'">
            {{agent.name}}
          </div>
          <!-- Activity bubble -->
          @if (agent.busy && agent.tooltip) {
            <div class="activity-bubble">{{agent.tooltip}}</div>
          }
        </div>
      }

      <!-- Plants -->
      <div class="plant" style="left:8px;top:320px">🌿</div>
      <div class="plant" style="right:8px;top:320px">🌿</div>

      <!-- Door/PC indicator -->
      <div class="pc-indicator" style="left:16px;top:200px">🖥️</div>
    </div>
  </div>

  <!-- Agent status list -->
  <div class="agent-status-list">
    @for (a of officeAgents(); track a.name) {
      <div class="status-chip" [style.border-color]="a.color + '44'">
        <div class="status-dot" [style.background]="a.busy ? a.color : '#3f3f46'"></div>
        <span class="status-emoji">{{a.emoji}}</span>
        <span class="status-name">{{a.name}}</span>
        <span class="status-role">{{a.role}}</span>
      </div>
    }
  </div>
</div>
  `,
  styles: [`
    .office-page { display: flex; flex-direction: column; height: 100%; padding: 16px; overflow: hidden; }
    .office-header {
      display: flex; align-items: center; gap: 16px;
      margin-bottom: 12px; flex-shrink: 0;
    }
    .page-title { font-size: 18px; font-weight: 700; color: var(--text-primary); }
    .page-subtitle { font-size: 11px; color: var(--text-muted); }

    .office-wrap { flex: 1; overflow: hidden; display: flex; justify-content: center; min-height: 0; }
    .office-room {
      position: relative;
      width: 600px;
      height: 380px;
      background: #1a1a20;
      border: 1px solid var(--border);
      border-radius: 12px;
      overflow: hidden;
      flex-shrink: 0;
    }

    /* Checkered floor */
    .floor-grid {
      position: absolute; inset: 0;
      display: grid;
      grid-template-columns: repeat(20, 1fr);
      grid-template-rows: repeat(13, 1fr);
    }
    .floor-cell { background: #1e1e28; }
    .floor-cell.dark { background: #181820; }

    /* Conference table */
    .conf-table {
      position: absolute;
      left: 50%; top: 50%;
      transform: translate(-50%, -50%);
      width: 100px; height: 60px;
      background: radial-gradient(ellipse, #2a2a35 60%, #1e1e28 100%);
      border: 2px solid #3a3a4a;
      border-radius: 50%;
    }

    /* Desk groups */
    .desk-group { position: absolute; display: flex; flex-direction: column; align-items: center; gap: 2px; }
    .desk {
      width: 36px; height: 20px;
      border-radius: 4px;
      border: 1px solid rgba(255,255,255,0.15);
      display: flex; align-items: center; justify-content: flex-end;
      padding-right: 4px;
    }
    .monitor {
      width: 10px; height: 9px;
      background: #1a1a2e;
      border: 1px solid #4a4a6a;
      border-radius: 2px;
    }
    .monitor::before {
      content: '';
      display: block;
      width: 100%; height: 100%;
      background: rgba(100,100,255,0.3);
      border-radius: 1px;
    }
    .sprite {
      width: 20px; height: 20px;
      border-radius: 4px;
      display: flex; align-items: center; justify-content: center;
      border: 1px solid rgba(255,255,255,0.2);
      font-size: 12px;
      cursor: pointer;
    }
    .sprite.bounce { animation: bounce 1.5s infinite; }
    @keyframes bounce {
      0%, 100% { transform: translateY(0); }
      50% { transform: translateY(-3px); }
    }
    .agent-badge {
      font-size: 9px; font-weight: 600; letter-spacing: 0.03em;
      border: 1px solid;
      border-radius: 3px; padding: 1px 4px;
      color: var(--text-secondary);
      white-space: nowrap;
      background: var(--bg-elevated);
    }
    .activity-bubble {
      position: absolute;
      top: -22px; left: 50%;
      transform: translateX(-50%);
      background: var(--bg-elevated);
      border: 1px solid var(--border);
      border-radius: 6px;
      padding: 3px 7px;
      font-size: 9px; white-space: nowrap; color: var(--text-secondary);
      pointer-events: none;
    }
    .plant { position: absolute; font-size: 20px; }
    .pc-indicator { position: absolute; font-size: 16px; }

    /* Status bar */
    .agent-status-list {
      flex-shrink: 0;
      display: flex; gap: 8px; flex-wrap: wrap;
      margin-top: 10px;
    }
    .status-chip {
      display: flex; align-items: center; gap: 5px;
      background: var(--bg-surface);
      border: 1px solid;
      border-radius: 6px;
      padding: 4px 10px;
    }
    .status-dot { width: 6px; height: 6px; border-radius: 50%; flex-shrink: 0; }
    .status-emoji { font-size: 13px; }
    .status-name { font-size: 12px; font-weight: 500; color: var(--text-primary); }
    .status-role { font-size: 10px; color: var(--text-muted); }
  `],
})
export class OfficeComponent implements OnInit {
  private svc = inject(AgentService);
  private _agents = signal<Agent[]>([]);

  // Generate floor cells (alternating dark pattern)
  floorCells = Array.from({ length: 260 }, (_, i) => {
    const row = Math.floor(i / 20);
    const col = i % 20;
    return (row + col) % 2 === 0;
  });

  officeAgents = () => {
    const agents = this._agents();
    const layout: { name: string; x: number; y: number; tooltip?: string }[] = [
      { name: 'Whis',    x: 260, y: 20,  tooltip: 'Build Council — routing tasks' },
      { name: 'Beerus',  x: 460, y: 60 },
      { name: 'Kakarot', x: 80,  y: 80,  tooltip: 'Coding task #2' },
      { name: 'Vegeta',  x: 460, y: 140 },
      { name: 'Piccolo', x: 20,  y: 200 },
      { name: 'Gohan',   x: 500, y: 240 },
      { name: 'Trunks',  x: 160, y: 280 },
      { name: 'Bulma',   x: 360, y: 300 },
    ];

    const colors: Record<string, { sprite: string; desk: string }> = {
      Whis:    { sprite: '#7c3aed', desk: '#2d2048' },
      Beerus:  { sprite: '#3b82f6', desk: '#1a2a48' },
      Kakarot: { sprite: '#f59e0b', desk: '#3a2a10' },
      Vegeta:  { sprite: '#ef4444', desk: '#3a1a1a' },
      Piccolo: { sprite: '#22c55e', desk: '#0a2a18' },
      Gohan:   { sprite: '#8b5cf6', desk: '#1e1a40' },
      Trunks:  { sprite: '#06b6d4', desk: '#0a2a30' },
      Bulma:   { sprite: '#ec4899', desk: '#2a0a20' },
    };

    return layout.map(l => {
      const agent = agents.find(a => a.name === l.name);
      const c = colors[l.name] ?? { sprite: '#71717a', desk: '#2a2a2a' };
      return {
        name: l.name,
        emoji: agent?.emoji ?? '🤖',
        role: agent?.role ?? '',
        x: l.x, y: l.y,
        color: c.sprite,
        deskColor: c.desk,
        busy: agent?.status === 'Working' || agent?.status === 'Thinking',
        tooltip: l.tooltip,
      } as OfficeAgent;
    });
  };

  ngOnInit(): void { this.svc.getAll().subscribe(d => this._agents.set(d)); }
}
