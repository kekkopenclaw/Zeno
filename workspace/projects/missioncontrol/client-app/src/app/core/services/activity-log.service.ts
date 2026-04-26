import { Injectable, inject } from '@angular/core';
import { ApiService } from './api.service';
import type { ActivityLog } from '../models';

@Injectable({ providedIn: 'root' })
export class ActivityLogService {
  private api = inject(ApiService);
  getByProject(projectId: number) { return this.api.get<ActivityLog[]>(`activitylogs/project/${projectId}`); }
  getByAgent(agentId: number) { return this.api.get<ActivityLog[]>(`activitylogs/agent/${agentId}`); }
  createLog(dto: { projectId: number; agentId?: number; message: string }) {
    return this.api.post<ActivityLog>('activitylogs', dto);
  }
}
