using Lorex.Cli;
using Lorex.Commands;
using Lorex.Core.Models;
using Lorex.Core.Serialization;
using Lorex.Core.Services;

namespace Lorex.Tests.Integration;

/// <summary>
/// Sandbox harness for integration tests. Creates isolated temp directories for project and global
/// roots, provides a local git registry factory, and dispatches to command Run() methods in-process.
/// </summary>
internal sealed class LorexTestHarness : IDisposable
{
    private readonly string _tempDir;

    /// <summary>Isolated project root (equivalent to the current working directory in real usage).</summary>
    public string ProjectRoot { get; }

    /// <summary>Isolated global root (equivalent to ~/.lorex in real usage).</summary>
    public string GlobalRoot { get; }

    public LorexTestHarness()
    {
        _tempDir    = Path.Combine(Path.GetTempPath(), $"lorex-test-{Guid.NewGuid():N}");
        ProjectRoot = Path.Combine(_tempDir, "project");
        GlobalRoot  = Path.Combine(_tempDir, "global");
        Directory.CreateDirectory(ProjectRoot);
        Directory.CreateDirectory(GlobalRoot);
        // Redirect all lorex home operations (~/.lorex/cache, ~/.lorex/taps, ~/.lorex/config.json)
        // to the isolated GlobalRoot so tests never touch the real user home directory.
        Environment.SetEnvironmentVariable("LOREX_HOME_OVERRIDE", GlobalRoot);

        // Provide a test git identity so commits succeed on CI runners with no global git config.
        Environment.SetEnvironmentVariable("GIT_AUTHOR_NAME",     "Lorex Test");
        Environment.SetEnvironmentVariable("GIT_AUTHOR_EMAIL",    "test@lorex.test");
        Environment.SetEnvironmentVariable("GIT_COMMITTER_NAME",  "Lorex Test");
        Environment.SetEnvironmentVariable("GIT_COMMITTER_EMAIL", "test@lorex.test");
    }

    // ── Command runners ──────────────────────────────────────────────────────

    /// <summary>Runs a lorex command against the project sandbox.</summary>
    public int Run(string command, params string[] args) =>
        Dispatch(command, args, cwd: ProjectRoot, homeRoot: null);

    /// <summary>Runs a lorex command against the global sandbox (appends --global).</summary>
    public int RunGlobal(string command, params string[] args) =>
        Dispatch(command, [..args, "--global"], cwd: null, homeRoot: GlobalRoot);

    private int Dispatch(string command, string[] args, string? cwd, string? homeRoot)
    {
        try
        {
            return command switch
            {
                "init"      => InitCommand.Run(args,      cwd, homeRoot),
                "install"   => InstallCommand.Run(args,   cwd, homeRoot),
                "uninstall" => UninstallCommand.Run(args, cwd, homeRoot),
                "create"    => CreateCommand.Run(args,    cwd),
                "publish"   => PublishCommand.Run(args,   cwd, homeRoot),
                "registry"  => RegistryCommand.Run(args,  cwd),
                "refresh"   => RefreshCommand.Run(args,   cwd),
                "sync"      => SyncCommand.Run(args,      cwd, homeRoot),
                "list"      => ListCommand.Run(args,      cwd, homeRoot),
                "status"    => StatusCommand.Run(args,    cwd, homeRoot),
                "tap"       => TapCommand.Run(args,       cwd, homeRoot),
                _ => throw new ArgumentException($"Unknown command: {command}"),
            };
        }
        catch (OperationCanceledException)
        {
            return 130;
        }
        catch (ArgumentException)
        {
            throw; // re-throw unknown-command errors so tests catch harness bugs
        }
        catch (Exception)
        {
            return 1; // mirror Program.cs top-level exception handler
        }
    }

    // ── Local registry factory ───────────────────────────────────────────────

    /// <summary>
    /// Creates a minimal local git repository usable as a lorex registry.
    /// Returns the directory path, which can be passed directly as a registry URL.
    /// </summary>
    public string CreateRegistry(string publishMode = "direct", string[]? skillNames = null, TapConfig[]? recommendedTaps = null)
    {
        var repoDir = Path.Combine(_tempDir, $"registry-{Guid.NewGuid():N}");
        Directory.CreateDirectory(repoDir);

        var git = ServiceFactory.Git;

        // Use -b to set the initial branch name (avoids platform-specific git default branch name issues)
        try
        {
            git.Run(repoDir, "init", "-b", "main");
        }
        catch (GitException)
        {
            // Older git versions don't support -b; fall back to init + rename
            git.Run(repoDir, "init");
            try { git.Run(repoDir, "checkout", "-b", "main"); } catch { }
        }

        // Set local git identity so the commit works in CI environments without a global config
        git.Run(repoDir, "config", "user.email", "test@lorex.test");
        git.Run(repoDir, "config", "user.name", "Lorex Test");

        // Allow pushing to this repo while its branch is checked out (needed for publish tests)
        git.Run(repoDir, "config", "receive.denyCurrentBranch", "ignore");

        // Write registry manifest
        var manifestJson = recommendedTaps is { Length: > 0 }
            ? System.Text.Json.JsonSerializer.Serialize(
                new RegistryPolicy
                {
                    PublishMode = publishMode,
                    BaseBranch = "main",
                    PrBranchPrefix = "lorex/",
                    RecommendedTaps = recommendedTaps,
                },
                LorexJsonContext.Default.RegistryPolicy)
            : $$"""{"publishMode":"{{publishMode}}","baseBranch":"main","prBranchPrefix":"lorex/"}""";

        File.WriteAllText(
            Path.Combine(repoDir, ".lorex-registry.json"),
            manifestJson);

        // Create initial skills
        foreach (var name in skillNames ?? [])
            AddSkillToRepo(repoDir, name);

        git.Run(repoDir, "add", "-A");
        git.Run(repoDir, "commit", "-m", "init registry");

        return repoDir;
    }

