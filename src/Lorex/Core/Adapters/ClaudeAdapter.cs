using Lorex.Core.Models;

namespace Lorex.Core.Adapters;

/// <summary>Adapter for Claude Code — projects lorex skills into <c>.claude/skills</c>.</summary>
public sealed class ClaudeAdapter : IAdapter
{
    public string Name => "claude";

    public AdapterProjection? GetProjection(string projectRoot, ArtifactKind kind) => kind switch
    {
        ArtifactKind.Skill => new SkillDirectoryProjection(Path.Combine(projectRoot, ".claude", "skills")),
        ArtifactKind.Prompt => new PromptProjection(Path.Combine(projectRoot, ".claude", "commands"), PromptProjectionStyle.Claude),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    public bool DetectExisting(string projectRoot) =>
        Directory.Exists(Path.Combine(projectRoot, ".claude", "skills")) ||
        File.Exists(Path.Combine(projectRoot, "CLAUDE.md"));

    public IReadOnlyList<string> LegacyPaths(string projectRoot) =>
        [Path.Combine(projectRoot, "CLAUDE.md")];
}
