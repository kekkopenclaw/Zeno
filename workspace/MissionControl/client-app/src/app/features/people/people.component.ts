import { Component, OnInit, inject, signal } from '@angular/core';
import { AgentService } from '../../core/services/agent.service';
import { AgentCreateModalComponent } from '../dashboard/agent-create-modal.component';
import { AgentDeleteModalComponent } from '../dashboard/agent-delete-modal.component';
import { CommonModule } from '@angular/common';
import type { Agent } from '../../core/models';

@Component({
  selector: 'app-people',
  standalone: true,
  imports: [CommonModule, AgentCreateModalComponent, AgentDeleteModalComponent],
  template: `
<div class="team-page" data-testid="people-page">
  <!-- Mission banner -->
  <div class="team-banner">
    <p>"An autonomous organization of AI agents that does work for me and produces value 24/7"</p>
  </div>

  <!-- Header -->
  <div class="team-header">
    <h1 class="team-title">Meet the Team</h1>
    <p class="team-count">{{agents().length}} AI agents, each with a real role and a real personality.</p>
    <p class="team-desc">
      We wanted to see what happens when AI doesn't just answer questions — but actually runs a company.
      Research markets. Write content. Post on social media. Ship products. All without being told what to do.
    </p>
    <button class="btn btn-primary" (click)="openModal()" style="margin-top:12px;" data-testid="add-teammate-btn-people">+ Add Teammate</button>
  </div>


  <app-agent-create-modal *ngIf="showModal"
    [availableModels]="availableModels"
    (created)="onAgentCreated($event)"
    (close)="closeModal()">
  </app-agent-create-modal>


<!-- Place modal at the end of the template to avoid stacking/overflow issues -->
<app-agent-delete-modal *ngIf="showDeleteModal"
  [agent]="agentToDelete"
  (confirm)="confirmDelete($event)"
  (close)="cancelDelete()">
</app-agent-delete-modal>

  @if (hero(); as h) {
    <!-- Hero agent card (Whis) -->
    <div class="hero-card">
      <div class="hero-inner">
        <div class="hero-avatar">{{h.emoji}}</div>
        <div class="hero-info">
          <div class="hero-name">{{h.name}}</div>
          <div class="hero-role">{{getSubtitle(h.role)}}</div>
          <div class="hero-desc">{{h.description}}</div>
          <div class="hero-skills">
            @for (skill of skillList(h.skills); track skill) {
              <span class="skill-badge">{{skill}}</span>
            }
          </div>
        </div>
      </div>
      <div class="hero-footer">ROLE CARD →</div>
    </div>
  }

  <!-- I/O flow indicator -->
  <div class="flow-row">
    <span class="flow-label">↓ INPUT SIGNAL</span>
    <div class="flow-line"></div>
    <span class="flow-label">OUTPUT ACTION ↓</span>
  </div>

  <!-- Sub-agent grid -->
  <div class="sub-grid">
    @for (a of subAgents(); track a.id) {
      <div class="sub-card team-agent-card" style="position:relative; background: var(--bg-elevated); border-radius: 16px; border: 1.5px solid var(--border); box-shadow: 0 1px 4px rgba(34,34,38,0.04); padding: 20px 16px 16px 16px; display: flex; flex-direction: column; align-items: flex-start; gap: 10px;">
        <button class="remove-agent-btn" title="Remove agent" (click)="removeAgent(a)">×</button>
        <div class="flex items-center gap-3 w-full">
          <div class="team-avatar"
               [style.background]="'linear-gradient(135deg, ' + roleColor(a.role) + '22, ' + roleColor(a.role) + '44)'"
               [style.borderColor]="roleColor(a.role) + '66'">
            <span class="team-emoji">{{a.emoji}}</span>
          </div>
          <div class="flex-1 min-w-0">
            <div class="team-name">{{a.name}}</div>
            <div class="team-role" [style.color]="roleColor(a.role)">{{getSubtitle(a.role)}}</div>
          </div>
        </div>
        <div class="team-desc">{{a.description}}</div>
        <div class="flex flex-wrap gap-2 mt-1">
          @for (skill of skillList(a.skills); track skill) {
            <span class="team-skill-badge"
                  [style.background]="roleColor(a.role) + '15'"
                  [style.borderColor]="roleColor(a.role) + '4D'"
                  [style.color]="roleColor(a.role)">{{skill}}</span>
          }
        </div>
        <div class="team-footer">ROLE CARD →</div>
      </div>
    }
  </div>
</div>
  `,
  styles: [`
    .team-page { padding: 24px 32px; max-width: 1100px; margin: 0 auto; }

    /* Banner */
    .team-banner {
      background: linear-gradient(135deg, rgba(124,58,237,0.12), rgba(59,130,246,0.08));
      border: 1px solid rgba(124,58,237,0.25);
      border-radius: 12px;
      padding: 18px 28px;
      text-align: center;
      margin-bottom: 28px;
      font-size: 14px;
      font-style: italic;
      color: var(--text-secondary);
      line-height: 1.6;
    }

    /* Header */
    .team-header { text-align: center; margin-bottom: 28px; }
    .team-title { font-size: 28px; font-weight: 700; color: var(--text-primary); margin-bottom: 6px; }
    .team-count { font-size: 14px; color: var(--text-secondary); margin-bottom: 10px; }
    .team-desc {
      font-size: 13px; color: var(--text-muted); max-width: 600px;
      margin: 0 auto; line-height: 1.7;
    }

    /* Hero card */
    .hero-card {
      background: var(--bg-surface);
      border: 1px solid rgba(124,58,237,0.35);
      border-radius: 14px;
      padding: 24px;
      margin-bottom: 20px;
    }
    .hero-inner { display: flex; align-items: flex-start; gap: 20px; }
    .hero-avatar {
      width: 64px; height: 64px;
      background: rgba(124,58,237,0.15);
      border: 1px solid rgba(124,58,237,0.3);
      border-radius: 14px;
      display: flex; align-items: center; justify-content: center;
      font-size: 30px; flex-shrink: 0;
    }
    .hero-info { flex: 1; }
    .hero-name { font-size: 20px; font-weight: 700; color: var(--text-primary); margin-bottom: 2px; }
    .hero-role { font-size: 13px; color: #a78bfa; font-weight: 500; margin-bottom: 6px; }
    .hero-desc { font-size: 13px; color: var(--text-secondary); line-height: 1.6; margin-bottom: 10px; }
    .hero-skills { display: flex; gap: 6px; flex-wrap: wrap; }
    .skill-badge {
      font-size: 11px;
      background: rgba(124,58,237,0.15);
      border: 1px solid rgba(124,58,237,0.3);
      color: #c4b5fd;
      padding: 3px 10px; border-radius: 4px;
    }
    .hero-footer { font-size: 11px; color: var(--text-muted); text-align: right; margin-top: 12px; }

    /* Flow */
    .flow-row {
      display: flex; align-items: center; justify-content: center; gap: 16px;
      margin: 16px 0; color: var(--text-muted); font-size: 11px;
    }
    .flow-line { flex: 1; max-width: 200px; height: 1px; background: var(--border); }

    /* Sub-agents */
    .sub-grid { display: grid; grid-template-columns: repeat(4, 1fr); gap: 12px; }
    .team-agent-card {
      border: 1px solid rgba(124,58,237,0.18); /* very light purple, matches Whis card */
      border-radius: 14px;
      transition: box-shadow 0.15s, border-color 0.15s;
      box-shadow: 0 1px 4px rgba(34,34,38,0.03);
    }
    .team-agent-card:hover {
      border-color: rgba(124,58,237,0.28);
      box-shadow: 0 2px 8px rgba(124,58,237,0.07);
    }
    .team-avatar {
      width: 44px;
      height: 44px;
      border-radius: 12px;
      display: flex;
      align-items: center;
      justify-content: center;
      font-size: 22px;
      font-weight: bold;
      border: 1px solid;
      margin-bottom: 10px;
      background: var(--bg-surface);
      transition: border-color 0.15s;
      box-shadow: 0 1px 4px rgba(34,34,38,0.04);
    }
    .team-emoji {
      font-size: 24px;
    }
    .team-name {
      font-size: 15px;
      font-weight: 600;
      color: var(--text-primary);
      margin-bottom: 2px;
      text-overflow: ellipsis;
      overflow: hidden;
      white-space: nowrap;
    }
    .team-role {
      font-size: 12px;
      font-weight: 500;
      color: #a78bfa;
      margin-bottom: 2px;
    }
    .team-desc {
      font-size: 12px;
      color: var(--text-muted);
      line-height: 1.5;
      margin-bottom: 6px;
      margin-top: 2px;
    }
    .team-skill-badge {
      font-size: 11px;
      background: rgba(124,58,237,0.08); /* fallback, overridden inline */
      border: 1px solid rgba(124,58,237,0.18); /* fallback, overridden inline */
      color: #a78bfa; /* fallback, overridden inline */
      padding: 2px 8px;
      border-radius: 4px;
      font-weight: 500;
      transition: border-color 0.15s, background 0.15s, color 0.15s;
    }
    .team-footer {
      font-size: 10px;
      color: var(--text-muted);
      margin-top: 4px;
      text-align: right;
      width: 100%;
    }
    .sub-top {
      display: flex; align-items: center; gap: 10px; margin-bottom: 10px;
      border-radius: 10px;
      padding: 4px 8px;
      background: var(--bg-surface);
    }
    .sub-top { display: flex; align-items: center; gap: 10px; margin-bottom: 10px; }
    .sub-avatar {
      width: 40px; height: 40px;
      background: var(--bg-elevated);
      border-radius: 10px;
      display: flex; align-items: center; justify-content: center;
      font-size: 20px; flex-shrink: 0;
      border: 2px solid var(--border);
      transition: border-color 0.15s;
    }
    .sub-info { min-width: 0; }
    .sub-name { font-size: 13px; font-weight: 600; color: var(--text-primary); }
    .sub-role { font-size: 11px; color: var(--text-muted); }
    .sub-desc { font-size: 11px; color: var(--text-muted); line-height: 1.5; margin-bottom: 10px; }
    .sub-skills { display: flex; gap: 4px; flex-wrap: wrap; margin-bottom: 10px; }
    .skill-mini {
      font-size: 10px; background: var(--bg-elevated); border: 1px solid var(--border);
      color: var(--text-muted); padding: 2px 7px; border-radius: 3px;
    }
    .sub-footer { font-size: 10px; color: var(--text-muted); }
    .remove-agent-btn {
      position: absolute;
      top: 8px;
      right: 8px;
      width: 22px;
      height: 22px;
      border: none;
      background: var(--bg-elevated);
      color: var(--text-muted);
      border-radius: 50%;
      font-size: 16px;
      line-height: 1;
      cursor: pointer;
      z-index: 2;
      transition: background 0.15s, color 0.15s;
      box-shadow: 0 1px 4px rgba(34,34,38,0.04);
    }
    .remove-agent-btn:hover {
      background: var(--bg-hover);
      color: var(--text-primary);
    }
  `]
})
export class PeopleComponent implements OnInit {
  agentToDelete: Agent | null = null;
  showDeleteModal = false;

