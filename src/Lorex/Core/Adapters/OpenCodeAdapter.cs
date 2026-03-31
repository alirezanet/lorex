using Lorex.Core.Models;

namespace Lorex.Core.Adapters;

/// <summary>Adapter for OpenCode — projects lorex skills into <c>.opencode/skills</c>.</summary>
public sealed class OpenCodeAdapter : IAdapter
{
    public string Name => "opencode";

    public AdapterProjection? GetProjection(string projectRoot, ArtifactKind kind) => kind switch
    {
        ArtifactKind.Skill => new SkillDirectoryProjection(Path.Combine(projectRoot, ".opencode", "skills")),
        ArtifactKind.Prompt => new PromptProjection(Path.Combine(projectRoot, ".opencode", "commands"), PromptProjectionStyle.OpenCode),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    public bool DetectExisting(string projectRoot) =>
        Directory.Exists(Path.Combine(projectRoot, ".opencode", "skills")) ||
        File.Exists(Path.Combine(projectRoot, "opencode.md"));

    public IReadOnlyList<string> LegacyPaths(string projectRoot) =>
        [Path.Combine(projectRoot, "opencode.md")];
}
