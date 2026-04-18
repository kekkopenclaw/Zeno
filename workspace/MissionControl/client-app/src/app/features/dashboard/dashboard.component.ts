
import { Component, OnInit, OnDestroy, Injector, inject, signal, computed, ChangeDetectionStrategy } from '@angular/core';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { Subscription } from 'rxjs';
import { toObservable } from '@angular/core/rxjs-interop';
import { ProjectService } from '../../core/services/project.service';
import { AgentService } from '../../core/services/agent.service';
import { TaskService } from '../../core/services/task.service';
import { ActivityLogService } from '../../core/services/activity-log.service';
import { SignalRService } from '../../core/services/signalr.service';
import { AgentCreateModalComponent } from './agent-create-modal.component';
import type { Project, Agent, TaskItem, ActivityLog } from '../../core/models';

/**
 * DashboardComponent
 *
 * The main live overview (projects, agents, tasks, pipeline, and activity feed)
 * - Subscribes to SignalRService for real-time feed, agent, and task updates
 * - All statistic blocks and feeds reflect live system state
 * - HTTP polling only used if SignalR connection is lost
 */
@Component({
  selector: 'app-dashboard',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DatePipe, RouterLink, AgentCreateModalComponent],
  template: `
<div class="page" data-testid="dashboard-page">
  <div class="page-header">
    <div>
      <h1 class="page-title">Dashboard</h1>
      <p class="page-subtitle">Autonomous AI lab — live overview</p>
    </div>
    <div style="display:flex;gap:8px">
      <button class="btn btn-primary" routerLink="/tasks">+ New Task</button>
    </div>
  </div>

  <div class="stats-grid">
    @for (s of stats(); track s.label) {
      <div class="stat-card">
        <div class="stat-icon" [style.color]="s.color">{{s.icon}}</div>
        <div class="stat-value" [style.color]="s.color">{{s.value}}</div>
        <div class="stat-label">{{s.label}}</div>
      </div>
    }
  </div>

  <div class="two-col">
    <div class="card">
      <div class="section-header">
        <span class="section-title">Agent Roster</span>
        <a routerLink="/agents" class="section-link">View all →</a>
      </div>
      @for (a of agents(); track a.id) {
        <div class="list-row">
          <!-- Removed emoji display -->
          <div class="list-main">
            <div class="list-primary">{{a.name}}</div>
            <div class="list-secondary">{{a.role}} · {{a.model}}</div>
          </div>
          <span class="badge" [style]="statusStyle(a.status)">{{a.status}}</span>
        </div>
      }
    </div>

    <div class="card">
      <div class="section-header">
        <span class="section-title">Task Pipeline</span>
        <a routerLink="/tasks" class="section-link">Kanban →</a>
      </div>
      @for (col of taskColumns(); track col.status) {
        <div class="pipeline-row">
          <div class="pipeline-dot" [style.background]="col.color"></div>
          <span class="pipeline-label">{{col.label}}</span>
          <span class="pipeline-count" [style.color]="col.color">{{col.count}}</span>
        </div>
      }
    </div>
  </div>

  <div class="card" style="margin-top:16px">
    <div class="section-header">
      <span class="section-title">Live Activity Feed</span>
      <span class="rt-indicator">
        <span class="rt-dot"></span> Live
      </span>
    </div>
    @for (log of recentLogs(); track log.id) {
      <div class="log-row">
        <span class="log-time">{{log.timestamp | date:'HH:mm:ss'}}</span>
        @if (log.agentEmoji || log.agentName) {
          <span class="log-agent">{{log.agentEmoji}} {{log.agentName}}</span>
        }
        <span class="log-msg">{{log.message}}</span>
      </div>
    }
    @if (recentLogs().length === 0) {
      <div class="empty-state">No activity yet — orchestrator will start soon</div>
    }
  </div>

  <!-- Teams section removed -->
</div>
  `,
  styles: [`
    .page { padding: 24px; max-width: 1100px; }
    .page-header { display: flex; align-items: center; justify-content: space-between; margin-bottom: 24px; }
    .page-title { font-size: 20px; font-weight: 700; color: var(--text-primary); }
    .page-subtitle { font-size: 12px; color: var(--text-muted); margin-top: 2px; }
    .stats-grid { display: grid; grid-template-columns: repeat(4,1fr); gap: 12px; margin-bottom: 16px; }
    .stat-card { background: var(--bg-surface); border: 1px solid var(--border); border-radius: 10px; padding: 16px; }
    .stat-icon { font-size: 18px; margin-bottom: 8px; }
    .stat-value { font-size: 28px; font-weight: 700; line-height: 1; }
    .stat-label { font-size: 11px; color: var(--text-muted); margin-top: 4px; text-transform: uppercase; letter-spacing: 0.06em; }
    .two-col { display: grid; grid-template-columns: 1fr 1fr; gap: 16px; }
    .section-header { display: flex; align-items: center; justify-content: space-between; margin-bottom: 12px; }
    .section-title { font-size: 12px; font-weight: 600; text-transform: uppercase; letter-spacing: 0.07em; color: var(--text-secondary); }
    .section-link { font-size: 11px; color: var(--accent); text-decoration: none; }
    .list-row { display: flex; align-items: center; gap: 10px; padding: 7px 0; border-bottom: 1px solid var(--border); }
    .list-row:last-child { border-bottom: none; }
    .list-main { flex: 1; min-width: 0; }
    .list-primary { font-size: 13px; font-weight: 500; color: var(--text-primary); }
    .list-secondary { font-size: 11px; color: var(--text-muted); margin-top: 1px; }
    .pipeline-row { display: flex; align-items: center; gap: 10px; padding: 6px 0; border-bottom: 1px solid var(--border); }
    .pipeline-row:last-child { border-bottom: none; }
    .pipeline-dot { width: 7px; height: 7px; border-radius: 50%; flex-shrink: 0; }
    .pipeline-label { flex: 1; font-size: 12px; color: var(--text-secondary); }
    .pipeline-count { font-size: 13px; font-weight: 600; }
    .rt-indicator { display: flex; align-items: center; gap: 5px; font-size: 11px; color: var(--green); }
    .rt-dot { display: inline-block; width: 6px; height: 6px; border-radius: 50%; background: var(--green); animation: pulse 2s infinite; }
    @keyframes pulse { 0%, 100% { opacity: 1; } 50% { opacity: 0.3; } }
    .log-row { display: flex; align-items: flex-start; gap: 8px; padding: 5px 0; border-bottom: 1px solid var(--border); }
    .log-row:last-child { border-bottom: none; }
    .log-time { font-size: 10px; color: var(--text-muted); font-family: monospace; flex-shrink: 0; margin-top: 2px; min-width: 56px; }
    .log-agent { font-size: 10px; background: var(--bg-elevated); border: 1px solid var(--border); border-radius: 3px; padding: 1px 5px; flex-shrink: 0; color: var(--accent); white-space: nowrap; }
    .log-msg { font-size: 12px; color: var(--text-secondary); line-height: 1.5; }
    .empty-state { color: var(--text-muted); font-size: 12px; padding: 8px 0; }
  `],
})
export class DashboardComponent implements OnInit, OnDestroy {
    // ...existing code...
  // ...existing code...
  private projectSvc = inject(ProjectService);
  private agentSvc   = inject(AgentService);
  private taskSvc    = inject(TaskService);
  private logSvc     = inject(ActivityLogService);
  private signalrSvc = inject(SignalRService);
  private injector   = inject(Injector);
  projects = signal<Project[]>([]);
  agents = signal<Agent[]>([]);
  tasks = signal<TaskItem[]>([]);
  logs = signal<ActivityLog[]>([]);

