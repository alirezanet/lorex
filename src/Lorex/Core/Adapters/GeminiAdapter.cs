using Lorex.Core.Models;

namespace Lorex.Core.Adapters;

/// <summary>Adapter for Gemini CLI — configures project settings to load lorex skills as context files.</summary>
public sealed class GeminiAdapter : IAdapter
{
    public string Name => "gemini";

    public AdapterProjection? GetProjection(string projectRoot, ArtifactKind kind) => kind switch
    {
        ArtifactKind.Skill => new GeminiContextProjection(Path.Combine(projectRoot, ".gemini", "settings.json")),
        ArtifactKind.Prompt => new PromptProjection(Path.Combine(projectRoot, ".gemini", "commands"), PromptProjectionStyle.Gemini),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    public bool DetectExisting(string projectRoot) =>
        File.Exists(Path.Combine(projectRoot, ".gemini", "settings.json")) ||
        File.Exists(Path.Combine(projectRoot, "GEMINI.md"));

    public IReadOnlyList<string> LegacyPaths(string projectRoot) =>
        [Path.Combine(projectRoot, "GEMINI.md")];
}
