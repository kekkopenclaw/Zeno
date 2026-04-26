import { Component, OnInit, OnDestroy, Injector, inject, signal, computed, ChangeDetectionStrategy } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Subscription } from 'rxjs';
import { toObservable } from '@angular/core/rxjs-interop';
import { AgentService } from '../../core/services/agent.service';
import { SignalRService } from '../../core/services/signalr.service';
import { AgentLogsComponent } from './agent-logs.component';
import { AgentEditModalComponent } from './agent-edit-modal.component';
import type { Agent } from '../../core/models';

const ROLE_EMOJIS: Record<string, string> = {
  GrandPriest: '🧙‍♂️', // Orchestration
  Whis:        '🧑‍🚀', // Oversight/Advisor
  Beerus:      '😼',   // Planning
  Kakarot:     '🦸‍♂️', // Coding
  Vegeta:      '🦸‍♂️', // Coding
  Piccolo:     '🟩',   // Refactoring
  Gohan:       '👦',   // Review
  Trunks:      '👦🏻',  // Memory
  Bulma:       '👩‍🔬', // Tooling
  Cell:        '🦗',   // Security
  Dende:       '🧑‍🦲', // Testing
  Jaco:        '👽',   // Compliance
  Shenron:     '🐉',   // Release
  Jiren:       '💪',   // Enforcement
  Zeno:        '👑',   // Oversight
};

const ROLE_COLORS: Record<string, string> = {
  GrandPriest: '#6366f1', // Orchestration
  Whis:        '#8b5cf6', // Oversight/Advisor
  Beerus:      '#ef4444', // Planning
  Kakarot:     '#f59e0b', // Coding
  Vegeta:      '#3b82f6', // Coding
  Piccolo:     '#22c55e', // Refactoring
  Gohan:       '#06b6d4', // Review
  Trunks:      '#a855f7', // Memory
  Bulma:       '#ec4899', // Tooling
  Cell:        '#10b981', // Security
  Dende:       '#fde047', // Testing
  Jaco:        '#f472b6', // Compliance
  Shenron:     '#16a34a', // Release
  Jiren:       '#f87171', // Enforcement
  Zeno:        '#0ea5e9', // Oversight
};

/**
 * AgentsComponent
 *
 * Shows the agent roster grid and (if selected) pipeline activity/logs for specific agents.
 * - Uses SignalRService for live-updating agent status, spawn, pause/resume events
 * - Connected to log panel via signal
 * - Falls back to polling if SignalR is lost
 */
