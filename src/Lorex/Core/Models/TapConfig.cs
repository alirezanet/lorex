namespace Lorex.Core.Models;

/// <summary>
/// Configuration for a tap — a read-only skill source (any git repository containing skills).
/// Persisted in <c>.lorex/lorex.json</c> as part of <see cref="LorexConfig.Taps"/>.
/// </summary>
public sealed record TapConfig
{
    /// <summary>Short identifier used to refer to this tap (e.g. <c>dotnet</c>).</summary>
    public string Name { get; init; } = "";

    /// <summary>Git URL of the tap repository.</summary>
    public string Url { get; init; } = "";

    /// <summary>
    /// Optional subdirectory within the repository to use as the skills root.
    /// Null means lorex auto-detects: prefers <c>skills/</c> if it exists, otherwise searches the whole repo.
    /// </summary>
    public string? Root { get; init; }
}