  removeAgent(agent: Agent) {
    this.agentToDelete = agent;
    this.showDeleteModal = true;
  }

  confirmDelete(agent: Agent) {
    if (!agent) return;
    this.svc.delete(agent.id).subscribe({
      next: () => {
        this.svc.getAll().subscribe(d => this.agents.set(d));
        this.showDeleteModal = false;
        this.agentToDelete = null;
      },
      error: err => {
        alert('Failed to remove agent: ' + (err?.error?.message || err.message || err));
        this.showDeleteModal = false;
        this.agentToDelete = null;
      }
    });
  }

  cancelDelete() {
    this.showDeleteModal = false;
    this.agentToDelete = null;
  }
  modalKey = 0;
  openModal() {
    this.modalKey++;
    this.showModal = true;
  }
  closeModal() {
    this.showModal = false;
  }
  roleColor(role?: string): string {
    const m: Record<string, string> = {
      GrandPriest: '#6366f1', Whis: '#8b5cf6', Beerus: '#ef4444', Kakarot: '#f59e0b',
      Vegeta: '#3b82f6', Piccolo: '#22c55e', Gohan: '#06b6d4', Trunks: '#a855f7',
      Bulma: '#ec4899', Cell: '#10b981', Dende: '#fde047', Jaco: '#f472b6',
      Shenron: '#16a34a', Jiren: '#f87171', Zeno: '#0ea5e9',
    };
    return m[role ?? ''] ?? '#71717a';
  }
  readonly availableModels = [
    'llama3',
    'qwen2.5-coder:14b-instruct-q4_K_M',
    'ollama/llama3',
    'ollama/llama3:70b',
    'ollama/mistral',
    'ollama/codellama',
    'ollama/deepseek-coder',
    'gpt-4o',
    'gpt-4o-mini',
    'claude-3-5-sonnet',
    'claude-3-haiku',
  ];
  private svc = inject(AgentService);
  agents = signal<Agent[]>([]);
  showModal = false;

  hero = () => this.agents().find(a => a.role === 'Whis') ?? this.agents()[0];
  subAgents = () => this.agents().filter(a => a.role !== 'Whis');

  getSubtitle(role: string): string {
    const m: Record<string, string> = {
      Whis: 'Chief Orchestrator', Beerus: 'Architect', Kakarot: 'Coder',
      Vegeta: 'Advanced Coder', Piccolo: 'Refactorer', Gohan: 'Reviewer',
      Trunks: 'Memory & Learning', Bulma: 'Tooling',
    };
    return m[role] ?? role;
  }

  skillList(skills: string): string[] {
    return skills?.split(',').map(s => s.trim()).filter(Boolean).slice(0, 3) ?? [];
  }

  ngOnInit(): void {
    this.svc.getAll().subscribe(d => this.agents.set(d));
  }

  onAgentCreated(agent: Partial<Agent>) {
    // Use the service to create the agent, then refresh list
    this.svc.create(agent as any).subscribe(() => {
      this.svc.getAll().subscribe(d => this.agents.set(d));
    });
  }
}

