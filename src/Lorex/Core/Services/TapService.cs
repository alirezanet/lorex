using Lorex.Core.Models;
using Lorex.Core.Serialization;

namespace Lorex.Core.Services;

/// <summary>
/// Manages tap registrations and their local git caches at <c>~/.lorex/taps/&lt;slug&gt;/</c>.
/// A tap is a read-only skill source — any git repository containing skills.
/// </summary>
public sealed class TapService(GitService git)
{
    private const string SyncTimestampFileName = ".lorex-tap-synced-at";
    private static readonly TimeSpan DefaultCacheTtl = TimeSpan.FromHours(1);

    private static readonly string TapCacheRoot =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".lorex", "taps");

    // ── Add ───────────────────────────────────────────────────────────────────

    /// <summary>
    /// Shallow-clones <paramref name="url"/>, discovers skills, then records the tap in the project
    /// config. Returns the skills found. Throws if <paramref name="name"/> is already in use, the URL
    /// is already registered, or no skills are found.
    /// </summary>
    public IReadOnlyList<SkillMetadata> Add(
        string projectRoot,
        SkillService skillService,
        string url,
        string name,
        string? root)
    {
        var config = skillService.ReadConfig(projectRoot);

        var sameName = config.Taps.FirstOrDefault(t =>
            string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase));
        if (sameName is not null)
            throw new InvalidOperationException(
                $"A tap named '{name}' already exists (url: {sameName.Url}). " +
                $"Remove it first: lorex tap remove {name}");

        var sameUrl = config.Taps.FirstOrDefault(t =>
            string.Equals(NormalizeUrl(t.Url), NormalizeUrl(url), StringComparison.OrdinalIgnoreCase));
        if (sameUrl is not null)
            throw new InvalidOperationException(
                $"This URL is already registered as tap '{sameUrl.Name}'.");

        EnsureCache(url);

        var cachePath = GetTapCachePath(url);
        var skills = DiscoverSkills(cachePath, root);

        if (skills.Count == 0)
            throw new InvalidOperationException(
                $"No skills found in '{url}'. " +
                $"Ensure the repository contains directories with SKILL.md files. " +
                $"If skills are in a subdirectory use --root <path>.");

        var tap = new TapConfig { Name = name, Url = url, Root = root };
        skillService.WriteConfig(projectRoot, config with { Taps = [.. config.Taps, tap] });

        return skills;
    }

    // ── Remove ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Removes the tap from the project config. The global cache is left intact —
    /// other projects using the same tap URL are unaffected.
    /// </summary>
    public void Remove(string projectRoot, SkillService skillService, string name)
    {
        var config = skillService.ReadConfig(projectRoot);

        _ = config.Taps.FirstOrDefault(t =>
            string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException(
                $"No tap named '{name}'. Run 'lorex tap list' to see configured taps.");

        skillService.WriteConfig(projectRoot, config with
        {
            Taps = config.Taps
                .Where(t => !string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase))
                .ToArray(),
        });
    }

    // ── List ──────────────────────────────────────────────────────────────────

    /// <summary>Returns all configured taps with their cached skill counts and cache status.</summary>
    public IReadOnlyList<(TapConfig Tap, int SkillCount, bool IsCached)> List(LorexConfig config) =>
        config.Taps.Select(tap =>
        {
            var cachePath = GetTapCachePath(tap.Url);
            var isCached = Directory.Exists(Path.Combine(cachePath, ".git"));
            var count = isCached ? DiscoverSkills(cachePath, tap.Root).Count : 0;
            return (tap, count, isCached);
        }).ToList();

    // ── Sync ──────────────────────────────────────────────────────────────────

    /// <summary>Pulls the latest content for all taps. Returns the names of taps that were synced.</summary>
    public IReadOnlyList<string> SyncAll(LorexConfig config)
    {
        var updated = new List<string>();
        foreach (var tap in config.Taps)
        {
            var cachePath = GetTapCachePath(tap.Url);
            if (!Directory.Exists(Path.Combine(cachePath, ".git")))
                continue;

            SyncCacheRepository(cachePath, tap.Url);
            WriteSyncTimestamp(cachePath);
            updated.Add(tap.Name);
        }
        return updated;
    }

    /// <summary>Force-pulls the latest content for a single tap by name.</summary>
    public void SyncOne(LorexConfig config, string tapName)
    {
        var tap = config.Taps.FirstOrDefault(t =>
            string.Equals(t.Name, tapName, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException(
                $"No tap named '{tapName}'. Run 'lorex tap list' to see configured taps.");

        EnsureCache(tap.Url, forceRefresh: true);
    }

    // ── Skill discovery ───────────────────────────────────────────────────────

    /// <summary>Returns all skills discoverable in a tap's cached clone.</summary>
    public IReadOnlyList<SkillMetadata> ListTapSkills(TapConfig tap)
    {
        var cachePath = GetTapCachePath(tap.Url);
        if (!Directory.Exists(Path.Combine(cachePath, ".git")))
            return [];
        return DiscoverSkills(cachePath, tap.Root);
    }

    /// <summary>Finds a skill's directory path within the tap's cache. Returns null if not found.</summary>
    public string? FindSkillPath(TapConfig tap, string skillName)
    {
        var cachePath = GetTapCachePath(tap.Url);
        if (!Directory.Exists(Path.Combine(cachePath, ".git")))
            return null;

        var searchRoot = ResolveSkillsRoot(cachePath, tap.Root);
        if (!Directory.Exists(searchRoot)) return null;

        foreach (var dir in RegistryService.EnumerateSkillDirectories(searchRoot))
        {
            if (string.Equals(Path.GetFileName(dir), skillName, StringComparison.OrdinalIgnoreCase))
                return dir;

            var metaName = ReadSkillName(dir);
            if (metaName is not null && string.Equals(metaName, skillName, StringComparison.OrdinalIgnoreCase))
                return dir;
        }

        return null;
    }

    // ── Cache management ──────────────────────────────────────────────────────

    /// <summary>
    /// Ensures the tap repo is cloned and reasonably fresh (within <see cref="DefaultCacheTtl"/>).
    /// Returns the local cache path.
    /// </summary>
    public string EnsureCache(string url, bool forceRefresh = false)
    {
        var cachePath = GetTapCachePath(url);

        if (Directory.Exists(Path.Combine(cachePath, ".git")))
        {
            if (forceRefresh || IsCacheStale(cachePath))
            {
                SyncCacheRepository(cachePath, url);
                WriteSyncTimestamp(cachePath);
            }
        }
        else
        {
            Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);
            git.CloneShallow(url, cachePath);
            SyncCacheRepository(cachePath, url);
            WriteSyncTimestamp(cachePath);
        }

        return cachePath;
    }

    /// <summary>Returns the global cache path for a tap URL.</summary>
    public string GetTapCachePath(string url) =>
        Path.Combine(TapCacheRoot, UrlToSlug(url));

    // ── Private helpers ───────────────────────────────────────────────────────

    internal static IReadOnlyList<SkillMetadata> DiscoverSkills(string cachePath, string? root)
    {
        var searchRoot = ResolveSkillsRoot(cachePath, root);
        if (!Directory.Exists(searchRoot)) return [];

        var results = new List<SkillMetadata>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var dir in RegistryService.EnumerateSkillDirectories(searchRoot))
        {
            var entryPath = SkillFileConvention.ResolveEntryPath(dir);
            if (entryPath is null) continue;
            try
            {
                var meta = SimpleYamlParser.ParseSkillMetadataFromMarkdown(File.ReadAllText(entryPath));
                if (seen.Add(meta.Name)) results.Add(meta);
            }
            catch (InvalidDataException) { }
        }

        return results;
    }

    private static string ResolveSkillsRoot(string cachePath, string? root)
    {
        if (root is not null)
            return Path.Combine(cachePath, root);

        // Conventional: prefer <cache>/skills/ if it exists; otherwise search whole repo root.
        var skillsSubdir = Path.Combine(cachePath, "skills");
        return Directory.Exists(skillsSubdir) ? skillsSubdir : cachePath;
    }

    private void SyncCacheRepository(string cacheDir, string url)
    {
        git.FetchPrune(cacheDir, "origin");
        git.UpdateRemoteHead(cacheDir, "origin");

        var defaultBranch = git.GetRemoteDefaultBranchFromUrl(url)
            ?? git.GetRemoteDefaultBranch(cacheDir, "origin");

        if (string.IsNullOrWhiteSpace(defaultBranch))
        {
            var branches = git.GetRemoteBranchNames(cacheDir, "origin");
            if (branches.Count == 0) return;
            defaultBranch = branches.Contains("main", StringComparer.Ordinal) ? "main"
                : branches.Contains("master", StringComparer.Ordinal) ? "master"
                : branches[0];
        }

        git.FetchBranchToRemoteTracking(cacheDir, "origin", defaultBranch);
        git.CheckoutResetToRemoteBranch(cacheDir, "origin", defaultBranch);
    }

    private static bool IsCacheStale(string cacheDir)
    {
        var stampPath = Path.Combine(cacheDir, SyncTimestampFileName);
        if (!File.Exists(stampPath)) return true;
        try { return DateTime.UtcNow - File.GetLastWriteTimeUtc(stampPath) > DefaultCacheTtl; }
        catch { return true; }
    }

    private static void WriteSyncTimestamp(string cacheDir)
    {
        try { File.WriteAllText(Path.Combine(cacheDir, SyncTimestampFileName), DateTime.UtcNow.ToString("O")); }
        catch { /* best-effort */ }
    }

    private static string? ReadSkillName(string skillDirectory)
    {
        try
        {
            var entryPath = SkillFileConvention.ResolveEntryPath(skillDirectory);
            if (entryPath is null) return null;
            var meta = SimpleYamlParser.ParseSkillMetadataFromMarkdown(File.ReadAllText(entryPath));
            return string.IsNullOrWhiteSpace(meta.Name) ? null : meta.Name;
        }
        catch { return null; }
    }

    private static string NormalizeUrl(string url) =>
        url.TrimEnd('/').ToLowerInvariant()
           .Replace(".git", string.Empty, StringComparison.OrdinalIgnoreCase);

    private static string UrlToSlug(string url)
    {
        var sanitized = url
            .Replace("https://", string.Empty)
            .Replace("http://", string.Empty)
            .Replace("git@", string.Empty)
            .Replace(':', '_')
            .Replace('/', '_')
            .TrimEnd('_');

        foreach (var ch in Path.GetInvalidFileNameChars())
            sanitized = sanitized.Replace(ch, '_');

        return sanitized;
    }
}
