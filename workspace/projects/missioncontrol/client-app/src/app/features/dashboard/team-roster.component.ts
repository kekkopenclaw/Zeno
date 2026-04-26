import { Component, Input, Output, EventEmitter } from '@angular/core';
import type { Team, Agent } from '../../core/models';

import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-team-roster',
  standalone: true,
  imports: [CommonModule],
  template: `
  <div class="team-roster" data-testid="team-roster">
    <div class="team-header">
      <span class="team-title">{{team.name}}</span>
      <span class="team-desc">{{team.description}}</span>
    </div>
    <div class="team-agents">
      <div class="agent-row" *ngFor="let agent of agents">
        <div class="agent-main">
          <span class="agent-name">{{agent.name}}</span>
          <span class="agent-role">{{agent.role}}</span>
          <span class="agent-model">{{agent.model}}</span>
        </div>
        <button class="remove-agent-btn" title="Remove agent" (click)="removeAgent.emit(agent)">×</button>
      </div>
      <div *ngIf="agents.length === 0" class="empty-state">No teammates yet</div>
    </div>
  </div>
  `,
  styles: [`
    .team-roster { border: 1px solid var(--border); border-radius: 10px; margin-bottom: 18px; background: var(--bg-surface); }
    .team-header { display: flex; align-items: center; gap: 10px; padding: 10px 16px; border-bottom: 1px solid var(--border); }
    .team-title { font-weight: 600; font-size: 15px; color: var(--text-primary); }
    .team-desc { font-size: 12px; color: var(--text-muted); flex: 1; }
    .btn-xs { font-size: 11px; padding: 2px 8px; }
    .team-agents { padding: 8px 16px; }
    .agent-row { display: flex; align-items: center; justify-content: space-between; gap: 8px; padding: 4px 0; position: relative; }
    .agent-main { display: flex; align-items: center; gap: 8px; }
    .remove-agent-btn {
      background: none;
      border: none;
      color: #ef4444;
      font-size: 18px;
      font-weight: bold;
      cursor: pointer;
      padding: 0 8px;
      line-height: 1;
      transition: color 0.15s;
      border-radius: 50%;
      margin-left: 8px;
    }
    .remove-agent-btn:hover {
      color: #dc2626;
      background: #fef2f2;
    }
    .agent-emoji { font-size: 16px; width: 24px; text-align: center; }
    .agent-name { font-weight: 500; color: var(--text-primary); }
    .agent-role, .agent-model { font-size: 11px; color: var(--text-muted); margin-left: 6px; }
    .empty-state { color: var(--text-muted); font-size: 12px; padding: 8px 0; }
    .modal-backdrop {
      position: fixed;
      top: 0; left: 0; right: 0; bottom: 0;
      background: rgba(0,0,0,0.18);
      z-index: 1000;
      display: flex;
      align-items: center;
      justify-content: center;
    }
    .modal {
      background: #fff;
      border-radius: 8px;
      box-shadow: 0 2px 16px rgba(0,0,0,0.18);
      padding: 24px 32px;
      min-width: 320px;
      max-width: 90vw;
    }
    .modal-title {
      font-size: 18px;
      font-weight: 600;
      margin-bottom: 12px;
    }
    .modal-body {
      font-size: 14px;
      margin-bottom: 18px;
    }
    .modal-actions {
      display: flex;
      gap: 12px;
      justify-content: flex-end;
    }
    .modal-btn {
      padding: 6px 18px;
      border-radius: 4px;
      border: none;
      font-size: 13px;
      cursor: pointer;
    }
    .modal-btn.cancel {
      background: #f3f4f6;
      color: #374151;
    }
    .modal-btn.delete {
      background: #ef4444;
      color: #fff;
    }
    .modal-btn.delete:hover {
      background: #dc2626;
    }
  `]
})
export class TeamRosterComponent {
  @Input() team!: Team;
  @Input() agents: Agent[] = [];
  @Output() removeAgent = new EventEmitter<Agent>();
}