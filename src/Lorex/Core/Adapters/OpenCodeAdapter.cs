namespace Lorex.Core.Adapters;

/// <summary>Adapter for OpenCode — projects lorex skills into <c>.opencode/skills</c>.</summary>
public sealed class OpenCodeAdapter : IAdapter
{
    public string Name => "opencode";

    public AdapterProjection GetProjection(string projectRoot) =>
        new SkillDirectoryProjection(Path.Combine(projectRoot, ".opencode", "skills"));

    public bool DetectExisting(string projectRoot) =>
        Directory.Exists(Path.Combine(projectRoot, ".opencode", "skills")) ||
        File.Exists(Path.Combine(projectRoot, "opencode.md"));
}
