using MissionControl.Application.DTOs;
using MissionControl.Application.Interfaces;
using MissionControl.Application.Options;
using MissionControl.Application.Services;
using MissionControl.Domain.Entities;
using MissionControl.Domain.Enums;
using MissionControl.Domain.Interfaces;
using Microsoft.Extensions.Options;
using Moq;
using FluentAssertions;

namespace MissionControl.Tests;

public class OrchestratorServiceTests
{
    [Fact]
    public async Task TickAsync_TodoTask_AdvancesPipeline_WhenAgentsExist()
    {
        // Arrange - verify that Todo tasks get processed through the pipeline
        var task = new TaskItem
        {
            Id              = 1,
            Title           = "Test task",
            Description     = "Short desc",
            Status          = TaskItemStatus.Todo,
            Priority        = TaskPriority.Medium,
            ProjectId       = 1,  // Test mode project
            ComplexityScore = 3,
            CreatedAt       = DateTime.UtcNow
        };

        var orchestratorAgent = new Agent
        {
            Id       = 10,
            Name     = "Orchestrator",
            Role     = AgentRole.Whis,
            Skills   = "Orchestration,Planning",
            Status   = AgentStatus.Idle,
            IsPaused = false,
            ProjectId = 1,
            ExecutionBackend = ExecutionBackend.Ollama
        };

        var codingAgent = new Agent
        {
            Id       = 11,
            Name     = "Coder",
            Role     = AgentRole.Kakarot,
            Skills   = "Coding,Implementation",
            Status   = AgentStatus.Idle,
            IsPaused = false,
            ProjectId = 1,
            ExecutionBackend = ExecutionBackend.Ollama
        };

        var taskRepo  = new Mock<ITaskRepository>();
        var agentRepo = new Mock<IAgentRepository>();
        var logRepo   = new Mock<IActivityLogRepository>();
        var notifier  = new Mock<ISignalRNotifier>();
        var logSvc    = new Mock<ILogService>();

        // Return both agents on each query
        taskRepo.Setup(r => r.GetByProjectIdAsync(1)).ReturnsAsync(new List<TaskItem> { task });
        agentRepo.Setup(r => r.GetByProjectIdAsync(1)).ReturnsAsync(new List<Agent> { orchestratorAgent, codingAgent });
        agentRepo.Setup(r => r.UpdateAsync(It.IsAny<Agent>())).Returns(Task.CompletedTask);

        taskRepo.Setup(r => r.UpdateAsync(It.IsAny<TaskItem>()))
                .Callback<TaskItem>(t => task.Status = t.Status)
                .Returns(Task.CompletedTask);

        var savedLog = new ActivityLog { Id = 1, ProjectId = 1, Message = "test", Timestamp = DateTime.UtcNow };
        logRepo.Setup(r => r.AddAsync(It.IsAny<ActivityLog>())).ReturnsAsync(savedLog);

        notifier.Setup(n => n.NotifyLogCreatedAsync(It.IsAny<ActivityLogDto>())).Returns(Task.CompletedTask);
        notifier.Setup(n => n.NotifyTaskUpdatedAsync(It.IsAny<TaskItemDto>())).Returns(Task.CompletedTask);
        notifier.Setup(n => n.NotifyAgentStartedAsync(It.IsAny<AgentDto>())).Returns(Task.CompletedTask);
        logSvc.Setup(l => l.WriteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), 
                                       It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string>()))
              .Returns(Task.CompletedTask);

        var config = Options.Create(new PipelineStageConfig
        {
            StageSkillRequirements = new Dictionary<string, List<string>>
            {
                ["Orchestration"] = new() { "Orchestration", "Planning" },
                ["Coding"] = new() { "Coding", "Implementation" }
            }
        });

        var svc = new OrchestratorService(
            taskRepo.Object, agentRepo.Object, logRepo.Object,
            notifier.Object, logSvc.Object, openClawRunner: null, stageConfig: config);

        // Act
        await svc.TickAsync(1);

        // Assert - verify that task was moved from Todo (task was processed)
        task.Status.Should().NotBe(TaskItemStatus.Todo);
        taskRepo.Verify(r => r.UpdateAsync(It.IsAny<TaskItem>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task TickAsync_ComplexTask_MovesToDecomposition()
    {
        // Arrange — high complexity score triggers Decomposition
        var task = new TaskItem
        {
            Id              = 2,
            Title           = "Complex task",
            Description     = "short",
            Status          = TaskItemStatus.Todo,
            Priority        = TaskPriority.Critical,
            ProjectId       = 99,
            ComplexityScore = 8,   // >= 7 → Decomposition
            CreatedAt       = DateTime.UtcNow
        };

        var taskRepo  = new Mock<ITaskRepository>();
        var agentRepo = new Mock<IAgentRepository>();
        var logRepo   = new Mock<IActivityLogRepository>();
        var notifier  = new Mock<ISignalRNotifier>();
        var logSvc    = new Mock<ILogService>();

        taskRepo.Setup(r => r.GetByProjectIdAsync(99)).ReturnsAsync(new List<TaskItem> { task });
        agentRepo.Setup(r => r.GetByProjectIdAsync(99)).ReturnsAsync(new List<Agent>());

        taskRepo.Setup(r => r.UpdateAsync(It.IsAny<TaskItem>()))
                .Callback<TaskItem>(t => task.Status = t.Status)
                .Returns(Task.CompletedTask);

        var savedLog = new ActivityLog { Id = 1, ProjectId = 99, Message = "x", Timestamp = DateTime.UtcNow };
        logRepo.Setup(r => r.AddAsync(It.IsAny<ActivityLog>())).ReturnsAsync(savedLog);
        notifier.Setup(n => n.NotifyLogCreatedAsync(It.IsAny<ActivityLogDto>())).Returns(Task.CompletedTask);
        notifier.Setup(n => n.NotifyTaskUpdatedAsync(It.IsAny<TaskItemDto>())).Returns(Task.CompletedTask);
        logSvc.Setup(l => l.WriteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), 
                                       It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string>()))
              .Returns(Task.CompletedTask);

        var svc = new OrchestratorService(
            taskRepo.Object, agentRepo.Object, logRepo.Object,
            notifier.Object, logSvc.Object);

        // Act
        await svc.TickAsync(99);

        // Assert — should have been pushed to Decomposition
        task.Status.Should().Be(TaskItemStatus.Decomposition);
    }

    [Fact]
    public async Task TickAsync_CreatesSubtasksForDecomposedTask()
    {
        // Arrange - task in Decomposition with no subtasks yet
        var task = new TaskItem
        {
            Id              = 3,
            Title           = "Multi-part task",
            Description     = "A task that needs breakdown",
            Status          = TaskItemStatus.Decomposition,
            Priority        = TaskPriority.High,
            ProjectId       = 99,
            ComplexityScore = 6,
            CreatedAt       = DateTime.UtcNow
        };

        var taskRepo  = new Mock<ITaskRepository>();
        var agentRepo = new Mock<IAgentRepository>();
        var logRepo   = new Mock<IActivityLogRepository>();
        var notifier  = new Mock<ISignalRNotifier>();
        var logSvc    = new Mock<ILogService>();

        taskRepo.Setup(r => r.GetByProjectIdAsync(99)).ReturnsAsync(new List<TaskItem> { task });
        agentRepo.Setup(r => r.GetByProjectIdAsync(99)).ReturnsAsync(new List<Agent>());
        taskRepo.Setup(r => r.AddAsync(It.IsAny<TaskItem>())).ReturnsAsync((TaskItem t) => t);

        var savedLog = new ActivityLog { Id = 1, ProjectId = 99, Message = "x", Timestamp = DateTime.UtcNow };
        logRepo.Setup(r => r.AddAsync(It.IsAny<ActivityLog>())).ReturnsAsync(savedLog);
        notifier.Setup(n => n.NotifyLogCreatedAsync(It.IsAny<ActivityLogDto>())).Returns(Task.CompletedTask);
        notifier.Setup(n => n.NotifyTaskUpdatedAsync(It.IsAny<TaskItemDto>())).Returns(Task.CompletedTask);
        logSvc.Setup(l => l.WriteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), 
                                       It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string>()))
              .Returns(Task.CompletedTask);

        var svc = new OrchestratorService(
            taskRepo.Object, agentRepo.Object, logRepo.Object,
            notifier.Object, logSvc.Object);

        // Act
        await svc.TickAsync(99);

        // Assert - verify AddAsync was called to create subtasks
        taskRepo.Verify(r => r.AddAsync(It.IsAny<TaskItem>()), Times.AtLeastOnce);
    }
}
