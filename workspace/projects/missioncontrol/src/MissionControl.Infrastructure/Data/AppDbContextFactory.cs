using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MissionControl.Infrastructure.Data;

/// <summary>Used exclusively by dotnet-ef migrations at design time.</summary>
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("Data Source=missioncontrol_design.db")
            .Options;
        return new AppDbContext(options);
    }
}
