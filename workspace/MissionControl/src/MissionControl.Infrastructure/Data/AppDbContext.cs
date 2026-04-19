using Microsoft.EntityFrameworkCore;
using MissionControl.Domain.Entities;
using MissionControl.Domain.Enums;

namespace MissionControl.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
        // Ensure WAL mode and busy timeout are set once per connection
        if (Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite")
        {
            // Ensure parent directory for SQLite DB exists
            var dbPath = Database.GetDbConnection().DataSource;
            var parentDir = System.IO.Path.GetDirectoryName(dbPath);
            if (!string.IsNullOrEmpty(parentDir))
                System.IO.Directory.CreateDirectory(parentDir);
            Database.OpenConnection();
            Database.ExecuteSqlRaw("PRAGMA journal_mode=WAL;");
            Database.ExecuteSqlRaw("PRAGMA busy_timeout=10000;"); // 10s for extra safety
        }
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await base.SaveChangesAsync(cancellationToken);
    }

    public DbSet<Project> Projects => Set<Project>();
    public DbSet<Agent> Agents => Set<Agent>();
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<TaskItem> Tasks => Set<TaskItem>();
    public DbSet<ActivityLog> ActivityLogs => Set<ActivityLog>();
    public DbSet<LogEntry> Logs => Set<LogEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<Team>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.HasOne(e => e.Project)
                .WithMany(p => p.Teams)
                .HasForeignKey(e => e.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Project>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(1000);
        });

        modelBuilder.Entity<Agent>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Model).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.Skills).HasMaxLength(500);
            entity.Property(e => e.Emoji).HasMaxLength(10);
            entity.Property(e => e.ExecutionBackend).HasConversion<string>().HasMaxLength(20);
            entity.HasOne(e => e.Project)
                .WithMany(p => p.Agents)
                .HasForeignKey(e => e.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Team)
                .WithMany(t => t.Agents)
                .HasForeignKey(e => e.TeamId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<TaskItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(300);
            entity.Property(e => e.ReviewNotes).HasMaxLength(1000);
            entity.HasOne(e => e.Project)
                .WithMany(p => p.Tasks)
                .HasForeignKey(e => e.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.AssignedAgent)
                .WithMany(a => a.Tasks)
                .HasForeignKey(e => e.AssignedAgentId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<ActivityLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.Project)
                .WithMany(p => p.ActivityLogs)
                .HasForeignKey(e => e.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Agent)
                .WithMany(a => a.ActivityLogs)
                .HasForeignKey(e => e.AgentId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<LogEntry>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Level).HasMaxLength(20);
            entity.Property(e => e.AgentName).HasMaxLength(100);
            entity.Property(e => e.TaskId).HasMaxLength(100);
            entity.Property(e => e.CorrelationId).HasMaxLength(100);
            entity.Property(e => e.Action).HasMaxLength(200);
            entity.Property(e => e.Source).HasMaxLength(50);
            entity.HasIndex(e => e.AgentName);
            entity.HasIndex(e => e.TaskId);
            entity.HasIndex(e => e.Level);
            entity.HasIndex(e => e.Timestamp);
        });

        // ── Seed ────────────────────────────────────────────────────────────────
        var epoch = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        modelBuilder.Entity<Project>().HasData(new Project
        {
            Id = 1,
            Name = "Alpha Project",
            Description = "The primary autonomous AI engineering lab",
            CreatedAt = epoch
        });

        modelBuilder.Entity<Agent>().HasData(
            new Agent { Id = 1, Name = "Whis",    Model = "llama3",                              ExecutionBackend = ExecutionBackend.OpenClaw,    ToolsEnabled = false, PushRole = false, Role = AgentRole.Whis,     Status = AgentStatus.Idle, Emoji = "🌀", Description = "Orchestrator — routes tasks, manages state machine, handles retries", Skills = "Orchestration,Routing,Escalation",                ProjectId = 1 },
            new Agent { Id = 2, Name = "Beerus",  Model = "qwen2.5-coder:14b-instruct-q4_K_M",  ExecutionBackend = ExecutionBackend.OpenClaw,  ToolsEnabled = true,  PushRole = false, Role = AgentRole.Beerus,   Status = AgentStatus.Idle, Emoji = "😼", Description = "Architect — high-level system design and decisions",                   Skills = "Architecture,SystemDesign,Planning",             ProjectId = 1 },
            new Agent { Id = 3, Name = "Kakarot", Model = "llama3",                              ExecutionBackend = ExecutionBackend.OpenClaw,    ToolsEnabled = false, PushRole = false, Role = AgentRole.Kakarot,  Status = AgentStatus.Idle, Emoji = "🔥", Description = "Standard coder for moderate complexity tasks",                         Skills = "Coding,Implementation,Testing",                  ProjectId = 1 },
            new Agent { Id = 4, Name = "Vegeta",  Model = "qwen2.5-coder:14b-instruct-q4_K_M",  ExecutionBackend = ExecutionBackend.OpenClaw,  ToolsEnabled = true,  PushRole = true,  Role = AgentRole.Vegeta,   Status = AgentStatus.Idle, Emoji = "⚡", Description = "Advanced coder — handles high complexity and critical tasks",          Skills = "AdvancedCoding,Performance,Security",            ProjectId = 1 },
            new Agent { Id = 5, Name = "Piccolo", Model = "llama3",                              ExecutionBackend = ExecutionBackend.OpenClaw,    ToolsEnabled = false, PushRole = false, Role = AgentRole.Piccolo,  Status = AgentStatus.Idle, Emoji = "🌿", Description = "Refactorer — cleans code, applies SOLID principles",                   Skills = "Refactoring,CleanCode,SOLID",                    ProjectId = 1 },
            new Agent { Id = 6, Name = "Gohan",   Model = "qwen2.5-coder:14b-instruct-q4_K_M",  ExecutionBackend = ExecutionBackend.OpenClaw,  ToolsEnabled = true,  PushRole = true,  Role = AgentRole.Gohan,    Status = AgentStatus.Idle, Emoji = "📖", Description = "Reviewer — inspects output, approves or triggers fix cycle",           Skills = "CodeReview,QualityAssurance,Security",           ProjectId = 1 },
            new Agent { Id = 7, Name = "Trunks",  Model = "llama3",                              ExecutionBackend = ExecutionBackend.OpenClaw,    ToolsEnabled = false, PushRole = false, Role = AgentRole.Trunks,   Status = AgentStatus.Idle, Emoji = "💾", Description = "Memory & learning — stores summaries, refines routing weights",        Skills = "MemoryManagement,Learning,Analytics",            ProjectId = 1 },
            new Agent { Id = 8, Name = "Bulma",   Model = "qwen2.5-coder:14b-instruct-q4_K_M",  ExecutionBackend = ExecutionBackend.OpenClaw,  ToolsEnabled = true,  PushRole = true,  Role = AgentRole.Bulma,    Status = AgentStatus.Idle, Emoji = "🔧", Description = "Tooling — manages context injection and prompt construction",          Skills = "Tooling,ContextManagement,PromptEngineering",    ProjectId = 1 },
            new Agent { Id = 9, Name = "Cell",    Model = "gpt-4o-mini",                        ExecutionBackend = ExecutionBackend.OpenClaw,    ToolsEnabled = false, PushRole = false, Role = AgentRole.Cell,     Status = AgentStatus.Idle, Emoji = "🦗", Description = "Security Auditor — static/dynamic analysis, vulnerability checks",      Skills = "Security,Audit,Analysis",                        ProjectId = 1 },
            new Agent { Id = 10, Name = "Dende",  Model = "gpt-4o-mini",                        ExecutionBackend = ExecutionBackend.OpenClaw,    ToolsEnabled = false, PushRole = false, Role = AgentRole.Dende,    Status = AgentStatus.Idle, Emoji = "🧑‍🦲", Description = "Test Engineer — automated/unit/integration/fuzz testing",            Skills = "Testing,QA,Fuzzing",                              ProjectId = 1 },
            new Agent { Id = 11, Name = "Shenron", Model = "gpt-4o-mini",                        ExecutionBackend = ExecutionBackend.OpenClaw,    ToolsEnabled = false, PushRole = false, Role = AgentRole.Shenron,  Status = AgentStatus.Idle, Emoji = "🐉", Description = "Release Manager — deployment, monitoring, rollback",                   Skills = "Release,Deployment,Monitoring",                   ProjectId = 1 },
            new Agent { Id = 12, Name = "Jaco",    Model = "gpt-4o-mini",                        ExecutionBackend = ExecutionBackend.OpenClaw,    ToolsEnabled = false, PushRole = false, Role = AgentRole.Jaco,     Status = AgentStatus.Idle, Emoji = "👽", Description = "Compliance/Legal — licensing, privacy, regulatory checks",              Skills = "Compliance,Legal,Privacy",                        ProjectId = 1 },
            new Agent { Id = 13, Name = "Zeno",    Model = "gpt-4o",                             ExecutionBackend = ExecutionBackend.OpenClaw,    ToolsEnabled = false, PushRole = false, Role = AgentRole.Zeno,     Status = AgentStatus.Idle, Emoji = "👑", Description = "Supreme Overseer — self-improvement, process optimization, ultimate authority", Skills = "Oversight,Optimization,Authority", ProjectId = 1 },
            new Agent { Id = 14, Name = "Jiren",   Model = "gpt-4o",                             ExecutionBackend = ExecutionBackend.OpenClaw,    ToolsEnabled = false, PushRole = false, Role = AgentRole.Jiren,    Status = AgentStatus.Idle, Emoji = "💪", Description = "Enforcer — ultimate strength, handles escalations, last-resort interventions", Skills = "Enforcement,Escalation,Intervention", ProjectId = 1 },
            new Agent { Id = 15, Name = "GrandPriest", Model = "gpt-4o",                        ExecutionBackend = ExecutionBackend.OpenClaw,    ToolsEnabled = false, PushRole = false, Role = AgentRole.GrandPriest, Status = AgentStatus.Idle, Emoji = "🧙‍♂️", Description = "Grand Priest — father of Whis, supreme angel, oversees all angels and orchestrators", Skills = "Oversight,Orchestration,Leadership", ProjectId = 1 }
        );

        modelBuilder.Entity<TaskItem>().HasData(
            new TaskItem { Id = 1, Title = "Design autonomous agent communication protocol", Description = "Architect the event-driven messaging layer between agents", Status = TaskItemStatus.Done,    Priority = TaskPriority.Critical, AssignedAgentId = 2, ProjectId = 1, CreatedAt = epoch, UpdatedAt = epoch.AddDays(1), ComplexityScore = 9 },
            new TaskItem { Id = 2, Title = "Implement OrchestratorService (Whis)",          Description = "State-machine driven task routing and agent assignment",         Status = TaskItemStatus.Coding,  Priority = TaskPriority.Critical, AssignedAgentId = 3, ProjectId = 1, CreatedAt = epoch.AddDays(1), ComplexityScore = 8 },
            new TaskItem { Id = 3, Title = "Refactor memory to structured summaries",       Description = "Remove full conversation storage, use JSON summaries only",       Status = TaskItemStatus.Review,  Priority = TaskPriority.High,     AssignedAgentId = 5, ProjectId = 1, CreatedAt = epoch.AddDays(2), ComplexityScore = 5 },
            new TaskItem { Id = 4, Title = "Build Angular dashboard real-time feed",        Description = "SignalR-connected live agent activity feed",                       Status = TaskItemStatus.Todo,    Priority = TaskPriority.High,     ProjectId = 1,       CreatedAt = epoch.AddDays(3), ComplexityScore = 4 },
            new TaskItem { Id = 5, Title = "Add context caching to ContextLoader",         Description = "Cache SOUL and AGENTS context to avoid prompt bloat",             Status = TaskItemStatus.Tooling,   Priority = TaskPriority.Medium,   AssignedAgentId = 8, ProjectId = 1, CreatedAt = epoch.AddDays(4), ComplexityScore = 3 },
            new TaskItem { Id = 6, Title = "Implement learning service (Trunks)",          Description = "Analyze past summaries and refine routing weights",               Status = TaskItemStatus.Architecture, Priority = TaskPriority.Medium,   ProjectId = 1,       CreatedAt = epoch.AddDays(5), ComplexityScore = 7 }
        );

        modelBuilder.Entity<ActivityLog>().HasData(
            new ActivityLog { Id = 1, ProjectId = 1, AgentId = 1, Message = "🌀 Whis initialized — autonomous orchestration active",              Timestamp = epoch },
            new ActivityLog { Id = 2, ProjectId = 1, AgentId = 2, Message = "😼 Beerus completed architecture design for agent communication",   Timestamp = epoch.AddHours(2) },
            new ActivityLog { Id = 3, ProjectId = 1, AgentId = 3, Message = "🔥 Kakarot started coding OrchestratorService",                     Timestamp = epoch.AddHours(4) },
            new ActivityLog { Id = 4, ProjectId = 1, AgentId = 7, Message = "💾 Trunks stored architecture decision to long-term memory",        Timestamp = epoch.AddHours(6) },
            new ActivityLog { Id = 5, ProjectId = 1, AgentId = 5, Message = "🌿 Piccolo refactored memory system to structured summaries",       Timestamp = epoch.AddHours(8) },
            new ActivityLog { Id = 6, ProjectId = 1, AgentId = 6, Message = "📖 Gohan reviewing memory refactor — checking structured output",   Timestamp = epoch.AddHours(10) }
        );
    }
}
