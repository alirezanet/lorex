using Lorex.Core.Models;
using Lorex.Core.Serialization;

namespace Lorex.Core.Services;

/// <summary>
/// Manages the local registry cache at <c>~/.lorex/cache/&lt;slug&gt;/</c>.
/// Each registry URL maps to a single cloned git repo that is shared across all projects on the machine.
/// </summary>
public sealed class RegistryService(GitService git)
{
    private static readonly string CacheRoot =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".lorex", "cache");

    /// <summary>Returns (and clones/updates) the local cache path for a registry URL.</summary>
    public string EnsureCache(string registryUrl)
    {
        var cacheDir = GetCachePath(registryUrl);

        if (Directory.Exists(Path.Combine(cacheDir, ".git")))
            git.Pull(cacheDir);
        else
        {
            Directory.CreateDirectory(Path.GetDirectoryName(cacheDir)!);
            git.CloneShallow(registryUrl, cacheDir);
        }

        return cacheDir;
    }

    /// <summary>Finds a skill folder inside the cached registry. Returns null if not found.</summary>
    public string? FindSkillPath(string registryUrl, string skillName)
    {
        var cacheDir = EnsureCache(registryUrl);
        var candidate = Path.Combine(cacheDir, "skills", skillName);
        return Directory.Exists(candidate) ? candidate : null;
    }

    /// <summary>Scans the cached registry and returns metadata for all available skills.</summary>
    public IReadOnlyList<SkillMetadata> ListAvailableSkills(string registryUrl)
    {
        var cacheDir = EnsureCache(registryUrl);
        var skillsRoot = Path.Combine(cacheDir, "skills");

        if (!Directory.Exists(skillsRoot))
            return [];

        var results = new List<SkillMetadata>();
        foreach (var dir in Directory.EnumerateDirectories(skillsRoot))
        {
            // New format: YAML frontmatter in skill.md
            var skillMd = Path.Combine(dir, "skill.md");
            if (File.Exists(skillMd))
            {
                try
                {
                    var meta = SimpleYamlParser.ParseSkillMetadataFromMarkdown(File.ReadAllText(skillMd));
                    results.Add(meta);
                    continue;
                }
                catch (InvalidDataException) { /* no frontmatter — fall through to legacy check */ }
            }

            // Legacy format: separate metadata.yaml
            var metaFile = Path.Combine(dir, "metadata.yaml");
            if (!File.Exists(metaFile))
                continue;

            try
            {
                var meta = SimpleYamlParser.ParseSkillMetadata(File.ReadAllText(metaFile));
                results.Add(meta);
            }
            catch (InvalidDataException)
            {
                // Skip malformed skills silently — registry may contain work-in-progress entries
            }
        }

        return results;
    }

    /// <summary>Resolves the local cache path for a given registry URL without fetching.</summary>
    public string GetCachePath(string registryUrl)
    {
        var slug = UrlToSlug(registryUrl);
        return Path.Combine(CacheRoot, slug);
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static string UrlToSlug(string url)
    {
        // Turn "https://github.com/org/repo" → "github.com_org_repo"
        var sanitized = url
            .Replace("https://", string.Empty)
            .Replace("http://", string.Empty)
            .Replace("git@", string.Empty)
            .Replace(':', '_')
            .Replace('/', '_')
            .TrimEnd('_');

        // Guard against path traversal
        foreach (var ch in Path.GetInvalidFileNameChars())
            sanitized = sanitized.Replace(ch, '_');

        return sanitized;
    }
}
