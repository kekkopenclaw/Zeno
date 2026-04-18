import { Component, OnInit, OnDestroy, Injector, inject, signal, computed, ChangeDetectionStrategy } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DatePipe } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { Subject, Subscription } from 'rxjs';
import { debounceTime, distinctUntilChanged, switchMap } from 'rxjs/operators';
import { toObservable } from '@angular/core/rxjs-interop';
import { MemoryService, ChromaSearchResult } from '../../core/services/memory.service';
import { SignalRService } from '../../core/services/signalr.service';
import { environment } from '../../../environments/environment';
import type { MemoryEntry } from '../../core/models';

interface PipelineTestResult {
  success: boolean;
  correlationId: string;
  taskId: number;
  summaryId: number;
  loopPassed: boolean;
  attempts: number;
  steps: Array<{step: string; message: string}>;
}

/**
 * MemoryComponent
 *
 * Handles long-term/insight memory entries (list, detail, add, semantic search, pipeline tests)
 * - Uses SignalRService for live update of memory, pipeline test, and semantic search results
 * - Reactive signals update UI in real time
 * - Falls back to HTTP polling if SignalR fails
 */
@Component({
  selector: 'app-memory',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, DatePipe],
  template: `
<div class="memory-layout" data-testid="memory-layout">

  <!-- ── Left panel: entry list ── -->
  <div class="memory-sidebar">
    <div class="mem-sidebar-header">
      <div class="mem-search">
        <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="11" cy="11" r="8"/><path d="m21 21-4.35-4.35"/></svg>
        <input [ngModel]="searchQuery()" (ngModelChange)="onSearchChange($event)"
               placeholder="Search memory..." class="mem-search-input" />
      </div>
      <div class="mem-header-actions">
        <button class="btn btn-primary" style="flex:1;font-size:11px"
                (click)="showAddForm.set(!showAddForm())">
          {{showAddForm() ? '✕ Cancel' : '+ Add Memory'}}
        </button>
        <!-- Semantic search button -->
        <button class="btn btn-semantic" [class.active]="showSemanticSearch()"
                (click)="toggleSemanticSearch()" title="Vector semantic search via Chroma (./chroma_data port 8000)">
          🔍 Semantic
        </button>
        <!-- Pipeline Test button -->
        <button class="btn btn-pipeline" [disabled]="pipelineTesting()"
                (click)="runPipelineTest()" title="End-to-end pipeline test: task → swarm → agent loop → memory save">
          {{pipelineTesting() ? '⏳ Testing…' : '🚀 Pipeline Test'}}
        </button>
      </div>
    </div>

    <!-- Memory object header -->
    <div class="mem-object">
      <div class="mem-object-icon">🧠</div>
      <div>
        <div class="mem-object-title">Long-Term Memory 🔥</div>
        <div class="mem-object-sub">{{entries().length}} entries · updated just now</div>
      </div>
    </div>

    <!-- Type tabs -->
    <div class="mem-tabs">
      @for (t of types; track t.value) {
        <button class="mem-tab" [class.active]="activeType() === t.value"
                (click)="activeType.set(t.value)">
          {{t.label}}
        </button>
      }
    </div>

    <!-- Entry list -->
    <div class="mem-list">
      @for (e of filtered(); track e.id) {
        <div class="mem-list-item" [class.selected]="selected()?.id === e.id" (click)="selected.set(e)">
          <div class="mem-list-title">{{e.title}}</div>
          <div class="mem-list-meta">{{e.createdAt | date:'EEE, MMM d'}} · {{e.content.length}} chars</div>
        </div>
      }
      @if (filtered().length === 0) {
        <div class="mem-empty">No entries found</div>
      }
    </div>
  </div>

  <!-- ── Right panel: add form OR pipeline log OR entry detail ── -->
  <div class="mem-detail">

    @if (showPipelineLog()) {
      <!-- Pipeline Test live log feed -->
      <div class="pipeline-panel">
        <div class="pipeline-header">
          <span class="pipeline-icon">🚀</span>
          <span class="pipeline-title">Pipeline Test</span>
          @if (pipelineResult(); as r) {
            <span class="badge" [style]="r.success ? 'background:rgba(34,197,94,0.1);color:#22c55e' : 'background:rgba(239,68,68,0.1);color:#ef4444'">
              {{r.success ? '✅ Passed' : '❌ Failed'}}
            </span>
          }
          <button class="btn btn-ghost" style="margin-left:auto;font-size:11px" (click)="showPipelineLog.set(false)">✕ Close</button>
        </div>
        <div class="pipeline-log">
          @for (step of pipelineSteps(); track step.step + step.message) {
            <div class="pipeline-step">
              <span class="step-label">{{step.step}}</span>
              <span class="step-msg">{{step.message}}</span>
            </div>
          }
          @if (pipelineSteps().length === 0 && pipelineTesting()) {
            <div class="pipeline-waiting">⏳ Waiting for steps...</div>
          }
        </div>
        @if (pipelineResult(); as r) {
          <div class="pipeline-summary">
            <div>Correlation: <code>{{r.correlationId}}</code></div>
            <div>Task ID: <code>{{r.taskId}}</code> · Summary ID: <code>{{r.summaryId}}</code></div>
            <div>Agent loop: {{r.loopPassed ? '✅ passed' : '⚠️ degraded'}} in {{r.attempts}} attempt(s)</div>
          </div>
        }
      </div>
    } @else if (showSemanticSearch()) {
      <!-- Semantic search panel — Chroma vector search at ./chroma_data port 8000 -->
      <div class="pipeline-panel">
        <div class="pipeline-header">
          <span class="pipeline-icon">🔍</span>
          <span class="pipeline-title">Semantic Search</span>
          <span style="font-size:10px;color:var(--text-muted);margin-left:4px">(Chroma · ./chroma_data · port 8000)</span>
          <button class="btn btn-ghost" style="margin-left:auto;font-size:11px" (click)="showSemanticSearch.set(false)">✕ Close</button>
        </div>
        <div style="display:flex;gap:8px;padding:8px 0">
          <input [ngModel]="semanticQuery()" (ngModelChange)="semanticQuery.set($event)"
                 placeholder="Natural-language query — e.g. 'null pointer fix'" class="form-input"
                 style="flex:1;margin:0" (keydown.enter)="runSemanticSearch()" />
          <button class="btn btn-primary" style="font-size:11px;white-space:nowrap"
                  [disabled]="semanticSearching() || !semanticQuery().trim()"
                  (click)="runSemanticSearch()">
            {{semanticSearching() ? '⏳ …' : 'Search'}}
          </button>
        </div>
        @if (semanticResults().length > 0) {
          <div class="pipeline-log" style="max-height:none;gap:0">
            @for (r of semanticResults(); track r.id) {
              <div class="sem-result">
                <div class="sem-result-header">
                  <code class="sem-id">{{r.id}}</code>
                  <span class="sem-dist">similarity: {{(1 - r.distance).toFixed(3)}}</span>
                </div>
                <div class="sem-doc">{{r.document}}</div>
                @if (r.metadata && objectKeys(r.metadata).length > 0) {
                  <div class="sem-meta">
                    @for (k of objectKeys(r.metadata); track k) {
                      <span class="mem-tag">{{k}}: {{r.metadata[k]}}</span>
                    }
                  </div>
                }
              </div>
            }
          </div>
        } @else if (!semanticSearching() && semanticQuery().trim()) {
          <div class="mem-empty" style="padding:24px">
            No results — Chroma may not be running, or no memories have been embedded yet.
          </div>
        }
      </div>
    } @else if (showAddForm()) {
      <div class="add-form">
        <h2 class="add-form-title">New Memory Entry</h2>
        <label class="add-label">Title</label>
        <input [ngModel]="newTitle()" (ngModelChange)="newTitle.set($event)" class="form-input" placeholder="Entry title…" />
        <label class="add-label">Content</label>
        <textarea [ngModel]="newContent()" (ngModelChange)="newContent.set($event)" class="form-textarea" placeholder="What was learned or decided…"></textarea>
        <div class="form-row">
          <div>
            <label class="add-label">Type</label>
            <select [ngModel]="newType()" (ngModelChange)="newType.set($event)" class="form-select">
              <option value="LongTerm">Long-Term</option>
              <option value="Insight">Insight</option>
              <option value="Decision">Decision</option>
            </select>
          </div>
          <div style="flex:1">
            <label class="add-label">Tags (comma-separated)</label>
            <input [ngModel]="newTags()" (ngModelChange)="newTags.set($event)" class="form-input" placeholder="tag1, tag2" />
          </div>
        </div>
        <div style="display:flex;justify-content:flex-end;gap:8px;margin-top:12px">
          <button class="btn btn-ghost" (click)="showAddForm.set(false)">Cancel</button>
          <button class="btn btn-primary" [disabled]="!newTitle().trim() || saving()"
                  (click)="createEntry()">
            {{saving() ? 'Saving…' : 'Save Entry'}}
          </button>
        </div>
      </div>
    } @else if (selected(); as entry) {
      <div class="mem-detail-header">
        <div>
          <div class="mem-detail-date">{{entry.createdAt | date:'EEEE, MMMM d, y'}}</div>
          <div class="mem-detail-meta">{{entry.createdAt | date:'MMMM d, yyyy'}} · {{entry.content.length}} chars</div>
        </div>
        <span class="badge" [style]="typeStyle(entry.type)">{{entry.type}}</span>
      </div>
      <h2 class="mem-detail-title">{{entry.title}}</h2>

      <div class="mem-detail-body">{{entry.content}}</div>

      @if (entry.tags) {
        <div class="mem-tags">
          @for (tag of entry.tags.split(','); track tag) {
            <span class="mem-tag">{{tag.trim()}}</span>
          }
        </div>
      }
    } @else {
      <div class="mem-no-select">
        <div class="mem-no-select-icon">💾</div>
        <div class="mem-no-select-text">Select an entry to read, or click "+ Add Memory"</div>
        <div class="mem-no-select-hint">Use 🚀 Pipeline Test to verify the full agent pipeline end-to-end.</div>
      </div>
    }
  </div>
</div>
  `,
  styles: [`
    .memory-layout { display: flex; height: 100%; overflow: hidden; }
    .memory-sidebar { width: 260px; flex-shrink: 0; border-right: 1px solid var(--border); display: flex; flex-direction: column; overflow: hidden; }
    .mem-sidebar-header { padding: 12px 10px 8px; border-bottom: 1px solid var(--border); display: flex; flex-direction: column; gap: 6px; }
    .mem-header-actions { display: flex; gap: 6px; }
    .mem-search { display: flex; align-items: center; gap: 6px; background: var(--bg-elevated); border: 1px solid var(--border); border-radius: 6px; padding: 6px 8px; color: var(--text-muted); }
    .mem-search-input { border: none; background: transparent; font-size: 12px; color: var(--text-primary); flex: 1; outline: none; }
    .mem-search-input::placeholder { color: var(--text-muted); }
    .btn-pipeline { background: rgba(139,92,246,0.12); border: 1px solid rgba(139,92,246,0.3); color: #a78bfa; padding: 4px 8px; font-size: 10px; border-radius: 5px; cursor: pointer; white-space: nowrap; transition: all 0.15s; }
    .btn-pipeline:hover:not(:disabled) { background: rgba(139,92,246,0.2); }
    .btn-pipeline:disabled { opacity: 0.5; cursor: not-allowed; }
    .btn-semantic { background: rgba(6,182,212,0.1); border: 1px solid rgba(6,182,212,0.3); color: #22d3ee; padding: 4px 8px; font-size: 10px; border-radius: 5px; cursor: pointer; white-space: nowrap; transition: all 0.15s; }
    .btn-semantic:hover { background: rgba(6,182,212,0.18); }
    .btn-semantic.active { background: rgba(6,182,212,0.22); border-color: rgba(6,182,212,0.5); }
    .mem-object { display: flex; align-items: center; gap: 10px; padding: 12px 12px 8px; border-bottom: 1px solid var(--border); }
    .mem-object-icon { font-size: 22px; }
    .mem-object-title { font-size: 13px; font-weight: 600; color: var(--text-primary); }
    .mem-object-sub { font-size: 11px; color: var(--text-muted); margin-top: 1px; }
    .mem-tabs { display: flex; gap: 4px; padding: 8px 10px; border-bottom: 1px solid var(--border); }
    .mem-tab { font-size: 11px; padding: 3px 8px; border-radius: 4px; border: 1px solid transparent; cursor: pointer; background: none; color: var(--text-muted); transition: all 0.1s; }
    .mem-tab:hover { background: var(--bg-elevated); color: var(--text-primary); }
    .mem-tab.active { background: var(--bg-elevated); border-color: var(--border); color: var(--text-primary); }
    .mem-list { flex: 1; overflow-y: auto; padding: 6px; }
    .mem-list-item { padding: 8px; border-radius: 6px; cursor: pointer; margin-bottom: 2px; transition: background 0.1s; border: 1px solid transparent; }
    .mem-list-item:hover { background: var(--bg-elevated); }
    .mem-list-item.selected { background: var(--bg-elevated); border-color: var(--border); }
    .mem-list-title { font-size: 12px; font-weight: 500; color: var(--text-primary); line-height: 1.4; }
    .mem-list-meta { font-size: 10px; color: var(--text-muted); margin-top: 2px; }
    .mem-empty { padding: 16px 8px; text-align: center; font-size: 12px; color: var(--text-muted); }
    .mem-detail { flex: 1; overflow-y: auto; padding: 28px 36px; }
    .mem-detail-header { display: flex; align-items: flex-start; justify-content: space-between; margin-bottom: 6px; }
    .mem-detail-date { font-size: 13px; font-weight: 600; color: var(--text-secondary); }
    .mem-detail-meta { font-size: 11px; color: var(--text-muted); margin-top: 2px; }
    .mem-detail-title { font-size: 22px; font-weight: 700; color: var(--text-primary); margin: 16px 0 12px; line-height: 1.3; }
    .mem-detail-body { font-size: 14px; color: var(--text-secondary); line-height: 1.8; white-space: pre-wrap; }
    .mem-tags { display: flex; gap: 6px; flex-wrap: wrap; margin-top: 20px; }
    .mem-tag { font-size: 11px; background: var(--bg-elevated); border: 1px solid var(--border); color: var(--text-muted); padding: 2px 8px; border-radius: 4px; }
    .mem-no-select { display: flex; flex-direction: column; align-items: center; justify-content: center; height: 100%; color: var(--text-muted); gap: 8px; }
    .mem-no-select-icon { font-size: 40px; margin-bottom: 4px; opacity: 0.4; }
    .mem-no-select-text { font-size: 13px; }
    .mem-no-select-hint { font-size: 11px; opacity: 0.6; }
    .add-form { max-width: 600px; }
    .add-form-title { font-size: 18px; font-weight: 700; color: var(--text-primary); margin-bottom: 20px; }
    .add-label { display: block; font-size: 11px; font-weight: 600; color: var(--text-muted); text-transform: uppercase; letter-spacing: 0.06em; margin-bottom: 4px; }
    .form-input, .form-select { width: 100%; padding: 8px 10px; margin-bottom: 14px; background: var(--bg-elevated); border: 1px solid var(--border); color: var(--text-primary); border-radius: 6px; font-size: 13px; box-sizing: border-box; }
    .form-textarea { width: 100%; height: 120px; padding: 8px 10px; margin-bottom: 14px; background: var(--bg-elevated); border: 1px solid var(--border); color: var(--text-primary); border-radius: 6px; font-size: 13px; resize: vertical; font-family: inherit; box-sizing: border-box; }
    .form-row { display: flex; gap: 12px; align-items: flex-start; }
    .form-row > div { flex: 1; }
    /* Pipeline panel */
    .pipeline-panel { display: flex; flex-direction: column; height: 100%; gap: 12px; }
    .pipeline-header { display: flex; align-items: center; gap: 8px; padding-bottom: 12px; border-bottom: 1px solid var(--border); }
    .pipeline-icon { font-size: 18px; }
    .pipeline-title { font-size: 15px; font-weight: 700; color: var(--text-primary); }
    .pipeline-log { flex: 1; overflow-y: auto; background: var(--bg-elevated); border: 1px solid var(--border); border-radius: 8px; padding: 12px; display: flex; flex-direction: column; gap: 6px; max-height: 400px; }
    .pipeline-step { display: flex; align-items: flex-start; gap: 10px; padding: 6px 0; border-bottom: 1px solid rgba(255,255,255,0.04); font-size: 12px; }
    .step-label { font-weight: 600; color: var(--accent); font-size: 10px; text-transform: uppercase; letter-spacing: 0.06em; min-width: 100px; flex-shrink: 0; margin-top: 1px; }
    .step-msg { color: var(--text-secondary); line-height: 1.5; }
    .pipeline-waiting { color: var(--text-muted); font-size: 12px; padding: 8px 0; }
    .pipeline-summary { background: var(--bg-elevated); border: 1px solid var(--border); border-radius: 6px; padding: 12px 16px; font-size: 12px; color: var(--text-secondary); display: flex; flex-direction: column; gap: 4px; }
    .pipeline-summary code { font-family: monospace; background: var(--bg-base); padding: 1px 4px; border-radius: 3px; color: var(--accent); font-size: 11px; }
    /* Semantic search results */
    .sem-result { padding: 10px 0; border-bottom: 1px solid rgba(255,255,255,0.05); }
    .sem-result:last-child { border-bottom: none; }
    .sem-result-header { display: flex; align-items: center; gap: 8px; margin-bottom: 4px; }
    .sem-id { font-family: monospace; font-size: 10px; color: var(--accent); background: var(--bg-base); padding: 1px 5px; border-radius: 3px; }
    .sem-dist { font-size: 10px; color: var(--text-muted); margin-left: auto; }
    .sem-doc { font-size: 12px; color: var(--text-secondary); line-height: 1.6; white-space: pre-wrap; }
    .sem-meta { display: flex; gap: 4px; flex-wrap: wrap; margin-top: 6px; }
  `],
})
export class MemoryComponent implements OnInit, OnDestroy {
  private svc     = inject(MemoryService);
  private http    = inject(HttpClient);
  private injector = inject(Injector);
  readonly signalr = inject(SignalRService);

