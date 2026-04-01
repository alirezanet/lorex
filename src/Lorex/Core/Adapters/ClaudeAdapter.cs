namespace Lorex.Core.Adapters;

/// <summary>Adapter for Claude Code — projects lorex skills into <c>.claude/skills</c>.</summary>
public sealed class ClaudeAdapter : IAdapter
{
    public string Name => "claude";

    public AdapterProjection GetProjection(string projectRoot) =>
        new SkillDirectoryProjection(Path.Combine(projectRoot, ".claude", "skills"));

    public bool DetectExisting(string projectRoot) =>
        Directory.Exists(Path.Combine(projectRoot, ".claude", "skills")) ||
        File.Exists(Path.Combine(projectRoot, "CLAUDE.md"));
}
