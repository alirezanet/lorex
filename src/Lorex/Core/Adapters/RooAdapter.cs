namespace Lorex.Core.Adapters;

/// <summary>Adapter for Roo Code (VS Code extension) — injects the skill index into <c>.roorules</c>.</summary>
public sealed class RooAdapter : IAdapter
{
    /// <inheritdoc/>
    public string Name => "roo";
    /// <inheritdoc/>
    public string TargetFilePath(string projectRoot) =>
        Path.Combine(projectRoot, ".roorules");
    /// <inheritdoc/>
    public bool DetectExisting(string projectRoot) =>
        File.Exists(TargetFilePath(projectRoot));
}
