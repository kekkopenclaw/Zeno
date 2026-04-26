namespace MissionControl.Application.Options;

/// <summary>
/// Configuration that maps each pipeline stage to the skill tags an agent must possess.
/// Loaded from appsettings.json "PipelineStaging" section.
/// This makes agent routing 100% dynamic — no hardcoded role names in the pipeline logic.
/// </summary>
public sealed class PipelineStageConfig
{
    public const string SectionName = "PipelineStaging";

    /// <summary>
    /// Maps a TaskItemStatus name (e.g. "Architecture") to a list of skill keywords.
    /// The orchestrator will pick the first idle agent whose Skills field contains any keyword.
    /// </summary>
    public Dictionary<string, List<string>> StageSkillRequirements { get; set; } = new();
}
