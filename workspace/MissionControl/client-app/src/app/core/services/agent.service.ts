import { Injectable, inject } from '@angular/core';
import { ApiService } from './api.service';
import type { Agent, CreateAgent } from '../models';

@Injectable({ providedIn: 'root' })
export class AgentService {
  private api = inject(ApiService);

  getAll()                         { return this.api.get<Agent[]>('agents'); }
  getByProject(projectId: number)  { return this.api.get<Agent[]>(`agents/project/${projectId}`); }
  create(dto: CreateAgent)         { return this.api.post<Agent>('agents', dto); }
  update(id: number, dto: Partial<Agent>) { return this.api.put<Agent>(`agents/${id}`, dto); }
  delete(id: number)               { return this.api.delete<void>(`agents/${id}`); }
  spawn(id: number, model: string) { return this.api.post<Agent>(`agents/${id}/spawn`, { model }); }
  pause(id: number)                { return this.api.post<Agent>(`agents/${id}/pause`, {}); }
  resume(id: number)               { return this.api.post<Agent>(`agents/${id}/resume`, {}); }
}
