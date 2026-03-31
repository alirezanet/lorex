using Lorex.Core.Models;
using Lorex.Core.Serialization;

namespace Lorex.Core.Services;

/// <summary>
/// Manages the local registry cache at <c>~/.lorex/cache/&lt;slug&gt;/</c>.
/// Each registry URL maps to a single cloned git repo that is shared across all projects on the machine.
/// </summary>
public sealed class RegistryService(GitService git)
{
    public const string RegistryManifestFileName = ".lorex-registry.json";

    private static readonly string CacheRoot =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".lorex", "cache");

    private static readonly string WorktreeRoot =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".lorex", "worktrees");

    /// <summary>Returns (and clones/updates) the local cache path for a registry URL.</summary>
    public string EnsureCache(string registryUrl)
    {
        var cacheDir = GetCachePath(registryUrl);

        if (Directory.Exists(Path.Combine(cacheDir, ".git")))
            SyncCacheRepository(cacheDir, registryUrl);
        else
        {
            Directory.CreateDirectory(Path.GetDirectoryName(cacheDir)!);
            git.CloneShallow(registryUrl, cacheDir);
            SyncCacheRepository(cacheDir, registryUrl);
        }

        return cacheDir;
    }

    /// <summary>Finds an artifact folder inside the cached registry. Returns null if not found.</summary>
    public string? FindArtifactPath(string registryUrl, Lorex.Core.Models.ArtifactKind kind, string artifactName, bool refresh = true)
    {
        var cacheDir = refresh ? EnsureCache(registryUrl) : GetCachePath(registryUrl);
        var candidate = Path.Combine(cacheDir, kind.FolderName(), artifactName);
        return Directory.Exists(candidate) ? candidate : null;
    }

    /// <summary>Scans the cached registry and returns metadata for all available artifacts of the requested kind.</summary>
    public IReadOnlyList<Lorex.Core.Models.ArtifactMetadata> ListAvailableArtifacts(string registryUrl, Lorex.Core.Models.ArtifactKind kind, bool refresh = true)
    {
        var cacheDir = refresh ? EnsureCache(registryUrl) : GetCachePath(registryUrl);
        var artifactsRoot = Path.Combine(cacheDir, kind.FolderName());

        if (!Directory.Exists(artifactsRoot))
            return [];

        var results = new List<Lorex.Core.Models.ArtifactMetadata>();
        foreach (var dir in Directory.EnumerateDirectories(artifactsRoot))
        {
            var entryFile = ArtifactFileConvention.ResolveEntryPath(kind, dir);
            if (entryFile is not null)
            {
                try
                {
                    var meta = SimpleYamlParser.ParseArtifactMetadataFromMarkdown(File.ReadAllText(entryFile));
                    results.Add(meta);
                    continue;
                }
                catch (InvalidDataException) { /* no frontmatter — fall through to legacy check */ }
            }

            if (kind != Lorex.Core.Models.ArtifactKind.Skill)
                continue;

            // Legacy skill format: separate metadata.yaml
            var metaFile = Path.Combine(dir, "metadata.yaml");
            if (!File.Exists(metaFile))
                continue;

            try
            {
                var meta = SimpleYamlParser.ParseArtifactMetadata(File.ReadAllText(metaFile));
                results.Add(meta);
            }
            catch (InvalidDataException)
            {
                // Skip malformed artifacts silently — registry may contain work-in-progress entries
            }
        }

        return results;
    }

    /// <summary>Returns the registry policy manifest, refreshing the local cache first by default.</summary>
    public RegistryPolicy? ReadRegistryPolicy(string registryUrl, bool refresh = true)
    {
        var cacheDir = refresh ? EnsureCache(registryUrl) : GetCachePath(registryUrl);
        return ReadRegistryPolicyFromDirectory(cacheDir);
    }

    /// <summary>Writes the initial registry policy manifest and pushes it to the remote registry.</summary>
    public RegistryPolicy InitializeRegistryPolicy(string registryUrl, RegistryPolicy policy)
    {
        var cacheDir = EnsureCache(registryUrl);
        var manifestPath = GetRegistryManifestPath(cacheDir);

        var existing = ReadRegistryPolicyFromDirectory(cacheDir);
        if (existing is not null)
            return existing;

        File.WriteAllText(
            manifestPath,
            System.Text.Json.JsonSerializer.Serialize(policy, LorexJsonContext.Default.RegistryPolicy) + "\n");

        if (!git.HasCommits(cacheDir))
            git.CheckoutOrphan(cacheDir, policy.BaseBranch);

        git.AddAll(cacheDir);
        git.Commit(cacheDir, "chore: initialize lorex registry policy");
        git.PushSetUpstream(cacheDir, "origin", policy.BaseBranch);

        return policy;
    }

    /// <summary>Updates the registry policy manifest using the registry's current publish policy.</summary>
    public RegistryPolicyUpdateResult UpdateRegistryPolicy(string registryUrl, RegistryPolicy updatedPolicy)
    {
        var cacheDir = EnsureCache(registryUrl);
        var currentPolicy = ReadRegistryPolicyFromDirectory(cacheDir)
            ?? throw new InvalidOperationException(
                $"Registry '{registryUrl}' is missing {RegistryManifestFileName}. Run `lorex init` to initialize it first.");

        ValidateTargetBaseBranch(cacheDir, registryUrl, currentPolicy, updatedPolicy);

        return currentPolicy.PublishMode switch
        {
            var mode when string.Equals(mode, RegistryPublishModes.Direct, StringComparison.OrdinalIgnoreCase)
                => UpdateRegistryPolicyDirect(registryUrl, cacheDir, currentPolicy, updatedPolicy),
            var mode when string.Equals(mode, RegistryPublishModes.PullRequest, StringComparison.OrdinalIgnoreCase)
                => UpdateRegistryPolicyViaPullRequest(registryUrl, cacheDir, currentPolicy, updatedPolicy),
            var mode when string.Equals(mode, RegistryPublishModes.ReadOnly, StringComparison.OrdinalIgnoreCase)
                => throw new InvalidOperationException(
                    $"Registry '{registryUrl}' is read-only. Updating {RegistryManifestFileName} is blocked by its current policy."),
            _ => throw new InvalidOperationException(
                $"Registry '{registryUrl}' has unsupported publish mode '{currentPolicy.PublishMode}'.")
        };
    }

    /// <summary>Resolves the local cache path for a given registry URL without fetching.</summary>
    public string GetCachePath(string registryUrl)
    {
        var slug = UrlToSlug(registryUrl);
        return Path.Combine(CacheRoot, slug);
    }

    /// <summary>Returns the local worktree root for transient PR publishing branches.</summary>
    public string GetWorktreeRoot(string registryUrl)
    {
        var slug = UrlToSlug(registryUrl);
        return Path.Combine(WorktreeRoot, slug);
    }

    /// <summary>Builds a GitHub compare URL when the registry remote is hosted on GitHub.</summary>
    public string? BuildPullRequestUrl(string registryUrl, string branchName, string baseBranch)
    {
        string? repoPath = null;

        if (registryUrl.StartsWith("https://github.com/", StringComparison.OrdinalIgnoreCase))
            repoPath = registryUrl["https://github.com/".Length..];
        else if (registryUrl.StartsWith("http://github.com/", StringComparison.OrdinalIgnoreCase))
            repoPath = registryUrl["http://github.com/".Length..];
        else if (registryUrl.StartsWith("git@github.com:", StringComparison.OrdinalIgnoreCase))
            repoPath = registryUrl["git@github.com:".Length..];

        if (string.IsNullOrWhiteSpace(repoPath))
            return null;

        repoPath = repoPath.TrimEnd('/');
        if (repoPath.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
            repoPath = repoPath[..^".git".Length];

        return $"https://github.com/{repoPath}/compare/{baseBranch}...{branchName}?expand=1";
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static string GetRegistryManifestPath(string cacheDir) =>
        Path.Combine(cacheDir, RegistryManifestFileName);

    private static RegistryPolicy? ReadRegistryPolicyFromDirectory(string cacheDir)
    {
        var manifestPath = GetRegistryManifestPath(cacheDir);
        if (!File.Exists(manifestPath))
            return null;

        try
        {
            var json = File.ReadAllText(manifestPath);
            var policy = System.Text.Json.JsonSerializer.Deserialize(json, LorexJsonContext.Default.RegistryPolicy);
            return policy is not null && RegistryPublishModes.IsValid(policy.PublishMode)
                ? policy
                : null;
        }
        catch
        {
            return null;
        }
    }

    private void SyncCacheRepository(string cacheDir, string registryUrl)
    {
        git.FetchPrune(cacheDir, "origin");
        git.UpdateRemoteHead(cacheDir, "origin");

        var defaultBranch = git.GetRemoteDefaultBranchFromUrl(registryUrl)
            ?? git.GetRemoteDefaultBranch(cacheDir, "origin");
        if (string.IsNullOrWhiteSpace(defaultBranch))
        {
            var remoteBranches = git.GetRemoteBranchNames(cacheDir, "origin");
            if (remoteBranches.Count == 0)
                return;

            if (remoteBranches.Contains("main", StringComparer.Ordinal))
                defaultBranch = "main";
            else if (remoteBranches.Contains("master", StringComparer.Ordinal))
                defaultBranch = "master";
            else
                defaultBranch = remoteBranches[0];
        }

        git.FetchBranchToRemoteTracking(cacheDir, "origin", defaultBranch);
        git.CheckoutResetToRemoteBranch(cacheDir, "origin", defaultBranch);
    }

    private RegistryPolicyUpdateResult UpdateRegistryPolicyDirect(
        string registryUrl,
        string cacheDir,
        RegistryPolicy currentPolicy,
        RegistryPolicy updatedPolicy)
    {
        git.FetchBranchToRemoteTracking(cacheDir, "origin", currentPolicy.BaseBranch);
        git.CheckoutResetToRemoteBranch(cacheDir, "origin", currentPolicy.BaseBranch);

        WriteRegistryPolicy(cacheDir, updatedPolicy);

        if (!git.HasChanges(cacheDir))
        {
            return new RegistryPolicyUpdateResult
            {
                PublishMode = RegistryPublishModes.Direct,
                Policy = updatedPolicy,
                BaseBranch = currentPolicy.BaseBranch,
            };
        }

        git.AddAll(cacheDir);
        git.Commit(cacheDir, "chore: update lorex registry policy");
        git.Push(cacheDir);

        return new RegistryPolicyUpdateResult
        {
            PublishMode = RegistryPublishModes.Direct,
            Policy = updatedPolicy,
            BaseBranch = currentPolicy.BaseBranch,
        };
    }

    private RegistryPolicyUpdateResult UpdateRegistryPolicyViaPullRequest(
        string registryUrl,
        string cacheDir,
        RegistryPolicy currentPolicy,
        RegistryPolicy updatedPolicy)
    {
        git.FetchBranchToRemoteTracking(cacheDir, "origin", currentPolicy.BaseBranch);
        git.CheckoutResetToRemoteBranch(cacheDir, "origin", currentPolicy.BaseBranch);

        var branchName = BuildPolicyUpdateBranchName(currentPolicy);
        var worktreeDir = Path.Combine(GetWorktreeRoot(registryUrl), branchName.Replace('/', Path.DirectorySeparatorChar));

        try
        {
            git.WorktreeAdd(cacheDir, worktreeDir, branchName, currentPolicy.BaseBranch);
            WriteRegistryPolicy(worktreeDir, updatedPolicy);

            if (!git.HasChanges(worktreeDir))
                throw new InvalidOperationException("Registry policy has no changes to submit.");

            git.AddAll(worktreeDir);
            git.Commit(worktreeDir, "chore: update lorex registry policy");
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

        return new RegistryPolicyUpdateResult
        {
            PublishMode = RegistryPublishModes.PullRequest,
            Policy = updatedPolicy,
            BranchName = branchName,
            BaseBranch = currentPolicy.BaseBranch,
            PullRequestUrl = BuildPullRequestUrl(registryUrl, branchName, currentPolicy.BaseBranch),
        };
    }

    private void ValidateTargetBaseBranch(
        string cacheDir,
        string registryUrl,
        RegistryPolicy currentPolicy,
        RegistryPolicy updatedPolicy)
    {
        try
        {
            git.FetchBranchToRemoteTracking(cacheDir, "origin", currentPolicy.BaseBranch);
        }
        catch (GitException ex)
        {
            throw new InvalidOperationException(
                $"Base branch '{currentPolicy.BaseBranch}' was not found in registry '{registryUrl}'. Details: {ex.Message}");
        }

        if (string.Equals(updatedPolicy.BaseBranch, currentPolicy.BaseBranch, StringComparison.Ordinal))
            return;

        try
        {
            git.FetchBranchToRemoteTracking(cacheDir, "origin", updatedPolicy.BaseBranch);
        }
        catch (GitException ex)
        {
            throw new InvalidOperationException(
                $"Base branch '{updatedPolicy.BaseBranch}' does not exist in registry '{registryUrl}'. Create it first before updating {RegistryManifestFileName}. Details: {ex.Message}");
        }
    }

    private static void WriteRegistryPolicy(string repoDir, RegistryPolicy policy)
    {
        File.WriteAllText(
            GetRegistryManifestPath(repoDir),
            System.Text.Json.JsonSerializer.Serialize(policy, LorexJsonContext.Default.RegistryPolicy) + "\n");
    }

    internal static string BuildPolicyUpdateBranchName(RegistryPolicy currentPolicy) =>
        $"{currentPolicy.PrBranchPrefix}registry-policy-{DateTime.UtcNow:yyyyMMddHHmmss}";

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