    /// <summary>Adds (or overwrites) a skill in a local registry repo. Call CommitRegistry to persist.</summary>
    public void AddSkillToRepo(string repoDir, string skillName, string? content = null)
    {
        var skillDir = Path.Combine(repoDir, "skills", skillName);
        Directory.CreateDirectory(skillDir);
        File.WriteAllText(
            Path.Combine(skillDir, "SKILL.md"),
            content ?? $"---\nname: {skillName}\ndescription: Test skill {skillName}\nversion: 1.0.0\ntags: test\n---\n\n# {skillName}\n\nTest skill.\n");
    }

    /// <summary>Adds a skill under a category subdirectory in a local registry repo (nested layout). Call CommitRegistry to persist.</summary>
    public void AddSkillToRepoNested(string repoDir, string category, string skillName, string? content = null)
    {
        var skillDir = Path.Combine(repoDir, "skills", category, skillName);
        Directory.CreateDirectory(skillDir);
        File.WriteAllText(
            Path.Combine(skillDir, "SKILL.md"),
            content ?? $"---\nname: {skillName}\ndescription: Test skill {skillName}\nversion: 1.0.0\ntags: test\n---\n\n# {skillName}\n\nTest skill.\n");
    }

    /// <summary>Overwrites a skill's SKILL.md in the registry cache (flat layout) to simulate in-place editing through a symlink.</summary>
    public void ModifyCacheSkill(string registryUrl, string skillName, string content)
    {
        var cacheDir = ServiceFactory.Registry.GetCachePath(registryUrl);
        File.WriteAllText(Path.Combine(cacheDir, "skills", skillName, "SKILL.md"), content);
    }

    /// <summary>Overwrites a skill's SKILL.md in the registry cache (nested layout) to simulate in-place editing through a symlink.</summary>
    public void ModifyCacheSkillNested(string registryUrl, string category, string skillName, string content)
    {
        var cacheDir = ServiceFactory.Registry.GetCachePath(registryUrl);
        File.WriteAllText(Path.Combine(cacheDir, "skills", category, skillName, "SKILL.md"), content);
    }

    /// <summary>Removes a skill directory from a local registry repo. Call CommitRegistry to persist.</summary>
    public void RemoveSkillFromRepo(string repoDir, string skillName)
    {
        var skillDir = Path.Combine(repoDir, "skills", skillName);
        if (Directory.Exists(skillDir))
            Directory.Delete(skillDir, recursive: true);
    }

    /// <summary>Stages all changes and creates a commit in a local registry repo.</summary>
    public void CommitRegistry(string repoDir, string message = "update registry")
    {
        var git = ServiceFactory.Git;
        git.Run(repoDir, "add", "-A");
        if (git.HasChanges(repoDir))
            git.Run(repoDir, "commit", "-m", message);
    }

    // ── Skill assertions ─────────────────────────────────────────────────────

    /// <summary>Asserts that a skill directory exists under the project's .lorex/skills/.</summary>
    public void AssertSkillInstalled(string name) =>
        Assert.True(
            Directory.Exists(SkillDir(name)),
            $"Expected skill '{name}' to be installed at {SkillDir(name)}");

    /// <summary>Asserts that no skill directory exists under the project's .lorex/skills/.</summary>
    public void AssertSkillNotInstalled(string name) =>
        Assert.False(
            Directory.Exists(SkillDir(name)),
            $"Expected skill '{name}' to NOT be installed");

    /// <summary>Asserts that the installed skill directory is a symlink.</summary>
    public void AssertIsSymlink(string name)
    {
        var dir = SkillDir(name);
        Assert.True(Directory.Exists(dir), $"Skill '{name}' is not installed");
        Assert.True(
            new DirectoryInfo(dir).LinkTarget is not null,
            $"Expected skill '{name}' to be a symlink but it is a real directory");
    }

