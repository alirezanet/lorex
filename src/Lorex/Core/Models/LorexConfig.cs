namespace Lorex.Core.Models;

/// <summary>
/// Project-level lorex configuration, persisted at <c>.lorex/lorex.json</c>.
/// Created by <c>lorex init</c> and updated by install/uninstall operations.
/// </summary>
public sealed record LorexConfig
{
    /// <summary>Git URL of the skill registry this project is connected to. Null when running registry-free (local-only mode).</summary>
    public string? Registry { get; init; }

    /// <summary>Adapter names whose config files receive the injected skill index (e.g. <c>copilot</c>, <c>codex</c>).</summary>
    public string[] Adapters { get; init; } = [];

    /// <summary>Skill names that have been installed into this project.</summary>
    public string[] InstalledSkills { get; init; } = [];
}
