using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace MissionControl.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CleanSlate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MemoryEntries");

            migrationBuilder.DropTable(
                name: "MemorySummaries");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MemoryEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AgentId = table.Column<int>(type: "INTEGER", nullable: true),
                    ProjectId = table.Column<int>(type: "INTEGER", nullable: false),
                    Confidence = table.Column<double>(type: "REAL", nullable: false, defaultValue: 0.5),
                    Content = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsRule = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    LastUsed = table.Column<DateTime>(type: "TEXT", nullable: true),
                    SuccessRate = table.Column<double>(type: "REAL", nullable: false, defaultValue: 0.5),
                    Tags = table.Column<string>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    UsageCount = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0)
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
                name: "MemorySummaries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProjectId = table.Column<int>(type: "INTEGER", nullable: false),
                    AgentRole = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    ComplexityScore = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Fix = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    Lesson = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    Problem = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    RetriesRequired = table.Column<int>(type: "INTEGER", nullable: false),
                    TaskItemId = table.Column<int>(type: "INTEGER", nullable: true)
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

            migrationBuilder.InsertData(
                table: "MemoryEntries",
                columns: new[] { "Id", "AgentId", "Confidence", "Content", "CreatedAt", "LastUsed", "ProjectId", "SuccessRate", "Tags", "Title", "Type" },
                values: new object[,]
                {
                    { 1, null, 0.5, "We will use strict state-machine driven execution. Only status transitions trigger agent actions. No polling loops.", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 1, 0.5, "architecture,state-machine,agents", "Architecture Decision: Event-Driven Agents", 2 },
                    { 2, 7, 0.5, "Injecting .md files into every prompt increased token usage 3×. Switched to ContextLoader with caching. 70% reduction in token spend.", new DateTime(2026, 1, 2, 0, 0, 0, 0, DateTimeKind.Utc), null, 1, 0.5, "performance,prompts,context", "Performance Insight: Prompt Bloat", 1 },
                    { 3, 7, 0.5, "Store only structured summaries: {task_id, problem, fix, lesson}. Never store raw conversation history.", new DateTime(2026, 1, 3, 0, 0, 0, 0, DateTimeKind.Utc), null, 1, 0.5, "memory,summaries,structured", "Memory Refactor Decision", 2 }
                });

            migrationBuilder.InsertData(
                table: "MemorySummaries",
                columns: new[] { "Id", "AgentRole", "ComplexityScore", "CreatedAt", "Fix", "Lesson", "Problem", "ProjectId", "RetriesRequired", "TaskItemId" },
                values: new object[] { 1, "Beerus", 9, new DateTime(2026, 1, 2, 0, 0, 0, 0, DateTimeKind.Utc), "Implemented SignalR hub with typed methods per event type", "Event-driven beats polling — always push, never pull", "Agent communication was polling-based and inefficient", 1, 0, 1 });

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
        }
    }
}
