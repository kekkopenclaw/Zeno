namespace MissionControl.Domain.Enums;

/// <summary>
/// Strict Kanban state machine: tasks MUST move through states in order.
/// Whis (orchestrator) enforces this — no skipping allowed.
/// </summary>
public enum TaskItemStatus
{
    Todo,           // Not yet picked up
    Decomposition,  // Whis/GrandPriest decomposing big tasks into subtasks
    Orchestration,  // GrandPriest/Whis orchestrating
    Architecture,   // Beerus designing
    Tooling,        // Bulma preparing tools/context
    Coding,         // Kakarot/Vegeta coding
    Refactoring,    // Piccolo refactoring
    Security,       // Cell auditing
    Testing,        // Dende testing
    Review,         // Gohan reviewing
    Compliance,     // Jaco compliance/legal
    Release,        // Shenron releasing
    Memory,         // Trunks memory/learning
    Enforcement,    // Jiren enforcing
    Oversight,      // Zeno oversight
    Done            // Completed and merged
}
