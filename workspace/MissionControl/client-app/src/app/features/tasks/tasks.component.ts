import { Component, OnInit, OnDestroy, Injector, inject, signal, computed } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { SlicePipe, DatePipe } from '@angular/common';
import { CdkDragDrop, DragDropModule } from '@angular/cdk/drag-drop';
import { Subscription } from 'rxjs';
import { toObservable } from '@angular/core/rxjs-interop';
import { TaskService } from '../../core/services/task.service';
import { AgentService } from '../../core/services/agent.service';
import { SignalRService } from '../../core/services/signalr.service';
import type { TaskItem, Agent } from '../../core/models';


// Dynamic, role-driven pipeline columns (match dashboard and backend)
const PIPELINE = [
  { status: 'Todo',           label: 'Todo',           color: '#52525b', bg: 'rgba(82,82,91,0.12)' },
  { status: 'Orchestration',  label: 'Orchestration',  color: '#6366f1', bg: 'rgba(99,102,241,0.10)' },
  { status: 'Architecture',   label: 'Architecture',   color: '#8b5cf6', bg: 'rgba(139,92,246,0.10)' },
  { status: 'Tooling',        label: 'Tooling',        color: '#ec4899', bg: 'rgba(236,72,153,0.10)' },
  { status: 'Coding',         label: 'Coding',         color: '#f59e0b', bg: 'rgba(245,158,11,0.10)' },
  { status: 'Refactoring',    label: 'Refactoring',    color: '#22c55e', bg: 'rgba(34,197,94,0.10)' },
  { status: 'Security',       label: 'Security',       color: '#10b981', bg: 'rgba(16,185,129,0.10)' },
  { status: 'Testing',        label: 'Testing',        color: '#fde047', bg: 'rgba(253,224,71,0.10)' },
  { status: 'Review',         label: 'Review',         color: '#a855f7', bg: 'rgba(168,85,247,0.10)' },
  { status: 'Compliance',     label: 'Compliance',     color: '#f472b6', bg: 'rgba(244,114,182,0.10)' },
  { status: 'Release',        label: 'Release',        color: '#16a34a', bg: 'rgba(22,163,74,0.10)' },
  { status: 'Memory',         label: 'Memory',         color: '#a855f7', bg: 'rgba(168,85,247,0.10)' },
  { status: 'Enforcement',    label: 'Enforcement',    color: '#f87171', bg: 'rgba(248,113,113,0.10)' },
  { status: 'Oversight',      label: 'Oversight',      color: '#0ea5e9', bg: 'rgba(14,165,233,0.10)' },
  { status: 'Done',           label: 'Done',           color: '#22c55e', bg: 'rgba(34,197,94,0.10)' },
];
type Status = (typeof PIPELINE)[number]['status'];

/**
 * TasksComponent
 *
 * Handles the task Kanban board.
 * - Injects SignalRService for live task/agent log updates
 * - All UI updates driven by reactive state signals
 * - Falls back to HTTP polling only if SignalR connection fails
 */
