namespace Lorex.Core.Adapters;

/// <summary>Adapter for GitHub Copilot — injects the skill index into <c>.github/copilot-instructions.md</c>.</summary>
public sealed class CopilotAdapter : IAdapter
{
    /// <inheritdoc/>
    public string Name => "copilot";
    /// <inheritdoc/>
    public string TargetFilePath(string projectRoot) =>
        Path.Combine(projectRoot, ".github", "copilot-instructions.md");
    /// <inheritdoc/>
    public bool DetectExisting(string projectRoot) =>
        File.Exists(TargetFilePath(projectRoot));
}
