namespace Lorex.Core.Models;

/// <summary>
/// Machine-level lorex configuration stored at ~/.lorex/config.json.
/// Tracks all registries the user has ever used on this machine (MRU order).
/// </summary>
public sealed record GlobalConfig
{
    /// <summary>All known registry URLs — most-recently-used first.</summary>
    public string[] Registries { get; init; } = [];
}
