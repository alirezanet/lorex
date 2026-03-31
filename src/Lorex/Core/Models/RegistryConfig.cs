namespace Lorex.Core.Models;

/// <summary>
/// Project-level connection details for a shared lorex registry.
/// </summary>
public sealed record RegistryConfig
{
    /// <summary>Git URL of the shared skill registry.</summary>
    public required string Url { get; init; }

    /// <summary>The effective registry-owned publish policy currently cached in this project.</summary>
    public required RegistryPolicy Policy { get; init; }
}
