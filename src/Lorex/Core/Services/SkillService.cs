using System.Text.Json;
using Lorex.Core.Models;
using Lorex.Core.Serialization;

namespace Lorex.Core.Services;

/// <summary>
/// Handles all skill lifecycle operations for a project: install, uninstall, sync, scaffold, and publish.
/// Works with the <see cref="RegistryService"/> cache and reads/writes <c>.lorex/lorex.json</c>.
/// </summary>
public sealed class SkillService(RegistryService registry)
{
    private const string LorexDir = ".lorex";
    private const string ConfigFile = "lorex.json";

    // ── Config I/O ────────────────────────────────────────────────────────────

    public LorexConfig ReadConfig(string projectRoot)
    {
        var path = ConfigPath(projectRoot);
        if (!File.Exists(path))
            throw new FileNotFoundException($"lorex is not initialised in this directory. Run `lorex init` first.", path);

        var json = File.ReadAllText(path);
        var config = JsonSerializer.Deserialize(json, LorexJsonContext.Default.LorexConfig)
            ?? throw new InvalidDataException("lorex.json is empty or invalid.");

        // Normalize: source-generated deserializers may not apply init-property defaults
        // for fields absent from the JSON (they stay at their CLR default, i.e. null).
        return config with
        {
            Adapters              = config.Adapters              ?? [],
            InstalledSkills       = config.InstalledSkills       ?? [],
            InstalledSkillVersions= config.InstalledSkillVersions?? [],
            Taps                  = config.Taps                  ?? [],
            InstalledSkillSources = config.InstalledSkillSources ?? [],
        };
    }

    public void WriteConfig(string projectRoot, LorexConfig config)
    {
        Directory.CreateDirectory(Path.Combine(projectRoot, LorexDir));
        var json = JsonSerializer.Serialize(config, LorexJsonContext.Default.LorexConfig);
        File.WriteAllText(ConfigPath(projectRoot), json);
    }

    // ── Global config ─────────────────────────────────────────────────────────

