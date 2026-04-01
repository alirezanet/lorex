namespace Lorex.Core.Adapters;

/// <summary>
/// Represents an AI agent adapter that lorex can project skills into using the agent's native conventions.
/// </summary>
public interface IAdapter
{
    /// <summary>Short identifier used in <c>lorex.json</c> and CLI prompts (for example <c>copilot</c>).</summary>
    string Name { get; }

    /// <summary>Returns the native projection surface lorex should maintain for the adapter.</summary>
    AdapterProjection GetProjection(string projectRoot);

    /// <summary>Returns <see langword="true"/> if lorex should suggest this adapter based on the current workspace.</summary>
    bool DetectExisting(string projectRoot);
}
