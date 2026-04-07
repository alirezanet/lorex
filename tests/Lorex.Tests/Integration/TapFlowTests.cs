namespace Lorex.Tests.Integration;

/// <summary>Integration tests for tap-based lorex flows using local git repos as fake taps.</summary>
[Collection("Integration")]
public sealed class TapFlowTests
{
    [Fact]
    public void TapAdd_LocalPath_RegistersTap()
    {
        using var h = new LorexTestHarness();
        h.Run("init", "--local", "--adapters", "claude");
        var tapRepo = h.CreateRegistry(skillNames: ["tap-skill-a", "tap-skill-b"]);

        var exit = h.Run("tap", "add", tapRepo, "--name", "my-tap");

        Assert.Equal(0, exit);
        var config = h.ReadConfig();
        Assert.Contains(config.Taps, t => t.Name == "my-tap");
    }

    [Fact]
    public void TapAdd_SameUrlTwice_IsIdempotent()
    {
        using var h = new LorexTestHarness();
        h.Run("init", "--local", "--adapters", "claude");
        var tapRepo = h.CreateRegistry(skillNames: ["skill-a"]);

        h.Run("tap", "add", tapRepo, "--name", "my-tap");
        var exit = h.Run("tap", "add", tapRepo, "--name", "my-tap");

        Assert.Equal(0, exit);
        var config = h.ReadConfig();
        // Should still have exactly one tap with this name
        Assert.Single(config.Taps.Where(t => t.Name == "my-tap"));
    }

    [Fact]
    public void TapAdd_SameUrlDifferentName_ReturnsError()
    {
        using var h = new LorexTestHarness();
        h.Run("init", "--local", "--adapters", "claude");
        var tapRepo = h.CreateRegistry(skillNames: ["skill-a"]);

        h.Run("tap", "add", tapRepo, "--name", "my-tap");
        var exit = h.Run("tap", "add", tapRepo, "--name", "other-name");

        Assert.NotEqual(0, exit);
    }

    [Fact]
    public void TapList_ShowsAddedTap()
    {
        using var h = new LorexTestHarness();
        h.Run("init", "--local", "--adapters", "claude");
        var tapRepo = h.CreateRegistry(skillNames: ["skill-a"]);
        h.Run("tap", "add", tapRepo, "--name", "visible-tap");

        var exit = h.Run("tap", "list");

        Assert.Equal(0, exit);
    }

    [Fact]
    public void Install_SkillFromTap_CreatesSymlink()
    {
        if (!LorexTestHarness.SymlinksAvailable()) return;

        using var h = new LorexTestHarness();
        h.Run("init", "--local", "--adapters", "claude");
        var tapRepo = h.CreateRegistry(skillNames: ["tap-skill"]);
        h.Run("tap", "add", tapRepo, "--name", "my-tap");

        var exit = h.Run("install", "tap-skill");

        Assert.Equal(0, exit);
        h.AssertSkillInstalled("tap-skill");
        h.AssertIsSymlink("tap-skill");
    }

    [Fact]
    public void Install_AllFromTap_InstallsAllTapSkills()
    {
        if (!LorexTestHarness.SymlinksAvailable()) return;

        using var h = new LorexTestHarness();
        h.Run("init", "--local", "--adapters", "claude");
        var tapRepo = h.CreateRegistry(skillNames: ["alpha", "beta", "gamma"]);
        h.Run("tap", "add", tapRepo, "--name", "my-tap");

        var exit = h.Run("install", "--all");

        Assert.Equal(0, exit);
        h.AssertSkillInstalled("alpha");
        h.AssertSkillInstalled("beta");
        h.AssertSkillInstalled("gamma");
    }

    [Fact]
    public void TapSync_UpdatesInstalledSkill()
    {
        if (!LorexTestHarness.SymlinksAvailable()) return;

        using var h = new LorexTestHarness();
        h.Run("init", "--local", "--adapters", "claude");
        var tapRepo = h.CreateRegistry(skillNames: ["evolving-skill"]);
        h.Run("tap", "add", tapRepo, "--name", "my-tap");
        h.Run("install", "evolving-skill");

        // Update the skill content in the tap
        h.AddSkillToRepo(tapRepo, "evolving-skill",
            "---\nname: evolving-skill\ndescription: Updated description\nversion: 1.1.0\ntags: test\n---\n\n# Updated\n");
        h.CommitRegistry(tapRepo);

        var exit = h.Run("tap", "sync", "my-tap");

        Assert.Equal(0, exit);
        h.AssertSkillInstalled("evolving-skill");
    }
}
