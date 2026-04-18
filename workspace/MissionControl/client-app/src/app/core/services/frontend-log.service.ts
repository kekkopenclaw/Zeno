import { Injectable, inject } from '@angular/core';
import { ApiService } from '../services/api.service';

export interface FrontendLogDto {
  level: string;
  message: string;
  correlationId?: string;
  url?: string;
  stackTrace?: string;
  timestamp: string;
}

@Injectable({ providedIn: 'root' })
export class FrontendLogService {
  private api = inject(ApiService);

  send(dto: FrontendLogDto) {
    return this.api.post<void>('logs/frontend', dto);
  }

  error(message: string, stackTrace?: string, url?: string) {
    const dto: FrontendLogDto = {
      level: 'Error',
      message,
      stackTrace,
      url: url ?? window.location.href,
      timestamp: new Date().toISOString(),
    };
    this.send(dto).subscribe({ error: () => {} });
  }

  warn(message: string) {
    const dto: FrontendLogDto = {
      level: 'Warning',
      message,
      url: window.location.href,
      timestamp: new Date().toISOString(),
    };
    this.send(dto).subscribe({ error: () => {} });
  }
}