@Component({
  selector: 'app-agents',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, AgentLogsComponent, AgentEditModalComponent],
  template: `
<div class="agents-layout" data-testid="agents-layout">
  <!-- ── Left: Agent Grid ── -->
  <div class="agents-main">
    <div class="section-header" style="padding:16px 20px 10px">
      <div>
        <h1 class="page-title">Agents</h1>
        <p class="page-subtitle">{{agents().length}} agents · {{workingCount()}} working</p>
      </div>
      <div class="header-actions">
        <div class="search-box">
          <input [ngModel]="search()" (ngModelChange)="search.set($event)"
                 placeholder="Search agents…" class="search-input" />
        </div>
        <div class="filter-chips">
          <button class="filter-chip" [class.active]="roleFilter() === ''" (click)="roleFilter.set('')">All</button>
          @for (r of roles; track r) {
            <button class="filter-chip" [class.active]="roleFilter() === r"
                    (click)="roleFilter.set(roleFilter() === r ? '' : r)">{{r}}</button>
          }
        </div>
        <div class="filter-chips">
          <button class="filter-chip" [class.active]="statusFilter() === ''" (click)="statusFilter.set('')">All</button>
          <button class="filter-chip" [class.active]="statusFilter() === 'Working'" (click)="toggleStatus('Working')">⚡ Working</button>
          <button class="filter-chip" [class.active]="statusFilter() === 'Idle'" (click)="toggleStatus('Idle')">✓ Idle</button>
        </div>
      </div>
    </div>

    <div class="agents-grid">
      @for (agent of filtered(); track agent.id) {
        <div class="agent-card" [class.selected]="selected()?.id === agent.id"
             [style.border-color]="selected()?.id === agent.id ? roleColor(agent.role) : ''"
             (click)="selectAgent(agent)">
          <div class="agent-avatar"
               [style.background]="'linear-gradient(135deg, ' + roleColor(agent.role) + '22, ' + roleColor(agent.role) + '44)'"
               [style.border-color]="roleColor(agent.role) + '66'">
            <span class="agent-emoji">{{agent.emoji || roleEmoji(agent.role)}}</span>
          </div>
          <div class="agent-info">
            <div class="agent-name">{{agent.name}}</div>
            <div class="agent-role" [style.color]="roleColor(agent.role)">{{agent.role}}</div>
            <div class="agent-model">{{agent.backendLabel || (agent.model + ' via ' + (agent.executionBackend || 'Ollama'))}}</div>
            <div class="agent-backend-badges">
              <span class="backend-badge"
                    [class.ollama]="agent.executionBackend === 'Ollama'"
                    [class.openclaw]="agent.executionBackend === 'OpenClaw'">
                {{agent.executionBackend || 'Ollama'}}
              </span>
              @if (agent.toolsEnabled) {
                <span class="backend-badge tools">🔧 Tools</span>
              }
              @if (agent.pushRole) {
                <span class="backend-badge push">🚀 Push</span>
              }
            </div>
            @if (agent.openClawAgentId) {
              <div class="agent-ocid">🔗 {{agent.openClawAgentId}}</div>
            }
          </div>
          <div class="agent-status-col">
            <span class="status-dot"
                  [class.working]="agent.status === 'Working'"
                  [class.idle]="agent.status === 'Idle'"
                  [class.paused]="agent.status === 'Paused'"></span>
            <span class="status-text" [style.color]="statusColor(agent.status)">{{agent.status}}</span>
          </div>
          <div class="agent-skills">
            @for (skill of agentSkills(agent.skills); track skill) {
              <span class="skill-badge"
                    [style.background]="roleColor(agent.role) + '22'"
                    [style.color]="roleColor(agent.role)">{{skill}}</span>
            }
          </div>
          <!-- Lifecycle buttons -->
          <div class="agent-actions" (click)="$event.stopPropagation()">
            @if (!agent.openClawAgentId) {
              <select class="model-select"
                      [ngModel]="spawnModels()[agent.id] || agent.model || 'llama3'"
                      (ngModelChange)="setSpawnModel(agent.id, $event)"
                      [disabled]="actionBusy()">
                @for (m of availableModels; track m) {
                  <option [value]="m">{{m}}</option>
                }
              </select>
              <button class="action-btn spawn" (click)="spawnAgent(agent)" [disabled]="actionBusy()">
                ▶ Spawn
              </button>
            }
            @if (agent.openClawAgentId && !agent.isPaused) {
              <button class="action-btn pause" (click)="pauseAgent(agent)" [disabled]="actionBusy()">
                ⏸ Pause
              </button>
            }
            @if (agent.openClawAgentId && agent.isPaused) {
              <button class="action-btn resume" (click)="resumeAgent(agent)" [disabled]="actionBusy()">
                ▶ Resume
              </button>
            }
            <button class="action-btn edit" (click)="openEdit(agent)" data-testid="edit-agent-btn">✏️ Edit</button>
          </div>
        </div>
      }
    </div>
  </div>

  <!-- ── Right: Log Panel ── -->
  @if (selected()) {
    <div class="log-panel">
      <div class="log-panel-header">
        <div class="log-agent-title">
          <span>{{selected()!.emoji}}</span>
          <div>
            <div style="font-size:14px;font-weight:600;color:var(--text-primary)">{{selected()!.name}}</div>
            <div style="font-size:11px" [style.color]="roleColor(selected()!.role)">{{selected()!.role}}</div>
          </div>
        </div>
        <button class="close-btn" (click)="selected.set(null)">✕</button>
      </div>
      <app-agent-logs [agentId]="selected()!.id" style="flex:1;display:flex;flex-direction:column;overflow:hidden" />
    </div>
  }

  @if (editAgent()) {
    <app-agent-edit-modal
      [agent]="editAgent()!"
      [availableModels]="availableModels"
      (saved)="onAgentSaved($event)"
      (close)="closeEdit()">
    </app-agent-edit-modal>
  }
</div>
  `,
  styles: [`
    .agents-layout { display: flex; height: 100%; overflow: hidden; }
    .agents-main { flex: 1; overflow-y: auto; min-width: 0; }
    .page-title { font-size: 20px; font-weight: 700; color: var(--text-primary); }
    .page-subtitle { font-size: 11px; color: var(--text-muted); margin-top: 2px; }
    .section-header { display: flex; align-items: flex-start; justify-content: space-between; gap: 12px; flex-wrap: wrap; }
    .header-actions { display: flex; align-items: center; gap: 8px; flex-wrap: wrap; }
    .search-box { background: var(--bg-elevated); border: 1px solid var(--border); border-radius: 6px; padding: 5px 8px; }
    .search-input { border: none; background: transparent; font-size: 12px; color: var(--text-primary); outline: none; width: 140px; }
    .search-input::placeholder { color: var(--text-muted); }
    .filter-chips { display: flex; gap: 4px; }
    .filter-chip { font-size: 10px; padding: 3px 8px; border-radius: 4px; border: 1px solid var(--border); cursor: pointer; background: none; color: var(--text-muted); transition: all 0.12s; }
    .filter-chip:hover { border-color: var(--accent); color: var(--accent); }
    .filter-chip.active { background: var(--accent-dim); border-color: var(--accent); color: #c4b5fd; }

    .agents-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(230px, 1fr)); gap: 10px; padding: 0 20px 20px; }
    .agent-card { background: var(--bg-surface); border: 1px solid var(--border); border-radius: 12px; padding: 14px; cursor: pointer; transition: all 0.15s; position: relative; }
    .agent-card:hover { border-color: var(--border-bright); box-shadow: 0 2px 16px rgba(0,0,0,0.3); }
    .agent-card.selected { box-shadow: 0 0 0 1px currentColor; }
    .agent-avatar { width: 44px; height: 44px; border-radius: 12px; border: 1px solid; display: flex; align-items: center; justify-content: center; margin-bottom: 10px; }
    .agent-emoji { font-size: 22px; }
    .agent-name { font-size: 14px; font-weight: 600; color: var(--text-primary); }
    .agent-role { font-size: 11px; font-weight: 500; margin-top: 1px; }
    .agent-model { font-size: 10px; color: var(--text-muted); margin-top: 2px; }
    .agent-backend-badges { display: flex; flex-wrap: wrap; gap: 3px; margin-top: 4px; }
    .backend-badge { font-size: 9px; border-radius: 3px; padding: 1px 5px; font-weight: 600; }
    .backend-badge.ollama { background: #f59e0b22; color: #f59e0b; border: 1px solid #f59e0b44; }
    .backend-badge.openclaw { background: #8b5cf622; color: #8b5cf6; border: 1px solid #8b5cf644; }
    .backend-badge.tools { background: #22c55e22; color: #22c55e; border: 1px solid #22c55e44; }
    .backend-badge.push { background: #3b82f622; color: #3b82f6; border: 1px solid #3b82f644; }
    .agent-ocid { font-size: 9px; color: var(--text-muted); margin-top: 2px; font-family: monospace; }
    .agent-status-col { display: flex; align-items: center; gap: 5px; position: absolute; top: 12px; right: 12px; }
    .status-dot { width: 7px; height: 7px; border-radius: 50%; }
    .status-dot.working { background: #f59e0b; animation: pulse 1.5s infinite; }
    .status-dot.idle { background: #22c55e; }
    .status-dot.paused { background: #71717a; }
    @keyframes pulse { 0%,100%{opacity:1}50%{opacity:0.4} }
    .status-text { font-size: 10px; font-weight: 500; }
    .agent-skills { display: flex; flex-wrap: wrap; gap: 4px; margin-top: 10px; }
    .skill-badge { font-size: 10px; border-radius: 3px; padding: 2px 6px; }
    .agent-actions { display: flex; gap: 4px; margin-top: 10px; flex-wrap: wrap; }
    .action-btn { font-size: 10px; padding: 3px 8px; border-radius: 4px; border: 1px solid; cursor: pointer; font-weight: 500; transition: all 0.12s; }
    .action-btn:disabled { opacity: 0.5; cursor: not-allowed; }
    .action-btn.spawn { border-color: #22c55e44; background: #22c55e11; color: #22c55e; }
    .action-btn.spawn:hover:not(:disabled) { background: #22c55e22; }
    .action-btn.pause { border-color: #f59e0b44; background: #f59e0b11; color: #f59e0b; }
    .action-btn.pause:hover:not(:disabled) { background: #f59e0b22; }
    .action-btn.resume { border-color: #3b82f644; background: #3b82f611; color: #3b82f6; }
    .action-btn.resume:hover:not(:disabled) { background: #3b82f622; }
    .action-btn.edit { border-color: #6366f144; background: #6366f111; color: #6366f1; }
    .action-btn.edit:hover:not(:disabled) { background: #6366f122; }
    .model-select { font-size: 10px; padding: 3px 4px; border-radius: 4px; border: 1px solid var(--border); background: var(--bg-elevated); color: var(--text-muted); cursor: pointer; max-width: 120px; }
    .model-select:disabled { opacity: 0.5; cursor: not-allowed; }

    /* Log panel */
    .log-panel { width: 340px; flex-shrink: 0; border-left: 1px solid var(--border); background: var(--bg-surface); display: flex; flex-direction: column; overflow: hidden; }
    .log-panel-header { display: flex; align-items: center; justify-content: space-between; padding: 14px 14px 10px; border-bottom: 1px solid var(--border); }
    .log-agent-title { display: flex; align-items: center; gap: 10px; font-size: 20px; }
    .close-btn { background: none; border: none; cursor: pointer; color: var(--text-muted); font-size: 14px; }
  `],
})
export class AgentsComponent implements OnInit, OnDestroy {
  roleEmoji(role?: string): string {
    return ROLE_EMOJIS[role ?? ''] ?? '🤖';
  }
  private svc        = inject(AgentService);
  private signalrSvc = inject(SignalRService);
  private injector   = inject(Injector);

