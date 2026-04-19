import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { CommonModule } from '@angular/common';
import { TeamService } from '../../core/services/team.service';
import type { Team, CreateTeam } from '../../core/models';

@Component({
  selector: 'app-teams',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
<div class="teams-page">
  <div class="teams-header">
    <div>
      <h1 class="page-title">Teams</h1>
      <p class="page-subtitle">{{teams().length}} teams</p>
    </div>
    <button class="btn btn-primary" (click)="toggleForm()" data-testid="new-team-btn">
      {{showForm() ? '✕ Cancel' : '+ New Team'}}
    </button>
  </div>

  @if (showForm()) {
    <div class="team-form-card" data-testid="team-form">
      <input [(ngModel)]="newTeam.name" placeholder="Team name..." class="form-input" data-testid="team-name-input" required />
      <textarea [(ngModel)]="newTeam.description" placeholder="Description..." class="form-textarea" data-testid="team-desc-input" rows="3"></textarea>
      <div style="display:flex;gap:8px;margin-top:8px;">
        <button class="btn btn-primary" (click)="createTeam()" data-testid="create-team-btn">Create Team</button>
        <button class="btn" (click)="toggleForm()">Cancel</button>
      </div>
    </div>
  }

  <!-- Search + filter bar -->
  <div class="search-row">
    <div class="search-box">
      <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="11" cy="11" r="8"/><path d="m21 21-4.35-4.35"/></svg>
      <input [(ngModel)]="search" placeholder="Search teams..." class="search-input" data-testid="team-search" />
    </div>
    <span class="results-count">{{filteredTeams().length}} teams</span>
  </div>

  <!-- Teams grid -->
  <div class="teams-grid">
    @for (team of pagedTeams(); track team.id) {
      @if (editingId() === team.id) {
        <!-- Inline edit form -->
        <div class="team-card editing" data-testid="team-edit-card">
          <input [(ngModel)]="editDraft.name" class="form-input" data-testid="team-edit-name" />
          <textarea [(ngModel)]="editDraft.description" class="form-textarea" rows="2" data-testid="team-edit-desc"></textarea>
          <div style="display:flex;gap:8px;margin-top:8px;">
            <button class="btn btn-primary" (click)="saveTeam(team.id)" data-testid="save-team-btn">Save</button>
            <button class="btn" (click)="cancelEdit()">Cancel</button>
          </div>
        </div>
      } @else {
        <div class="team-card" data-testid="team-card">
          <div class="team-card-header">
            <span class="team-icon">👥</span>
            <div class="team-actions">
              <button class="btn btn-sm" (click)="startEdit(team)" title="Edit team" data-testid="edit-team-btn">✏️</button>
              <button class="btn btn-sm btn-danger" (click)="deleteTeam(team.id)" title="Delete team" data-testid="delete-team-btn">🗑</button>
            </div>
          </div>
          <div class="team-name">{{team.name}}</div>
          <div class="team-desc">{{team.description}}</div>
          <div class="team-meta">Project {{team.projectId}}</div>
        </div>
      }
    }
    @if (filteredTeams().length === 0) {
      <div class="empty-state" data-testid="teams-empty">
        <p>No teams found. Create the first one!</p>
      </div>
    }
  </div>

  <!-- Pagination -->
  @if (totalPages() > 1) {
    <div class="pagination" data-testid="team-pagination">
      <button class="btn btn-sm" [disabled]="page() === 0" (click)="prevPage()">← Prev</button>
      <span>Page {{page() + 1}} / {{totalPages()}}</span>
      <button class="btn btn-sm" [disabled]="page() === totalPages() - 1" (click)="nextPage()">Next →</button>
    </div>
  }
</div>
  `,
  styles: [`
    .teams-page { padding: 24px; max-width: 1100px; }
    .teams-header { display:flex;justify-content:space-between;align-items:flex-start;margin-bottom:20px; }
    .page-title { font-size:20px;font-weight:700;color:var(--text-primary); }
    .page-subtitle { font-size:11px;color:var(--text-muted);margin-top:2px; }
    .team-form-card { background:var(--bg-elevated);border:1px solid var(--border);border-radius:12px;padding:20px;margin-bottom:20px;display:flex;flex-direction:column;gap:10px; }
    .search-row { display:flex;align-items:center;gap:12px;margin-bottom:16px; }
    .search-box { display:flex;align-items:center;gap:6px;background:var(--bg-elevated);border:1px solid var(--border);border-radius:6px;padding:5px 8px;color:var(--text-muted); }
    .search-input { border:none;background:transparent;font-size:12px;color:var(--text-primary);outline:none;width:160px; }
    .results-count { font-size:12px;color:var(--text-muted); }
    .teams-grid { display:grid;grid-template-columns:repeat(auto-fill,minmax(280px,1fr));gap:16px;margin-bottom:20px; }
    .team-card { background:var(--bg-elevated);border:1.5px solid var(--border);border-radius:12px;padding:16px;transition:border-color 0.15s; }
    .team-card:hover { border-color:var(--accent); }
    .team-card.editing { border-color:var(--accent);display:flex;flex-direction:column;gap:8px; }
    .team-card-header { display:flex;justify-content:space-between;align-items:center;margin-bottom:10px; }
    .team-icon { font-size:24px; }
    .team-actions { display:flex;gap:4px; }
    .team-name { font-weight:600;font-size:16px;margin-bottom:4px; }
    .team-desc { font-size:13px;color:var(--text-muted);margin-bottom:8px; }
    .team-meta { font-size:11px;color:var(--text-muted); }
    .pagination { display:flex;align-items:center;gap:12px;justify-content:center;padding:16px 0; }
    .btn-danger { color:#ef4444;border-color:#ef4444; }
    .form-input,.form-textarea { padding:7px 10px;border:1px solid var(--border);border-radius:6px;font-size:14px;background:var(--bg-surface);color:var(--text);width:100%;box-sizing:border-box; }
    .form-textarea { resize:vertical; }
    .empty-state { grid-column:1/-1;text-align:center;padding:40px;color:var(--text-muted); }
  `]
})
export class TeamsComponent implements OnInit {
  private teamService = inject(TeamService);

  teams    = signal<Team[]>([]);
  search   = '';
  showForm = signal(false);
  newTeam: CreateTeam = { name: '', description: '', projectId: 1 };
  editingId = signal<number | null>(null);
  editDraft: Partial<Team> = {};

  readonly pageSize = 12;
  page = signal(0);

  filteredTeams = computed(() => {
    const q = this.search.toLowerCase();
    return this.teams().filter(t =>
      !q || t.name.toLowerCase().includes(q) || t.description.toLowerCase().includes(q)
    );
  });

  totalPages = computed(() => Math.max(1, Math.ceil(this.filteredTeams().length / this.pageSize)));

  pagedTeams = computed(() => {
    const start = this.page() * this.pageSize;
    return this.filteredTeams().slice(start, start + this.pageSize);
  });

  ngOnInit(): void {
    this.loadTeams();
  }

  private loadTeams(): void {
    this.teamService.getByProject(1).subscribe(t => this.teams.set(t));
  }

  toggleForm(): void {
    this.showForm.update(v => !v);
    this.newTeam = { name: '', description: '', projectId: 1 };
  }

  createTeam(): void {
    if (!this.newTeam.name.trim()) return;
    this.teamService.create(this.newTeam).subscribe(team => {
      this.teams.update(all => [...all, team]);
      this.showForm.set(false);
      this.newTeam = { name: '', description: '', projectId: 1 };
    });
  }

  startEdit(team: Team): void {
    this.editingId.set(team.id);
    this.editDraft = { name: team.name, description: team.description };
  }

  cancelEdit(): void {
    this.editingId.set(null);
  }

  saveTeam(id: number): void {
    const dto: CreateTeam = {
      name: this.editDraft.name ?? '',
      description: this.editDraft.description ?? '',
      projectId: 1
    };
    this.teamService.update(id, dto).subscribe(updated => {
      if (updated) {
        this.teams.update(all => all.map(t => t.id === id ? updated : t));
      }
      this.editingId.set(null);
    });
  }

  deleteTeam(id: number): void {
    if (!confirm('Delete this team?')) return;
    this.teamService.delete(id).subscribe(() => {
      this.teams.update(all => all.filter(t => t.id !== id));
    });
  }

  prevPage(): void { this.page.update(p => Math.max(0, p - 1)); }
  nextPage(): void { this.page.update(p => Math.min(this.totalPages() - 1, p + 1)); }
}
