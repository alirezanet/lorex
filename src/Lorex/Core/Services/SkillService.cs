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

    // ── Uninstall ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Removes a skill symlink/copy from .lorex/skills and records the removal in lorex.json.
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
    /// Creates a symlink .lorex/skills/&lt;name&gt; → registry cache path.
    /// Falls back to a file copy if symlinks are unavailable (e.g. Windows without Developer Mode).
    /// Records the skill in lorex.json.
    /// </summary>
    public bool InstallSkill(string projectRoot, string skillName)
    {
        var config = ReadConfig(projectRoot);
        if (config.Registry is null)
            throw new InvalidOperationException("No registry configured. Run `lorex init <url>` to connect a registry.");
        var sourcePath = registry.FindSkillPath(config.Registry, skillName)
            ?? throw new InvalidOperationException($"Skill '{skillName}' not found in registry '{config.Registry}'.");

        var linkPath = SkillDir(projectRoot, skillName);
        Directory.CreateDirectory(Path.Combine(projectRoot, LorexDir, "skills"));

        // Remove any existing link or directory at the target
        if (Directory.Exists(linkPath))
            Directory.Delete(linkPath, recursive: true);

        var usedSymlink = TryCreateSymlink(linkPath, sourcePath);
        if (!usedSymlink)
            CopySkillFolder(sourcePath, linkPath);

        if (!config.InstalledSkills.Contains(skillName, StringComparer.OrdinalIgnoreCase))
        {
            WriteConfig(projectRoot, config with
            {
                InstalledSkills = [.. config.InstalledSkills, skillName]
            });
        }

        return usedSymlink;
    }

    // ── Sync ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Pulls the registry cache so all symlinked skills automatically reflect updates.
    /// For copy-based installs (symlink fallback), re-copies skills whose version changed.
    /// Returns skill names that were updated.
    /// </summary>
    public IReadOnlyList<string> SyncSkills(string projectRoot)
    {
        var config = ReadConfig(projectRoot);

        // Pull the registry cache — symlinks automatically point to the fresh content
        if (config.Registry is null) return [];
        registry.EnsureCache(config.Registry);

        var updated = new List<string>();

        foreach (var skillName in config.InstalledSkills)
        {
            var linkPath = SkillDir(projectRoot, skillName);

            // If this is a real symlink, the pull above already updated it — just report it
            if (IsSymlink(linkPath))
            {
                // Verify the symlink target still exists (skill not deleted from registry)
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

            // Copy-based fallback: update if version changed
            var sourcePath = registry.FindSkillPath(config.Registry, skillName);
            if (sourcePath is null) continue;

            var sourceVersion = GetVersion(sourcePath);
            var localVersion = Directory.Exists(linkPath) ? GetVersion(linkPath) : null;

            if (sourceVersion != localVersion)
            {
                CopySkillFolder(sourcePath, linkPath);
                updated.Add(skillName);
            }
        }

        return updated;
    }

    // ── Publish ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Publishes a local skill from .lorex/skills/&lt;name&gt; to the registry, then converts the real
    /// directory into a symlink pointing at the registry cache.
    /// </summary>
    public void PublishSkill(string projectRoot, string skillName, GitService git)
    {
        var config = ReadConfig(projectRoot);
        if (config.Registry is null)
            throw new InvalidOperationException(
                "No registry configured. Run `lorex init <url>` to connect a registry before publishing.");
        var localPath = SkillDir(projectRoot, skillName);

        if (!Directory.Exists(localPath) || IsSymlink(localPath))
            throw new InvalidOperationException(
                $"Skill '{skillName}' not found locally or is already a registry-linked skill.");

        var cacheDir = registry.GetCachePath(config.Registry);
        if (!Directory.Exists(Path.Combine(cacheDir, ".git")))
            throw new InvalidOperationException(
                "Registry is not cached locally. Run `lorex sync` first.");

        // Pull latest before publishing to reduce merge conflicts
        git.Pull(cacheDir);

        var destination = Path.Combine(cacheDir, "skills", skillName);
        CopySkillFolder(localPath, destination);

        git.AddAll(cacheDir);
        git.Commit(cacheDir, $"feat: publish skill '{skillName}'");
        git.Push(cacheDir);

        // Replace the local copy with a symlink to the registry cache
        Directory.Delete(localPath, recursive: true);
        InstallSkill(projectRoot, skillName);
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

        var skillPath = Path.Combine(dir, "skill.md");

        if (File.Exists(skillPath))
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
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)));
    }

    private static string? GetVersion(string skillDir)
    {
        // Prefer frontmatter in skill.md (new format)
        var skillMd = Path.Combine(skillDir, "skill.md");
        if (File.Exists(skillMd))
        {
            var yaml = SimpleYamlParser.ExtractFrontmatterYaml(File.ReadAllText(skillMd));
            if (yaml is not null)
            {
                var dict = SimpleYamlParser.ParseToDictionary(yaml);
                if (dict.TryGetValue("version", out var v)) return v;
            }
        }

        // Fallback: legacy metadata.yaml
        var metaFile = Path.Combine(skillDir, "metadata.yaml");
        if (!File.Exists(metaFile)) return null;
        var legacyDict = SimpleYamlParser.ParseToDictionary(File.ReadAllText(metaFile));
        return legacyDict.TryGetValue("version", out var lv) ? lv : null;
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

