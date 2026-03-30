namespace Lorex.Core.Adapters;

/// <summary>Adapter for Claude Code (Anthropic) — injects the skill index into <c>CLAUDE.md</c>.</summary>
public sealed class ClaudeAdapter : IAdapter
{
    /// <inheritdoc/>
    public string Name => "claude";
    /// <inheritdoc/>
    public string TargetFilePath(string projectRoot) =>
        Path.Combine(projectRoot, "CLAUDE.md");
    /// <inheritdoc/>
    public bool DetectExisting(string projectRoot) =>
        File.Exists(TargetFilePath(projectRoot));
}
