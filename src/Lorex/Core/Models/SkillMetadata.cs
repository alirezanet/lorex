namespace Lorex.Core.Models;

/// <summary>Metadata for a lorex skill, parsed from YAML frontmatter in <c>SKILL.md</c> or the legacy <c>skill.md</c>.</summary>
public sealed record SkillMetadata
{
    /// <summary>Unique identifier for the skill (kebab-case, e.g. <c>auth-overview</c>).</summary>
    public required string Name { get; init; }

    /// <summary>Human-readable summary of what this skill teaches the AI agent.</summary>
    public required string Description { get; init; }

    /// <summary>Semantic version of the skill content. Defaults to <c>1.0.0</c>.</summary>
    public string Version { get; init; } = "1.0.0";

    /// <summary>Categorisation tags used for filtering in <c>lorex list</c>.</summary>
    public string[] Tags { get; init; } = [];

    /// <summary>Team or individual responsible for maintaining this skill.</summary>
    public string Owner { get; init; } = string.Empty;
}
