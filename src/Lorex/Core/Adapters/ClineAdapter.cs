namespace Lorex.Core.Adapters;

/// <summary>Adapter for Cline — projects lorex skills into <c>.cline/skills</c>.</summary>
public sealed class ClineAdapter : IAdapter
{
    public string Name => "cline";

    public AdapterProjection GetProjection(string projectRoot) =>
        new SkillDirectoryProjection(Path.Combine(projectRoot, ".cline", "skills"));

    public bool DetectExisting(string projectRoot) =>
        Directory.Exists(Path.Combine(projectRoot, ".cline", "skills")) ||
        File.Exists(Path.Combine(projectRoot, ".clinerules"));

    public IReadOnlyList<string> LegacyPaths(string projectRoot) =>
        [Path.Combine(projectRoot, ".clinerules")];
}