  private subs: Subscription[] = [];

  stats = computed(() => [
    { label: 'Projects',    value: this.projects().length, icon: '📁', color: '#8b5cf6' },
    { label: 'Agents',      value: this.agents().length,   icon: '🤖', color: '#3b82f6' },
    { label: 'In Pipeline', value: this.tasks().filter(t => !['Todo','Done'].includes(t.status)).length, icon: '⚡', color: '#f59e0b' },
    { label: 'Completed',   value: this.tasks().filter(t => t.status === 'Done').length, icon: '✓', color: '#22c55e' },
  ]);

  // Dynamic, role-driven pipeline columns
  taskColumns = computed(() => {
    const t = this.tasks();
    // Define the canonical pipeline order and mapping to roles/colors
    const pipeline: { status: string, label: string, color: string, role: string }[] = [
      { status: 'Todo',           label: 'Todo',           color: '#52525b', role: 'Whis/GrandPriest' },
      { status: 'Orchestration',  label: 'Orchestration',  color: '#6366f1', role: 'GrandPriest/Whis' },
      { status: 'Architecture',   label: 'Architecture',   color: '#8b5cf6', role: 'Beerus' },
      { status: 'Tooling',        label: 'Tooling',        color: '#ec4899', role: 'Bulma' },
      { status: 'Coding',         label: 'Coding',         color: '#f59e0b', role: 'Kakarot/Vegeta/Piccolo' },
      { status: 'Refactoring',    label: 'Refactoring',    color: '#22c55e', role: 'Piccolo' },
      { status: 'Security',       label: 'Security',       color: '#10b981', role: 'Cell' },
      { status: 'Testing',        label: 'Testing',        color: '#fde047', role: 'Dende' },
      { status: 'Review',         label: 'Review',         color: '#a855f7', role: 'Gohan' },
      { status: 'Compliance',     label: 'Compliance',     color: '#f472b6', role: 'Jaco' },
      { status: 'Release',        label: 'Release',        color: '#16a34a', role: 'Shenron' },
      { status: 'Memory',         label: 'Memory',         color: '#a855f7', role: 'Trunks' },
      { status: 'Enforcement',    label: 'Enforcement',    color: '#f87171', role: 'Jiren' },
      { status: 'Oversight',      label: 'Oversight',      color: '#0ea5e9', role: 'Zeno' },
      { status: 'Done',           label: 'Done',           color: '#22c55e', role: 'All' },
    ];
    return pipeline
      .map(col => ({
        ...col,
        count: t.filter(x => x.status === col.status).length
      }));
  });