@Component({
  selector: 'app-tasks',
  standalone: true,
  imports: [FormsModule, SlicePipe, DatePipe, DragDropModule],
  template: `
<div class="tasks-page">
  <!-- ── Header ── -->
  <div class="tasks-header">
    <div>
      <h1 class="page-title">Tasks</h1>
      <p class="page-subtitle">{{allTasks().length}} tasks · {{activeTasks()}} in pipeline · {{doneTasks()}} done</p>
    </div>
    <div class="header-actions">
      <!-- Search -->
      <div class="search-box">
        <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="11" cy="11" r="8"/><path d="m21 21-4.35-4.35"/></svg>
        <input [ngModel]="searchQuery()" (ngModelChange)="searchQuery.set($event)" placeholder="Search tasks..." class="search-input" />
        @if (searchQuery()) {
          <button class="clear-btn" (click)="searchQuery.set('')">✕</button>
        }
      </div>
      <!-- Priority filter -->
      <div class="filter-chips">
        @for (p of priorities; track p) {
          <button class="filter-chip" [class.active]="selectedPriority() === p" (click)="togglePriority(p)">
            {{p}}
          </button>
        }
      </div>
      <button class="btn btn-primary" data-testid="toggle-add-task" (click)="showAddForm.set(!showAddForm())">
        {{showAddForm() ? '✕ Cancel' : '+ Add Task'}}
      </button>
    </div>
  </div>

  <!-- ── Add Task Form ── -->
  <div class="add-task-form visible" data-testid="add-task-form">
    <input data-testid="task-title" [ngModel]="newTask().title" (ngModelChange)="patchNewTask('title', $event)" placeholder="Task title..." class="form-input" />
    <textarea data-testid="task-desc" [ngModel]="newTask().description" (ngModelChange)="patchNewTask('description', $event)" placeholder="Description..." class="form-textarea"></textarea>
    <div class="form-row">
      <select [ngModel]="newTask().priority" (ngModelChange)="patchNewTask('priority', $event)" class="form-select">
        <option value="Low">Low</option>
        <option value="Medium">Medium</option>
        <option value="High">High</option>
        <option value="Critical">Critical</option>
      </select>
      <select [ngModel]="newTask().agentId" (ngModelChange)="patchNewTask('agentId', $event)" class="form-select">
        <option value="">Auto-assign</option>
        @for (a of agents(); track a.id) {
          <option [value]="a.id">{{a.emoji}} {{a.name}}</option>
        }
      </select>
      <button class="btn btn-primary" data-testid="create-task" [disabled]="!newTask().title.trim()" (click)="addTask()">
        Create Task
      </button>
    </div>
  </div>

  <!-- ── Kanban Board ── -->
  <div class="kanban-board">
    @for (col of columns; track col.status) {
      <div class="kanban-col">
        <div class="col-header" [style.border-bottom-color]="col.color + '44'">
          <div class="col-dot" [style.background]="col.color"></div>
          <span class="col-title" [style.color]="col.color">{{col.label}}</span>
          <span class="col-count">{{columnTasks(col.status).length}}</span>
        </div>

        <div class="col-body"
             cdkDropList
             [id]="'col-' + col.status"
             [cdkDropListData]="columnTasks(col.status)"
             [cdkDropListConnectedTo]="connectedIds"
             (cdkDropListDropped)="onDrop($event, col.status)">
          @for (task of columnTasks(col.status); track task.id) {
            <div cdkDrag class="task-card" [style.border-left-color]="priorityColor(task.priority)">
              <!-- Drag handle indicator -->
              <div cdkDragHandle class="drag-handle">⠿</div>
              <button class="task-delete-btn" (click)="deleteTask(task); $event.stopPropagation()" title="Delete task" data-testid="delete-task-btn">🗑</button>
              <div class="task-title">{{task.title}}</div>
              @if (task.description) {
                <div class="task-desc">{{task.description | slice:0:72}}{{task.description.length > 72 ? '…' : ''}}</div>
              }
              <div class="task-meta">
                <span class="badge" [style]="priorityBadge(task.priority)">{{task.priority}}</span>
                @if (task.retryCount > 0) {
                  <span class="retry-badge" title="Retry count">↺{{task.retryCount}}</span>
                }
                @if (task.assignedAgentEmoji || task.assignedAgentName) {
                  <span class="agent-chip">{{task.assignedAgentEmoji}} {{task.assignedAgentName}}</span>
                }
                @if (task.complexityScore > 0) {
                  <span class="complexity-badge" title="Complexity">⚡{{task.complexityScore}}</span>
                }
              </div>
              @if (task.reviewNotes && task.status === 'Fix') {
                <div class="review-notes">📝 {{task.reviewNotes}}</div>
              }
            </div>
          }

          @if (columnTasks(col.status).length === 0) {
            <div class="col-empty">
              @if (col.status === 'Todo') { Drop tasks here or add via "+ Add Task" }
              @else { Waiting for pipeline... }
            </div>
          }
        </div>
      </div>
    }
  </div>

  <!-- ── Real-time indicator ── -->
  <div class="realtime-bar">
    <span class="rt-dot"></span>
    <span>Orchestrator running — tasks move automatically every ~2s</span>
    @if (lastUpdate()) {
      <span class="rt-update">Last update: {{lastUpdate() | date:'HH:mm:ss'}}</span>
    }
  </div>
</div>
  `,
  styles: [`
    .tasks-page { display: flex; flex-direction: column; height: 100%; overflow: hidden; }

    .tasks-header {
      display: flex; align-items: center; justify-content: space-between;
      padding: 16px 20px 10px; flex-shrink: 0;
    }
    .page-title { font-size: 20px; font-weight: 700; color: var(--text-primary); }
    .page-subtitle { font-size: 11px; color: var(--text-muted); margin-top: 2px; }

    .header-actions { display: flex; align-items: center; gap: 10px; flex-wrap: wrap; }

    .search-box {
      display: flex; align-items: center; gap: 6px;
      background: var(--bg-elevated); border: 1px solid var(--border);
      border-radius: 6px; padding: 5px 8px; color: var(--text-muted);
    }
    .search-input {
      border: none; background: transparent; font-size: 12px;
      color: var(--text-primary); outline: none; width: 150px;
    }
    .search-input::placeholder { color: var(--text-muted); }
    .clear-btn {
      background: none; border: none; cursor: pointer;
      color: var(--text-muted); font-size: 10px; padding: 0;
    }

    .filter-chips { display: flex; gap: 4px; }
    .filter-chip {
      font-size: 10px; padding: 3px 8px; border-radius: 4px;
      border: 1px solid var(--border); cursor: pointer;
      background: none; color: var(--text-muted); transition: all 0.1s;
    }
    .filter-chip:hover { border-color: var(--accent); color: var(--accent); }
    .filter-chip.active { background: var(--accent-dim); border-color: var(--accent); color: #c4b5fd; }

    /* Add form */
    .add-task-form {
      margin: 0 20px 10px;
      background: var(--bg-surface);
      border: 1px solid var(--accent);
      border-radius: 10px;
      padding: 14px;
      flex-shrink: 0;
      display: none;
    }
    .add-task-form.visible {
      display: block;
    }
    .add-task-form.hidden {
      display: none;
    }
    .form-input, .form-textarea, .form-select {
      width: 100%; padding: 8px 10px; margin-bottom: 8px;
      background: var(--bg-elevated); border: 1px solid var(--border);
      color: var(--text-primary); border-radius: 6px; font-size: 13px;
    }
    .form-textarea { height: 64px; resize: vertical; font-family: inherit; }
    .form-select { width: auto; min-width: 130px; margin-bottom: 0; cursor: pointer; }
    .form-row { display: flex; gap: 8px; align-items: center; }
    .form-row .btn { flex-shrink: 0; }

    /* Board */
    .kanban-board {
      display: flex; gap: 8px;
      overflow-x: auto; overflow-y: hidden;
      padding: 0 20px 12px; flex: 1; min-height: 0;
    }
    .kanban-col {
      min-width: 186px; width: 186px; flex-shrink: 0;
      background: var(--bg-surface); border: 1px solid var(--border);
      border-radius: 10px; display: flex; flex-direction: column;
    }
    .col-header {
      display: flex; align-items: center; gap: 6px;
      padding: 8px 10px; border-bottom: 2px solid;
      flex-shrink: 0;
    }
    .col-dot { width: 7px; height: 7px; border-radius: 50%; flex-shrink: 0; }
    .col-title { font-size: 10px; font-weight: 700; text-transform: uppercase; letter-spacing: 0.07em; flex: 1; }
    .col-count {
      font-size: 10px; background: var(--bg-elevated); border: 1px solid var(--border);
      border-radius: 999px; padding: 1px 6px; color: var(--text-muted);
    }
    .col-body { padding: 6px; flex: 1; overflow-y: auto; min-height: 40px; }
    .task-card {
      background: var(--bg-elevated); border: 1px solid var(--border);
      border-left-width: 3px; border-radius: 7px; padding: 8px 8px 8px 10px;
      margin-bottom: 5px; cursor: grab; position: relative;
      transition: box-shadow 0.12s, border-color 0.12s;
    }
    .task-card:hover { box-shadow: 0 2px 10px rgba(0,0,0,0.4); border-color: var(--border-bright); }
    .task-delete-btn { position:absolute;top:6px;right:6px;background:none;border:none;cursor:pointer;opacity:0;font-size:14px;color:var(--text-muted);padding:2px 4px;border-radius:4px;transition:opacity 0.15s; }
    .task-card:hover .task-delete-btn { opacity:1; }
    .drag-handle {
      position: absolute; right: 6px; top: 8px;
      color: var(--text-muted); font-size: 12px; cursor: grab;
      opacity: 0;
    }
    .task-card:hover .drag-handle { opacity: 1; }
    .task-title { font-size: 12px; font-weight: 500; color: var(--text-primary); line-height: 1.4; padding-right: 12px; }
    .task-desc { font-size: 11px; color: var(--text-muted); margin-top: 3px; line-height: 1.4; }
    .task-meta { display: flex; align-items: center; gap: 4px; flex-wrap: wrap; margin-top: 6px; }
    .retry-badge { font-size: 10px; color: #ef4444; background: rgba(239,68,68,0.1); border-radius: 3px; padding: 1px 4px; }
    .agent-chip {
      font-size: 10px; color: var(--text-muted); background: var(--bg-surface);
      border: 1px solid var(--border); border-radius: 3px; padding: 1px 5px;
      max-width: 80px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap;
    }
    .complexity-badge { font-size: 10px; color: var(--text-muted); }
    .review-notes {
      font-size: 10px; color: #f59e0b; background: rgba(245,158,11,0.08);
      border-radius: 3px; padding: 4px 6px; margin-top: 5px; line-height: 1.4;
    }
    .col-empty {
      padding: 14px 8px; text-align: center;
      font-size: 11px; color: var(--text-muted);
      border: 1px dashed var(--border); border-radius: 6px;
    }

    /* CDK drag preview */
    .cdk-drag-preview {
      background: var(--bg-elevated); border: 1px solid var(--accent);
      border-radius: 7px; padding: 8px 10px;
      box-shadow: 0 8px 32px rgba(0,0,0,0.7);
    }
    .cdk-drag-placeholder { opacity: 0; }
    .cdk-drag-animating { transition: transform 200ms; }

    /* Real-time bar */
    .realtime-bar {
      flex-shrink: 0; padding: 6px 20px;
      background: var(--bg-surface); border-top: 1px solid var(--border);
      display: flex; align-items: center; gap: 7px;
      font-size: 11px; color: var(--text-muted);
    }
    .rt-dot { width: 6px; height: 6px; border-radius: 50%; background: var(--green); animation: pulse 2s infinite; flex-shrink: 0; }
    @keyframes pulse { 0%, 100% { opacity: 1; } 50% { opacity: 0.3; } }
    .rt-update { margin-left: auto; font-family: monospace; font-size: 10px; }
  `],
})
export class TasksComponent implements OnInit, OnDestroy {
  private svc        = inject(TaskService);
  private agentSvc   = inject(AgentService);
  private signalrSvc = inject(SignalRService);
  private injector   = inject(Injector);

