import { Component, OnInit, effect, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { ActivityLogService } from '../../core/services/activity-log.service';
import { SignalRService } from '../../core/services/signalr.service';
import type { ActivityLog } from '../../core/models';

@Component({
  selector: 'app-logs-page',
  standalone: true,
  imports: [DatePipe],
  template: `
<div class="logs">
  <h1>Logs</h1>
  <div class="list">
    @for (log of logs(); track log.id) {
      <div class="row">
        <span>{{log.timestamp | date:'HH:mm:ss'}}</span>
        <span>{{log.message}}</span>
      </div>
    }
  </div>
</div>
  `,
})
export class LogsPageComponent implements OnInit {
  private activity = inject(ActivityLogService);
  private signalr = inject(SignalRService);
  logs = signal<ActivityLog[]>([]);

  ngOnInit(): void {
    this.activity.getByProject(1).subscribe(d => this.logs.set(d));
    effect(() => {
      const live = this.signalr.activityFeed();
      if (live.length > 0) this.logs.set(live);
    });
  }
}
