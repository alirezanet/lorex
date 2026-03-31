namespace Lorex.Core.Adapters;

/// <summary>Adapter for Gemini CLI — configures project settings to load lorex skills as context files.</summary>
public sealed class GeminiAdapter : IAdapter
{
    public string Name => "gemini";

    public AdapterProjection GetProjection(string projectRoot) =>
        new GeminiContextProjection(Path.Combine(projectRoot, ".gemini", "settings.json"));

    public bool DetectExisting(string projectRoot) =>
        File.Exists(Path.Combine(projectRoot, ".gemini", "settings.json")) ||
        File.Exists(Path.Combine(projectRoot, "GEMINI.md"));

    public IReadOnlyList<string> LegacyPaths(string projectRoot) =>
        [Path.Combine(projectRoot, "GEMINI.md")];
}
