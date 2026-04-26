import { Component, signal, computed } from '@angular/core';

interface CalendarTask {
  id: number;
  title: string;
  time: string;
  color: string;
  bg: string;
}

const WEEK_TASKS: Record<number, CalendarTask[]> = {
  0: [ // Mon
    { id: 1, title: 'ai scarcity res...', time: '5:00 AM',  color: '#c4b5fd', bg: 'rgba(124,58,237,0.35)' },
    { id: 2, title: 'morning brief',     time: '8:00 AM',  color: '#fbbf24', bg: 'rgba(180,130,0,0.35)' },
    { id: 3, title: 'competitor yo...',  time: '10:00 AM', color: '#fca5a5', bg: 'rgba(180,30,30,0.35)' },
  ],
  1: [ // Tue
    { id: 4, title: 'ai scarcity res...', time: '5:00 AM',  color: '#c4b5fd', bg: 'rgba(124,58,237,0.35)' },
    { id: 5, title: 'morning brief',     time: '8:00 AM',  color: '#fbbf24', bg: 'rgba(180,130,0,0.35)' },
    { id: 6, title: 'competitor yo...',  time: '10:00 AM', color: '#fca5a5', bg: 'rgba(180,30,30,0.35)' },
  ],
  2: [ // Wed
    { id: 7,  title: 'ai scarcity res...', time: '5:00 AM',  color: '#c4b5fd', bg: 'rgba(124,58,237,0.35)' },
    { id: 8,  title: 'morning brief',     time: '8:00 AM',  color: '#fbbf24', bg: 'rgba(180,130,0,0.35)' },
    { id: 9,  title: 'newsletter rem...', time: '9:00 AM',  color: '#6ee7b7', bg: 'rgba(16,120,70,0.35)' },
    { id: 10, title: 'competitor yo...',  time: '10:00 AM', color: '#fca5a5', bg: 'rgba(180,30,30,0.35)' },
  ],
  3: [ // Thu
    { id: 11, title: 'ai scarcity res...', time: '5:00 AM',  color: '#c4b5fd', bg: 'rgba(124,58,237,0.35)' },
    { id: 12, title: 'morning brief',     time: '8:00 AM',  color: '#fbbf24', bg: 'rgba(180,130,0,0.35)' },
    { id: 13, title: 'competitor yo...',  time: '10:00 AM', color: '#fca5a5', bg: 'rgba(180,30,30,0.35)' },
  ],
  4: [ // Fri
    { id: 14, title: 'ai scarcity res...', time: '5:00 AM',  color: '#c4b5fd', bg: 'rgba(124,58,237,0.35)' },
    { id: 15, title: 'morning brief',     time: '8:00 AM',  color: '#fbbf24', bg: 'rgba(180,130,0,0.35)' },
    { id: 16, title: 'competitor yo...',  time: '10:00 AM', color: '#fca5a5', bg: 'rgba(180,30,30,0.35)' },
  ],
  5: [ // Sat
    { id: 17, title: 'ai scarcity res...', time: '5:00 AM',  color: '#c4b5fd', bg: 'rgba(124,58,237,0.35)' },
    { id: 18, title: 'morning brief',     time: '8:00 AM',  color: '#fbbf24', bg: 'rgba(180,130,0,0.35)' },
    { id: 19, title: 'competitor yo...',  time: '10:00 AM', color: '#fca5a5', bg: 'rgba(180,30,30,0.35)' },
  ],
  6: [ // Sun
    { id: 20, title: 'ai scarcity res...', time: '5:00 AM',  color: '#c4b5fd', bg: 'rgba(124,58,237,0.35)' },
    { id: 21, title: 'morning brief',     time: '8:00 AM',  color: '#fbbf24', bg: 'rgba(180,130,0,0.35)' },
    { id: 22, title: 'competitor yo...',  time: '10:00 AM', color: '#fca5a5', bg: 'rgba(180,30,30,0.35)' },
  ],
};

