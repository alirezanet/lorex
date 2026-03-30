namespace Lorex.Core.Adapters;

/// <summary>Adapter for OpenCode — injects the skill index into <c>opencode.md</c>.</summary>
public sealed class OpenCodeAdapter : IAdapter
{
    /// <inheritdoc/>
    public string Name => "opencode";
    /// <inheritdoc/>
    public string TargetFilePath(string projectRoot) =>
        Path.Combine(projectRoot, "opencode.md");
    /// <inheritdoc/>
    public bool DetectExisting(string projectRoot) =>
        File.Exists(TargetFilePath(projectRoot));
}
