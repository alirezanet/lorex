using Lorex.Cli;

namespace Lorex.Tests.Integration;

/// <summary>Integration tests for registry-backed lorex flows using a local git repo as a fake registry.</summary>
[Collection("Integration")]
public sealed class RegistryFlowTests
{
    [Fact]
    public void Init_WithLocalRegistry_ConnectsSuccessfully()
    {
        using var h = new LorexTestHarness();
        var registry = h.CreateRegistry(skillNames: ["auth", "linting"]);

        var exit = h.Run("init", registry, "--adapters", "claude");

        Assert.Equal(0, exit);
        var config = h.ReadConfig();
        Assert.NotNull(config.Registry);
        Assert.Equal(registry, config.Registry.Url);
    }

    [Fact]
    public void Init_WithNonExistentPath_ReturnsError()
    {
        using var h = new LorexTestHarness();
        var fakePath = Path.Combine(h.ProjectRoot, "does-not-exist");

        var exit = h.Run("init", fakePath, "--adapters", "claude");

        Assert.NotEqual(0, exit);
    }

    [Fact]
    public void Init_WithPathThatIsNotAGitRepo_ReturnsError()
    {
        using var h = new LorexTestHarness();
        var notARepo = Path.Combine(Path.GetTempPath(), $"lorex-notarepo-{Guid.NewGuid():N}");
        Directory.CreateDirectory(notARepo);
        try
        {
            var exit = h.Run("init", notARepo, "--adapters", "claude");
            Assert.NotEqual(0, exit);
        }
        finally
        {
            Directory.Delete(notARepo, recursive: true);
        }
    }

    [Fact]
    public void Install_SkillFromRegistry_CreatesSymlink()
    {
        if (!LorexTestHarness.SymlinksAvailable()) return;

        using var h = new LorexTestHarness();
        var registry = h.CreateRegistry(skillNames: ["auth"]);
        h.Run("init", registry, "--adapters", "claude");

        var exit = h.Run("install", "auth");

        Assert.Equal(0, exit);
        h.AssertSkillInstalled("auth");
        h.AssertIsSymlink("auth");
        h.AssertAdapterProjected("claude", "auth");
    }

    [Fact]
    public void Install_AllSkills_InstallsEveryRegistrySkill()
    {
        if (!LorexTestHarness.SymlinksAvailable()) return;

        using var h = new LorexTestHarness();
        var registry = h.CreateRegistry(skillNames: ["auth", "linting", "testing"]);
        h.Run("init", registry, "--adapters", "claude");

        var exit = h.Run("install", "--all");

        Assert.Equal(0, exit);
        h.AssertSkillInstalled("auth");
        h.AssertSkillInstalled("linting");
        h.AssertSkillInstalled("testing");
    }

    [Fact]
    public void Install_AlreadyInstalled_IsIdempotent()
    {
        if (!LorexTestHarness.SymlinksAvailable()) return;

        using var h = new LorexTestHarness();
        var registry = h.CreateRegistry(skillNames: ["auth"]);
        h.Run("init", registry, "--adapters", "claude");
        h.Run("install", "auth");

        // Second install should also succeed
        var exit = h.Run("install", "auth");

        Assert.Equal(0, exit);
        h.AssertSkillInstalled("auth");
    }

    [Fact]
    public void Sync_PicksUpNewSkillAddedToRegistry()
    {
        if (!LorexTestHarness.SymlinksAvailable()) return;

        using var h = new LorexTestHarness();
        var registry = h.CreateRegistry(skillNames: ["auth"]);
        h.Run("init", registry, "--adapters", "claude");
        h.Run("install", "auth");

        // Add a new skill to the registry
        h.AddSkillToRepo(registry, "new-skill");
        h.CommitRegistry(registry);

        // Sync and install the new skill
        h.Run("sync");
        var exit = h.Run("install", "new-skill");

        Assert.Equal(0, exit);
        h.AssertSkillInstalled("new-skill");
    }

