namespace Lorex.Core.Adapters;

/// <summary>Adapter for OpenAI Codex — projects lorex skills into <c>.agents/skills</c>.</summary>
public sealed class CodexAdapter : IAdapter
{
    public string Name => "codex";

    public AdapterProjection GetProjection(string projectRoot) =>
        new SkillDirectoryProjection(Path.Combine(projectRoot, ".agents", "skills"));

    public bool DetectExisting(string projectRoot) =>
        Directory.Exists(Path.Combine(projectRoot, ".agents", "skills")) ||
        File.Exists(Path.Combine(projectRoot, "AGENTS.md"));

    public IReadOnlyList<string> LegacyPaths(string projectRoot) =>
        [Path.Combine(projectRoot, "AGENTS.md")];
}
