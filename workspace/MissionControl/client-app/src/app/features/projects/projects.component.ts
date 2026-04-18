import { Component, OnInit, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ProjectService } from '../../core/services/project.service';
import type { Project } from '../../core/models';

@Component({
  selector: 'app-projects',
  standalone: true,
  imports: [DatePipe, FormsModule],
  template: `
<div style="padding:24px;max-width:1000px" data-testid="projects-page">
  <div style="display:flex;align-items:center;justify-content:space-between;margin-bottom:24px">
    <div>
      <h1 style="font-size:20px;font-weight:700;color:var(--text-primary)">Projects</h1>
      <p style="font-size:12px;color:var(--text-muted);margin-top:2px">Autonomous mission workspaces</p>
    </div>
    <button class="btn btn-primary" (click)="showForm = !showForm">
      {{showForm ? '✕ Cancel' : '+ New Project'}}
    </button>
  </div>

  @if (showForm) {
    <div class="new-project-form">
      <input [(ngModel)]="newName" placeholder="Project name…" class="form-input" />
      <textarea [(ngModel)]="newDesc" placeholder="Description…" class="form-textarea"></textarea>
      <div style="display:flex;justify-content:flex-end;gap:8px">
        <button class="btn btn-ghost" (click)="showForm = false">Cancel</button>
        <button class="btn btn-primary" [disabled]="!newName.trim() || saving()"
                (click)="createProject()">
          {{saving() ? 'Creating…' : 'Create Project'}}
        </button>
      </div>
    </div>
  }

  <div style="display:grid;grid-template-columns:repeat(3,1fr);gap:12px">
    @for (p of projects(); track p.id) {
      <div class="card card-hover" style="cursor:pointer">
        <div style="display:flex;align-items:flex-start;justify-content:space-between;margin-bottom:10px">
          <div style="width:36px;height:36px;background:rgba(124,58,237,0.15);border:1px solid rgba(124,58,237,0.3);border-radius:8px;display:flex;align-items:center;justify-content:center;font-size:18px">📁</div>
          <span class="badge" style="background:rgba(34,197,94,0.1);color:#22c55e">Active</span>
        </div>
        <div style="font-size:14px;font-weight:600;color:var(--text-primary);margin-bottom:5px">{{p.name}}</div>
        <div style="font-size:12px;color:var(--text-muted);line-height:1.5">{{p.description}}</div>
        <div style="font-size:10px;color:var(--text-muted);margin-top:10px;padding-top:10px;border-top:1px solid var(--border)">Created {{p.createdAt | date:'MMM d, y'}}</div>
      </div>
    }
    <div class="card" (click)="showForm = true"
         style="cursor:pointer;border-style:dashed;display:flex;flex-direction:column;align-items:center;justify-content:center;gap:8px;min-height:140px;color:var(--text-muted)">
      <span style="font-size:24px;opacity:0.4">+</span>
      <span style="font-size:12px">New Project</span>
    </div>
  </div>
</div>
  `,
  styles: [`
    .new-project-form {
      background: var(--bg-surface); border: 1px solid var(--accent);
      border-radius: 10px; padding: 16px; margin-bottom: 20px;
    }
    .form-input, .form-textarea {
      width: 100%; padding: 8px 10px; margin-bottom: 10px;
      background: var(--bg-elevated); border: 1px solid var(--border);
      color: var(--text-primary); border-radius: 6px; font-size: 13px;
      box-sizing: border-box;
    }
    .form-textarea { height: 72px; resize: vertical; font-family: inherit; }
  `],
})
export class ProjectsComponent implements OnInit {
  private svc = inject(ProjectService);
  projects = signal<Project[]>([]);
  showForm = false;
  saving = signal(false);
  newName = '';
  newDesc = '';

  ngOnInit(): void { this.svc.getAll().subscribe(d => this.projects.set(d)); }

  createProject(): void {
    if (!this.newName.trim()) return;
    this.saving.set(true);
    this.svc.create({ name: this.newName.trim(), description: this.newDesc.trim() }).subscribe({
      next: p => {
        this.projects.update(list => [p, ...list]);
        this.newName = '';
        this.newDesc = '';
        this.showForm = false;
        this.saving.set(false);
      },
      error: () => this.saving.set(false),
    });
  }
}
