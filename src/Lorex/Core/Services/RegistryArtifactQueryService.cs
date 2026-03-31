using Lorex.Core.Models;

namespace Lorex.Core.Services;

/// <summary>
/// Reads registry artifact metadata and computes install/recommendation views for a project.
/// </summary>
public sealed class RegistryArtifactQueryService(RegistryService registry, GitService git)
{
    public IReadOnlyList<ArtifactMetadata> ListAvailableArtifacts(LorexConfig config, ArtifactKind kind, bool refresh = true)
    {
        if (config.Registry is null)
            return [];

        return registry.ListAvailableArtifacts(config.Registry.Url, kind, refresh);
    }

    public List<string> GetInstallableArtifactNames(
        IReadOnlyList<ArtifactMetadata> available,
        LorexConfig config,
        ArtifactKind kind) =>
        [.. available
            .Where(artifact => !config.Artifacts.Get(kind).Contains(artifact.Name, StringComparer.OrdinalIgnoreCase))
            .OrderBy(artifact => artifact.Name, StringComparer.OrdinalIgnoreCase)
            .Select(artifact => artifact.Name)];

    public List<string> GetRecommendedArtifactNames(
        string projectRoot,
        IReadOnlyList<ArtifactMetadata> available,
        LorexConfig config,
        ArtifactKind kind) =>
        [.. available
            .Where(artifact => !config.Artifacts.Get(kind).Contains(artifact.Name, StringComparer.OrdinalIgnoreCase))
            .Where(artifact => IsRecommendedForProject(artifact, GetProjectTagKeys(projectRoot)))
            .OrderBy(artifact => artifact.Name, StringComparer.OrdinalIgnoreCase)
            .Select(artifact => artifact.Name)];

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

    public bool IsRecommendedForProject(ArtifactMetadata artifact, IReadOnlyCollection<string> projectTagKeys)
    {
        if (projectTagKeys.Count == 0 || artifact.Tags.Length == 0)
            return false;

        var tagSet = artifact.Tags
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
