namespace Lorex.Core.Adapters;

/// <summary>
/// Represents an AI agent tool adapter that lorex can inject the skill index into.
/// Each adapter targets a specific config file convention (e.g. <c>AGENTS.md</c>, <c>.cursorrules</c>).
/// </summary>
public interface IAdapter
{
    /// <summary>Short identifier used in <c>lorex.json</c> and CLI prompts (e.g. <c>copilot</c>).</summary>
    string Name { get; }

    /// <summary>Absolute path to the config file this adapter writes to.</summary>
    string TargetFilePath(string projectRoot);

    /// <summary>Returns <see langword="true"/> if the adapter's target config file already exists in the project.</summary>
    bool DetectExisting(string projectRoot);
}
