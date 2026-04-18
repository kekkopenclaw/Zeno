import { Injectable, inject } from '@angular/core';
import { ApiService } from './api.service';
import type { Project, CreateProject } from '../models';

@Injectable({ providedIn: 'root' })
export class ProjectService {
  private api = inject(ApiService);
  getAll() { return this.api.get<Project[]>('projects'); }
  getById(id: number) { return this.api.get<Project>(`projects/${id}`); }
  create(dto: CreateProject) { return this.api.post<Project>('projects', dto); }
  delete(id: number) { return this.api.delete<void>(`projects/${id}`); }
}
