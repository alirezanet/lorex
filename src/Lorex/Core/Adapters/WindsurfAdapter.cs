using Lorex.Core.Models;

namespace Lorex.Core.Adapters;

/// <summary>Adapter for Windsurf — projects lorex skills into <c>.windsurf/skills</c>.</summary>
public sealed class WindsurfAdapter : IAdapter
{
    public string Name => "windsurf";

    public AdapterProjection? GetProjection(string projectRoot, ArtifactKind kind) => kind switch
    {
        ArtifactKind.Skill => new SkillDirectoryProjection(Path.Combine(projectRoot, ".windsurf", "skills")),
        ArtifactKind.Prompt => new PromptProjection(Path.Combine(projectRoot, ".windsurf", "workflows"), PromptProjectionStyle.Workflow),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    public bool DetectExisting(string projectRoot) =>
        Directory.Exists(Path.Combine(projectRoot, ".windsurf", "skills")) ||
        File.Exists(Path.Combine(projectRoot, ".windsurfrules"));

    public IReadOnlyList<string> LegacyPaths(string projectRoot) =>
        [Path.Combine(projectRoot, ".windsurfrules")];
}
