using MissionControl.Application.DTOs;
using MissionControl.Application.Interfaces;
using MissionControl.Application.Services;
using MissionControl.Domain.Entities;
using MissionControl.Domain.Enums;
using MissionControl.Domain.Interfaces;
using Moq;
using FluentAssertions;

namespace MissionControl.Tests;

public class AgentServiceTests
{
    [Fact]
    public async Task GetAllAsync_ReturnsMappedDtos()
    {
        var repo = new Mock<IAgentRepository>();
        repo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Agent>
        {
            new() 
            { 
                Id=1, Name="Coder", Model="gpt-4o-mini", Role=AgentRole.Kakarot, 
                Status=AgentStatus.Idle, Skills="Coding,Testing", ProjectId=1 
            }
        });
        var svc = new AgentService(repo.Object);
        var result = await svc.GetAllAsync();
        result.Should().HaveCount(1);
        result[0].Name.Should().Be("Coder");
        result[0].Role.Should().Be("Kakarot");
    }

    [Fact]
    public async Task UpdateAsync_ReturnsNull_WhenAgentNotFound()
    {
        var repo = new Mock<IAgentRepository>();
        repo.Setup(r => r.GetByIdAsync(999)).ReturnsAsync((Agent?)null);
        var svc = new AgentService(repo.Object);
        var result = await svc.UpdateAsync(999, new CreateAgentDto { Name = "X", Model = "m", Role = "Kakarot", ProjectId=1 });
        result.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_ReturnsTrue_WhenAgentExists()
    {
        var repo = new Mock<IAgentRepository>();
        var agent = new Agent { Id = 7, Name = "Del", Role = AgentRole.Gohan, Model = "m", ProjectId=1 };
        repo.Setup(r => r.GetByIdAsync(7)).ReturnsAsync(agent);
        repo.Setup(r => r.DeleteAsync(agent)).Returns(Task.CompletedTask);
        var svc = new AgentService(repo.Object);
        var ok = await svc.DeleteAsync(7);
        ok.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteAsync_ReturnsFalse_WhenAgentNotFound()
    {
        var repo = new Mock<IAgentRepository>();
        repo.Setup(r => r.GetByIdAsync(999)).ReturnsAsync((Agent?)null);
        var svc = new AgentService(repo.Object);
        var ok = await svc.DeleteAsync(999);
        ok.Should().BeFalse();
    }

    [Fact]
    public async Task GetByProjectIdAsync_ReturnsMappedDtos()
    {
        var repo = new Mock<IAgentRepository>();
        repo.Setup(r => r.GetByProjectIdAsync(1)).ReturnsAsync(new List<Agent>
        {
            new() 
            { 
                Id=1, Name="Agent1", Model="gpt-4", Role=AgentRole.Vegeta, 
                Status=AgentStatus.Idle, Skills="Testing", ProjectId=1, ExecutionBackend=ExecutionBackend.Ollama
            }
        });
        var svc = new AgentService(repo.Object);
        var result = await svc.GetByProjectIdAsync(1);
        result.Should().HaveCount(1);
        result[0].Name.Should().Be("Agent1");
        result[0].ProjectId.Should().Be(1);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsDto_WhenExists()
    {
        var repo = new Mock<IAgentRepository>();
        var agent = new Agent 
        { 
            Id=5, Name="TestAgent", Model="claude-3", Role=AgentRole.Whis, 
            Status=AgentStatus.Working, Skills="Planning", ProjectId=2, ExecutionBackend=ExecutionBackend.Ollama
        };
        repo.Setup(r => r.GetByIdAsync(5)).ReturnsAsync(agent);
        var svc = new AgentService(repo.Object);
        var result = await svc.GetByIdAsync(5);
        result.Should().NotBeNull();
        result!.Name.Should().Be("TestAgent");
        result.Status.Should().Be("Working");
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNull_WhenNotFound()
    {
        var repo = new Mock<IAgentRepository>();
        repo.Setup(r => r.GetByIdAsync(999)).ReturnsAsync((Agent?)null);
        var svc = new AgentService(repo.Object);
        var result = await svc.GetByIdAsync(999);
        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateAsync_UpdatesAgentProperties()
    {
        var repo = new Mock<IAgentRepository>();
        var agent = new Agent 
        { 
            Id=3, Name="OldName", Model="gpt-3.5", Role=AgentRole.Kakarot, 
            Status=AgentStatus.Idle, Skills="Coding", ProjectId=1, ExecutionBackend=ExecutionBackend.Ollama
        };
        repo.Setup(r => r.GetByIdAsync(3)).ReturnsAsync(agent);
        repo.Setup(r => r.UpdateAsync(It.IsAny<Agent>())).Returns(Task.CompletedTask);

        var svc = new AgentService(repo.Object);
        var updateDto = new CreateAgentDto 
        { 
            Name = "NewName", 
            Model = "gpt-4o-mini", 
            Role = "Gohan", 
            ProjectId = 1,
            Description = "Updated agent"
        };

        var result = await svc.UpdateAsync(3, updateDto);

        result.Should().NotBeNull();
        result!.Name.Should().Be("NewName");
        repo.Verify(r => r.UpdateAsync(It.Is<Agent>(a => a.Name == "NewName")), Times.Once);
    }
}
