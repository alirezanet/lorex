namespace Lorex.Core.Adapters;

/// <summary>Adapter for OpenAI Codex — injects the skill index into <c>AGENTS.md</c>.</summary>
public sealed class CodexAdapter : IAdapter
{
    /// <inheritdoc/>
    public string Name => "codex";
    /// <inheritdoc/>
    public string TargetFilePath(string projectRoot) =>
        Path.Combine(projectRoot, "AGENTS.md");
    /// <inheritdoc/>
    public bool DetectExisting(string projectRoot) =>
        File.Exists(TargetFilePath(projectRoot));
}
