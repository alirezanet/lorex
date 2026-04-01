namespace Lorex.Core.Adapters;

/// <summary>Adapter for Windsurf — projects lorex skills into <c>.windsurf/skills</c>.</summary>
public sealed class WindsurfAdapter : IAdapter
{
    public string Name => "windsurf";

    public AdapterProjection GetProjection(string projectRoot) =>
        new SkillDirectoryProjection(Path.Combine(projectRoot, ".windsurf", "skills"));

    public bool DetectExisting(string projectRoot) =>
        Directory.Exists(Path.Combine(projectRoot, ".windsurf", "skills")) ||
        File.Exists(Path.Combine(projectRoot, ".windsurfrules"));
}