  recentLogs = computed(() =>
    [...this.logs()].sort((a, b) => new Date(b.timestamp).getTime() - new Date(a.timestamp).getTime()).slice(0, 8),
  );

  statusStyle(s: string): string {
    const m: Record<string, string> = {
      Idle:    'background:rgba(34,197,94,0.1);color:#22c55e',
      Working: 'background:rgba(245,158,11,0.1);color:#f59e0b',
      Paused:  'background:rgba(82,82,91,0.2);color:#71717a',
    };
    return m[s] ?? 'background:rgba(82,82,91,0.2);color:#71717a';
  }

  ngOnInit(): void {
    this.projectSvc.getAll().subscribe(d => {
      this.projects.set(d);
    });
    this.agentSvc.getAll().subscribe(d => this.agents.set(d));
    this.taskSvc.getAll().subscribe(d => this.tasks.set(d));
    this.logSvc.getByProject(1).subscribe(d => this.logs.set(d));

    // Real-time log feed — only update when the feed is non-empty to avoid
    // overwriting the initial HTTP-loaded logs on the first emission (which is [])
    // { injector } is required so toObservable can create an effect in the correct context.
    this.subs.push(
      toObservable(this.signalrSvc.activityFeed, { injector: this.injector }).subscribe(feed => {
        if (feed.length > 0) this.logs.set(feed);
      }),
    );
    // Real-time task updates in pipeline bar
    this.subs.push(
      toObservable(this.signalrSvc.lastTaskUpdate, { injector: this.injector }).subscribe(updated => {
        if (!updated) return;
        // Update task in state
        this.tasks.update(tasks => {
          const idx = tasks.findIndex(t => t.id === updated.id);
          if (idx >= 0) { const next = [...tasks]; next[idx] = updated; return next; }
          return [updated, ...tasks];
        });
      }),
    );
  }

  ngOnDestroy(): void { this.subs.forEach(s => s.unsubscribe()); }
}
