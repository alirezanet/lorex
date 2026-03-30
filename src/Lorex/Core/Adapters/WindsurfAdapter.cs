namespace Lorex.Core.Adapters;

/// <summary>Adapter for Windsurf (Codeium) — injects the skill index into <c>.windsurfrules</c>.</summary>
public sealed class WindsurfAdapter : IAdapter
{
    /// <inheritdoc/>
    public string Name => "windsurf";
    /// <inheritdoc/>
    public string TargetFilePath(string projectRoot) =>
        Path.Combine(projectRoot, ".windsurfrules");
    /// <inheritdoc/>
    public bool DetectExisting(string projectRoot) =>
        File.Exists(TargetFilePath(projectRoot));
}
