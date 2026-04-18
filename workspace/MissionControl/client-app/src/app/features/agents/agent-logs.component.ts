import {
  Component, Input, OnInit, OnDestroy, OnChanges,
  inject, signal, computed, effect,
  ViewChild, ElementRef,
  ChangeDetectionStrategy,
} from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { toObservable } from '@angular/core/rxjs-interop';
import { Subscription } from 'rxjs';
import { ActivityLogService } from '../../core/services/activity-log.service';
import { SignalRService } from '../../core/services/signalr.service';
import type { ActivityLog } from '../../core/models';

/**
 * AgentLogsComponent
 *
 * Displays all logs for a specific agent. Subscribes to agent log lines in real time via SignalRService.
 * - Auto-scrolls when new lines arrive; supports search/filter
 * - Falls back to polling via parent if SignalR is unavailable
 */
@Component({
  selector: 'app-agent-logs',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DatePipe, FormsModule],
  template: `
<div class="log-panel-inner" data-testid="log-panel-inner">
  <div class="log-header">
    <div class="log-search">
      <input [ngModel]="logSearch()" (ngModelChange)="logSearch.set($event)"
             placeholder="Filter logs…" class="search-input" style="width:100%" />
    </div>
    <div class="log-live">
      <span class="rt-dot"></span>
      <span style="font-size:10px;color:var(--text-muted)">Live</span>
    </div>
  </div>

  <div class="log-list" #logContainer>
    @if (loading()) {
      <div class="log-status">Loading logs…</div>
    }
    @for (log of filteredLogs(); track log.id) {
      <div class="log-entry">
        <span class="log-time">{{log.timestamp | date:'MMM d HH:mm:ss'}}</span>
        <span class="log-msg">{{log.message}}</span>
      </div>
    }
    @if (!loading() && filteredLogs().length === 0) {
      <div class="log-status">No logs yet for this agent</div>
    }
  </div>

  <div class="log-footer">
    {{allLogs().length}} entries
  </div>
</div>
  `,
  styles: [`
    .log-panel-inner { display: flex; flex-direction: column; height: 100%; }
    .log-header { display: flex; align-items: center; gap: 8px; padding: 8px 14px; border-bottom: 1px solid var(--border); }
    .log-search { flex: 1; }
    .log-live { display: flex; align-items: center; gap: 4px; }
    .search-input { border: none; background: var(--bg-elevated); border: 1px solid var(--border); border-radius: 6px; padding: 5px 8px; font-size: 12px; color: var(--text-primary); outline: none; }
    .search-input::placeholder { color: var(--text-muted); }
    .log-list { flex: 1; overflow-y: auto; padding: 4px 0; }
    .log-entry { display: flex; flex-direction: column; gap: 1px; padding: 5px 14px; border-bottom: 1px solid var(--border); }
    .log-entry:last-child { border-bottom: none; }
    .log-time { font-size: 9px; font-family: monospace; color: var(--text-muted); }
    .log-msg { font-size: 11px; color: var(--text-secondary); line-height: 1.4; }
    .log-status { padding: 20px 14px; color: var(--text-muted); font-size: 12px; text-align: center; }
    .log-footer { padding: 6px 14px; border-top: 1px solid var(--border); font-size: 10px; color: var(--text-muted); }
    .rt-dot { display: inline-block; width: 6px; height: 6px; border-radius: 50%; background: var(--green, #22c55e); animation: pulse 2s infinite; }
    @keyframes pulse { 0%,100%{opacity:1}50%{opacity:0.3} }
  `],
})
export class AgentLogsComponent implements OnInit, OnDestroy, OnChanges {
  @Input() agentId!: number;

  @ViewChild('logContainer') private logContainer?: ElementRef<HTMLElement>;

  private activitySvc = inject(ActivityLogService);
  private signalrSvc  = inject(SignalRService);

  allLogs  = signal<ActivityLog[]>([]);
  loading  = signal(false);
  logSearch = signal('');

  filteredLogs = computed(() => {
    const q = this.logSearch().trim().toLowerCase();
    const logs = this.allLogs();
    return q ? logs.filter(l => l.message.toLowerCase().includes(q)) : logs;
  });

  private liveLineSub?: Subscription;

  constructor() {
    // Auto-scroll to bottom whenever new logs arrive
    effect(() => {
      const _ = this.allLogs(); // track signal
      // Defer to next frame so the DOM has rendered the new entry first
      queueMicrotask(() => {
        const el = this.logContainer?.nativeElement;
        if (el) el.scrollTop = el.scrollHeight;
      });
    });
  }

  ngOnInit(): void {
    this.loadLogs();
    this.subscribeToLive();
  }

  ngOnChanges(): void {
    if (this.agentId) {
      this.allLogs.set([]);
      this.loadLogs();
    }
  }

  ngOnDestroy(): void {
    this.liveLineSub?.unsubscribe();
  }

  private loadLogs(): void {
    if (!this.agentId) return;
    this.loading.set(true);
    this.activitySvc.getByAgent(this.agentId).subscribe({
      next: logs => { this.allLogs.set(logs); this.loading.set(false); },
      error: ()  => this.loading.set(false),
    });
  }

  private subscribeToLive(): void {
    this.liveLineSub = toObservable(this.signalrSvc.agentLogLines).subscribe(data => {
      if (!data) return;
      // Match by numeric id or "mc-{id}" string
      const matchId = data.agentId === String(this.agentId) || data.agentId === `mc-${this.agentId}`;
      if (!matchId) return;
      const synthetic: ActivityLog = {
        id:        Date.now(),
        agentId:   this.agentId,
        projectId: 0,
        message:   data.line,
        timestamp: new Date().toISOString(),
      };
      this.allLogs.update(logs => [...logs, synthetic]);
    });
  }
}
