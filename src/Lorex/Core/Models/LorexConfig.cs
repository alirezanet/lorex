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
}