  allTasks = signal<TaskItem[]>([]);
  agents = signal<Agent[]>([]);
  lastUpdate = signal<Date | null>(null);

  searchQuery = signal('');
  selectedPriority = signal('');
  showAddForm = signal(false);
  newTask = signal({ title: '', description: '', priority: 'Medium', agentId: '' });
  priorities = ['Low', 'Medium', 'High', 'Critical'];

  columns = PIPELINE;
  connectedIds = PIPELINE.map(col => 'col-' + col.status);

  private sub?: Subscription;
  private pollSub?: Subscription;

  activeTasks = computed(() => this.allTasks().filter(t => !['Todo', 'Done'].includes(t.status)).length);
  doneTasks  = computed(() => this.allTasks().filter(t => t.status === 'Done').length);

  filteredTasks = computed(() => {
    let tasks = this.allTasks();
    const q = this.searchQuery().trim().toLowerCase();
    if (q) tasks = tasks.filter(t =>
      t.title.toLowerCase().includes(q) || t.description.toLowerCase().includes(q),
    );
    const priority = this.selectedPriority();
    if (priority) tasks = tasks.filter(t => t.priority === priority);
    return tasks;
  });

  columnTasks(status: Status): TaskItem[] {
    return this.filteredTasks().filter(t => t.status === status);
  }

