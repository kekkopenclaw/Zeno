import { Component, Input, Output, EventEmitter, OnChanges } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NgFor } from '@angular/common';
import type { Agent } from '../../core/models';

@Component({
  selector: 'app-agent-edit-modal',
  standalone: true,
  imports: [FormsModule, NgFor],
  template: `
  <div class="modal-backdrop" (click)="close.emit()" data-testid="edit-modal-backdrop"></div>
  <div class="modal" data-testid="agent-edit-modal">
    <h2>Edit Agent</h2>
    <form (ngSubmit)="submit()" data-testid="edit-agent-form">
      <label>Name</label>
      <input type="text" [(ngModel)]="draft.name" name="name" required data-testid="edit-name" />
      <label>Role / Capability</label>
      <input type="text" [(ngModel)]="draft.role" name="role" required data-testid="edit-role" placeholder="e.g. Coding, Architecture, Security" />
      <label>LLM Model</label>
      <select [(ngModel)]="draft.model" name="model" required data-testid="edit-model">
        <option value="" disabled>Select model</option>
        <option *ngFor="let m of availableModels" [value]="m">{{m}}</option>
      </select>
      <label>Skills (comma separated)</label>
      <input type="text" [(ngModel)]="draft.skills" name="skills" data-testid="edit-skills" placeholder="Coding,Testing,Security" />
      <label>Description</label>
      <textarea [(ngModel)]="draft.description" name="description" rows="3" data-testid="edit-description"></textarea>
      <label>Execution Backend</label>
      <select [(ngModel)]="draft.executionBackend" name="executionBackend" data-testid="edit-backend">
        <option value="OpenClaw">OpenClaw</option>
        <option value="Ollama">Ollama</option>
      </select>
      <div style="display:flex;gap:8px;align-items:center;margin-top:6px;">
        <input type="text" [(ngModel)]="draft.emoji" name="emoji" maxlength="2" style="width:60px;" placeholder="Emoji" data-testid="edit-emoji" />
        <input type="color" [(ngModel)]="draft.color" name="color" style="width:32px;height:32px;padding:0;border:none;background:none;" data-testid="edit-color" />
        <label style="display:flex;align-items:center;gap:4px;font-size:13px;">
          <input type="checkbox" [(ngModel)]="draft.toolsEnabled" name="toolsEnabled" /> Tools
        </label>
        <label style="display:flex;align-items:center;gap:4px;font-size:13px;">
          <input type="checkbox" [(ngModel)]="draft.pushRole" name="pushRole" /> Push
        </label>
      </div>
      <div style="display:flex;gap:8px;margin-top:12px;">
        <button class="btn btn-primary" type="submit" data-testid="save-agent">Save Changes</button>
        <button class="btn" type="button" (click)="close.emit()" data-testid="cancel-edit-agent">Cancel</button>
      </div>
    </form>
  </div>
  `,
  styles: [`
    .modal-backdrop { position:fixed;inset:0;background:rgba(0,0,0,0.25);z-index:200; }
    .modal { position:fixed;top:50%;left:50%;transform:translate(-50%,-50%);background:var(--bg-surface);border-radius:12px;padding:28px;z-index:201;min-width:360px;max-width:480px;width:90%;box-shadow:0 8px 40px rgba(0,0,0,0.18); }
    h2 { font-size:18px;margin-bottom:16px; }
    label { font-size:12px;color:var(--text-muted);margin-bottom:2px;display:block; }
    form { display:flex;flex-direction:column;gap:8px; }
    input,select,textarea { padding:7px 10px;border:1px solid var(--border);border-radius:6px;font-size:14px;background:var(--bg-elevated);color:var(--text);width:100%;box-sizing:border-box; }
    textarea { resize:vertical; }
  `]
})
export class AgentEditModalComponent implements OnChanges {
  @Input() agent!: Agent;
  @Input() availableModels: string[] = [];
  @Output() saved  = new EventEmitter<Partial<Agent>>();
  @Output() close  = new EventEmitter<void>();

  draft: Partial<Agent> = {};

  ngOnChanges(): void {
    if (this.agent) {
      this.draft = { ...this.agent };
    }
  }

  submit(): void {
    if (!this.draft.name || !this.draft.model) return;
    this.saved.emit({ ...this.draft });
    this.close.emit();
  }
}
