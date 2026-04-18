import { Component, Input, Output, EventEmitter } from '@angular/core';
import { FormsModule } from '@angular/forms';
import type { Agent } from '../../core/models';

import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-agent-create-modal',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
  <div class="modal-backdrop" (click)="close.emit()" data-testid="modal-backdrop"></div>
  <div class="modal" data-testid="agent-create-modal">
    <h2>Add Teammate</h2>
    <form (ngSubmit)="submit()" #f="ngForm" data-testid="add-teammate-form">
      <input type="text" placeholder="Name" [(ngModel)]="model.name" name="name" required data-testid="input-name" />
      <input type="text" placeholder="Role" [(ngModel)]="model.role" name="role" required data-testid="input-role" />
      <select [(ngModel)]="model.model" name="model" required data-testid="select-model">
        <option value="" disabled>Select model</option>
        <option *ngFor="let m of availableModels" [value]="m">{{m}}</option>
      </select>
      <input type="text" placeholder="Skills (comma separated)" [(ngModel)]="model.skills" name="skills" data-testid="input-skills" />
      <div style="display:flex;gap:8px;align-items:center;">
        <input type="text" placeholder="Emoji" [(ngModel)]="model.emoji" name="emoji" maxlength="2" style="width:60px;" data-testid="input-emoji" />
        <input type="color" [(ngModel)]="model.color" name="color" style="width:32px;height:32px;padding:0;border:none;background:none;" data-testid="input-color" />
      </div>
      <button class="btn btn-primary" type="submit" data-testid="submit-add-teammate">Add</button>
      <button class="btn" type="button" (click)="close.emit()" data-testid="cancel-add-teammate">Cancel</button>
    </form>
  </div>
  `,
  styles: [`
    .modal-backdrop { position: fixed; inset: 0; background: rgba(0,0,0,0.2); z-index: 100; }
    .modal { position: fixed; top: 50%; left: 50%; transform: translate(-50%,-50%); background: var(--bg-surface); border-radius: 10px; padding: 24px; z-index: 101; min-width: 320px; box-shadow: 0 4px 32px rgba(0,0,0,0.12); }
    h2 { font-size: 18px; margin-bottom: 16px; }
    form { display: flex; flex-direction: column; gap: 10px; }
    input { padding: 7px 10px; border: 1px solid var(--border); border-radius: 5px; font-size: 14px; }
    .btn { margin-top: 8px; }
  `]
})
export class AgentCreateModalComponent {
  @Input() teamId!: number;
  @Input() availableModels: string[] = [];
  @Output() created = new EventEmitter<Partial<Agent>>();
  @Output() close = new EventEmitter<void>();
  model: Partial<Agent> = { name: '', role: '', model: '', skills: '', emoji: '', color: '#8b5cf6' };
  ngOnChanges() {
    this.resetForm();
  }
  resetForm() {
    this.model = { name: '', role: '', model: '', skills: '', emoji: '', color: '#8b5cf6' };
  }
  submit() {
    if (!this.model.name || !this.model.role || !this.model.model) return;
    this.created.emit({ ...this.model });
    this.close.emit();
    this.resetForm();
  }
}
