import { Injectable } from '@angular/core';
import { ApiService } from './api.service';
import type { Team, Agent } from '../models';
import { Observable } from 'rxjs';

@Injectable({ providedIn: 'root' })
export class TeamService {
  constructor(private api: ApiService) {}

  getByProject(projectId: number): Observable<Team[]> {
    return this.api.get<Team[]>(`teams/by-project/${projectId}`);
  }

  create(dto: Partial<Team>): Observable<Team> {
    return this.api.post<Team>(`teams`, dto);
  }

  addAgent(teamId: number, agentId: number): Observable<void> {
    return this.api.post<void>(`teams/${teamId}/add-agent`, agentId);
  }

  removeAgentFromTeam(teamId: number, agentId: number): Observable<void> {
    return this.api.post<void>(`teams/${teamId}/remove-agent`, agentId);
  }

  getAgents(teamId: number): Observable<Agent[]> {
    return this.api.get<Agent[]>(`teams/${teamId}/agents`);
  }

  delete(teamId: number): Observable<void> {
    return this.api.delete<void>(`teams/${teamId}`);
  }
}