  priorityColor(p: string): string {
    const m: Record<string, string> = { Low: '#52525b', Medium: '#3b82f6', High: '#f59e0b', Critical: '#ef4444' };
    return m[p] ?? '#52525b';
  }
  priorityBadge(p: string): string {
    const m: Record<string, string> = {
      Low:      'background:rgba(82,82,91,0.2);color:#71717a',
      Medium:   'background:rgba(59,130,246,0.1);color:#60a5fa',
      High:     'background:rgba(245,158,11,0.1);color:#f59e0b',
      Critical: 'background:rgba(239,68,68,0.15);color:#f87171',
    };
    return m[p] ?? 'background:rgba(82,82,91,0.2);color:#71717a';
  }

  togglePriority(p: string): void {
    this.selectedPriority.update(cur => cur === p ? '' : p);
  }

  patchNewTask(field: string, value: string): void {
    this.newTask.update(t => ({ ...t, [field]: value }));
  }

  deleteTask(task: TaskItem): void {
    this.svc.delete(task.id).subscribe({
      next: () => {
        this.allTasks.update(all => all.filter(t => t.id !== task.id));
      }
    });
  }

  addTask(): void {
    const task = this.newTask();
    if (!task.title.trim()) return;
    this.svc.create({
      title: task.title,
      description: task.description,
      priority: task.priority,
      assignedAgentId: task.agentId ? Number(task.agentId) : undefined,
      projectId: 1,
    }).subscribe(created => {
      this.allTasks.update(tasks => [created, ...tasks]);
      this.newTask.set({ title: '', description: '', priority: 'Medium', agentId: '' });
      this.showAddForm.set(false);
      this.lastUpdate.set(new Date());
    });
  }