    private static readonly string GlobalConfigPath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".lorex", "config.json");

    public GlobalConfig ReadGlobalConfig()
    {
        if (!File.Exists(GlobalConfigPath)) return new GlobalConfig();
        try
        {
            var json = File.ReadAllText(GlobalConfigPath);
            var config = JsonSerializer.Deserialize(json, LorexJsonContext.Default.GlobalConfig) ?? new GlobalConfig();
            return config with { Registries = config.Registries ?? [] };
        }
        catch { return new GlobalConfig(); }
    }

    public void SaveGlobalRegistry(string registryUrl)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(GlobalConfigPath)!);
        var existing = ReadGlobalConfig().Registries;
        // Upsert: move to front (MRU), deduplicate, keep up to 20 entries
        var updated = new[] { registryUrl }
            .Concat(existing.Where(r => !r.Equals(registryUrl, StringComparison.OrdinalIgnoreCase)))
            .Take(20)
            .ToArray();
        var config = new GlobalConfig { Registries = updated };
        File.WriteAllText(GlobalConfigPath, JsonSerializer.Serialize(config, LorexJsonContext.Default.GlobalConfig));
    }

    public LorexConfig RefreshRegistryPolicy(string projectRoot, bool refreshRegistry = true, bool forceRefresh = false)
    {
        var config = ReadConfig(projectRoot);
        if (config.Registry is null)
            return config;

        var policy = registry.ReadRegistryPolicy(config.Registry.Url, refreshRegistry, forceRefresh)
            ?? throw new InvalidOperationException(
                $"Registry '{config.Registry.Url}' is missing {RegistryService.RegistryManifestFileName}.");

        if (config.Registry.Policy == policy)
            return config;

        var updated = config with
        {
            Registry = config.Registry with { Policy = policy }
        };

        WriteConfig(projectRoot, updated);
        return updated;
    }

    // ── Version helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Returns the version for an installed skill: uses the cached value from <paramref name="config"/>
    /// when available, otherwise reads it live from the skill file. Returns <c>"?"</c> if neither source
    /// has a version.
    /// </summary>
    public string GetInstalledSkillVersion(string projectRoot, string skillName, LorexConfig config)
    {
        if (config.InstalledSkillVersions.TryGetValue(skillName, out var cached) && !string.IsNullOrEmpty(cached))
            return cached;

        return ReadSkillVersion(SkillDir(projectRoot, skillName)) ?? "?";
    }

    /// <summary>
    /// Reads the version string from a skill directory's entry file. Returns null on any failure.
    /// </summary>
    private static string? ReadSkillVersion(string skillDir)
    {
        var path = SkillFileConvention.ResolveEntryPath(skillDir);
        if (path is null) return null;
        try { return SimpleYamlParser.ParseSkillMetadataFromMarkdown(File.ReadAllText(path)).Version; }
        catch { return null; }
    }

    /// <summary>
    /// Reads the version from each skill directory and persists the results into
    /// <c>InstalledSkillVersions</c> in a single <c>lorex.json</c> write.
    /// Used by <c>lorex init</c> to back-fill versions for built-in and discovered skills.
    /// </summary>
    public void TrackInstalledVersions(string projectRoot, IEnumerable<string> skillNames)
    {
        var config = ReadConfig(projectRoot);
        var versions = new Dictionary<string, string>(config.InstalledSkillVersions, StringComparer.OrdinalIgnoreCase);

        foreach (var name in skillNames)
        {
            var version = ReadSkillVersion(SkillDir(projectRoot, name));
            if (version is not null)
                versions[name] = version;
        }

        WriteConfig(projectRoot, config with { InstalledSkillVersions = versions });
    }

    // ── Uninstall ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Removes a skill directory or symlink from .lorex/skills and records the removal in lorex.json.
    /// Does not touch the registry cache.
    /// </summary>
    public void UninstallSkill(string projectRoot, string skillName)
    {
        var linkPath = SkillDir(projectRoot, skillName);
        if (Directory.Exists(linkPath))
            Directory.Delete(linkPath, recursive: true);

        var config = ReadConfig(projectRoot);
        var versions = new Dictionary<string, string>(config.InstalledSkillVersions, StringComparer.OrdinalIgnoreCase);
        versions.Remove(skillName);
        var sources = new Dictionary<string, string>(config.InstalledSkillSources, StringComparer.OrdinalIgnoreCase);
        sources.Remove(skillName);
        WriteConfig(projectRoot, config with
        {
            InstalledSkills = config.InstalledSkills
                .Where(s => !s.Equals(skillName, StringComparison.OrdinalIgnoreCase))
                .ToArray(),
            InstalledSkillVersions = versions,
            InstalledSkillSources  = sources,
        });
    }

    // ── Install ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a symlink .lorex/skills/&lt;name&gt; → registry cache path and records the skill in lorex.json.
    /// Symlinks are required for registry installs.
    /// </summary>
    public void InstallSkill(string projectRoot, string skillName, bool overwriteLocalSkill = false, bool refreshRegistry = true)
    {
        var config = ReadConfig(projectRoot);
        if (config.Registry is null)
            throw new InvalidOperationException("No registry configured. Run `lorex init <url>` to connect a registry.");
        var sourcePath = registry.FindSkillPath(config.Registry.Url, skillName, refresh: refreshRegistry)
            ?? throw new InvalidOperationException($"Skill '{skillName}' not found in registry '{config.Registry.Url}'.");

        var linkPath = SkillDir(projectRoot, skillName);
        Directory.CreateDirectory(Path.Combine(projectRoot, LorexDir, "skills"));

        // Remove any existing link or directory at the target
        if (Directory.Exists(linkPath))
        {
            if (!IsSymlink(linkPath) && !overwriteLocalSkill)
            {
                throw new InvalidOperationException(
                    $"Skill '{skillName}' already exists locally at '{linkPath}'. Overwrite requires direct user approval.");
            }

            Directory.Delete(linkPath, recursive: true);
        }

        if (!TryCreateSymlink(linkPath, sourcePath))
        {
            throw new InvalidOperationException(
                "Lorex requires symlink support for installed registry skills. Enable symlinks and try again.");
        }

        config = ReadConfig(projectRoot);
        var versions = new Dictionary<string, string>(config.InstalledSkillVersions, StringComparer.OrdinalIgnoreCase);
        var version = ReadSkillVersion(linkPath);
        if (version is not null)
            versions[skillName] = version;

        WriteConfig(projectRoot, config with
        {
            InstalledSkills = config.InstalledSkills.Contains(skillName, StringComparer.OrdinalIgnoreCase)
                ? config.InstalledSkills
                : [.. config.InstalledSkills, skillName],
            InstalledSkillVersions = versions,
        });
    }

    /// <summary>
    /// Installs multiple skills in parallel. The registry cache is refreshed once upfront,
    /// a name→path index is built once, symlinks are created concurrently, and <c>lorex.json</c>
    /// is written once at the end.
    /// </summary>
    public IReadOnlyList<string> InstallSkillsBatch(
        string projectRoot,
        IReadOnlyList<string> skillNames,
        Func<string, bool>? shouldOverwrite = null)
    {
        var config = ReadConfig(projectRoot);
        if (config.Registry is null)
            throw new InvalidOperationException("No registry configured. Run `lorex init <url>` to connect a registry.");

        // Single upfront cache refresh + build path index once
        registry.EnsureCache(config.Registry.Url);
        var pathIndex = registry.BuildSkillPathIndex(config.Registry.Url, refresh: false);

        var skillsDir = Path.Combine(projectRoot, LorexDir, "skills");
        Directory.CreateDirectory(skillsDir);

        var installed = new System.Collections.Concurrent.ConcurrentBag<string>();
        var errors = new System.Collections.Concurrent.ConcurrentBag<(string SkillName, Exception Error)>();

        Parallel.ForEach(skillNames, skillName =>
        {
            try
            {
                if (!pathIndex.TryGetValue(skillName, out var sourcePath))
                    throw new InvalidOperationException($"Skill '{skillName}' not found in registry '{config.Registry.Url}'.");

                var linkPath = SkillDir(projectRoot, skillName);

                if (Directory.Exists(linkPath))
                {
                    var overwrite = shouldOverwrite?.Invoke(skillName) ?? false;
                    if (!IsSymlink(linkPath) && !overwrite)
                        return; // skip — local skill, not approved for overwrite

                    Directory.Delete(linkPath, recursive: true);
                }

                if (!TryCreateSymlink(linkPath, sourcePath))
                {
                    throw new InvalidOperationException(
                        "Lorex requires symlink support for installed registry skills. Enable symlinks and try again.");
                }

                installed.Add(skillName);
            }
            catch (Exception ex)
            {
                errors.Add((skillName, ex));
            }
        });

        if (errors.Count > 0)
        {
            var first = errors.First();
            throw new InvalidOperationException(
                $"Failed to install skill '{first.SkillName}': {first.Error.Message}", first.Error);
        }

        // Single config write at the end
        if (installed.Count > 0)
        {
            config = ReadConfig(projectRoot); // re-read in case of concurrent changes
            var allInstalled = config.InstalledSkills
                .Concat(installed)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var versions = new Dictionary<string, string>(config.InstalledSkillVersions, StringComparer.OrdinalIgnoreCase);
            foreach (var name in installed)
            {
                var v = ReadSkillVersion(SkillDir(projectRoot, name));
                if (v is not null) versions[name] = v;
            }

            WriteConfig(projectRoot, config with { InstalledSkills = allInstalled, InstalledSkillVersions = versions });
        }

        return [.. installed];
    }

    // ── Tap installs ──────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a symlink <c>.lorex/skills/&lt;name&gt;</c> → tap cache path, records the skill and
    /// its tap source in <c>lorex.json</c>.
    /// </summary>
    public void InstallSkillFromTap(
        string     projectRoot,
        string     skillName,
        Core.Models.TapConfig tap,
        string     sourcePath,
        bool       overwriteLocalSkill = false)
    {
        var linkPath = SkillDir(projectRoot, skillName);
        Directory.CreateDirectory(Path.Combine(projectRoot, LorexDir, "skills"));

        if (Directory.Exists(linkPath))
        {
            if (!IsSymlink(linkPath) && !overwriteLocalSkill)
                throw new InvalidOperationException(
                    $"Skill '{skillName}' already exists locally at '{linkPath}'. Overwrite requires direct user approval.");
            Directory.Delete(linkPath, recursive: true);
        }

        if (!TryCreateSymlink(linkPath, sourcePath))
            throw new InvalidOperationException(
                "Lorex requires symlink support for installed tap skills. Enable symlinks and try again.");

        var config = ReadConfig(projectRoot);
        var versions = new Dictionary<string, string>(config.InstalledSkillVersions, StringComparer.OrdinalIgnoreCase);
        var version = ReadSkillVersion(linkPath);
        if (version is not null) versions[skillName] = version;

        var sources = new Dictionary<string, string>(config.InstalledSkillSources, StringComparer.OrdinalIgnoreCase);
        sources[skillName] = $"tap:{tap.Name}";

        WriteConfig(projectRoot, config with
        {
            InstalledSkills = config.InstalledSkills.Contains(skillName, StringComparer.OrdinalIgnoreCase)
                ? config.InstalledSkills
                : [.. config.InstalledSkills, skillName],
            InstalledSkillVersions = versions,
            InstalledSkillSources  = sources,
        });
    }

    /// <summary>
    /// Installs a batch of tap skills efficiently (one config write at the end).
    /// <paramref name="skillTapPaths"/> maps skill name → (TapConfig, source path in cache).
    /// </summary>
    public IReadOnlyList<string> InstallTapSkillsBatch(
        string projectRoot,
        IReadOnlyDictionary<string, (Core.Models.TapConfig Tap, string SourcePath)> skillTapPaths,
        Func<string, bool>? shouldOverwrite = null)
    {
        var skillsDir = Path.Combine(projectRoot, LorexDir, "skills");
        Directory.CreateDirectory(skillsDir);

        var installed = new List<string>();
        var errors    = new System.Collections.Concurrent.ConcurrentBag<(string, Exception)>();

        foreach (var (skillName, (tap, sourcePath)) in skillTapPaths)
        {
            try
            {
                var linkPath = SkillDir(projectRoot, skillName);
                if (Directory.Exists(linkPath))
                {
                    if (!IsSymlink(linkPath) && !(shouldOverwrite?.Invoke(skillName) ?? false))
                        continue;
                    Directory.Delete(linkPath, recursive: true);
                }

                if (!TryCreateSymlink(linkPath, sourcePath))
                    throw new InvalidOperationException(
                        "Lorex requires symlink support for installed tap skills. Enable symlinks and try again.");

                installed.Add(skillName);
            }
            catch (Exception ex)
            {
                errors.Add((skillName, ex));
            }
        }

        if (errors.Count > 0)
        {
            var first = errors.First();
            throw new InvalidOperationException(
                $"Failed to install tap skill '{first.Item1}': {first.Item2.Message}", first.Item2);
        }

        if (installed.Count > 0)
        {
            var config = ReadConfig(projectRoot);
            var allInstalled = config.InstalledSkills
                .Concat(installed)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var versions = new Dictionary<string, string>(config.InstalledSkillVersions, StringComparer.OrdinalIgnoreCase);
            var sources  = new Dictionary<string, string>(config.InstalledSkillSources,  StringComparer.OrdinalIgnoreCase);

            foreach (var name in installed)
            {
                var v = ReadSkillVersion(SkillDir(projectRoot, name));
                if (v is not null) versions[name] = v;
                var tap = skillTapPaths[name].Tap;
                sources[name] = $"tap:{tap.Name}";
            }

            WriteConfig(projectRoot, config with
            {
                InstalledSkills        = allInstalled,
                InstalledSkillVersions = versions,
                InstalledSkillSources  = sources,
            });
        }

        return installed;
    }

    // ── Direct URL install ────────────────────────────────────────────────────

    /// <summary>
    /// Installs a skill directly from a git URL without requiring a registered tap.
    /// Supports GitHub tree URLs (<c>https://github.com/owner/repo/tree/branch/path</c>) and plain
    /// repo URLs (single-skill repos only). The skill is copied — not symlinked — and its source URL
    /// is recorded so <c>lorex status</c> can display it.
    /// Returns the installed skill name.
    /// </summary>
    public string InstallSkillFromUrl(string projectRoot, string url, GitService git)
    {
        var (repoUrl, skillPath) = ParseGitHubSkillUrl(url);

        var tempDir = Path.Combine(Path.GetTempPath(), $"lorex-url-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(tempDir);
            git.CloneShallow(repoUrl, tempDir);

            string skillSourceDir;
            string skillName;

            if (skillPath is not null)
            {
                skillSourceDir = Path.GetFullPath(
                    Path.Combine(tempDir, skillPath.Replace('/', Path.DirectorySeparatorChar)));

                // Guard against path traversal
                if (!skillSourceDir.StartsWith(
                        Path.GetFullPath(tempDir) + Path.DirectorySeparatorChar,
                        StringComparison.OrdinalIgnoreCase)
                    && !skillSourceDir.Equals(Path.GetFullPath(tempDir), StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Invalid skill path.");

                if (!Directory.Exists(skillSourceDir))
                    throw new InvalidOperationException(
                        $"Path '{skillPath}' not found in repository '{repoUrl}'.");

                var entryPath = SkillFileConvention.ResolveEntryPath(skillSourceDir)
                    ?? throw new InvalidOperationException(
                        $"No SKILL.md found at '{skillPath}' in repository '{repoUrl}'.");

                var meta = SimpleYamlParser.ParseSkillMetadataFromMarkdown(File.ReadAllText(entryPath));
                skillName = string.IsNullOrWhiteSpace(meta.Name)
                    ? Path.GetFileName(skillSourceDir)
                    : meta.Name;
            }
            else
            {
                // Auto-discover skills in the repo
                var searchRoot = Directory.Exists(Path.Combine(tempDir, "skills"))
                    ? Path.Combine(tempDir, "skills")
                    : tempDir;

                var discovered = RegistryService.EnumerateSkillDirectories(searchRoot).ToList();

                if (discovered.Count == 0)
                    throw new InvalidOperationException(
                        $"No skills found in '{repoUrl}'. " +
                        $"Specify a skill path (e.g. {url}/tree/main/skill-name) " +
                        $"or use 'lorex tap add' to browse all skills in the repository.");

                if (discovered.Count > 1)
                {
                    var names = discovered.Select(d => Path.GetFileName(d) ?? d);
                    throw new InvalidOperationException(
                        $"Multiple skills found in '{repoUrl}': {string.Join(", ", names)}. " +
                        $"Specify one: lorex install {url}/tree/main/<skill-name>, " +
                        $"or use 'lorex tap add' to browse all.");
                }

                skillSourceDir = discovered[0];
                var ep = SkillFileConvention.ResolveEntryPath(skillSourceDir)!;
                var meta = SimpleYamlParser.ParseSkillMetadataFromMarkdown(File.ReadAllText(ep));
                skillName = string.IsNullOrWhiteSpace(meta.Name)
                    ? Path.GetFileName(skillSourceDir) ?? "skill"
                    : meta.Name;
            }

            // Copy skill files to .lorex/skills/<name>/
            Directory.CreateDirectory(Path.Combine(projectRoot, LorexDir, "skills"));
            var destDir = SkillDir(projectRoot, skillName);

            if (Directory.Exists(destDir))
            {
                if (IsSymlink(destDir))
                    Directory.Delete(destDir, recursive: true);
                else
                    throw new InvalidOperationException(
                        $"Skill '{skillName}' already exists locally at '{destDir}'. " +
                        $"Remove it first: lorex uninstall {skillName}");
            }

            CopySkillFolder(skillSourceDir, destDir);

            var config = ReadConfig(projectRoot);
            var versions = new Dictionary<string, string>(config.InstalledSkillVersions, StringComparer.OrdinalIgnoreCase);
            var v = ReadSkillVersion(destDir);
            if (v is not null) versions[skillName] = v;

            var sources = new Dictionary<string, string>(config.InstalledSkillSources, StringComparer.OrdinalIgnoreCase);
            sources[skillName] = $"url:{url}";

            WriteConfig(projectRoot, config with
            {
                InstalledSkills = config.InstalledSkills.Contains(skillName, StringComparer.OrdinalIgnoreCase)
                    ? config.InstalledSkills
                    : [.. config.InstalledSkills, skillName],
                InstalledSkillVersions = versions,
                InstalledSkillSources  = sources,
            });

            return skillName;
        }
        finally
        {
            try { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true); }
            catch { /* best-effort cleanup */ }
        }
    }

    /// <summary>
    /// Parses a GitHub URL into a (repoUrl, skillPath?) tuple.
    /// Handles <c>https://github.com/owner/repo/tree/branch/path</c> and plain repo URLs.
    /// </summary>
    internal static (string RepoUrl, string? SkillPath) ParseGitHubSkillUrl(string url)
    {
        url = url.TrimEnd('/');

        // Normalise SSH → HTTPS
        if (url.StartsWith("git@github.com:", StringComparison.OrdinalIgnoreCase))
            url = "https://github.com/" + url["git@github.com:".Length..];

        // GitHub tree URL: https://github.com/owner/repo/tree/branch/path
        var treeIdx = url.IndexOf("/tree/", StringComparison.OrdinalIgnoreCase);
        if (treeIdx >= 0)
        {
            var repoUrl   = url[..treeIdx];
            var afterTree = url[(treeIdx + "/tree/".Length)..];
            var slashIdx  = afterTree.IndexOf('/');
            if (slashIdx >= 0)
            {
                var path = afterTree[(slashIdx + 1)..];
                return (repoUrl, string.IsNullOrWhiteSpace(path) ? null : path);
            }
            return (repoUrl, null);
        }

        return (url, null);
    }

    // ── Sync ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Pulls the registry cache so all symlinked skills automatically reflect updates.
    /// Returns skill names that were updated.
    /// </summary>
    public IReadOnlyList<string> SyncSkills(
        string projectRoot,
        IReadOnlyCollection<string>? approvedOverwriteSkillNames = null,
        bool refreshRegistry = true)
    {
        var config = ReadConfig(projectRoot);
        var approved = approvedOverwriteSkillNames?.ToHashSet(StringComparer.OrdinalIgnoreCase)
            ?? [];

        // Pull the registry cache — symlinks automatically point to the fresh content
        if (config.Registry is null) return [];
        if (refreshRegistry)
            registry.EnsureCache(config.Registry.Url);

        var updated = new List<string>();

        foreach (var skillName in config.InstalledSkills)
        {
            var linkPath = SkillDir(projectRoot, skillName);

            if (IsSymlink(linkPath))
            {
                var target = new DirectoryInfo(linkPath).LinkTarget;
                if (target is not null && Directory.Exists(target))
                {
                    updated.Add(skillName);
                }
                else
                {
                    // Target vanished — reinstall using the already-synced cache
                    InstallSkill(projectRoot, skillName, refreshRegistry: false);
                    updated.Add(skillName);
                }
                continue;
            }

            var sourcePath = registry.FindSkillPath(config.Registry.Url, skillName, refresh: false);
            if (sourcePath is null) continue;

            if (!approved.Contains(skillName))
                continue;

            InstallSkill(projectRoot, skillName, overwriteLocalSkill: true, refreshRegistry: false);
            updated.Add(skillName);
        }

        // Refresh stored versions for every updated skill in one write
        if (updated.Count > 0)
        {
            config = ReadConfig(projectRoot);
            var versions = new Dictionary<string, string>(config.InstalledSkillVersions, StringComparer.OrdinalIgnoreCase);
            foreach (var name in updated)
            {
                var v = ReadSkillVersion(SkillDir(projectRoot, name));
                if (v is not null) versions[name] = v;
            }
            WriteConfig(projectRoot, config with { InstalledSkillVersions = versions });
        }

        return updated;
    }

    // ── Publish ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Publishes a local skill from .lorex/skills/&lt;name&gt; to the registry, then converts the real
    /// directory into a symlink pointing at the registry cache.
    /// </summary>
    public PublishResult PublishSkill(string projectRoot, string skillName, GitService git)
    {
        var config = ReadConfig(projectRoot);
        if (config.Registry is null)
            throw new InvalidOperationException(
                "No registry configured. Run `lorex init <url>` to connect a registry before publishing.");
        var localPath = SkillDir(projectRoot, skillName);

        if (!Directory.Exists(localPath) || IsSymlink(localPath))
            throw new InvalidOperationException(
                $"Skill '{skillName}' not found locally or is already a registry-linked skill.");

        return config.Registry.Policy.PublishMode switch
        {
            var mode when string.Equals(mode, RegistryPublishModes.Direct, StringComparison.OrdinalIgnoreCase)
                => PublishSkillDirect(projectRoot, skillName, localPath, config.Registry, git),
            var mode when string.Equals(mode, RegistryPublishModes.PullRequest, StringComparison.OrdinalIgnoreCase)
                => PublishSkillViaPullRequest(skillName, localPath, config.Registry, git),
            var mode when string.Equals(mode, RegistryPublishModes.ReadOnly, StringComparison.OrdinalIgnoreCase)
                => throw new InvalidOperationException(
                    $"Registry '{config.Registry.Url}' is read-only. Publishing is disabled by {RegistryService.RegistryManifestFileName}."),
            _ => throw new InvalidOperationException(
                $"Registry '{config.Registry.Url}' has unsupported publish mode '{config.Registry.Policy.PublishMode}'.")
        };
    }

    // ── Scaffold ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Scaffolds a new skill directly into .lorex/skills/&lt;name&gt; as a local (non-registry) skill.
    /// The user can author it there and later run `lorex publish` to push it to the registry.
    /// </summary>
    public void ScaffoldSkill(string projectRoot, string name, string description, string[] tags, string owner)
    {
        var dir = SkillDir(projectRoot, name);
        Directory.CreateDirectory(dir);

        var skillPath = SkillFileConvention.CanonicalPath(dir);

        if (SkillFileConvention.ResolveEntryPath(dir) is not null)
            throw new InvalidOperationException($"Skill '{name}' already exists at {dir}");

        File.WriteAllText(skillPath, BuildSkillWithFrontmatter(name, description, tags, owner));

        // Register as installed so it appears in the agent index immediately
        var config = ReadConfig(projectRoot);
        if (!config.InstalledSkills.Contains(name, StringComparer.OrdinalIgnoreCase))
        {
            WriteConfig(projectRoot, config with
            {
                InstalledSkills = [.. config.InstalledSkills, name]
            });
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    public string SkillDir(string projectRoot, string skillName) =>
        Path.Combine(projectRoot, LorexDir, "skills", skillName);

    internal static string BuildPublishBranchName(RegistryPolicy policy, string skillName) =>
        $"{policy.PrBranchPrefix}{SanitizeBranchSegment(skillName)}-{DateTime.UtcNow:yyyyMMddHHmmss}";

    internal static string SanitizeBranchSegment(string value)
    {
        var chars = value
            .Select(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' ? char.ToLowerInvariant(ch) : '-')
            .ToArray();

        var collapsed = new string(chars);
        while (collapsed.Contains("--", StringComparison.Ordinal))
            collapsed = collapsed.Replace("--", "-", StringComparison.Ordinal);

        return collapsed.Trim('-');
    }

    /// <summary>
    /// Returns all skill directory names currently present under <c>.lorex/skills</c>.
    /// </summary>
    public IReadOnlyList<string> DiscoverInstalledSkillNames(string projectRoot)
    {
        var skillsDir = Path.Combine(projectRoot, LorexDir, "skills");
        if (!Directory.Exists(skillsDir))
            return [];

        return [.. Directory.EnumerateDirectories(skillsDir)
            .Select(Path.GetFileName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)!];
    }

    /// <summary>Returns skill names in .lorex/skills/ that are real directories (not symlinks), i.e. locally authored, unpublished skills.
    /// Built-in skills (embedded in the binary) are excluded.</summary>
    public IEnumerable<string> LocalOnlySkills(string projectRoot)
    {
        var skillsDir = Path.Combine(projectRoot, LorexDir, "skills");
        if (!Directory.Exists(skillsDir)) yield break;
        var builtIns = BuiltInSkillService.SkillNames().ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var dir in Directory.EnumerateDirectories(skillsDir))
        {
            var name = Path.GetFileName(dir);
            if (new DirectoryInfo(dir).LinkTarget is null && !builtIns.Contains(name))
                yield return name;
        }
    }

    public bool RequiresOverwriteApproval(string projectRoot, string skillName)
    {
        var path = SkillDir(projectRoot, skillName);
        return Directory.Exists(path) && !IsSymlink(path);
    }

    private PublishResult PublishSkillDirect(string projectRoot, string skillName, string localPath, RegistryConfig registryConfig, GitService git)
    {
        var cacheDir = registry.GetCachePath(registryConfig.Url);
        if (!Directory.Exists(Path.Combine(cacheDir, ".git")))
            throw new InvalidOperationException(
                "Registry is not cached locally. Run `lorex sync` first.");

        // Pull latest before publishing to reduce merge conflicts
        registry.EnsureCache(registryConfig.Url);

        var destination = Path.Combine(cacheDir, "skills", skillName);
        CopySkillFolder(localPath, destination);

        git.AddAll(cacheDir);
        git.Commit(cacheDir, $"feat: publish skill '{skillName}'");
        git.Push(cacheDir);

        // Replace the local copy with a symlink to the registry cache
        Directory.Delete(localPath, recursive: true);
        InstallSkill(projectRoot, skillName);

        return new PublishResult
        {
            SkillName = skillName,
            PublishMode = RegistryPublishModes.Direct,
        };
    }

    private PublishResult PublishSkillViaPullRequest(string skillName, string localPath, RegistryConfig registryConfig, GitService git)
    {
        var cacheDir = registry.EnsureCache(registryConfig.Url);
        var policy = registryConfig.Policy;
        try
        {
            git.FetchBranchToRemoteTracking(cacheDir, "origin", policy.BaseBranch);
        }
        catch (GitException ex)
        {
            throw new InvalidOperationException(
                $"Base branch '{policy.BaseBranch}' was not found in registry '{registryConfig.Url}'. " +
                $"Update {RegistryService.RegistryManifestFileName} or create the branch first. Details: {ex.Message}");
        }

        git.CheckoutResetToRemoteBranch(cacheDir, "origin", policy.BaseBranch);

        var branchName = BuildPublishBranchName(policy, skillName);
        var worktreeDir = Path.Combine(registry.GetWorktreeRoot(registryConfig.Url), branchName.Replace('/', Path.DirectorySeparatorChar));

        try
        {
            git.WorktreeAdd(cacheDir, worktreeDir, branchName, policy.BaseBranch);
            var destination = Path.Combine(worktreeDir, "skills", skillName);
            CopySkillFolder(localPath, destination);

            if (!git.HasChanges(worktreeDir))
                throw new InvalidOperationException($"Skill '{skillName}' has no changes to publish.");

            git.AddAll(worktreeDir);
            git.Commit(worktreeDir, $"feat: publish skill '{skillName}'");
            git.PushSetUpstream(worktreeDir, "origin", branchName);
        }
        finally
        {
            try
            {
                if (Directory.Exists(worktreeDir))
                    git.WorktreeRemove(cacheDir, worktreeDir);
            }
            catch
            {
                // Best-effort cleanup only; the pushed branch remains on the remote either way.
            }
        }

        return new PublishResult
        {
            SkillName = skillName,
            PublishMode = RegistryPublishModes.PullRequest,
            BranchName = branchName,
            BaseBranch = policy.BaseBranch,
            PullRequestUrl = registry.BuildPullRequestUrl(registryConfig.Url, branchName, policy.BaseBranch),
        };
    }

    private static string ConfigPath(string projectRoot) =>
        Path.Combine(projectRoot, LorexDir, ConfigFile);

    /// <summary>
    /// Attempts to create a directory symlink. Returns true on success, false if not permitted.
    /// </summary>
    private static bool TryCreateSymlink(string linkPath, string targetPath)
    {
        try
        {
            Directory.CreateSymbolicLink(linkPath, targetPath);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static bool IsSymlink(string path) =>
        Directory.Exists(path) &&
        new DirectoryInfo(path).LinkTarget is not null;

    private static void CopySkillFolder(string source, string destination)
    {
        if (Directory.Exists(destination))
            Directory.Delete(destination, recursive: true);
        Directory.CreateDirectory(destination);

        foreach (var file in Directory.EnumerateFiles(source))
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), overwrite: true);

        foreach (var dir in Directory.EnumerateDirectories(source))
            CopySkillFolder(dir, Path.Combine(destination, Path.GetFileName(dir)));
    }

    private static string BuildSkillWithFrontmatter(string name, string description, string[] tags, string owner) =>
        $"""
        ---
        name: {name}
        description: {description}
        version: 1.0.0
        tags: {string.Join(", ", tags)}
        owner: {owner}
        ---

        # {name}

        > {description}

        <!-- Author this skill using your AI coding agent. -->
        <!-- Describe architecture, constraints, flows, patterns, pitfalls. -->

        """;
}

