using Lorex.Core.Services;

namespace Lorex.Commands;

/// <summary>
/// Lightweight manual wiring — no DI container needed (AOT-safe).
/// </summary>
internal static class ServiceFactory
{
    internal static readonly GitService Git = new();
    internal static readonly RegistryService Registry = new(Git);
    internal static readonly SkillService Skills = new(Registry);
    internal static readonly AdapterService Adapters = new();
}
