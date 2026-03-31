using Lorex.Commands;
using Lorex.Core.Models;

namespace Lorex.Tests;

public sealed class CommandArgumentTests
{
    [Fact]
    public void InstallCommand_ParseSkillNames_FiltersAllFlagAndDeduplicates()
    {
        var parsed = InstallCommand.ParseSkillNames(["auth", "--all", "Auth", "api"]);

        Assert.Equal(["auth", "api"], parsed);
    }

    [Fact]
    public void InstallCommand_GetInstallableSkillNames_ExcludesAlreadyInstalledSkills()
    {
        var config = new LorexConfig
        {
            InstalledSkills = ["auth"],
        };

        var available = new[]
        {
            new SkillMetadata { Name = "api", Description = "API" },
            new SkillMetadata { Name = "auth", Description = "Auth" },
            new SkillMetadata { Name = "build", Description = "Build" },
        };

        var installable = InstallCommand.GetInstallableSkillNames(available, config);

        Assert.Equal(["api", "build"], installable);
    }

    [Fact]
    public void UninstallCommand_ParseSkillNames_FiltersAllFlagAndDeduplicates()
    {
        var parsed = UninstallCommand.ParseSkillNames(["auth", "--all", "Auth", "api"]);

        Assert.Equal(["auth", "api"], parsed);
    }

    [Fact]
    public void UninstallCommand_GetInstalledSkillNames_ReturnsSortedSkillNames()
    {
        var config = new LorexConfig
        {
            InstalledSkills = ["zeta", "alpha", "beta"],
        };

        var installed = UninstallCommand.GetInstalledSkillNames(config);

        Assert.Equal(["alpha", "beta", "zeta"], installed);
    }

    [Fact]
    public void InitCommand_GetDefaultAdapters_PrefersDetectedAdapters()
    {
        var defaults = InitCommand.GetDefaultAdapters(["claude", "codex"]);

        Assert.Equal(["claude", "codex"], defaults);
    }

    [Fact]
    public void InitCommand_GetDefaultAdapters_FallsBackToCopilotAndCodex()
    {
        var defaults = InitCommand.GetDefaultAdapters([]);

        Assert.Equal(["copilot", "codex"], defaults);
    }
}
