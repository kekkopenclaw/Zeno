namespace MissionControl.Domain.Enums;

/// <summary>
/// Dragon Ball Z–named agent specializations.
/// Each agent has a single clear responsibility — no overlapping roles.
/// </summary>
public enum AgentRole
{
    /// <summary>Orchestrator — picks tasks, routes to agents, handles retries/escalation</summary>
    Whis,

    /// <summary>Architect — high-level design, system design decisions</summary>
    Beerus,

    /// <summary>Standard coder for moderate complexity tasks</summary>
    Kakarot,

    /// <summary>Advanced coder for high-complexity / critical tasks</summary>
    Vegeta,

    /// <summary>Refactorer — cleans up, improves structure after coding</summary>
    Piccolo,

    /// <summary>Reviewer — inspects output and approves or rejects</summary>
    Gohan,

    /// <summary>Memory &amp; learning — stores summaries, updates routing weights</summary>
    Trunks,

    /// <summary>Tooling — manages standards, injects context, prompt construction</summary>
    Bulma,

    /// <summary>Security Auditor — static/dynamic analysis, vulnerability checks</summary>
    Cell,

    /// <summary>Test Engineer — automated/unit/integration/fuzz testing</summary>
    Dende,

    /// <summary>Release Manager — deployment, monitoring, rollback</summary>
    Shenron,

    /// <summary>Compliance/Legal — licensing, privacy, regulatory checks</summary>
    Jaco,

    /// <summary>Supreme Overseer — self-improvement, process optimization, ultimate authority</summary>
    Zeno,

    /// <summary>Enforcer — ultimate strength, handles escalations, last-resort interventions</summary>
    Jiren,

    /// <summary>Grand Priest — father of Whis, supreme angel, oversees all angels and orchestrators</summary>
    GrandPriest
}