@Component({
  selector: 'app-calendar',
  standalone: true,
  imports: [],
  template: `
<div class="cal-page" data-testid="calendar-page">
  <div class="cal-header">
    <div>
      <h1 class="page-title">Calendar</h1>
      <p class="page-subtitle">Autonomous agent schedule · this week</p>
    </div>
    <div style="display:flex;gap:8px;align-items:center">
      <button class="btn btn-ghost" (click)="prevWeek()">‹</button>
      <span class="week-label">{{weekLabel()}}</span>
      <button class="btn btn-ghost" (click)="nextWeek()">›</button>
      <button class="btn btn-ghost" (click)="today()">Today</button>
    </div>
  </div>

  <!-- Week grid -->
  <div class="week-grid">
    @for (day of weekDays(); track day.label) {
      <div class="day-col" [class.today]="day.isToday">
        <div class="day-header">
          <span class="day-name">{{day.name}}</span>
          <span class="day-num" [class.today-num]="day.isToday">{{day.num}}</span>
        </div>
        <div class="day-body">
          @for (task of day.tasks; track task.id) {
            <div class="cal-task" [style.background]="task.bg" [style.border-left-color]="task.color">
              <div class="cal-task-title" [style.color]="task.color">{{task.title}}</div>
              <div class="cal-task-time">{{task.time}}</div>
            </div>
          }
        </div>
      </div>
    }
  </div>

  <!-- Next Up -->
  <div class="next-up">
    <div class="next-up-header">
      <span class="next-up-icon">📅</span>
      <span class="next-up-title">Next Up</span>
    </div>
    <div class="next-up-list">
      @for (item of nextUp; track item.id) {
        <div class="next-up-item">
          <div class="next-up-dot" [style.background]="item.color"></div>
          <span>{{item.title}}</span>
          <span class="next-up-time">{{item.time}}</span>
        </div>
      }
    </div>
  </div>
</div>
  `,
  styles: [`
    .cal-page { display: flex; flex-direction: column; height: 100%; padding: 20px; overflow: hidden; }
    .cal-header { display: flex; align-items: center; justify-content: space-between; margin-bottom: 16px; flex-shrink: 0; }
    .page-title { font-size: 20px; font-weight: 700; color: var(--text-primary); }
    .page-subtitle { font-size: 12px; color: var(--text-muted); margin-top: 2px; }
    .week-label { font-size: 13px; color: var(--text-secondary); min-width: 160px; text-align: center; }

    /* Week grid */
    .week-grid { display: flex; gap: 8px; flex: 1; overflow: hidden; }
    .day-col {
      flex: 1; min-width: 0;
      background: var(--bg-surface);
      border: 1px solid var(--border);
      border-radius: 10px;
      display: flex; flex-direction: column;
      overflow: hidden;
    }
    .day-col.today { border-color: var(--accent); }
    .day-header {
      display: flex; flex-direction: column; align-items: center;
      padding: 8px 4px;
      border-bottom: 1px solid var(--border);
      flex-shrink: 0;
    }
    .day-name { font-size: 10px; color: var(--text-muted); text-transform: uppercase; letter-spacing: 0.08em; }
    .day-num { font-size: 16px; font-weight: 600; color: var(--text-secondary); margin-top: 1px; }
    .today-num {
      width: 28px; height: 28px;
      background: var(--accent);
      color: #fff !important;
      border-radius: 50%;
      display: flex; align-items: center; justify-content: center;
      font-size: 13px;
    }
    .day-body { flex: 1; overflow-y: auto; padding: 6px; }
    .cal-task {
      background: rgba(39,39,46,0.8);
      border-left: 3px solid;
      border-radius: 0 5px 5px 0;
      padding: 6px 8px;
      margin-bottom: 5px;
    }
    .cal-task-title { font-size: 11px; font-weight: 500; line-height: 1.3; }
    .cal-task-time { font-size: 10px; color: var(--text-muted); margin-top: 2px; }

    /* Next Up */
    .next-up {
      flex-shrink: 0;
      margin-top: 12px;
      background: var(--bg-surface);
      border: 1px solid var(--border);
      border-radius: 10px;
      padding: 12px 16px;
    }
    .next-up-header { display: flex; align-items: center; gap: 6px; margin-bottom: 8px; }
    .next-up-icon { font-size: 14px; }
    .next-up-title { font-size: 12px; font-weight: 600; color: var(--text-secondary); }
    .next-up-list { display: flex; gap: 12px; flex-wrap: wrap; }
    .next-up-item {
      display: flex; align-items: center; gap: 6px;
      font-size: 12px; color: var(--text-secondary);
    }
    .next-up-dot { width: 6px; height: 6px; border-radius: 50%; flex-shrink: 0; }
    .next-up-time { color: var(--text-muted); font-size: 11px; }
  `],
})
export class CalendarComponent {
  private offsetWeeks = signal(0);

  weekDays = computed(() => {
    const now = new Date();
    const start = new Date(now);
    const dayOfWeek = now.getDay();
    const diffToMon = (dayOfWeek === 0 ? -6 : 1 - dayOfWeek) + this.offsetWeeks() * 7;
    start.setDate(now.getDate() + diffToMon);
    const days = ['Mon','Tue','Wed','Thu','Fri','Sat','Sun'];
    return Array.from({ length: 7 }, (_, i) => {
      const d = new Date(start);
      d.setDate(start.getDate() + i);
      const isToday = d.toDateString() === now.toDateString();
      return {
        name: days[i],
        num: d.getDate(),
        isToday,
        label: d.toDateString(),
        tasks: WEEK_TASKS[i] ?? [],
      };
    });
  });

  weekLabel = computed(() => {
    const days = this.weekDays();
    const first = days[0];
    const last  = days[6];
    const months = ['Jan','Feb','Mar','Apr','May','Jun','Jul','Aug','Sep','Oct','Nov','Dec'];
    const now = new Date();
    const startD = new Date(now);
    const dow = now.getDay();
    const diff = (dow === 0 ? -6 : 1 - dow) + this.offsetWeeks() * 7;
    startD.setDate(now.getDate() + diff);
    const endD = new Date(startD); endD.setDate(startD.getDate() + 6);
    return `${months[startD.getMonth()]} ${startD.getDate()} – ${months[endD.getMonth()]} ${endD.getDate()}, ${endD.getFullYear()}`;
  });

  nextUp = [
    { id: 1, title: 'AI Scarcity Research', time: 'Tomorrow 5:00 AM', color: '#c4b5fd' },
    { id: 2, title: 'Morning Brief',        time: 'Tomorrow 8:00 AM', color: '#fbbf24' },
    { id: 3, title: 'Competitor Analysis',  time: 'Tomorrow 10:00 AM', color: '#fca5a5' },
  ];

  prevWeek(): void { this.offsetWeeks.update(v => v - 1); }
  nextWeek(): void { this.offsetWeeks.update(v => v + 1); }
  today():    void { this.offsetWeeks.set(0); }
}
