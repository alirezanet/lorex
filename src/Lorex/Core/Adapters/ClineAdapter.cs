namespace Lorex.Core.Adapters;

/// <summary>Adapter for Cline (VS Code extension) — injects the skill index into <c>.clinerules</c>.</summary>
public sealed class ClineAdapter : IAdapter
{
    /// <inheritdoc/>
    public string Name => "cline";
    /// <inheritdoc/>
    public string TargetFilePath(string projectRoot) =>
        Path.Combine(projectRoot, ".clinerules");
    /// <inheritdoc/>
    public bool DetectExisting(string projectRoot) =>
        File.Exists(TargetFilePath(projectRoot));
}