  // All reactive state as signals — prevents ExpressionChangedAfterItHasBeenCheckedError
  entries      = signal<MemoryEntry[]>([]);
  selected     = signal<MemoryEntry | null>(null);
  searchQuery  = signal('');
  activeType   = signal('All');
  showAddForm  = signal(false);
  saving       = signal(false);

  // New-entry form fields as signals
  newTitle   = signal('');
  newContent = signal('');
  newType    = signal('Decision');
  newTags    = signal('');

  // Pipeline test state
  pipelineTesting  = signal(false);
  showPipelineLog  = signal(false);
  pipelineResult   = signal<PipelineTestResult | null>(null);

  // Semantic search state (Chroma vector search — ./chroma_data port 8000, results injected pre-prompt)
  showSemanticSearch = signal(false);
  semanticQuery      = signal('');
  semanticSearching  = signal(false);
  semanticResults    = signal<import('../../core/services/memory.service').ChromaSearchResult[]>([]);
  pipelineSteps    = signal<Array<{step: string; message: string}>>([]);

  // Subject for debounced backend search — avoids excessive API calls while typing
  private readonly searchSubject = new Subject<string>();
  private subscriptions: Subscription[] = [];

  types = [
    { value: 'All',      label: 'All' },
    { value: 'LongTerm', label: 'Long-Term' },
    { value: 'Insight',  label: 'Insight' },
    { value: 'Decision', label: 'Decision' },
  ];

