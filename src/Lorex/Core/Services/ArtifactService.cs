using System.Text.Json;
using Lorex.Core.Models;
using Lorex.Core.Serialization;

namespace Lorex.Core.Services;

/// <summary>
/// Handles all artifact lifecycle operations for a project: install, uninstall, sync, scaffold, and publish.
/// Works with the <see cref="RegistryService"/> cache and reads/writes <c>.lorex/lorex.json</c>.
/// </summary>
public sealed class ArtifactService(RegistryService registry)
{
    private const string LorexDir = ".lorex";
    private const string ConfigFile = "lorex.json";

    private static readonly string GlobalConfigPath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".lorex", "config.json");

    public LorexConfig ReadConfig(string projectRoot)
    {
        var path = ConfigPath(projectRoot);
        if (!File.Exists(path))
            throw new FileNotFoundException("lorex is not initialised in this directory. Run `lorex init` first.", path);

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

    public GlobalConfig ReadGlobalConfig()
    {
        if (!File.Exists(GlobalConfigPath))
            return new GlobalConfig();

        try
        {
            var json = File.ReadAllText(GlobalConfigPath);
            return JsonSerializer.Deserialize(json, LorexJsonContext.Default.GlobalConfig) ?? new GlobalConfig();
        }
        catch
        {
            return new GlobalConfig();
        }
    }

    public void SaveGlobalRegistry(string registryUrl)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(GlobalConfigPath)!);
        var existing = ReadGlobalConfig().Registries;
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

    public void UninstallArtifact(string projectRoot, ArtifactKind kind, string artifactName)
    {
        var linkPath = ArtifactDir(projectRoot, kind, artifactName);
        if (Directory.Exists(linkPath))
            Directory.Delete(linkPath, recursive: true);

        var config = ReadConfig(projectRoot);
        var updated = config.Artifacts.Get(kind)
            .Where(name => !name.Equals(artifactName, StringComparison.OrdinalIgnoreCase));

        WriteConfig(projectRoot, config with
        {
            Artifacts = config.Artifacts.With(kind, updated)
        });
    }

    public void InstallArtifact(string projectRoot, ArtifactKind kind, string artifactName, bool overwriteLocalArtifact = false)
    {
        var config = ReadConfig(projectRoot);
        if (config.Registry is null)
            throw new InvalidOperationException("No registry configured. Run `lorex init <url>` to connect a registry.");

        var sourcePath = registry.FindArtifactPath(config.Registry.Url, kind, artifactName)
            ?? throw new InvalidOperationException(
                $"{kind.Title()} '{artifactName}' not found in registry '{config.Registry.Url}'.");

        var linkPath = ArtifactDir(projectRoot, kind, artifactName);
        Directory.CreateDirectory(Path.Combine(projectRoot, LorexDir, kind.FolderName()));

        if (Directory.Exists(linkPath))
        {
            if (!IsSymlink(linkPath) && !overwriteLocalArtifact)
            {
                throw new InvalidOperationException(
                    $"{kind.Title()} '{artifactName}' already exists locally at '{linkPath}'. Overwrite requires direct user approval.");
            }

            Directory.Delete(linkPath, recursive: true);
        }

        if (!TryCreateSymlink(linkPath, sourcePath))
        {
            throw new InvalidOperationException(
                $"Lorex requires symlink support for installed registry {kind.DisplayNamePlural()}. Enable symlinks and try again.");
        }

        if (!config.Artifacts.Get(kind).Contains(artifactName, StringComparer.OrdinalIgnoreCase))
        {
            WriteConfig(projectRoot, config with
            {
                Artifacts = config.Artifacts.With(kind, [.. config.Artifacts.Get(kind), artifactName])
            });
        }
    }

    public IReadOnlyList<string> SyncArtifacts(
        string projectRoot,
        ArtifactKind kind,
        IReadOnlyCollection<string>? approvedOverwriteArtifactNames = null,
        bool refreshRegistry = true)
    {
        var config = ReadConfig(projectRoot);
        var approved = approvedOverwriteArtifactNames?.ToHashSet(StringComparer.OrdinalIgnoreCase) ?? [];

        if (config.Registry is null)
            return [];

        if (refreshRegistry)
            registry.EnsureCache(config.Registry.Url);

        var updated = new List<string>();
        foreach (var artifactName in config.Artifacts.Get(kind))
        {
            var linkPath = ArtifactDir(projectRoot, kind, artifactName);

            if (IsSymlink(linkPath))
            {
                var target = new DirectoryInfo(linkPath).LinkTarget;
                if (target is not null && Directory.Exists(target))
                {
                    updated.Add(artifactName);
                }
                else
                {
                    InstallArtifact(projectRoot, kind, artifactName);
                    updated.Add(artifactName);
                }

                continue;
            }

            var sourcePath = registry.FindArtifactPath(config.Registry.Url, kind, artifactName, refresh: false);
            if (sourcePath is null || !approved.Contains(artifactName))
                continue;

            InstallArtifact(projectRoot, kind, artifactName, overwriteLocalArtifact: true);
            updated.Add(artifactName);
        }

        return updated;
    }

    public PublishResult PublishArtifact(string projectRoot, ArtifactKind kind, string artifactName, GitService git)
    {
        var config = ReadConfig(projectRoot);
        if (config.Registry is null)
        {
            throw new InvalidOperationException(
                "No registry configured. Run `lorex init <url>` to connect a registry before publishing.");
        }

        var localPath = ArtifactDir(projectRoot, kind, artifactName);
        if (!Directory.Exists(localPath) || IsSymlink(localPath))
        {
            throw new InvalidOperationException(
                $"{kind.Title()} '{artifactName}' not found locally or is already a registry-linked {kind.DisplayName()}.");
        }

        return config.Registry.Policy.PublishMode switch
        {
            var mode when string.Equals(mode, RegistryPublishModes.Direct, StringComparison.OrdinalIgnoreCase)
                => PublishArtifactDirect(projectRoot, kind, artifactName, localPath, config.Registry, git),
            var mode when string.Equals(mode, RegistryPublishModes.PullRequest, StringComparison.OrdinalIgnoreCase)
                => PublishArtifactViaPullRequest(kind, artifactName, localPath, config.Registry, git),
            var mode when string.Equals(mode, RegistryPublishModes.ReadOnly, StringComparison.OrdinalIgnoreCase)
                => throw new InvalidOperationException(
                    $"Registry '{config.Registry.Url}' is read-only. Publishing is disabled by {RegistryService.RegistryManifestFileName}."),
            _ => throw new InvalidOperationException(
                $"Registry '{config.Registry.Url}' has unsupported publish mode '{config.Registry.Policy.PublishMode}'.")
        };
    }

    public void ScaffoldArtifact(string projectRoot, ArtifactKind kind, string name, string description, string[] tags, string owner)
    {
        var dir = ArtifactDir(projectRoot, kind, name);
        Directory.CreateDirectory(dir);

        var artifactPath = ArtifactFileConvention.CanonicalPath(kind, dir);
        if (ArtifactFileConvention.ResolveEntryPath(kind, dir) is not null)
            throw new InvalidOperationException($"{kind.Title()} '{name}' already exists at {dir}");

        File.WriteAllText(artifactPath, BuildArtifactWithFrontmatter(kind, name, description, tags, owner));

        var config = ReadConfig(projectRoot);
        if (!config.Artifacts.Get(kind).Contains(name, StringComparer.OrdinalIgnoreCase))
        {
            WriteConfig(projectRoot, config with
            {
                Artifacts = config.Artifacts.With(kind, [.. config.Artifacts.Get(kind), name])
            });
        }
    }

    public string ArtifactDir(string projectRoot, ArtifactKind kind, string artifactName) =>
        Path.Combine(projectRoot, LorexDir, kind.FolderName(), artifactName);

    public IReadOnlyList<string> DiscoverInstalledArtifactNames(string projectRoot, ArtifactKind kind)
    {
        var artifactsDir = Path.Combine(projectRoot, LorexDir, kind.FolderName());
        if (!Directory.Exists(artifactsDir))
            return [];

        return [.. Directory.EnumerateDirectories(artifactsDir)
            .Select(Path.GetFileName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)!];
    }

    public IEnumerable<string> LocalOnlyArtifacts(string projectRoot, ArtifactKind kind)
    {
        var artifactsDir = Path.Combine(projectRoot, LorexDir, kind.FolderName());
        if (!Directory.Exists(artifactsDir))
            yield break;

        var builtIns = kind == ArtifactKind.Skill
            ? BuiltInSkillService.SkillNames().ToHashSet(StringComparer.OrdinalIgnoreCase)
            : [];

        foreach (var dir in Directory.EnumerateDirectories(artifactsDir))
        {
            var name = Path.GetFileName(dir);
            if (new DirectoryInfo(dir).LinkTarget is null && !builtIns.Contains(name))
                yield return name;
        }
    }

    public bool RequiresOverwriteApproval(string projectRoot, ArtifactKind kind, string artifactName)
    {
        var path = ArtifactDir(projectRoot, kind, artifactName);
        return Directory.Exists(path) && !IsSymlink(path);
    }

    public ArtifactMetadata ReadArtifactMetadata(string projectRoot, ArtifactKind kind, string artifactName)
    {
        var artifactDir = ArtifactDir(projectRoot, kind, artifactName);
        var entryPath = ArtifactFileConvention.ResolveEntryPath(kind, artifactDir)
            ?? throw new InvalidOperationException(
                $"{kind.Title()} '{artifactName}' is missing {kind.CanonicalFileName()}.");

        return SimpleYamlParser.ParseArtifactMetadataFromMarkdown(File.ReadAllText(entryPath));
    }

    public string ReadArtifactBody(string projectRoot, ArtifactKind kind, string artifactName)
    {
        var artifactDir = ArtifactDir(projectRoot, kind, artifactName);
        var entryPath = ArtifactFileConvention.ResolveEntryPath(kind, artifactDir)
            ?? throw new InvalidOperationException(
                $"{kind.Title()} '{artifactName}' is missing {kind.CanonicalFileName()}.");

        return ArtifactFileConvention.ExtractBody(File.ReadAllText(entryPath));
    }

    internal static string BuildPublishBranchName(RegistryPolicy policy, ArtifactKind kind, string artifactName) =>
        $"{policy.PrBranchPrefix}{kind.CliValue()}-{SanitizeBranchSegment(artifactName)}-{DateTime.UtcNow:yyyyMMddHHmmss}";

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

    private PublishResult PublishArtifactDirect(
        string projectRoot,
        ArtifactKind kind,
        string artifactName,
        string localPath,
        RegistryConfig registryConfig,
        GitService git)
    {
        var cacheDir = registry.GetCachePath(registryConfig.Url);
        if (!Directory.Exists(Path.Combine(cacheDir, ".git")))
            throw new InvalidOperationException("Registry is not cached locally. Run `lorex sync` first.");

        registry.EnsureCache(registryConfig.Url);

        var destination = Path.Combine(cacheDir, kind.FolderName(), artifactName);
        CopyArtifactFolder(localPath, destination);

        git.AddAll(cacheDir);
        git.Commit(cacheDir, $"feat: publish {kind.DisplayName()} '{artifactName}'");
        git.Push(cacheDir);

        Directory.Delete(localPath, recursive: true);
        InstallArtifact(projectRoot, kind, artifactName);

        return new PublishResult
        {
            ArtifactKind = kind,
            ArtifactName = artifactName,
            PublishMode = RegistryPublishModes.Direct,
        };
    }

    private PublishResult PublishArtifactViaPullRequest(
        ArtifactKind kind,
        string artifactName,
        string localPath,
        RegistryConfig registryConfig,
        GitService git)
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

        var branchName = BuildPublishBranchName(policy, kind, artifactName);
        var worktreeDir = Path.Combine(registry.GetWorktreeRoot(registryConfig.Url), branchName.Replace('/', Path.DirectorySeparatorChar));

        try
        {
            git.WorktreeAdd(cacheDir, worktreeDir, branchName, policy.BaseBranch);
            var destination = Path.Combine(worktreeDir, kind.FolderName(), artifactName);
            CopyArtifactFolder(localPath, destination);

            if (!git.HasChanges(worktreeDir))
                throw new InvalidOperationException($"{kind.Title()} '{artifactName}' has no changes to publish.");

            git.AddAll(worktreeDir);
            git.Commit(worktreeDir, $"feat: publish {kind.DisplayName()} '{artifactName}'");
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
            ArtifactKind = kind,
            ArtifactName = artifactName,
            PublishMode = RegistryPublishModes.PullRequest,
            BranchName = branchName,
            BaseBranch = policy.BaseBranch,
            PullRequestUrl = registry.BuildPullRequestUrl(registryConfig.Url, branchName, policy.BaseBranch),
        };
    }

    private static string ConfigPath(string projectRoot) =>
        Path.Combine(projectRoot, LorexDir, ConfigFile);

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
        Directory.Exists(path) && new DirectoryInfo(path).LinkTarget is not null;

    private static void CopyArtifactFolder(string source, string destination)
    {
        if (Directory.Exists(destination))
            Directory.Delete(destination, recursive: true);
        Directory.CreateDirectory(destination);

        foreach (var file in Directory.EnumerateFiles(source))
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), overwrite: true);

        foreach (var dir in Directory.EnumerateDirectories(source))
            CopyArtifactFolder(dir, Path.Combine(destination, Path.GetFileName(dir)));
    }

    private static string BuildArtifactWithFrontmatter(ArtifactKind kind, string name, string description, string[] tags, string owner) =>
        kind switch
        {
            ArtifactKind.Skill => $$"""
                ---
                name: {{name}}
                description: {{description}}
                version: 1.0.0
                tags: {{string.Join(", ", tags)}}
                owner: {{owner}}
                ---

                # {{name}}

                > {{description}}

                <!-- Author this skill using your AI coding agent. -->
                <!-- Describe architecture, constraints, flows, patterns, pitfalls. -->

                """,
            ArtifactKind.Prompt => $$"""
                ---
                name: {{name}}
                description: {{description}}
                version: 1.0.0
                tags: {{string.Join(", ", tags)}}
                owner: {{owner}}
                ---

                # {{name}}

                > {{description}}

                <!-- Author this prompt using your AI coding agent. -->
                <!-- Write a reusable task prompt that can be invoked explicitly. -->

                """,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };
}
