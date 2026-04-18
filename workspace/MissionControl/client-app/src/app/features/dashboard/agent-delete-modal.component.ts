import { Component, Input, Output, EventEmitter } from '@angular/core';
import type { Agent } from '../../core/models';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-agent-delete-modal',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="modal-backdrop" (click)="close.emit()"></div>
    <div class="modal">
      <h2>Remove Agent</h2>
      <div class="modal-message">
        Are you sure you want to remove <b>{{agent?.name}}</b>? This will delete the agent from both MissionControl and OpenClaw.
      </div>
      <div class="modal-actions">
        <button class="btn" type="button" (click)="close.emit()">Cancel</button>
        <button class="btn btn-danger" type="button" (click)="onConfirm()">Remove</button>
      </div>
    </div>
  `,
  styles: [`
    .modal-backdrop { position: fixed; inset: 0; background: rgba(0,0,0,0.2); z-index: 100; }
    .modal { position: fixed; top: 50%; left: 50%; transform: translate(-50%,-50%); background: var(--bg-surface); border-radius: 10px; padding: 24px; z-index: 101; min-width: 320px; box-shadow: 0 4px 32px rgba(0,0,0,0.12); }
    h2 { font-size: 18px; margin-bottom: 16px; }
    .modal-message { font-size: 14px; margin-bottom: 18px; color: var(--text-secondary); }
    .modal-actions { display: flex; gap: 10px; justify-content: flex-end; }
    .btn { padding: 7px 18px; border-radius: 5px; border: 1px solid var(--border); background: var(--bg-elevated); color: var(--text-primary); cursor: pointer; }
    .btn-danger { background: #ef4444; color: #fff; border: none; }
    .btn-danger:hover { background: #dc2626; }
  `]
})
export class AgentDeleteModalComponent {
  @Input() agent: Agent | null = null;
    @Output() confirm = new EventEmitter<Agent>();
  @Output() close = new EventEmitter<void>();
  
    onConfirm() {
      if (this.agent) {
        this.confirm.emit(this.agent);
      }
    }
}