  // Computed filter applies type filter + local text pre-filter (instant feedback while typing).
  // The debounced backend search also updates entries with server-side results.
  filtered = computed(() => {
    let r = this.entries();
    const type = this.activeType();
    if (type !== 'All') r = r.filter(e => e.type === type);
    const q = this.searchQuery().trim().toLowerCase();
    if (q) r = r.filter(e => e.title.toLowerCase().includes(q) || e.content.toLowerCase().includes(q));
    return [...r].sort((a, b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime());
  });

  typeStyle(type: string): string {
    const m: Record<string, string> = {
      LongTerm: 'background:rgba(139,92,246,0.15);color:#a78bfa',
      Insight:  'background:rgba(59,130,246,0.1);color:#60a5fa',
      Decision: 'background:rgba(245,158,11,0.1);color:#f59e0b',
    };
    return m[type] ?? '';
  }

  ngOnInit(): void {
    // Load all entries on first render
    this.svc.getAll().subscribe(d => {
      this.entries.set(d);
      if (d.length > 0) this.selected.set(d[0]);
    });

    // Debounced backend search: fires 300 ms after the user stops typing.
    // Empty query reloads all entries; non-empty queries call the search API.
    this.subscriptions.push(
      this.searchSubject.pipe(
        debounceTime(300),
        distinctUntilChanged(),
        switchMap(q =>
          q.trim().length >= 2
            ? this.svc.search(1, q)
            : this.svc.getAll()
        ),
      ).subscribe(results => {
        this.entries.set(results);
        // Keep selection valid after search — clear if the selected entry is no longer in results
        const sel = this.selected();
        if (sel && !results.some(e => e.id === sel.id)) this.selected.set(null);
      })
    );

    // Listen for new memories via SignalR — refresh entry list live.
    // { injector } is required so toObservable can create an effect in the correct context.
    this.subscriptions.push(
      toObservable(this.signalr.lastMemoryAdded, { injector: this.injector }).subscribe(mem => {
        if (!mem) return;  // skip null/initial emission
        this.entries.update(list => {
          if (list.some(e => e.id === mem.id)) return list;
          return [mem, ...list];
        });
      })
    );

    // Listen for pipeline test steps broadcast via SignalR
    this.subscriptions.push(
      toObservable(this.signalr.pipelineTestSteps, { injector: this.injector }).subscribe(steps => {
        if (steps.length > 0) {
          this.pipelineSteps.set(steps.map(s => ({ step: s.step, message: s.message })));
        }
      })
    );
  }

  /** Toggles the semantic search panel. */
  toggleSemanticSearch(): void {
    const next = !this.showSemanticSearch();
    this.showSemanticSearch.set(next);
    if (next) {
      this.showAddForm.set(false);
      this.showPipelineLog.set(false);
    }
  }

  /**
   * Runs a semantic vector search against the Chroma v2 vector store via GET /api/memory/semantic.
   * Chroma must be running at port 8000 for results to be returned.
   * Results are ranked by cosine similarity and injected into future agent prompts.
   */
  runSemanticSearch(): void {
    const q = this.semanticQuery().trim();
    if (!q) return;
    this.semanticSearching.set(true);
    this.semanticResults.set([]);
    this.svc.semanticSearch(q).subscribe({
      next: results => {
        this.semanticResults.set(results);
        this.semanticSearching.set(false);
      },
      error: () => {
        this.semanticResults.set([]);
        this.semanticSearching.set(false);
      }
    });
  }

  /** Exposes Object.keys to the template. */
  objectKeys = Object.keys;

  /** Called from the search input's (ngModelChange) — updates signal and triggers debounced backend search. */
  onSearchChange(q: string): void {
    this.searchQuery.set(q);
    this.searchSubject.next(q);
  }

  /** Runs the full pipeline test: task → swarm → agent loop → memory save → live log feed */
  runPipelineTest(): void {
    this.pipelineTesting.set(true);
    this.showPipelineLog.set(true);
    this.showAddForm.set(false);
    this.showSemanticSearch.set(false);
    this.pipelineResult.set(null);
    this.pipelineSteps.set([]);
    // Clear SignalR step buffer so we start fresh
    this.signalr.pipelineTestSteps.set([]);

    this.http.post<PipelineTestResult>(
      `${environment.apiUrl.replace('/api', '')}/api/pipeline-test?projectId=1`,
      {}
    ).subscribe({
      next: result => {
        this.pipelineResult.set(result);
        this.pipelineTesting.set(false);
        // Reload entries so the new memory entry appears immediately
        this.svc.getAll().subscribe(d => this.entries.set(d));
      },
      error: err => {
        this.pipelineResult.set({
          success: false,
          correlationId: '',
          taskId: 0,
          summaryId: 0,
          loopPassed: false,
          attempts: 0,
          steps: [{ step: 'Error', message: err.message ?? 'Request failed' }],
        });
        this.pipelineTesting.set(false);
      }
    });
  }

  createEntry(): void {
    const title = this.newTitle().trim();
    if (!title) return;
    this.saving.set(true);
    this.svc.create({
      projectId: 1,
      title,
      content: this.newContent().trim(),
      type:    this.newType(),
      tags:    this.newTags().trim(),
    }).subscribe({
      next: entry => {
        this.entries.update(list => [entry, ...list]);
        this.selected.set(entry);
        this.newTitle.set('');
        this.newContent.set('');
        this.newType.set('Decision');
        this.newTags.set('');
        this.showAddForm.set(false);
        this.saving.set(false);
      },
      error: () => this.saving.set(false),
    });
  }

  ngOnDestroy(): void {
    this.subscriptions.forEach(s => s.unsubscribe());
  }
}

