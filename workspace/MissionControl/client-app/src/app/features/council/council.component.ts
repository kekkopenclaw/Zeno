import { Component } from '@angular/core';

@Component({
  selector: 'app-council',
  standalone: true,
  imports: [],
  template: `
<div class="page" style="padding:24px;max-width:900px" data-testid="council-page">
  <div style="margin-bottom:24px">
    <h1 style="font-size:20px;font-weight:700;color:var(--text-primary)">Council</h1>
    <p style="font-size:12px;color:var(--text-muted);margin-top:4px">Strategic decisions, approvals and team discussions</p>
  </div>

  <div class="card" style="margin-bottom:12px">
    <div style="display:flex;align-items:center;gap:10px;margin-bottom:16px">
      <span style="font-size:16px">📋</span>
      <span style="font-size:13px;font-weight:600;color:var(--text-primary)">Pending Approvals</span>
      <span class="badge" style="background:rgba(239,68,68,0.1);color:#f87171;margin-left:auto">2 waiting</span>
    </div>
    @for (item of approvals; track item.id) {
      <div style="display:flex;align-items:center;gap:12px;padding:10px 0;border-bottom:1px solid var(--border)">
        <span style="font-size:13px;color:var(--text-secondary);flex:1">{{item.title}}</span>
        <span class="badge" style="background:rgba(245,158,11,0.1);color:#f59e0b">{{item.agent}}</span>
        <button class="btn btn-primary" style="font-size:11px;padding:4px 10px">Approve</button>
        <button class="btn btn-ghost" style="font-size:11px;padding:4px 10px">Reject</button>
      </div>
    }
  </div>

  <div class="card">
    <div style="display:flex;align-items:center;gap:10px;margin-bottom:16px">
      <span style="font-size:16px">⊕</span>
      <span style="font-size:13px;font-weight:600;color:var(--text-primary)">Agent Discussions</span>
    </div>
    @for (msg of discussions; track msg.id) {
      <div style="display:flex;gap:10px;padding:8px 0;border-bottom:1px solid var(--border)">
        <span style="font-size:16px;flex-shrink:0">{{msg.emoji}}</span>
        <div>
          <div style="display:flex;gap:8px;align-items:center;margin-bottom:3px">
            <span style="font-size:12px;font-weight:600;color:var(--text-primary)">{{msg.agent}}</span>
            <span style="font-size:10px;color:var(--text-muted)">{{msg.time}}</span>
          </div>
          <div style="font-size:12px;color:var(--text-secondary);line-height:1.6">{{msg.text}}</div>
        </div>
      </div>
    }
  </div>
</div>
  `,
})
export class CouncilComponent {
  approvals = [
    { id: 1, title: 'Deploy refactored MemoryService to production', agent: 'Piccolo' },
    { id: 2, title: 'Update routing weights after 50 completed tasks', agent: 'Trunks' },
  ];
  discussions = [
    { id: 1, emoji: '🌀', agent: 'Whis',    time: '2 min ago',  text: 'Routing task #6 (Complexity: 7) to Vegeta. Standard Kakarot is at capacity.' },
    { id: 2, emoji: '📖', agent: 'Gohan',   time: '8 min ago',  text: 'Review of task #3 (Memory Refactor) passed. Moving to Done. Zero security issues found.' },
    { id: 3, emoji: '💾', agent: 'Trunks',  time: '15 min ago', text: 'Stored lesson: ContextLoader caching reduced token spend by 70% on 12 tasks.' },
    { id: 4, emoji: '🌿', agent: 'Piccolo', time: '22 min ago', text: 'Refactor complete. Removed 340 lines of dead code. SOLID score improved from 6/10 to 9/10.' },
  ];
}
