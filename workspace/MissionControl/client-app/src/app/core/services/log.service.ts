import { Injectable, inject } from '@angular/core';
import { ApiService } from './api.service';

export interface LogEntry {
  id: number;
  timestamp: string;
  level: string;
  agentName?: string;
  taskId?: string;
  correlationId?: string;
  action?: string;
  message: string;
  exception?: string;
  source: string;
}

@Injectable({ providedIn: 'root' })
export class LogService {
  private api = inject(ApiService);

  getAll(limit = 200) { return this.api.get<LogEntry[]>(`logs?limit=${limit}`); }
  getByTask(taskId: string, limit = 100) { return this.api.get<LogEntry[]>(`logs/taskId/${taskId}?limit=${limit}`); }
  getByAgent(agentName: string, limit = 100) { return this.api.get<LogEntry[]>(`logs/agent/${agentName}?limit=${limit}`); }
  getByLevel(level: string, limit = 100) { return this.api.get<LogEntry[]>(`logs/level/${level}?limit=${limit}`); }
}
