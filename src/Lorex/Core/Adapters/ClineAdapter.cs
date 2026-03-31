using Lorex.Core.Models;

namespace Lorex.Core.Adapters;

/// <summary>Adapter for Cline — projects lorex skills into <c>.cline/skills</c>.</summary>
public sealed class ClineAdapter : IAdapter
{
    public string Name => "cline";

    public AdapterProjection? GetProjection(string projectRoot, ArtifactKind kind) => kind switch
    {
        ArtifactKind.Skill => new SkillDirectoryProjection(Path.Combine(projectRoot, ".cline", "skills")),
        ArtifactKind.Prompt => new PromptProjection(Path.Combine(projectRoot, ".clinerules", "workflows"), PromptProjectionStyle.Workflow),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    public bool DetectExisting(string projectRoot) =>
        Directory.Exists(Path.Combine(projectRoot, ".cline", "skills")) ||
        File.Exists(Path.Combine(projectRoot, ".clinerules"));

    public IReadOnlyList<string> LegacyPaths(string projectRoot) =>
        [Path.Combine(projectRoot, ".clinerules")];
}
