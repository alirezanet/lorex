using Lorex.Core.Models;
using Lorex.Core.Services;

namespace Lorex.Tests;

public sealed class SkillServiceTests
{
    [Fact]
    public void DiscoverInstalledSkillNames_ReturnsExistingLorexSkillDirectories()
    {
        var projectRoot = Path.Combine(Path.GetTempPath(), $"lorex-test-{Guid.NewGuid():N}");

        try
        {
            Directory.CreateDirectory(Path.Combine(projectRoot, ".lorex", "skills", "lorex"));
            Directory.CreateDirectory(Path.Combine(projectRoot, ".lorex", "skills", "lorex-contributing"));

            var service = new SkillService(new RegistryService(new GitService()));
            var discovered = service.DiscoverInstalledSkillNames(projectRoot);

            Assert.Contains("lorex", discovered);
            Assert.Contains("lorex-contributing", discovered);
        }
        finally
        {
            if (Directory.Exists(projectRoot))
                Directory.Delete(projectRoot, recursive: true);
        }
    }

    [Fact]
    public void TrackInstalledVersions_PopulatesVersionsFromSkillFiles()
    {
        var projectRoot = Path.Combine(Path.GetTempPath(), $"lorex-test-{Guid.NewGuid():N}");

        try
        {
            var service = new SkillService(new RegistryService(new GitService()));
            service.WriteConfig(projectRoot, new LorexConfig { InstalledSkills = ["my-skill"] });

            var skillDir = Path.Combine(projectRoot, ".lorex", "skills", "my-skill");
            Directory.CreateDirectory(skillDir);
            File.WriteAllText(Path.Combine(skillDir, "SKILL.md"),
                "---\nname: my-skill\ndescription: test\nversion: 2.3.0\n---\n# my-skill\n");

            service.TrackInstalledVersions(projectRoot, ["my-skill"]);
            var config = service.ReadConfig(projectRoot);

            Assert.True(config.InstalledSkillVersions.TryGetValue("my-skill", out var version));
            Assert.Equal("2.3.0", version);
        }
        finally
        {
            if (Directory.Exists(projectRoot))
                Directory.Delete(projectRoot, recursive: true);
        }
    }

    [Fact]
    public void TrackInstalledVersions_SkipsSkillsWithNoSkillFile()
    {
        var projectRoot = Path.Combine(Path.GetTempPath(), $"lorex-test-{Guid.NewGuid():N}");

        try
        {
            var service = new SkillService(new RegistryService(new GitService()));
            service.WriteConfig(projectRoot, new LorexConfig { InstalledSkills = ["ghost-skill"] });

            // No skill directory / SKILL.md — should not throw and should leave versions empty
            service.TrackInstalledVersions(projectRoot, ["ghost-skill"]);
            var config = service.ReadConfig(projectRoot);

            Assert.False(config.InstalledSkillVersions.ContainsKey("ghost-skill"));
        }
        finally
        {
            if (Directory.Exists(projectRoot))
                Directory.Delete(projectRoot, recursive: true);
        }
    }

    [Fact]
    public void ReadConfig_ReturnsNonNullCollections_WhenFieldsMissingFromJson()
    {
        // Reproduces: source-generated deserializer does not apply init-property defaults
        // for fields absent from JSON — they arrive null without this normalization.
        var projectRoot = Path.Combine(Path.GetTempPath(), $"lorex-test-{Guid.NewGuid():N}");

        try
        {
            var lorexDir = Path.Combine(projectRoot, ".lorex");
            Directory.CreateDirectory(lorexDir);
            // Minimal JSON — intentionally omits adapters, installedSkills, installedSkillVersions
            File.WriteAllText(Path.Combine(lorexDir, "lorex.json"), "{}");

            var service = new SkillService(new RegistryService(new GitService()));
            var config = service.ReadConfig(projectRoot);

            Assert.NotNull(config.Adapters);
            Assert.NotNull(config.InstalledSkills);
            Assert.NotNull(config.InstalledSkillVersions);
        }
        finally
        {
            if (Directory.Exists(projectRoot))
                Directory.Delete(projectRoot, recursive: true);
        }
    }

    [Fact]
    public void GetInstalledSkillVersion_ReadsFromFileWhenNotInCache()
    {
        var projectRoot = Path.Combine(Path.GetTempPath(), $"lorex-test-{Guid.NewGuid():N}");

        try
        {
            var service = new SkillService(new RegistryService(new GitService()));
            // Config with no cached versions
            service.WriteConfig(projectRoot, new LorexConfig { InstalledSkills = ["my-skill"] });

            var skillDir = Path.Combine(projectRoot, ".lorex", "skills", "my-skill");
            Directory.CreateDirectory(skillDir);
            File.WriteAllText(Path.Combine(skillDir, "SKILL.md"),
                "---\nname: my-skill\ndescription: test\nversion: 3.1.0\n---\n# my-skill\n");

            var config = service.ReadConfig(projectRoot);
            var version = service.GetInstalledSkillVersion(projectRoot, "my-skill", config);

            Assert.Equal("3.1.0", version);
        }
        finally
        {
            if (Directory.Exists(projectRoot))
                Directory.Delete(projectRoot, recursive: true);
        }
    }

    [Fact]
    public void UninstallSkill_RemovesVersionFromInstalledSkillVersions()
    {
        var projectRoot = Path.Combine(Path.GetTempPath(), $"lorex-test-{Guid.NewGuid():N}");

        try
        {
            var service = new SkillService(new RegistryService(new GitService()));
            var skillDir = Path.Combine(projectRoot, ".lorex", "skills", "my-skill");
            Directory.CreateDirectory(skillDir);
            service.WriteConfig(projectRoot, new LorexConfig
            {
                InstalledSkills = ["my-skill"],
                InstalledSkillVersions = new Dictionary<string, string> { ["my-skill"] = "1.0.0" },
            });

            service.UninstallSkill(projectRoot, "my-skill");
            var config = service.ReadConfig(projectRoot);

            Assert.DoesNotContain("my-skill", config.InstalledSkills);
            Assert.False(config.InstalledSkillVersions.ContainsKey("my-skill"));
        }
        finally
        {
            if (Directory.Exists(projectRoot))
                Directory.Delete(projectRoot, recursive: true);
        }
    }
}
