using MissionControl.Infrastructure;
using MissionControl.Infrastructure.Data;

using MissionControl.Application.Services;
using MissionControl.Application.Options;
using MissionControl.API.Middleware;
using Microsoft.EntityFrameworkCore;
using MissionControl.Infrastructure.Hubs;
using Serilog;
using Serilog.Events;

// ── Serilog bootstrap logger (captures startup errors before DI is ready) ─────
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // ── Serilog ───────────────────────────────────────────────────────────────────
    builder.Host.UseSerilog((ctx, services, cfg) => cfg
        .ReadFrom.Configuration(ctx.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .Enrich.WithMachineName()
        .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{AgentName:-10} {Action:-20}] {Message:lj}{NewLine}{Exception}")
        .WriteTo.File(
            path: "logs/missioncontrol-.log",
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 14,
            outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{AgentName}] [{Action}] [{CorrelationId}] {Message:lj}{NewLine}{Exception}"));

    // Controllers
    builder.Services.AddControllers();

    // CORS — allow any origin with credentials so SignalR WebSocket works cross-origin
    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(policy =>
        {
            policy.SetIsOriginAllowed(_ => true)
                  .AllowAnyMethod()
                  .AllowAnyHeader()
                  .AllowCredentials();
        });
    });

    // OpenAPI / Swagger (.NET 10 built-in)
    builder.Services.AddOpenApi();

    // SignalR
    builder.Services.AddSignalR();

    // Pipeline stage config (skill-based agent routing)
    builder.Services.Configure<PipelineStageConfig>(
        builder.Configuration.GetSection(PipelineStageConfig.SectionName));

    // Infrastructure (EF Core, repos, background orchestration, SignalR notifier)
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? "Data Source=missioncontrol.db";
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddInfrastructure(connectionString);

    // Application services used by controllers
    builder.Services.AddScoped<ProjectService>();
    builder.Services.AddScoped<AgentService>();
    builder.Services.AddScoped<TaskService>();

    builder.Services.AddScoped<ActivityLogService>();
    var app = builder.Build();

    // Auto-migrate database and seed agents on startup
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        try
        {
            db.Database.Migrate();
            MissionControl.Infrastructure.Data.DbSeeder.SeedAllAsync(db).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Database migration or seeding failed — the application will continue but may be unstable.");
        }
    }

    // OpenAPI spec at /openapi/v1.json
    app.MapOpenApi();

    app.UseCorrelationId();
    app.UseCors();
    app.UseSerilogRequestLogging();
    app.UseAuthorization();
    app.MapControllers();
    app.MapHub<AgentHub>("/hubs/agent");


    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "MissionControl host terminated unexpectedly.");
}
finally
{
    Log.CloseAndFlush();
}
