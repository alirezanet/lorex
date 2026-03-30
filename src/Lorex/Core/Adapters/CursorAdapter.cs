namespace Lorex.Core.Adapters;

/// <summary>Adapter for Cursor — injects the skill index into <c>.cursorrules</c>.</summary>
public sealed class CursorAdapter : IAdapter
{
    /// <inheritdoc/>
    public string Name => "cursor";
    /// <inheritdoc/>
    public string TargetFilePath(string projectRoot) =>
        Path.Combine(projectRoot, ".cursorrules");
    /// <inheritdoc/>
    public bool DetectExisting(string projectRoot) =>
        File.Exists(TargetFilePath(projectRoot));
}