  agents       = signal<Agent[]>([]);
  selected     = signal<Agent | null>(null);
  actionBusy   = signal(false);
  editAgent    = signal<Agent | null>(null);

  // Per-agent model overrides for the spawn selector
  spawnModels  = signal<Record<number, string>>({});

  readonly availableModels = [
    // ── Ollama (http://127.0.0.1:11434) — fast local inference, no tool calling ──
    'llama3',
    'qwen2.5-coder:14b-instruct-q4_K_M',
    // ── OpenClaw-prefixed — advanced agent runner with tool/skill/plugin support ──
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

  // All filter state as signals — no plain properties in computed()
  search       = signal('');
  roleFilter   = signal('');
  statusFilter = signal('');

  roles = Object.keys(ROLE_COLORS);

  private lastAgentStarted$ = toObservable(this.signalrSvc.lastAgentStarted, { injector: this.injector });
  private lastAgentUpdated$ = toObservable(this.signalrSvc.lastAgentUpdated, { injector: this.injector });
  private pollingTick$ = toObservable(this.signalrSvc.pollingTick, { injector: this.injector });
  private sub?: Subscription;
  private updatedSub?: Subscription;
  private pollSub?: Subscription;

  workingCount = computed(() => this.agents().filter(a => a.status === 'Working').length);

  filtered = computed(() => {
    let list = this.agents();
    const q = this.search().trim().toLowerCase();
    if (q) list = list.filter(a => a.name.toLowerCase().includes(q) || a.role?.toLowerCase().includes(q));
    const rf = this.roleFilter();
    if (rf) list = list.filter(a => a.role === rf);
    const sf = this.statusFilter();
    if (sf) list = list.filter(a => a.status === sf);
    return list;
  });

  roleColor(role?: string): string { return ROLE_COLORS[role ?? ''] ?? '#71717a'; }
  statusColor(status: string): string {
    return status === 'Working' ? '#f59e0b' : status === 'Paused' ? '#71717a' : '#22c55e';
  }

  agentSkills(skills?: string): string[] {
    if (!skills) return [];
    return skills.split(',').map(s => s.trim()).filter(Boolean).slice(0, 3);
  }

  setSpawnModel(agentId: number, model: string): void {
    this.spawnModels.update(m => ({ ...m, [agentId]: model }));
  }

  toggleStatus(s: string) { this.statusFilter.set(this.statusFilter() === s ? '' : s); }

  selectAgent(agent: Agent): void {
    this.selected.set(agent);
  }

  openEdit(agent: Agent): void {
    this.editAgent.set(agent);
  }

  closeEdit(): void {
    this.editAgent.set(null);
  }

  onAgentSaved(patch: Partial<Agent>): void {
    const id = patch.id;
    if (!id) return;
    this.svc.update(id, patch).subscribe({
      next: updated => {
        this.agents.update(list => list.map(a => a.id === updated.id ? updated : a));
        if (this.selected()?.id === updated.id) this.selected.set(updated);
      },
    });
  }

  spawnAgent(agent: Agent): void {
    this.actionBusy.set(true);
    const model = this.spawnModels()[agent.id] ?? agent.model ?? 'ollama/llama3';
    this.svc.spawn(agent.id, model).subscribe({
      next: updated => {
        this.agents.update(list => list.map(a => a.id === updated.id ? updated : a));
        if (this.selected()?.id === updated.id) this.selected.set(updated);
        this.actionBusy.set(false);
      },
      error: () => this.actionBusy.set(false),
    });
  }

  pauseAgent(agent: Agent): void {
    this.actionBusy.set(true);
    this.svc.pause(agent.id).subscribe({
      next: updated => {
        this.agents.update(list => list.map(a => a.id === updated.id ? updated : a));
        if (this.selected()?.id === updated.id) this.selected.set(updated);
        this.actionBusy.set(false);
      },
      error: () => this.actionBusy.set(false),
    });
  }

  resumeAgent(agent: Agent): void {
    this.actionBusy.set(true);
    this.svc.resume(agent.id).subscribe({
      next: updated => {
        this.agents.update(list => list.map(a => a.id === updated.id ? updated : a));
        if (this.selected()?.id === updated.id) this.selected.set(updated);
        this.actionBusy.set(false);
      },
      error: () => this.actionBusy.set(false),
    });
  }

  ngOnInit(): void {
    this.svc.getAll().subscribe(d => this.agents.set(d));

    // Real-time agent status updates
    // { injector } is required so toObservable can create an effect in the correct context.
    this.sub = this.lastAgentStarted$.subscribe(updated => {
      if (!updated) return;
      this.agents.update(list =>
        list.map(a => a.id === updated.id ? { ...a, status: updated.status } : a),
      );
    });

    // Real-time agent property updates (edit/update)
    this.updatedSub = this.lastAgentUpdated$.subscribe(updated => {
      if (!updated) return;
      this.agents.update(list => list.map(a => a.id === updated.id ? updated : a));
      if (this.selected()?.id === updated.id) this.selected.set(updated);
    });

    // Polling fallback
    this.pollSub = this.pollingTick$.subscribe(tick => {
      if (tick === 0) return;
      this.svc.getAll().subscribe(d => this.agents.set(d));
    });
  }

  ngOnDestroy(): void {
    this.sub?.unsubscribe();
    this.updatedSub?.unsubscribe();
    this.pollSub?.unsubscribe();
  }
}
