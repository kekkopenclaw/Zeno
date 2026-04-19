import { Injectable } from '@angular/core';
import { ApiService } from './api.service';
import type { Team, CreateTeam, Agent } from '../models';
import { Observable } from 'rxjs';

@Injectable({ providedIn: 'root' })
export class TeamService {
  constructor(private api: ApiService) {}

  getByProject(projectId: number): Observable<Team[]> {
    return this.api.get<Team[]>(`teams/by-project/${projectId}`);
  }

  getById(id: number): Observable<Team> {
    return this.api.get<Team>(`teams/${id}`);
  }

  create(dto: CreateTeam): Observable<Team> {
    return this.api.post<Team>(`teams`, dto);
  }

  update(id: number, dto: CreateTeam): Observable<Team> {
    return this.api.put<Team>(`teams/${id}`, dto);
  }

  addAgent(teamId: number, agentId: number): Observable<void> {
    return this.api.post<void>(`teams/${teamId}/add-agent`, agentId);
  }

  /** Removes an agent from this team (does not delete the agent). */
  removeAgent(teamId: number, agentId: number): Observable<void> {
    return this.api.post<void>(`teams/${teamId}/remove-agent`, agentId);
  }

  getAgents(teamId: number): Observable<Agent[]> {
    return this.api.get<Agent[]>(`teams/${teamId}/agents`);
  }

  delete(teamId: number): Observable<void> {
    return this.api.delete<void>(`teams/${teamId}`);
  }
}

