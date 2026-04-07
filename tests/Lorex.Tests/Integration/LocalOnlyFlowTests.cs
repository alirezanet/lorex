namespace Lorex.Tests.Integration;

/// <summary>Integration tests for local-only lorex flows (no registry, no network).</summary>
[Collection("Integration")]
public sealed class LocalOnlyFlowTests
{
    [Fact]
    public void InitLocal_CreatesLorexJson()
    {
        using var h = new LorexTestHarness();

        var exit = h.Run("init", "--local", "--adapters", "claude");

        Assert.Equal(0, exit);
        Assert.True(File.Exists(Path.Combine(h.ProjectRoot, ".lorex", "lorex.json")));
    }

    [Fact]
    public void InitLocal_ThenCreate_SkillAppearsInConfig()
    {
        using var h = new LorexTestHarness();
        h.Run("init", "--local", "--adapters", "claude");

        var exit = h.Run("create", "my-skill", "--description", "A test skill", "--tags", "test", "--owner", "tester");

        Assert.Equal(0, exit);
        h.AssertSkillInstalled("my-skill");
        h.AssertIsRealDir("my-skill");

        var config = h.ReadConfig();
        Assert.Contains("my-skill", config.InstalledSkills);
    }

    [Fact]
    public void InitLocal_ThenCreate_ThenStatus_ReturnsZero()
    {
        using var h = new LorexTestHarness();
        h.Run("init", "--local", "--adapters", "claude");
        h.Run("create", "status-skill", "--description", "desc", "--tags", "", "--owner", "");

        var exit = h.Run("status");

        Assert.Equal(0, exit);
    }

    [Fact]
    public void InitLocal_ThenCreate_ThenRefresh_ProjectsToAdapter()
    {
        if (!LorexTestHarness.SymlinksAvailable()) return;

        using var h = new LorexTestHarness();
        h.Run("init", "--local", "--adapters", "claude");
        h.Run("create", "refresh-skill", "--description", "desc", "--tags", "", "--owner", "");

        var exit = h.Run("refresh");

        Assert.Equal(0, exit);
        h.AssertAdapterProjected("claude", "refresh-skill");
    }

    [Fact]
    public void InitLocal_WithNoAdapters_ReturnsZero()
    {
        using var h = new LorexTestHarness();

        var exit = h.Run("init", "--local", "--adapters", "");

        Assert.Equal(0, exit);
        var config = h.ReadConfig();
        Assert.Empty(config.Adapters);
    }

    [Fact]
    public void Create_WithoutPriorInit_ReturnsNonZero()
    {
        using var h = new LorexTestHarness();
        // No init — ProjectRootLocator should throw / return error
        var exit = h.Run("create", "orphan-skill");
        Assert.NotEqual(0, exit);
    }

    [Fact]
    public void InitLocal_BuiltInSkillsAreInstalled()
    {
        using var h = new LorexTestHarness();
        h.Run("init", "--local", "--adapters", "claude");

        var config = h.ReadConfig();
        // lorex built-in skill is always seeded
        Assert.Contains("lorex", config.InstalledSkills);
    }
}
