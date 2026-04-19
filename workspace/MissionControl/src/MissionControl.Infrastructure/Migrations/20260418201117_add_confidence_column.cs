using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace MissionControl.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class add_confidence_column : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Logs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Level = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    AgentName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    TaskId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    CorrelationId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Action = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Message = table.Column<string>(type: "TEXT", nullable: false),
                    Exception = table.Column<string>(type: "TEXT", nullable: true),
                    Source = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Logs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Projects",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Projects", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MemorySummaries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProjectId = table.Column<int>(type: "INTEGER", nullable: false),
                    TaskItemId = table.Column<int>(type: "INTEGER", nullable: true),
                    Problem = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    Fix = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    Lesson = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    AgentRole = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    RetriesRequired = table.Column<int>(type: "INTEGER", nullable: false),
                    ComplexityScore = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MemorySummaries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MemorySummaries_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Teams",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    ProjectId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Teams", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Teams_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Agents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Model = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    Role = table.Column<int>(type: "INTEGER", nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Skills = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Emoji = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    Color = table.Column<string>(type: "TEXT", nullable: true),
                    OpenClawAgentId = table.Column<string>(type: "TEXT", nullable: true),
                    IsPaused = table.Column<bool>(type: "INTEGER", nullable: false),
                    ProjectId = table.Column<int>(type: "INTEGER", nullable: false),
                    ExecutionBackend = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    ToolsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    PushRole = table.Column<bool>(type: "INTEGER", nullable: false),
                    TeamId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Agents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Agents_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Agents_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ActivityLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AgentId = table.Column<int>(type: "INTEGER", nullable: true),
                    ProjectId = table.Column<int>(type: "INTEGER", nullable: false),
                    Message = table.Column<string>(type: "TEXT", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActivityLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ActivityLogs_Agents_AgentId",
                        column: x => x.AgentId,
                        principalTable: "Agents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ActivityLogs_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MemoryEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProjectId = table.Column<int>(type: "INTEGER", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    Content = table.Column<string>(type: "TEXT", nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    Tags = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    AgentId = table.Column<int>(type: "INTEGER", nullable: true),
                    Confidence = table.Column<double>(type: "REAL", nullable: false, defaultValue: 0.5),
                    UsageCount = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    SuccessRate = table.Column<double>(type: "REAL", nullable: false, defaultValue: 0.5),
                    LastUsed = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsRule = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MemoryEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MemoryEntries_Agents_AgentId",
                        column: x => x.AgentId,
                        principalTable: "Agents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_MemoryEntries_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Tasks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Title = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    Priority = table.Column<int>(type: "INTEGER", nullable: false),
                    AssignedAgentId = table.Column<int>(type: "INTEGER", nullable: true),
                    ProjectId = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    StatusEnteredAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    RetryCount = table.Column<int>(type: "INTEGER", nullable: false),
                    ReviewFailCount = table.Column<int>(type: "INTEGER", nullable: false),
                    ReviewNotes = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    ComplexityScore = table.Column<int>(type: "INTEGER", nullable: false),
                    ParentTaskId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Tasks_Agents_AssignedAgentId",
                        column: x => x.AssignedAgentId,
                        principalTable: "Agents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Tasks_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Tasks_Tasks_ParentTaskId",
                        column: x => x.ParentTaskId,
                        principalTable: "Tasks",
                        principalColumn: "Id");
                });

            migrationBuilder.InsertData(
                table: "Projects",
                columns: new[] { "Id", "CreatedAt", "Description", "Name" },
                values: new object[] { 1, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "The primary autonomous AI engineering lab", "Alpha Project" });

            migrationBuilder.InsertData(
                table: "Agents",
                columns: new[] { "Id", "Color", "Description", "Emoji", "ExecutionBackend", "IsPaused", "Model", "Name", "OpenClawAgentId", "ProjectId", "PushRole", "Role", "Skills", "Status", "TeamId", "ToolsEnabled" },
                values: new object[,]
                {
                    { 1, null, "Orchestrator — routes tasks, manages state machine, handles retries", "🌀", "OpenClaw", false, "llama3", "Whis", null, 1, false, 0, "Orchestration,Routing,Escalation", 0, null, false },
                    { 2, null, "Architect — high-level system design and decisions", "😼", "OpenClaw", false, "qwen2.5-coder:14b-instruct-q4_K_M", "Beerus", null, 1, false, 1, "Architecture,SystemDesign,Planning", 0, null, true },
                    { 3, null, "Standard coder for moderate complexity tasks", "🔥", "OpenClaw", false, "llama3", "Kakarot", null, 1, false, 2, "Coding,Implementation,Testing", 0, null, false },
                    { 4, null, "Advanced coder — handles high complexity and critical tasks", "⚡", "OpenClaw", false, "qwen2.5-coder:14b-instruct-q4_K_M", "Vegeta", null, 1, true, 3, "AdvancedCoding,Performance,Security", 0, null, true },
                    { 5, null, "Refactorer — cleans code, applies SOLID principles", "🌿", "OpenClaw", false, "llama3", "Piccolo", null, 1, false, 4, "Refactoring,CleanCode,SOLID", 0, null, false },
                    { 6, null, "Reviewer — inspects output, approves or triggers fix cycle", "📖", "OpenClaw", false, "qwen2.5-coder:14b-instruct-q4_K_M", "Gohan", null, 1, true, 5, "CodeReview,QualityAssurance,Security", 0, null, true },
                    { 7, null, "Memory & learning — stores summaries, refines routing weights", "💾", "OpenClaw", false, "llama3", "Trunks", null, 1, false, 6, "MemoryManagement,Learning,Analytics", 0, null, false },
                    { 8, null, "Tooling — manages context injection and prompt construction", "🔧", "OpenClaw", false, "qwen2.5-coder:14b-instruct-q4_K_M", "Bulma", null, 1, true, 7, "Tooling,ContextManagement,PromptEngineering", 0, null, true },
                    { 9, null, "Security Auditor — static/dynamic analysis, vulnerability checks", "🦗", "OpenClaw", false, "gpt-4o-mini", "Cell", null, 1, false, 8, "Security,Audit,Analysis", 0, null, false },
                    { 10, null, "Test Engineer — automated/unit/integration/fuzz testing", "🧑‍🦲", "OpenClaw", false, "gpt-4o-mini", "Dende", null, 1, false, 9, "Testing,QA,Fuzzing", 0, null, false },
                    { 11, null, "Release Manager — deployment, monitoring, rollback", "🐉", "OpenClaw", false, "gpt-4o-mini", "Shenron", null, 1, false, 10, "Release,Deployment,Monitoring", 0, null, false },
                    { 12, null, "Compliance/Legal — licensing, privacy, regulatory checks", "👽", "OpenClaw", false, "gpt-4o-mini", "Jaco", null, 1, false, 11, "Compliance,Legal,Privacy", 0, null, false },
                    { 13, null, "Supreme Overseer — self-improvement, process optimization, ultimate authority", "👑", "OpenClaw", false, "gpt-4o", "Zeno", null, 1, false, 12, "Oversight,Optimization,Authority", 0, null, false },
                    { 14, null, "Enforcer — ultimate strength, handles escalations, last-resort interventions", "💪", "OpenClaw", false, "gpt-4o", "Jiren", null, 1, false, 13, "Enforcement,Escalation,Intervention", 0, null, false },
                    { 15, null, "Grand Priest — father of Whis, supreme angel, oversees all angels and orchestrators", "🧙‍♂️", "OpenClaw", false, "gpt-4o", "GrandPriest", null, 1, false, 14, "Oversight,Orchestration,Leadership", 0, null, false }
                });

            migrationBuilder.InsertData(
                table: "MemoryEntries",
                columns: new[] { "Id", "AgentId", "Confidence", "Content", "CreatedAt", "LastUsed", "ProjectId", "SuccessRate", "Tags", "Title", "Type" },
                values: new object[] { 1, null, 0.5, "We will use strict state-machine driven execution. Only status transitions trigger agent actions. No polling loops.", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 1, 0.5, "architecture,state-machine,agents", "Architecture Decision: Event-Driven Agents", 2 });

            migrationBuilder.InsertData(
                table: "MemorySummaries",
                columns: new[] { "Id", "AgentRole", "ComplexityScore", "CreatedAt", "Fix", "Lesson", "Problem", "ProjectId", "RetriesRequired", "TaskItemId" },
                values: new object[] { 1, "Beerus", 9, new DateTime(2026, 1, 2, 0, 0, 0, 0, DateTimeKind.Utc), "Implemented SignalR hub with typed methods per event type", "Event-driven beats polling — always push, never pull", "Agent communication was polling-based and inefficient", 1, 0, 1 });

            migrationBuilder.InsertData(
                table: "Tasks",
                columns: new[] { "Id", "AssignedAgentId", "ComplexityScore", "CreatedAt", "Description", "ParentTaskId", "Priority", "ProjectId", "RetryCount", "ReviewFailCount", "ReviewNotes", "Status", "StatusEnteredAt", "Title", "UpdatedAt" },
                values: new object[,]
                {
                    { 4, null, 4, new DateTime(2026, 1, 4, 0, 0, 0, 0, DateTimeKind.Utc), "SignalR-connected live agent activity feed", null, 2, 1, 0, 0, null, 0, null, "Build Angular dashboard real-time feed", null },
                    { 6, null, 7, new DateTime(2026, 1, 6, 0, 0, 0, 0, DateTimeKind.Utc), "Analyze past summaries and refine routing weights", null, 1, 1, 0, 0, null, 3, null, "Implement learning service (Trunks)", null }
                });

            migrationBuilder.InsertData(
                table: "ActivityLogs",
                columns: new[] { "Id", "AgentId", "Message", "ProjectId", "Timestamp" },
                values: new object[,]
                {
                    { 1, 1, "🌀 Whis initialized — autonomous orchestration active", 1, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 2, 2, "😼 Beerus completed architecture design for agent communication", 1, new DateTime(2026, 1, 1, 2, 0, 0, 0, DateTimeKind.Utc) },
                    { 3, 3, "🔥 Kakarot started coding OrchestratorService", 1, new DateTime(2026, 1, 1, 4, 0, 0, 0, DateTimeKind.Utc) },
                    { 4, 7, "💾 Trunks stored architecture decision to long-term memory", 1, new DateTime(2026, 1, 1, 6, 0, 0, 0, DateTimeKind.Utc) },
                    { 5, 5, "🌿 Piccolo refactored memory system to structured summaries", 1, new DateTime(2026, 1, 1, 8, 0, 0, 0, DateTimeKind.Utc) },
                    { 6, 6, "📖 Gohan reviewing memory refactor — checking structured output", 1, new DateTime(2026, 1, 1, 10, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.InsertData(
                table: "MemoryEntries",
                columns: new[] { "Id", "AgentId", "Confidence", "Content", "CreatedAt", "LastUsed", "ProjectId", "SuccessRate", "Tags", "Title", "Type" },
                values: new object[,]
                {
                    { 2, 7, 0.5, "Injecting .md files into every prompt increased token usage 3×. Switched to ContextLoader with caching. 70% reduction in token spend.", new DateTime(2026, 1, 2, 0, 0, 0, 0, DateTimeKind.Utc), null, 1, 0.5, "performance,prompts,context", "Performance Insight: Prompt Bloat", 1 },
                    { 3, 7, 0.5, "Store only structured summaries: {task_id, problem, fix, lesson}. Never store raw conversation history.", new DateTime(2026, 1, 3, 0, 0, 0, 0, DateTimeKind.Utc), null, 1, 0.5, "memory,summaries,structured", "Memory Refactor Decision", 2 }
                });

            migrationBuilder.InsertData(
                table: "Tasks",
                columns: new[] { "Id", "AssignedAgentId", "ComplexityScore", "CreatedAt", "Description", "ParentTaskId", "Priority", "ProjectId", "RetryCount", "ReviewFailCount", "ReviewNotes", "Status", "StatusEnteredAt", "Title", "UpdatedAt" },
                values: new object[,]
                {
                    { 1, 2, 9, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Architect the event-driven messaging layer between agents", null, 3, 1, 0, 0, null, 15, null, "Design autonomous agent communication protocol", new DateTime(2026, 1, 2, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 2, 3, 8, new DateTime(2026, 1, 2, 0, 0, 0, 0, DateTimeKind.Utc), "State-machine driven task routing and agent assignment", null, 3, 1, 0, 0, null, 5, null, "Implement OrchestratorService (Whis)", null },
                    { 3, 5, 5, new DateTime(2026, 1, 3, 0, 0, 0, 0, DateTimeKind.Utc), "Remove full conversation storage, use JSON summaries only", null, 2, 1, 0, 0, null, 9, null, "Refactor memory to structured summaries", null },
                    { 5, 8, 3, new DateTime(2026, 1, 5, 0, 0, 0, 0, DateTimeKind.Utc), "Cache SOUL and AGENTS context to avoid prompt bloat", null, 1, 1, 0, 0, null, 4, null, "Add context caching to ContextLoader", null }
                });

            migrationBuilder.CreateIndex(
                name: "IX_ActivityLogs_AgentId",
                table: "ActivityLogs",
                column: "AgentId");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityLogs_ProjectId",
                table: "ActivityLogs",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Agents_ProjectId",
                table: "Agents",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Agents_TeamId",
                table: "Agents",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_Logs_AgentName",
                table: "Logs",
                column: "AgentName");

            migrationBuilder.CreateIndex(
                name: "IX_Logs_Level",
                table: "Logs",
                column: "Level");

            migrationBuilder.CreateIndex(
                name: "IX_Logs_TaskId",
                table: "Logs",
                column: "TaskId");

            migrationBuilder.CreateIndex(
                name: "IX_Logs_Timestamp",
                table: "Logs",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_MemoryEntries_AgentId",
                table: "MemoryEntries",
                column: "AgentId");

            migrationBuilder.CreateIndex(
                name: "IX_MemoryEntries_ProjectId",
                table: "MemoryEntries",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_MemorySummaries_ProjectId",
                table: "MemorySummaries",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_AssignedAgentId",
                table: "Tasks",
                column: "AssignedAgentId");

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_ParentTaskId",
                table: "Tasks",
                column: "ParentTaskId");

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_ProjectId",
                table: "Tasks",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Teams_ProjectId",
                table: "Teams",
                column: "ProjectId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActivityLogs");

            migrationBuilder.DropTable(
                name: "Logs");

            migrationBuilder.DropTable(
                name: "MemoryEntries");

            migrationBuilder.DropTable(
                name: "MemorySummaries");

            migrationBuilder.DropTable(
                name: "Tasks");

            migrationBuilder.DropTable(
                name: "Agents");

            migrationBuilder.DropTable(
                name: "Teams");

            migrationBuilder.DropTable(
                name: "Projects");
        }
    }
}
