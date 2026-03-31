using Lorex.Core.Models;

namespace Lorex.Core.Services;

/// <summary>
/// Reads registry skill metadata and computes install/recommendation views for a project.
/// </summary>
public sealed class RegistrySkillQueryService(RegistryService registry, GitService git)
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
        LorexConfig config) =>
        [.. available
            .Where(skill => !config.InstalledSkills.Contains(skill.Name, StringComparer.OrdinalIgnoreCase))
            .Where(skill => IsRecommendedForProject(skill, GetProjectTagKeys(projectRoot)))
            .OrderBy(skill => skill.Name, StringComparer.OrdinalIgnoreCase)
            .Select(skill => skill.Name)];

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
