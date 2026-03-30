namespace Lorex.Core.Adapters;

/// <summary>Adapter for Gemini CLI (Google) — injects the skill index into <c>GEMINI.md</c>.</summary>
public sealed class GeminiAdapter : IAdapter
{
    /// <inheritdoc/>
    public string Name => "gemini";
    /// <inheritdoc/>
    public string TargetFilePath(string projectRoot) =>
        Path.Combine(projectRoot, "GEMINI.md");
    /// <inheritdoc/>
    public bool DetectExisting(string projectRoot) =>
        File.Exists(TargetFilePath(projectRoot));
}
