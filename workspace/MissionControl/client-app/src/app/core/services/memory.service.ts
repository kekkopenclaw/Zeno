import { Injectable, inject } from '@angular/core';
import { ApiService } from './api.service';
import type { MemoryEntry, CreateMemory } from '../models';

export interface ChromaSearchResult {
  id: string;
  document: string;
  distance: number;
  metadata: Record<string, string>;
}

@Injectable({ providedIn: 'root' })
export class MemoryService {
  private api = inject(ApiService);
  getAll() { return this.api.get<MemoryEntry[]>('memory'); }
  getByProject(projectId: number) { return this.api.get<MemoryEntry[]>(`memory/project/${projectId}`); }
  search(projectId: number, query: string) { return this.api.get<MemoryEntry[]>(`memory/project/${projectId}/search?q=${encodeURIComponent(query)}`); }
  /** Semantic vector search via Chroma v2 — returns top-K nearest neighbours by cosine similarity. */
  semanticSearch(query: string, topK = 6) {
    return this.api.get<ChromaSearchResult[]>(`memory/semantic?query=${encodeURIComponent(query)}&topK=${topK}`);
  }
  create(dto: CreateMemory) { return this.api.post<MemoryEntry>('memory', dto); }
  delete(id: number) { return this.api.delete<void>(`memory/${id}`); }
}
