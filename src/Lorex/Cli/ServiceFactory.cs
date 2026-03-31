using Lorex.Core.Services;

namespace Lorex.Cli;

/// <summary>
/// Lightweight manual wiring — no DI container needed (AOT-safe).
/// </summary>
internal static class ServiceFactory
{
    internal static readonly GitService Git = new();
    internal static readonly RegistryService Registry = new(Git);
    internal static readonly ArtifactService Artifacts = new(Registry);
    internal static readonly RegistryArtifactQueryService RegistryArtifacts = new(Registry, Git);
    internal static readonly AdapterService Adapters = new();
}
