export interface Project {
  id: number;
  name: string;
  description: string;
  createdAt: string;
}

export interface Agent {
  id: number;
  name: string;
  model: string;
  status: string; // 'Idle' | 'Working' | 'Thinking' | 'Paused'
  role: string;   // 'Whis' | 'Beerus' | 'Kakarot' | 'Vegeta' | 'Piccolo' | 'Gohan' | 'Trunks' | 'Bulma'
  description: string;
  skills: string;
  emoji: string;
  openClawAgentId?: string;
  isPaused?: boolean;
  projectId: number;
  color?: string;
  /** "Ollama" | "OpenClaw" */
  executionBackend: string;
  /** Whether this agent has tool/plugin/function-call support (OpenClaw only). */
  toolsEnabled: boolean;
  /** Whether this agent can review, deploy, or publish. */
  pushRole: boolean;
  /** Human-readable label, e.g. "llama3 via Ollama" or "qwen2.5-coder:14b-instruct-q4_K_M via OpenClaw". */
  backendLabel: string;
}

export interface TaskItem {
  id: number;
  title: string;
  description: string;
  status: string;
  priority: string;
  assignedAgentId?: number;
  assignedAgentName?: string;
  assignedAgentEmoji?: string;
  projectId: number;
  createdAt: string;
  updatedAt?: string;
  statusEnteredAt?: string;
  retryCount: number;
  reviewFailCount: number;
  reviewNotes?: string;
  complexityScore: number;
}

export interface MemoryEntry {
  id: number;
  projectId: number;
  title: string;
  content: string;
  type: string; // 'LongTerm' | 'Insight' | 'Decision'
  tags: string;
  createdAt: string;
  agentId?: number;
}

export interface MemorySummary {
  id: number;
  projectId: number;
  taskItemId?: number;
  problem: string;
  fix: string;
  lesson: string;
  agentRole: string;
  retriesRequired: number;
  complexityScore: number;
  createdAt: string;
}

export interface ActivityLog {
  id: number;
  agentId?: number;
  agentName?: string;
  agentEmoji?: string;
  projectId: number;
  message: string;
  timestamp: string;
}

export interface CreateProject { name: string; description: string; }
export interface CreateAgent extends Partial<Agent> {
  name: string;
  model: string;
  role: string;
  projectId: number;
  teamId?: number;
  color?: string;
}
export interface CreateTask    { title: string; description: string; projectId: number; priority: string; assignedAgentId?: number; complexityScore?: number; }
export interface CreateMemory  { projectId: number; title: string; content: string; type: string; tags: string; }
export interface UpdateTaskStatus { status: string; reviewNotes?: string; }

export interface Team {
  id: number;
  name: string;
  description: string;
  projectId: number;
}
