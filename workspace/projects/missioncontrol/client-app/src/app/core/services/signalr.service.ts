import { Injectable, signal, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import * as signalR from '@microsoft/signalr';
import { environment } from '../../../environments/environment';
import type { ActivityLog, TaskItem, MemoryEntry, Agent } from '../models';

/** Payload emitted by PipelineTestProgress hub event */
export interface PipelineTestStep {
  step: string;
  message: string;
  correlationId: string;
  projectId: number;
  extra?: Record<string, unknown>;
}

@Injectable({ providedIn: 'root' })
/**
 * SignalRService
 *
 * Handles real-time connectivity to backend via SignalR and auto-fallback to HTTP polling.
 * - Subscribes to 'live' agent/task/memory/log events via SignalR hub if possible
 * - Uses built-in SignalR automatic reconnect
 * - If (and only if) fully disconnected, falls back to HTTP polling
 * - Keeps activity feed and application state in sync for all users
 */
export class SignalRService {
  private hub!: signalR.HubConnection;
  private http = inject(HttpClient);
  private pollingTimer?: ReturnType<typeof setInterval>;

  activityFeed = signal<ActivityLog[]>([]);
  lastTaskUpdate = signal<TaskItem | null>(null);
  lastMemoryAdded = signal<MemoryEntry | null>(null);
  lastAgentStarted = signal<Agent | null>(null);
  lastAgentUpdated = signal<Agent | null>(null);
  agentLogLines = signal<{agentId: string; line: string} | null>(null);

  /** true when the WebSocket hub connection is active */
  isConnected = signal(false);

  /**
   * Increments each polling cycle when SignalR is unavailable.
   * Components can use toObservable(pollingTick).pipe(skip(1)) to trigger HTTP reload.
   */
  pollingTick = signal(0);

  /** Result of a TestConnection() hub round-trip */
  connectionTestResult = signal<{ok: boolean; time: string; connectionId: string} | null>(null);

  /** Live steps broadcast by PipelineTestController */
  pipelineTestSteps = signal<PipelineTestStep[]>([]);

  /**
   * Attempt to connect to backend SignalR hub and subscribe to all live events.
   * Automatically uses WebSocket if possible. Fallbacks to HTTP polling only if all automatic reconnects fail.
   */
  startConnection(): void {
    this.hub = new signalR.HubConnectionBuilder()
      .withUrl(environment.hubUrl, {
        withCredentials: true,
      })
      .withAutomaticReconnect()
      .configureLogging(signalR.LogLevel.Warning)
      .build();

    // Live event: backend says a new log was created (push to activityFeed)
    this.hub.on('LogCreated', (log: ActivityLog) => {
      this.activityFeed.update(f => [log, ...f].slice(0, 50));
    });
    // Live event: backend says agent activity changed (push to activityFeed)
    this.hub.on('AgentActivityUpdated', (log: ActivityLog) => {
      this.activityFeed.update(f => [log, ...f].slice(0, 50));
    });
    // Live event: backend says a task has been updated (one-task update flow)
    this.hub.on('TaskUpdated', (task: TaskItem) => {
      this.lastTaskUpdate.set(task);
    });
    // Live event: new memory entry added
    this.hub.on('MemoryAdded', (mem: MemoryEntry) => {
      this.lastMemoryAdded.set(mem);
    });
    // Live event: agent was started
    this.hub.on('AgentStarted', (agent: Agent) => {
      this.lastAgentStarted.set(agent);
    });
    // Live event: agent properties updated (from edit/update operations)
    this.hub.on('AgentUpdated', (agent: Agent) => {
      this.lastAgentUpdated.set(agent);
      this.lastAgentStarted.set(agent); // also trigger agent list refresh
    });
    // Live event: new log line from agent output/stream
    this.hub.on('AgentLogLine', (data: {agentId: string; line: string}) => {
      this.agentLogLines.set(data);
    });

    // Round-trip test connection event response
    this.hub.on('ConnectionTestResult', (data: {ok: boolean; time: string; connectionId: string}) => {
      this.connectionTestResult.set(data);
    });

    // Live event: pipeline test step broadcast for memory tab/log feed
    this.hub.on('PipelineTestProgress', (step: PipelineTestStep) => {
      // Append in chronological order; cap at 100 entries
      this.pipelineTestSteps.update(s => [...s, step].slice(-100));
      // Also push to main activity feed for visibility
      this.activityFeed.update(f => [{
        id: Date.now(),
        agentId: undefined,
        projectId: step.projectId,
        message: `[PipelineTest] ${step.step}: ${step.message}`,
        timestamp: new Date().toISOString(),
      } as ActivityLog, ...f].slice(0, 50));
    });

    // If SignalR builtin reconnect succeeds after a disconnect, stop polling and consider full live {}
    this.hub.onreconnected(() => {
      this.isConnected.set(true);
      this.stopPolling();
    });

    // If SignalR disconnects and does not recover after all internal retries, only then fallback to HTTP polling
    this.hub.onclose(() => {
      this.isConnected.set(false);
      this.startPolling();
    });

    // Initial connect attempt. If fails, fallback to polling (will retry SignalR at next reload).
    this.hub.start()
      .then(() => {
        this.isConnected.set(true);
        this.stopPolling();
      })
      .catch(() => {
        this.startPolling();
      });
  }

  /** Invoke TestConnection() on the hub — result arrives via connectionTestResult signal */
  testConnection(): void {
    if (this.hub?.state === signalR.HubConnectionState.Connected) {
      this.hub.invoke('TestConnection').catch(err =>
        console.warn('TestConnection failed:', err));
    }
  }

  /** Start polling the REST API when SignalR is unavailable */
  private startPolling(): void {
    this.stopPolling();
    this.pollNow();
    this.pollingTimer = setInterval(() => this.pollNow(), 3000);
  }

  private stopPolling(): void {
    if (this.pollingTimer) {
      clearInterval(this.pollingTimer);
      this.pollingTimer = undefined;
    }
  }

  private pollNow(): void {
    this.http
      .get<ActivityLog[]>(`${environment.apiUrl}/activitylogs/project/1?limit=20`)
      .subscribe({
        next: logs => {
          if (logs.length > 0) {
            const sorted = [...logs].sort(
              (a, b) => new Date(b.timestamp).getTime() - new Date(a.timestamp).getTime(),
            );
            this.activityFeed.set(sorted.slice(0, 50));
          }
          this.pollingTick.update(n => n + 1);
        },
        error: () => {
          this.pollingTick.update(n => n + 1);
        },
      });
  }

  stopConnection(): void {
    this.stopPolling();
    this.hub?.stop();
  }
}