    /// <summary>Asserts that the installed skill directory is a real (non-symlink) directory.</summary>
    public void AssertIsRealDir(string name)
    {
        var dir = SkillDir(name);
        Assert.True(Directory.Exists(dir), $"Skill '{name}' is not installed");
        Assert.True(
            new DirectoryInfo(dir).LinkTarget is null,
            $"Expected skill '{name}' to be a real directory but it is a symlink");
    }

    /// <summary>Asserts that the adapter has projected the skill into its native target location.</summary>
    public void AssertAdapterProjected(string adapter, string skillName)
    {
        var target = adapter switch
        {
            "claude"   => Path.Combine(ProjectRoot, ".claude",   "skills", skillName),
            "copilot"  => Path.Combine(ProjectRoot, ".github",   "skills", skillName),
            "codex"    => Path.Combine(ProjectRoot, ".agents",   "skills", skillName),
            "cursor"   => Path.Combine(ProjectRoot, ".cursor",   "rules",  $"{skillName}.mdc"),
            "windsurf" => Path.Combine(ProjectRoot, ".windsurf", "skills", skillName),
            "cline"    => Path.Combine(ProjectRoot, ".cline",    "skills", skillName),
            "roo"      => Path.Combine(ProjectRoot, ".roo",      "rules-code", $"{skillName}.md"),
            _ => throw new ArgumentException($"Unknown adapter '{adapter}'"),
        };
        Assert.True(
            File.Exists(target) || Directory.Exists(target),
            $"Expected adapter '{adapter}' to have projected skill '{skillName}' at {target}");
    }

    /// <summary>Asserts that a skill directory exists under the global root's .lorex/skills/.</summary>
    public void AssertGlobalSkillInstalled(string name) =>
        Assert.True(
            Directory.Exists(GlobalSkillDir(name)),
            $"Expected global skill '{name}' to be installed at {GlobalSkillDir(name)}");

    /// <summary>Asserts that the globally installed skill directory is a symlink.</summary>
    public void AssertGlobalIsSymlink(string name)
    {
        var dir = GlobalSkillDir(name);
        Assert.True(Directory.Exists(dir), $"Global skill '{name}' is not installed");
        Assert.True(
            new DirectoryInfo(dir).LinkTarget is not null,
            $"Expected global skill '{name}' to be a symlink but it is a real directory");
    }

    // ── Config helpers ────────────────────────────────────────────────────────

    /// <summary>Reads .lorex/lorex.json from the project root.</summary>
    public LorexConfig ReadConfig() => ServiceFactory.Skills.ReadConfig(ProjectRoot);

    /// <summary>Reads .lorex/lorex.json from the global root.</summary>
    public LorexConfig ReadGlobalConfig() => ServiceFactory.Skills.ReadConfig(GlobalRoot);

    /// <summary>
    /// Reads the registry policy for the given registry URL from the lorex cache.
    /// Reads from the lorex cache (not the registry working tree) because
    /// <c>UpdateRegistryPolicy</c> writes there before pushing to origin.
    /// </summary>
    public RegistryPolicy ReadRegistryPolicy(string registryUrl)
    {
        var cachePath    = ServiceFactory.Registry.GetCachePath(registryUrl);
        var manifestPath = Path.Combine(cachePath, RegistryService.RegistryManifestFileName);
        var json = File.ReadAllText(manifestPath);
        return System.Text.Json.JsonSerializer.Deserialize(json, LorexJsonContext.Default.RegistryPolicy)
            ?? throw new InvalidOperationException($"Failed to deserialize registry policy from {manifestPath}");
    }

    // ── Symlink availability ──────────────────────────────────────────────────

    /// <summary>
    /// Returns true when symlinks can be created in the current environment.
    /// On Windows, requires Developer Mode or administrator elevation.
    /// Tests that require symlinks should guard with: <c>if (!LorexTestHarness.SymlinksAvailable()) return;</c>
    /// </summary>
    public static bool SymlinksAvailable() =>
        !OperatingSystem.IsWindows() || WindowsDevModeHelper.IsSymlinkAvailable();

    // ── Dispose ───────────────────────────────────────────────────────────────

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("LOREX_HOME_OVERRIDE",   null);
        Environment.SetEnvironmentVariable("GIT_AUTHOR_NAME",       null);
        Environment.SetEnvironmentVariable("GIT_AUTHOR_EMAIL",      null);
        Environment.SetEnvironmentVariable("GIT_COMMITTER_NAME",    null);
        Environment.SetEnvironmentVariable("GIT_COMMITTER_EMAIL",   null);

        if (Directory.Exists(_tempDir))
        {
            try { Directory.Delete(_tempDir, recursive: true); }
            catch { /* best-effort; locked files (e.g. git index) can occasionally block deletion */ }
        }
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private string SkillDir(string name) =>
        Path.Combine(ProjectRoot, ".lorex", "skills", name);

    private string GlobalSkillDir(string name) =>
        Path.Combine(GlobalRoot, ".lorex", "skills", name);
}