    [Fact]
    public void Sync_AutoRemovesSkillDeletedFromRegistry()
    {
        if (!LorexTestHarness.SymlinksAvailable()) return;

        using var h = new LorexTestHarness();
        var registry = h.CreateRegistry(skillNames: ["auth", "to-delete"]);
        h.Run("init", registry, "--adapters", "claude");
        h.Run("install", "--all");

        h.AssertSkillInstalled("to-delete");

        // Delete the skill from the registry
        h.RemoveSkillFromRepo(registry, "to-delete");
        h.CommitRegistry(registry);

        var exit = h.Run("sync");

        Assert.Equal(0, exit);
        h.AssertSkillNotInstalled("to-delete");
        // Other skill should remain
        h.AssertSkillInstalled("auth");
    }

    [Fact]
    public void Sync_StaleRemoval_CleansAdapterProjections()
    {
        if (!LorexTestHarness.SymlinksAvailable()) return;

        using var h = new LorexTestHarness();
        var registry = h.CreateRegistry(skillNames: ["stale-skill"]);
        h.Run("init", registry, "--adapters", "claude");
        h.Run("install", "stale-skill");

        // Verify it was projected
        h.AssertAdapterProjected("claude", "stale-skill");

        // Delete from registry, sync should clean up both .lorex/skills and .claude/skills
        h.RemoveSkillFromRepo(registry, "stale-skill");
        h.CommitRegistry(registry);
        h.Run("sync");

        // Adapter projection should also be gone
        var adapterTarget = Path.Combine(h.ProjectRoot, ".claude", "skills", "stale-skill");
        Assert.False(
            Directory.Exists(adapterTarget),
            $"Expected adapter projection to be removed at {adapterTarget}");
    }

    [Fact]
    public void Publish_RegistryInstalledSkill_DirectMode_CommitsAndPushes()
    {
        if (!LorexTestHarness.SymlinksAvailable()) return;

        using var h = new LorexTestHarness();
        var registry = h.CreateRegistry(publishMode: "direct", skillNames: ["registry-skill"]);
        h.Run("init", registry, "--adapters", "claude");
        h.Run("install", "registry-skill");
        h.AssertIsSymlink("registry-skill");

        // Simulate editing the skill; the symlink target lives in the registry cache.
        const string modified = "---\nname: registry-skill\ndescription: Modified\nversion: 2.0.0\ntags: test\n---\n\n# Modified\n";
        h.ModifyCacheSkill(registry, "registry-skill", modified);

        var exit = h.Run("publish", "registry-skill");

        Assert.Equal(0, exit);
        var log = ServiceFactory.Git.Run(registry, "log", "--oneline", "-3");
        Assert.Contains("publish skill", log, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void EnsureCache_WithDirtyTrackedFiles_SkipsCheckout()
    {
        if (!LorexTestHarness.SymlinksAvailable()) return;

        using var h = new LorexTestHarness();
        var registry = h.CreateRegistry(publishMode: "direct", skillNames: ["auth"]);
        h.Run("init", registry, "--adapters", "claude");
        h.Run("install", "auth");

        // Modify a tracked file in the cache (simulates an in-progress skill edit).
        const string modified = "---\nname: auth\ndescription: Work in progress\nversion: 2.0.0\n---\n\n# Auth\n";
        h.ModifyCacheSkill(registry, "auth", modified);

        // Force a cache refresh — the guard in SyncCacheRepository must skip the destructive
        // checkout so the in-progress edit is preserved.
        ServiceFactory.Registry.EnsureCache(registry, forceRefresh: true);

        var cacheDir = ServiceFactory.Registry.GetCachePath(registry);
        var content = File.ReadAllText(Path.Combine(cacheDir, "skills", "auth", "SKILL.md"));
        Assert.Contains("Work in progress", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Publish_LocalSkill_AppearsInRegistry()
    {
        if (!LorexTestHarness.SymlinksAvailable()) return;

        using var h = new LorexTestHarness();
        var registry = h.CreateRegistry(publishMode: "direct");
        h.Run("init", registry, "--adapters", "claude");

        // Create a local skill and publish it
        h.Run("create", "my-contribution", "--description", "A contributed skill", "--tags", "test", "--owner", "tester");
        var exit = h.Run("publish", "my-contribution");

        Assert.Equal(0, exit);

        // After publish + sync, the skill should be a symlink (registry-backed)
        h.Run("sync");
        h.AssertSkillInstalled("my-contribution");
    }
}
