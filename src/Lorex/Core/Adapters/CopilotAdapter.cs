namespace Lorex.Core.Adapters;

/// <summary>Adapter for GitHub Copilot — projects lorex skills into <c>.github/skills</c>.</summary>
public sealed class CopilotAdapter : IAdapter
{
    public string Name => "copilot";

    public AdapterProjection GetProjection(string projectRoot) =>
        new SkillDirectoryProjection(Path.Combine(projectRoot, ".github", "skills"));

    public bool DetectExisting(string projectRoot) =>
        Directory.Exists(Path.Combine(projectRoot, ".github", "skills")) ||
        File.Exists(Path.Combine(projectRoot, ".github", "copilot-instructions.md"));
}
