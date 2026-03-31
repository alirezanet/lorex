using Lorex.Core.Models;

namespace Lorex.Core.Adapters;

/// <summary>Adapter for GitHub Copilot — projects lorex skills into <c>.github/skills</c>.</summary>
public sealed class CopilotAdapter : IAdapter
{
    public string Name => "copilot";

    public AdapterProjection? GetProjection(string projectRoot, ArtifactKind kind) => kind switch
    {
        ArtifactKind.Skill => new SkillDirectoryProjection(Path.Combine(projectRoot, ".github", "skills")),
        ArtifactKind.Prompt => new PromptProjection(
            Path.Combine(projectRoot, ".github", "prompts"),
            PromptProjectionStyle.Copilot,
            Path.Combine(projectRoot, ".vscode", "settings.json")),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    public bool DetectExisting(string projectRoot) =>
        Directory.Exists(Path.Combine(projectRoot, ".github", "skills")) ||
        File.Exists(Path.Combine(projectRoot, ".github", "copilot-instructions.md"));

    public IReadOnlyList<string> LegacyPaths(string projectRoot) =>
        [Path.Combine(projectRoot, ".github", "copilot-instructions.md")];
}
