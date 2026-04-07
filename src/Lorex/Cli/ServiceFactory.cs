using Lorex.Core.Services;

namespace Lorex.Cli;

/// <summary>
/// Lightweight manual wiring — no DI container needed (AOT-safe).
/// </summary>
internal static class ServiceFactory
{
    internal static readonly GitService Git = new();
    internal static readonly RegistryService Registry = new(Git);
    internal static readonly SkillService Skills = new(Registry);
    internal static readonly TapService Taps = new(Git);
    internal static readonly RegistrySkillQueryService RegistrySkills = new(Registry, Git, Taps);
    internal static readonly AdapterService Adapters = new();
}
