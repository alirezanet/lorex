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
        return JsonSerializer.Deserialize(json, LorexJsonContext.Default.LorexConfig)
            ?? throw new InvalidDataException("lorex.json is empty or invalid.");
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
            return JsonSerializer.Deserialize(json, LorexJsonContext.Default.GlobalConfig) ?? new GlobalConfig();
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

    public LorexConfig RefreshRegistryPolicy(string projectRoot, bool refreshRegistry = true)
    {
        var config = ReadConfig(projectRoot);
        if (config.Registry is null)
            return config;

        var policy = registry.ReadRegistryPolicy(config.Registry.Url, refreshRegistry)
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
        WriteConfig(projectRoot, config with
        {
            InstalledSkills = config.InstalledSkills
                .Where(s => !s.Equals(skillName, StringComparison.OrdinalIgnoreCase))
                .ToArray()
        });
    }

    // ── Install ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a symlink .lorex/skills/&lt;name&gt; → registry cache path and records the skill in lorex.json.
    /// Symlinks are required for registry installs.
    /// </summary>
    public void InstallSkill(string projectRoot, string skillName)
    {
        var config = ReadConfig(projectRoot);
        if (config.Registry is null)
            throw new InvalidOperationException("No registry configured. Run `lorex init <url>` to connect a registry.");
        var sourcePath = registry.FindSkillPath(config.Registry.Url, skillName)
            ?? throw new InvalidOperationException($"Skill '{skillName}' not found in registry '{config.Registry.Url}'.");

        var linkPath = SkillDir(projectRoot, skillName);
        Directory.CreateDirectory(Path.Combine(projectRoot, LorexDir, "skills"));

        // Remove any existing link or directory at the target
        if (Directory.Exists(linkPath))
            Directory.Delete(linkPath, recursive: true);

        if (!TryCreateSymlink(linkPath, sourcePath))
        {
            throw new InvalidOperationException(
                "Lorex requires symlink support for installed registry skills. Enable symlinks and try again.");
        }

        if (!config.InstalledSkills.Contains(skillName, StringComparer.OrdinalIgnoreCase))
        {
            WriteConfig(projectRoot, config with
            {
                InstalledSkills = [.. config.InstalledSkills, skillName]
            });
        }

    }

    // ── Sync ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Pulls the registry cache so all symlinked skills automatically reflect updates.
    /// Returns skill names that were updated.
    /// </summary>
    public IReadOnlyList<string> SyncSkills(string projectRoot)
    {
        var config = ReadConfig(projectRoot);

        // Pull the registry cache — symlinks automatically point to the fresh content
        if (config.Registry is null) return [];
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
                    // Target vanished — reinstall
                    InstallSkill(projectRoot, skillName);
                    updated.Add(skillName);
                }
                continue;
            }

            var sourcePath = registry.FindSkillPath(config.Registry.Url, skillName);
            if (sourcePath is null) continue;

            if (Directory.Exists(linkPath))
                Directory.Delete(linkPath, recursive: true);

            InstallSkill(projectRoot, skillName);
            updated.Add(skillName);
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

