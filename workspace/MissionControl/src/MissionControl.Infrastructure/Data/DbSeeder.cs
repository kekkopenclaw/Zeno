using MissionControl.Domain.Entities;
using MissionControl.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MissionControl.Infrastructure.Data;

public static class DbSeeder
{
    public static async Task SeedAgentsAsync(AppDbContext db)
    {
        if (await db.Agents.AnyAsync()) return;

        var agents = new List<Agent>
        {
            new() { Id = 1, Name = "Whis",    Role = AgentRole.Whis,    Model = "llama3", Description = "Orchestrator — routes tasks, manages state machine, handles retries", Skills = "Orchestration,Routing,Escalation", Emoji = "🌀", ProjectId = 1 },
            new() { Id = 2, Name = "Beerus",  Role = AgentRole.Beerus,  Model = "qwen2.5-coder:14b-instruct-q4_K_M", Description = "Architect — high-level system design and decisions", Skills = "Architecture,SystemDesign,Planning", Emoji = "😼", ProjectId = 1 },
            new() { Id = 3, Name = "Kakarot", Role = AgentRole.Kakarot, Model = "llama3", Description = "Standard coder for moderate complexity tasks", Skills = "Coding,Implementation,Testing", Emoji = "🔥", ProjectId = 1 },
            new() { Id = 4, Name = "Vegeta",  Role = AgentRole.Vegeta,  Model = "qwen2.5-coder:14b-instruct-q4_K_M", Description = "Advanced coder — handles high complexity and critical tasks", Skills = "AdvancedCoding,Performance,Security", Emoji = "⚡", ProjectId = 1 },
            new() { Id = 5, Name = "Piccolo", Role = AgentRole.Piccolo, Model = "llama3", Description = "Refactorer — cleans code, applies SOLID principles", Skills = "Refactoring,CleanCode,SOLID", Emoji = "🌿", ProjectId = 1 },
            new() { Id = 6, Name = "Gohan",   Role = AgentRole.Gohan,   Model = "qwen2.5-coder:14b-instruct-q4_K_M", Description = "Reviewer — inspects output, approves or triggers fix cycle", Skills = "CodeReview,QualityAssurance,Security", Emoji = "📖", ProjectId = 1 },
            new() { Id = 7, Name = "Trunks",  Role = AgentRole.Trunks,  Model = "llama3", Description = "Memory & learning — stores summaries, refines routing weights", Skills = "MemoryManagement,Learning,Analytics", Emoji = "💾", ProjectId = 1 },
            new() { Id = 8, Name = "Bulma",   Role = AgentRole.Bulma,   Model = "qwen2.5-coder:14b-instruct-q4_K_M", Description = "Tooling — manages context injection and prompt construction", Skills = "Tooling,ContextManagement,PromptEngineering", Emoji = "🔧", ProjectId = 1 },
            new() { Id = 9, Name = "Cell",    Role = AgentRole.Cell,    Model = "gpt-4o-mini", Description = "Security Auditor — static/dynamic analysis, vulnerability checks", Skills = "Security,Audit,Analysis", Emoji = "🦗", ProjectId = 1 },
            new() { Id = 10, Name = "Dende",   Role = AgentRole.Dende,   Model = "gpt-4o-mini", Description = "Test Engineer — automated/unit/integration/fuzz testing", Skills = "Testing,QA,Fuzzing", Emoji = "🧑‍🦲", ProjectId = 1 },
            new() { Id = 11, Name = "Shenron", Role = AgentRole.Shenron, Model = "gpt-4o-mini", Description = "Release Manager — deployment, monitoring, rollback", Skills = "Release,Deployment,Monitoring", Emoji = "🐉", ProjectId = 1 },
            new() { Id = 12, Name = "Jaco",    Role = AgentRole.Jaco,    Model = "gpt-4o-mini", Description = "Compliance/Legal — licensing, privacy, regulatory checks", Skills = "Compliance,Legal,Privacy", Emoji = "👽", ProjectId = 1 },
            new() { Id = 13, Name = "Zeno",    Role = AgentRole.Zeno,    Model = "gpt-4o", Description = "Supreme Overseer — self-improvement, process optimization, ultimate authority", Skills = "Oversight,Optimization,Authority", Emoji = "👑", ProjectId = 1 },
            new() { Id = 14, Name = "Jiren",   Role = AgentRole.Jiren,   Model = "gpt-4o", Description = "Enforcer — ultimate strength, handles escalations, last-resort interventions", Skills = "Enforcement,Escalation,Intervention", Emoji = "💪", ProjectId = 1 },
            new() { Id = 15, Name = "GrandPriest", Role = AgentRole.GrandPriest, Model = "gpt-4o", Description = "Grand Priest — father of Whis, supreme angel, oversees all angels and orchestrators", Skills = "Oversight,Orchestration,Leadership", Emoji = "🧙‍♂️", ProjectId = 1 },
        };

        db.Agents.AddRange(agents);
        await db.SaveChangesAsync();
    }
}
