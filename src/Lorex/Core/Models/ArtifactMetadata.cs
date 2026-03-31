namespace Lorex.Core.Models;

/// <summary>
/// Metadata for a lorex artifact, parsed from YAML frontmatter in its canonical markdown entry file.
/// </summary>
public sealed record ArtifactMetadata
{
    /// <summary>Unique identifier for the artifact (kebab-case, for example <c>auth-overview</c>).</summary>
    public required string Name { get; init; }

    /// <summary>Human-readable summary of what this artifact is for.</summary>
    public required string Description { get; init; }

    /// <summary>Semantic version of the artifact content. Defaults to <c>1.0.0</c>.</summary>
    public string Version { get; init; } = "1.0.0";

    /// <summary>Categorisation tags used for filtering in registry listing flows.</summary>
    public string[] Tags { get; init; } = [];

    /// <summary>Team or individual responsible for maintaining the artifact.</summary>
    public string Owner { get; init; } = string.Empty;
}
