using Lorex.Core.Models;

namespace Lorex.Core.Services;

/// <summary>
/// Reads registry and tap skill metadata and computes install/recommendation views for a project.
/// </summary>
public sealed class RegistrySkillQueryService(RegistryService registry, GitService git, TapService taps)
{
    public IReadOnlyList<SkillMetadata> ListAvailableSkills(LorexConfig config, bool refresh = true)
    {
        if (config.Registry is null)
            return [];

        return registry.ListAvailableSkills(config.Registry.Url, refresh);
    }

    public List<string> GetInstallableSkillNames(
        IReadOnlyList<SkillMetadata> available,
        LorexConfig config) =>
        [.. available
            .Where(skill => !config.InstalledSkills.Contains(skill.Name, StringComparer.OrdinalIgnoreCase))
            .OrderBy(skill => skill.Name, StringComparer.OrdinalIgnoreCase)
            .Select(skill => skill.Name)];

    public List<string> GetRecommendedSkillNames(
        string projectRoot,
        IReadOnlyList<SkillMetadata> available,
        LorexConfig config)
    {
        var projectTagKeys = GetProjectTagKeys(projectRoot);
        return [.. available
            .Where(skill => !config.InstalledSkills.Contains(skill.Name, StringComparer.OrdinalIgnoreCase))
            .Where(skill => IsRecommendedForProject(skill, projectTagKeys))
            .OrderBy(skill => skill.Name, StringComparer.OrdinalIgnoreCase)
            .Select(skill => skill.Name)];
    }

    public string[] GetProjectTagKeys(string projectRoot)
    {
        var keys = new List<string>();

        var slug = git.TryGetProjectSlug(projectRoot);
        if (!string.IsNullOrWhiteSpace(slug))
            keys.Add(slug);

        var folderName = Path.GetFileName(Path.TrimEndingDirectorySeparator(projectRoot));
        var normalizedFolderName = NormalizeProjectTag(folderName);
        if (!string.IsNullOrWhiteSpace(normalizedFolderName))
            keys.Add(normalizedFolderName);

        return [.. keys
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Distinct(StringComparer.OrdinalIgnoreCase)];
    }

    /// <summary>
    /// Returns all skills from the primary registry and all configured taps, together with a source map.
    /// Primary registry wins over taps on name conflicts; taps are queried in config order (first wins).
    /// Source values: <c>"registry"</c> or <c>"tap:&lt;name&gt;"</c>.
    /// </summary>
    public (IReadOnlyList<SkillMetadata> Skills, Dictionary<string, string> Sources) ListAllSkills(
        LorexConfig config,
        bool refresh = true)
    {
        var seen    = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result  = new List<SkillMetadata>();
        var sources = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Primary registry first (wins on conflicts)
        if (config.Registry is not null)
        {
            foreach (var skill in registry.ListAvailableSkills(config.Registry.Url, refresh))
            {
                if (seen.Add(skill.Name))
                {
                    result.Add(skill);
                    sources[skill.Name] = "registry";
                }
            }
        }

        // Taps in config order (first tap wins over later taps for the same skill name)
        foreach (var tap in config.Taps)
        {
            foreach (var skill in taps.ListTapSkills(tap))
            {
                if (seen.Add(skill.Name))
                {
                    result.Add(skill);
                    sources[skill.Name] = $"tap:{tap.Name}";
                }
            }
        }

        return (result, sources);
    }

    /// <summary>
    /// Filters <paramref name="skills"/> by an optional full-text search term and/or an optional exact tag.
    /// When both are supplied both conditions must hold.
    /// </summary>
    public IReadOnlyList<SkillMetadata> FilterBySearch(
        IReadOnlyList<SkillMetadata> skills,
        string? search,
        string? tag)
    {
        IEnumerable<SkillMetadata> result = skills;

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            result = result.Where(skill =>
                skill.Name.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                skill.Description.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                skill.Tags.Any(t => t.Contains(s, StringComparison.OrdinalIgnoreCase)));
        }

        if (!string.IsNullOrWhiteSpace(tag))
        {
            var t = tag.Trim();
            result = result.Where(skill =>
                skill.Tags.Contains(t, StringComparer.OrdinalIgnoreCase));
        }

        return [.. result];
    }

    public bool IsRecommendedForProject(SkillMetadata skill, IReadOnlyCollection<string> projectTagKeys)
    {
        if (projectTagKeys.Count == 0 || skill.Tags.Length == 0)
            return false;

        var tagSet = skill.Tags
            .Select(NormalizeProjectTag)
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return projectTagKeys.Any(tagSet.Contains);
    }

    public static string NormalizeProjectTag(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return value.Trim().ToLowerInvariant().Replace('\\', '/');
    }
}
