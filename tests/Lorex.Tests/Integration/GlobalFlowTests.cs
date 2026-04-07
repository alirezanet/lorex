namespace Lorex.Tests.Integration;

/// <summary>Integration tests for global lorex flows (--global flag, ~/.lorex equivalent).</summary>
[Collection("Integration")]
public sealed class GlobalFlowTests
{
    [Fact]
    public void InitGlobal_CreatesGlobalLorexJson()
    {
        using var h = new LorexTestHarness();

        var exit = h.RunGlobal("init", "--local", "--adapters", "claude");

        Assert.Equal(0, exit);
        Assert.True(File.Exists(Path.Combine(h.GlobalRoot, ".lorex", "lorex.json")));
    }

    [Fact]
    public void InitGlobal_WithLocalRegistry_ConnectsSuccessfully()
    {
        using var h = new LorexTestHarness();
        var registry = h.CreateRegistry(skillNames: ["global-skill"]);

        var exit = h.RunGlobal("init", registry, "--adapters", "claude");

        Assert.Equal(0, exit);
        var config = h.ReadGlobalConfig();
        Assert.NotNull(config.Registry);
    }

    [Fact]
    public void InstallGlobal_SkillLandsInGlobalRoot()
    {
        if (!LorexTestHarness.SymlinksAvailable()) return;

        using var h = new LorexTestHarness();
        var registry = h.CreateRegistry(skillNames: ["global-skill"]);
        h.RunGlobal("init", registry, "--adapters", "claude");

        var exit = h.RunGlobal("install", "global-skill");

        Assert.Equal(0, exit);

        // Skill should land in global root, not project root
        var globalSkillDir = Path.Combine(h.GlobalRoot, ".lorex", "skills", "global-skill");
        Assert.True(Directory.Exists(globalSkillDir), $"Expected global skill at {globalSkillDir}");

        // Project root should NOT have the skill
        var projectSkillDir = Path.Combine(h.ProjectRoot, ".lorex", "skills", "global-skill");
        Assert.False(Directory.Exists(projectSkillDir), "Global skill should not appear in project root");
    }

    [Fact]
    public void StatusGlobal_ReturnsZero_WhenGlobalInitialised()
    {
        using var h = new LorexTestHarness();
        h.RunGlobal("init", "--local", "--adapters", "claude");

        var exit = h.RunGlobal("status");

        Assert.Equal(0, exit);
    }

    [Fact]
    public void StatusGlobal_ReturnsError_WhenNotInitialised()
    {
        using var h = new LorexTestHarness();
        // Do NOT call init --global

        var exit = h.RunGlobal("status");

        Assert.NotEqual(0, exit);
    }

    [Fact]
    public void SyncGlobal_UpdatesGlobalSkills()
    {
        if (!LorexTestHarness.SymlinksAvailable()) return;

        using var h = new LorexTestHarness();
        var registry = h.CreateRegistry(skillNames: ["global-skill"]);
        h.RunGlobal("init", registry, "--adapters", "claude");
        h.RunGlobal("install", "global-skill");

        // Add a new skill to the registry
        h.AddSkillToRepo(registry, "new-global-skill");
        h.CommitRegistry(registry);

        var exit = h.RunGlobal("sync");

        Assert.Equal(0, exit);

        // Existing global skill should still be installed
        var globalSkillDir = Path.Combine(h.GlobalRoot, ".lorex", "skills", "global-skill");
        Assert.True(Directory.Exists(globalSkillDir));
    }

    [Fact]
    public void GlobalAndProjectSkillsAreIndependent()
    {
        if (!LorexTestHarness.SymlinksAvailable()) return;

        using var h = new LorexTestHarness();
        var registry = h.CreateRegistry(skillNames: ["shared-skill"]);

        // Init both project and global with the same registry
        h.Run("init", registry, "--adapters", "claude");
        h.RunGlobal("init", registry, "--adapters", "claude");

        h.Run("install", "shared-skill");
        h.RunGlobal("install", "shared-skill");

        // Both should have the skill, in their respective roots
        var projectSkillDir = Path.Combine(h.ProjectRoot, ".lorex", "skills", "shared-skill");
        var globalSkillDir  = Path.Combine(h.GlobalRoot,  ".lorex", "skills", "shared-skill");

        Assert.True(Directory.Exists(projectSkillDir), "Project skill missing");
        Assert.True(Directory.Exists(globalSkillDir),  "Global skill missing");
        // They should be independent (different paths)
        Assert.NotEqual(projectSkillDir, globalSkillDir);
    }
}
