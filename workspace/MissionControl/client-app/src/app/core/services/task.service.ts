import { Injectable, inject } from '@angular/core';
import { ApiService } from './api.service';
import type { TaskItem, CreateTask, UpdateTaskStatus } from '../models';

@Injectable({ providedIn: 'root' })
export class TaskService {
  private api = inject(ApiService);
  getAll() { return this.api.get<TaskItem[]>('tasks'); }
  getByProject(projectId: number) { return this.api.get<TaskItem[]>(`tasks/project/${projectId}`); }
  create(dto: CreateTask) { return this.api.post<TaskItem>('tasks', dto); }
  updateStatus(id: number, dto: UpdateTaskStatus) { return this.api.patch<TaskItem>(`tasks/${id}/status`, dto); }
  delete(id: number) { return this.api.delete<void>(`tasks/${id}`); }
}
