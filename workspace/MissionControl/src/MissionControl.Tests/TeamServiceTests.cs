using MissionControl.Application.DTOs;
using MissionControl.Application.Services;
using MissionControl.Domain.Entities;
using MissionControl.Domain.Interfaces;
using Moq;
using FluentAssertions;

namespace MissionControl.Tests;

public class TeamServiceTests
{
    private static (TeamService svc, Mock<ITeamRepository> repo) Build()
    {
        var repo      = new Mock<ITeamRepository>();
        var agentRepo = new Mock<IAgentRepository>();
        return (new TeamService(repo.Object, agentRepo.Object), repo);
    }

    [Fact]
    public async Task CreateAsync_ReturnsCreatedTeamDto()
    {
        var (svc, repo) = Build();
        var team = new Team { Id = 1, Name = "Alpha", Description = "Desc", ProjectId = 1 };
        repo.Setup(r => r.AddAsync(It.IsAny<Team>()))
            .Callback<Team>(t => { t.Id = 1; t.Name = team.Name; t.Description = team.Description; })
            .Returns(Task.CompletedTask);

        var dto = new CreateTeamDto { Name = "Alpha", Description = "Desc", ProjectId = 1 };
        var result = await svc.CreateAsync(dto);

        result.Name.Should().Be("Alpha");
        result.ProjectId.Should().Be(1);
    }

    [Fact]
    public async Task UpdateAsync_ReturnsUpdatedDto_WhenTeamExists()
    {
        var (svc, repo) = Build();
        var existing = new Team { Id = 5, Name = "OldName", Description = "OldDesc", ProjectId = 1 };
        repo.Setup(r => r.GetByIdAsync(5)).ReturnsAsync(existing);
        repo.Setup(r => r.UpdateAsync(It.IsAny<Team>())).Returns(Task.CompletedTask);

        var dto = new CreateTeamDto { Name = "NewName", Description = "NewDesc", ProjectId = 1 };
        var result = await svc.UpdateAsync(5, dto);

        result.Should().NotBeNull();
        result!.Name.Should().Be("NewName");
    }

    [Fact]
    public async Task UpdateAsync_ReturnsNull_WhenTeamNotFound()
    {
        var (svc, repo) = Build();
        repo.Setup(r => r.GetByIdAsync(999)).ReturnsAsync((Team?)null);

        var result = await svc.UpdateAsync(999, new CreateTeamDto { Name = "X", Description = "", ProjectId = 1 });
        result.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_ReturnsTrue_WhenTeamExists()
    {
        var (svc, repo) = Build();
        var t = new Team { Id = 3, Name = "T", Description = "", ProjectId = 1 };
        repo.Setup(r => r.GetByIdAsync(3)).ReturnsAsync(t);
        repo.Setup(r => r.DeleteAsync(t)).Returns(Task.CompletedTask);

        var success = await svc.DeleteAsync(3);
        success.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteAsync_ReturnsFalse_WhenTeamNotFound()
    {
        var (svc, repo) = Build();
        repo.Setup(r => r.GetByIdAsync(99)).ReturnsAsync((Team?)null);

        var success = await svc.DeleteAsync(99);
        success.Should().BeFalse();
    }

    [Fact]
    public async Task GetByProjectIdAsync_ReturnsTeamDtos()
    {
        var (svc, repo) = Build();
        var teams = new List<Team>
        {
            new() { Id = 1, Name = "Team A", Description = "First", ProjectId = 1 },
            new() { Id = 2, Name = "Team B", Description = "Second", ProjectId = 1 }
        };
        repo.Setup(r => r.GetByProjectIdAsync(1)).ReturnsAsync(teams);

        var result = await svc.GetByProjectIdAsync(1);

        result.Should().HaveCount(2);
        result.First().Name.Should().Be("Team A");
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsTeamDto_WhenExists()
    {
        var (svc, repo) = Build();
        var team = new Team { Id = 7, Name = "Team Seven", Description = "Desc", ProjectId = 2 };
        repo.Setup(r => r.GetByIdAsync(7)).ReturnsAsync(team);

        var result = await svc.GetByIdAsync(7);

        result.Should().NotBeNull();
        result!.Name.Should().Be("Team Seven");
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNull_WhenNotFound()
    {
        var (svc, repo) = Build();
        repo.Setup(r => r.GetByIdAsync(999)).ReturnsAsync((Team?)null);

        var result = await svc.GetByIdAsync(999);

        result.Should().BeNull();
    }
}