  onDrop(event: CdkDragDrop<TaskItem[]>, targetStatus: Status): void {
    if (event.previousContainer === event.container) return;
    const task = event.previousContainer.data[event.previousIndex];
    const prevStatus = task.status;
    this.allTasks.update(tasks =>
      tasks.map(t => t.id === task.id ? { ...t, status: targetStatus } : t),
    );
    this.lastUpdate.set(new Date());
    this.svc.updateStatus(task.id, { status: targetStatus }).subscribe({
      error: () => {
        this.allTasks.update(tasks =>
          tasks.map(t => t.id === task.id ? { ...t, status: prevStatus } : t),
        );
      },
    });
  }

  ngOnInit(): void {
    this.svc.getAll().subscribe(d => this.allTasks.set(d));
    this.agentSvc.getAll().subscribe(d => this.agents.set(d));

    // Subscribe to real-time task updates via SignalR
    // { injector } is required so toObservable can create an effect in the correct context.
    this.sub = toObservable(this.signalrSvc.lastTaskUpdate, { injector: this.injector }).subscribe(updated => {
      if (!updated) return;
      this.allTasks.update(tasks => {
        const idx = tasks.findIndex(t => t.id === updated.id);
        if (idx >= 0) {
          const next = [...tasks];
          next[idx] = updated;
          return next;
        }
        return [updated, ...tasks];
      });
      this.lastUpdate.set(new Date());
    });

    // When SignalR is unavailable, reload all tasks on each polling tick
    this.pollSub = toObservable(this.signalrSvc.pollingTick, { injector: this.injector }).subscribe(tick => {
      if (tick === 0) return; // skip the initial emission
      this.svc.getAll().subscribe(d => {
        this.allTasks.set(d);
        this.lastUpdate.set(new Date());
      });
    });
  }

  ngOnDestroy(): void {
    this.sub?.unsubscribe();
    this.pollSub?.unsubscribe();
  }
}
