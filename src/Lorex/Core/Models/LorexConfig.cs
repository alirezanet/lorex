namespace Lorex.Core.Models;

/// <summary>
/// Project-level lorex configuration, persisted at <c>.lorex/lorex.json</c>.
/// Created by <c>lorex init</c> and updated by install/uninstall operations.
/// </summary>
public sealed record LorexConfig
{
    /// <summary>Shared registry configuration for this project. Null when running registry-free (local-only mode).</summary>
    public RegistryConfig? Registry { get; init; }

    /// <summary>Adapter names whose native integration surfaces lorex maintains (for example <c>copilot</c>, <c>codex</c>).</summary>
    public string[] Adapters { get; init; } = [];

    /// <summary>Skill names that have been installed into this project.</summary>
    public string[] InstalledSkills { get; init; } = [];

    /// <summary>Maps installed skill name → version string at the time of install/sync.</summary>
    public Dictionary<string, string> InstalledSkillVersions { get; init; } = [];

    /// <summary>Read-only skill sources added via <c>lorex tap add</c>.</summary>
    public TapConfig[] Taps { get; init; } = [];

    /// <summary>
    /// Maps installed skill name → source identifier.
    /// Values: <c>tap:&lt;name&gt;</c> for tap-sourced skills, <c>url:&lt;url&gt;</c> for directly-installed skills.
    /// Absent entries are primary-registry skills.
    /// </summary>
    public Dictionary<string, string> InstalledSkillSources { get; init; } = [];
}
