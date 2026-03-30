namespace Lorex.Core.Adapters;

/// <summary>Adapter for OpenClaw — injects the skill index into <c>AGENTS.md</c> (same file as the Codex adapter).</summary>
public sealed class OpenClawAdapter : IAdapter
{
    /// <inheritdoc/>
    public string Name => "openclaw";
    /// <inheritdoc/>
    public string TargetFilePath(string projectRoot) =>
        Path.Combine(projectRoot, "AGENTS.md");
    /// <inheritdoc/>
    public bool DetectExisting(string projectRoot) =>
        File.Exists(TargetFilePath(projectRoot));
}
