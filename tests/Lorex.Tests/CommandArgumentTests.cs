using Lorex.Commands;
using Lorex.Cli;
using Lorex.Core.Models;
using Lorex.Core.Services;

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
    public void InstallCommand_ParseSkillNames_FiltersRecommendedFlag()
    {
        var parsed = InstallCommand.ParseSkillNames(["auth", "--recommended", "api"]);

        Assert.Equal(["auth", "api"], parsed);
    }

    [Fact]
    public void InstallCommand_ParseSkillNames_FiltersGlobalFlag()
    {
        var parsed = InstallCommand.ParseSkillNames(["auth", "--global", "api"]);

        Assert.Equal(["auth", "api"], parsed);
    }

    [Fact]
    public void InstallCommand_WantsGlobal_DetectsGlobalFlag()
    {
        Assert.True(InstallCommand.WantsGlobal(["--global"]));
        Assert.True(InstallCommand.WantsGlobal(["--all", "--global"]));
        Assert.True(InstallCommand.WantsGlobal(["auth", "--global"]));
        Assert.False(InstallCommand.WantsGlobal(["--all"]));
        Assert.False(InstallCommand.WantsGlobal([]));
    }

    [Fact]
    public void RegistrySkillQueryService_GetInstallableSkillNames_ExcludesAlreadyInstalledSkills()
    {
        var config = new LorexConfig
        {
            InstalledSkills = ["auth"],
        };
        var service = new RegistrySkillQueryService(new RegistryService(new GitService()), new GitService(), new TapService(new GitService()));

        var available = new[]
        {
            new SkillMetadata { Name = "api", Description = "API" },
            new SkillMetadata { Name = "auth", Description = "Auth" },
            new SkillMetadata { Name = "build", Description = "Build" },
        };

        var installable = service.GetInstallableSkillNames(available, config);

        Assert.Equal(["api", "build"], installable);
    }

    [Fact]
    public void RegistrySkillQueryService_IsRecommendedForProject_MatchesProjectTags()
    {
        var service = new RegistrySkillQueryService(new RegistryService(new GitService()), new GitService(), new TapService(new GitService()));

        var matchingSkill = new SkillMetadata
        {
            Name = "api",
            Description = "API",
            Tags = ["alirezanet/lorex"],
        };
        var unrelatedSkill = new SkillMetadata
        {
            Name = "build",
            Description = "Build",
            Tags = ["dotnet"],
        };

        Assert.True(service.IsRecommendedForProject(matchingSkill, ["alirezanet/lorex", "lorex"]));
        Assert.False(service.IsRecommendedForProject(unrelatedSkill, ["alirezanet/lorex", "lorex"]));
    }

    [Fact]
    public void RegistrySkillQueryService_NormalizeProjectTag_LowercasesAndNormalizesSlashes()
    {
        var normalized = RegistrySkillQueryService.NormalizeProjectTag("AliRezaNet\\Lorex");

        Assert.Equal("alirezanet/lorex", normalized);
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

    [Fact]
    public void SkillService_SanitizeBranchSegment_NormalizesUnsafeCharacters()
    {
        var sanitized = Lorex.Core.Services.SkillService.SanitizeBranchSegment("Auth Logic/Flow!");

        Assert.Equal("auth-logic-flow", sanitized);
    }

    [Fact]
    public void RegistryPublishModes_IsValid_RecognizesSupportedModes()
    {
        Assert.True(RegistryPublishModes.IsValid(RegistryPublishModes.Direct));
        Assert.True(RegistryPublishModes.IsValid(RegistryPublishModes.PullRequest));
        Assert.True(RegistryPublishModes.IsValid(RegistryPublishModes.ReadOnly));
        Assert.False(RegistryPublishModes.IsValid("custom"));
    }

    [Fact]
    public void RegistryPolicyPrompts_OrderedChoices_PutsCurrentModeFirst()
    {
        var choices = RegistryPolicyPrompts.OrderedChoices(RegistryPublishModes.ReadOnly);

        Assert.Equal(RegistryPolicyPrompts.ReadOnlyChoice, choices[0]);
        Assert.Equal(3, choices.Count);
    }

    [Fact]
    public void RegistryPolicyPrompts_BuildPolicy_UsesProvidedPullRequestPrefix()
    {
        var policy = RegistryPolicyPrompts.BuildPolicy(
            RegistryPolicyPrompts.PullRequestChoice,
            "main",
            "review/");

        Assert.Equal(RegistryPublishModes.PullRequest, policy.PublishMode);
        Assert.Equal("main", policy.BaseBranch);
        Assert.Equal("review/", policy.PrBranchPrefix);
    }

    [Fact]
    public void RegistryService_BuildPullRequestUrl_HandlesGitHubRemotes()
    {
        var service = new Lorex.Core.Services.RegistryService(new Lorex.Core.Services.GitService());

        var httpsUrl = service.BuildPullRequestUrl("https://github.com/acme/skills.git", "lorex/auth-logic", "main");
        var sshUrl = service.BuildPullRequestUrl("git@github.com:acme/skills.git", "lorex/auth-logic", "main");

        Assert.Equal("https://github.com/acme/skills/compare/main...lorex/auth-logic?expand=1", httpsUrl);
        Assert.Equal("https://github.com/acme/skills/compare/main...lorex/auth-logic?expand=1", sshUrl);
    }

    [Fact]
    public void GitService_ParseDefaultBranchFromLsRemote_ReadsHeadSymref()
    {
        const string output = """
            ref: refs/heads/main	HEAD
            0123456789abcdef0123456789abcdef01234567	HEAD
            """;

        var branch = Lorex.Core.Services.GitService.ParseDefaultBranchFromLsRemote(output);

        Assert.Equal("main", branch);
    }

    [Fact]
    public void GitService_ParseRemoteBranchNames_ExtractsRemoteBranches()
    {
        const string output = """
            origin/HEAD
            origin/main
            origin/dev
            """;

        var branches = Lorex.Core.Services.GitService.ParseRemoteBranchNames(output, "origin");

        Assert.Equal(["dev", "main"], branches);
    }

    [Fact]
    public void GitService_ParseRepositorySlug_HandlesHttpsAndSshRemotes()
    {
        var https = Lorex.Core.Services.GitService.ParseRepositorySlug("https://github.com/alirezanet/lorex.git");
        var ssh = Lorex.Core.Services.GitService.ParseRepositorySlug("git@github.com:alirezanet/lorex.git");

        Assert.Equal("alirezanet/lorex", https);
        Assert.Equal("alirezanet/lorex", ssh);
    }

    [Fact]
    public void ListCommand_HasUpdate_ReturnsTrueWhenRegistryVersionIsNewer()
    {
        var versions = new Dictionary<string, string> { ["auth"] = "1.0.0" };
        Assert.True(ListCommand.HasUpdate("2.0.0", versions, "auth"));
        Assert.True(ListCommand.HasUpdate("1.1.0", versions, "auth"));
        Assert.True(ListCommand.HasUpdate("1.0.1", versions, "auth"));
    }

    [Fact]
    public void ListCommand_HasUpdate_ReturnsFalseWhenVersionsAreEqualOrOlder()
    {
        var versions = new Dictionary<string, string> { ["auth"] = "1.0.0" };
        Assert.False(ListCommand.HasUpdate("1.0.0", versions, "auth")); // same
        Assert.False(ListCommand.HasUpdate("0.9.0", versions, "auth")); // older
    }

    [Fact]
    public void ListCommand_HasUpdate_ReturnsFalseWhenSkillNotInCache()
    {
        var versions = new Dictionary<string, string>();
        Assert.False(ListCommand.HasUpdate("2.0.0", versions, "auth"));
    }

    [Fact]
    public void ListCommand_HasUpdate_ReturnsFalseForNonSemverStrings()
    {
        // Non-parseable versions should not throw or report an update
        var versions = new Dictionary<string, string> { ["auth"] = "latest" };
        Assert.False(ListCommand.HasUpdate("latest", versions, "auth"));
        Assert.False(ListCommand.HasUpdate("2.0.0", versions, "auth"));
    }

    // ── InstallCommand: ParseSkillNames excludes value-flag arguments ─────────

    [Fact]
    public void InstallCommand_ParseSkillNames_FiltersSearchFlagAndItsValue()
    {
        var parsed = InstallCommand.ParseSkillNames(["auth", "--search", "react", "api"]);

        Assert.Equal(["auth", "api"], parsed);
    }

    [Fact]
    public void InstallCommand_ParseSkillNames_FiltersTagFlagAndItsValue()
    {
        var parsed = InstallCommand.ParseSkillNames(["auth", "--tag", "dotnet", "api"]);

        Assert.Equal(["auth", "api"], parsed);
    }

    // ── InstallCommand: ParseSearch / ParseTag ─────────────────────────────

    [Fact]
    public void InstallCommand_ParseSearch_ReturnsSearchValue()
    {
        Assert.Equal("react", InstallCommand.ParseSearch(["--search", "react"]));
        Assert.Null(InstallCommand.ParseSearch(["auth"]));
    }

    [Fact]
    public void InstallCommand_ParseTag_ReturnsTagValue()
    {
        Assert.Equal("dotnet", InstallCommand.ParseTag(["--tag", "dotnet"]));
        Assert.Null(InstallCommand.ParseTag(["auth"]));
    }

    // ── ListCommand: flag parsing ──────────────────────────────────────────

    [Fact]
    public void ListCommand_ParseSearch_ReturnsSearchValue()
    {
        Assert.Equal("auth", ListCommand.ParseSearch(["--search", "auth"]));
    }

    [Fact]
    public void ListCommand_ParseSearch_ReturnsNullWhenAbsent()
    {
        Assert.Null(ListCommand.ParseSearch([]));
        Assert.Null(ListCommand.ParseSearch(["--tag", "dotnet"]));
    }

    [Fact]
    public void ListCommand_ParseTag_ReturnsTagValue()
    {
        Assert.Equal("dotnet", ListCommand.ParseTag(["--tag", "dotnet"]));
    }

    [Fact]
    public void ListCommand_ParsePage_DefaultsToOne()
    {
        Assert.Equal(1, ListCommand.ParsePage([]));
        Assert.Equal(1, ListCommand.ParsePage(["--search", "x"]));
    }

    [Fact]
    public void ListCommand_ParsePage_ReturnsSpecifiedValue()
    {
        Assert.Equal(3, ListCommand.ParsePage(["--page", "3"]));
    }

    [Fact]
    public void ListCommand_ParsePageSize_DefaultsTwentyFive()
    {
        Assert.Equal(25, ListCommand.ParsePageSize([]));
    }

    [Fact]
    public void ListCommand_ParsePageSize_ReturnsSpecifiedValue()
    {
        Assert.Equal(50, ListCommand.ParsePageSize(["--page-size", "50"]));
        Assert.Equal(0, ListCommand.ParsePageSize(["--page-size", "0"]));  // 0 = show all
    }

    // ── RegistrySkillQueryService.FilterBySearch ───────────────────────────

    [Fact]
    public void RegistrySkillQueryService_FilterBySearch_ReturnsAllWhenBothNull()
    {
        var service = new RegistrySkillQueryService(new RegistryService(new GitService()), new GitService(), new TapService(new GitService()));
        var skills = new[]
        {
            new SkillMetadata { Name = "auth", Description = "Auth" },
            new SkillMetadata { Name = "api",  Description = "API"  },
        };

        var result = service.FilterBySearch(skills, null, null);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void RegistrySkillQueryService_FilterBySearch_FiltersByName()
    {
        var service = new RegistrySkillQueryService(new RegistryService(new GitService()), new GitService(), new TapService(new GitService()));
        var skills = new[]
        {
            new SkillMetadata { Name = "auth-logic",    Description = "Token validation" },
            new SkillMetadata { Name = "api-standards", Description = "REST conventions" },
        };

        var result = service.FilterBySearch(skills, "auth", null);

        Assert.Single(result);
        Assert.Equal("auth-logic", result[0].Name);
    }

    [Fact]
    public void RegistrySkillQueryService_FilterBySearch_FiltersByDescription()
    {
        var service = new RegistrySkillQueryService(new RegistryService(new GitService()), new GitService(), new TapService(new GitService()));
        var skills = new[]
        {
            new SkillMetadata { Name = "auth",  Description = "Token validation and session rules" },
            new SkillMetadata { Name = "build", Description = "CI pipeline setup" },
        };

        var result = service.FilterBySearch(skills, "session", null);

        Assert.Single(result);
        Assert.Equal("auth", result[0].Name);
    }

    [Fact]
    public void RegistrySkillQueryService_FilterBySearch_FiltersByTagSubstring()
    {
        var service = new RegistrySkillQueryService(new RegistryService(new GitService()), new GitService(), new TapService(new GitService()));
        var skills = new[]
        {
            new SkillMetadata { Name = "auth",  Description = "Auth",  Tags = ["security", "dotnet"] },
            new SkillMetadata { Name = "build", Description = "Build", Tags = ["ci"]                 },
        };

        var result = service.FilterBySearch(skills, "secur", null);

        Assert.Single(result);
        Assert.Equal("auth", result[0].Name);
    }

    [Fact]
    public void RegistrySkillQueryService_FilterBySearch_FiltersByExactTag()
    {
        var service = new RegistrySkillQueryService(new RegistryService(new GitService()), new GitService(), new TapService(new GitService()));
        var skills = new[]
        {
            new SkillMetadata { Name = "auth",  Description = "Auth",  Tags = ["security", "dotnet"] },
            new SkillMetadata { Name = "build", Description = "Build", Tags = ["ci", "dotnet"]       },
            new SkillMetadata { Name = "api",   Description = "API",   Tags = ["rest"]               },
        };

        var result = service.FilterBySearch(skills, null, "dotnet");

        Assert.Equal(2, result.Count);
        Assert.DoesNotContain(result, s => s.Name == "api");
    }

    [Fact]
    public void RegistrySkillQueryService_FilterBySearch_BothFiltersApplied()
    {
        var service = new RegistrySkillQueryService(new RegistryService(new GitService()), new GitService(), new TapService(new GitService()));
        var skills = new[]
        {
            new SkillMetadata { Name = "auth",  Description = "Auth rules",  Tags = ["security", "dotnet"] },
            new SkillMetadata { Name = "build", Description = "Build rules", Tags = ["dotnet"]             },
        };

        // search matches both by description ("rules"), but tag "security" only matches auth
        var result = service.FilterBySearch(skills, "rules", "security");

        Assert.Single(result);
        Assert.Equal("auth", result[0].Name);
    }

    [Fact]
    public void SkillService_RequiresOverwriteApproval_IsTrueForLocalDirectoryAndFalseForSymlink()
    {
        var projectRoot = Path.Combine(Path.GetTempPath(), $"lorex-test-{Guid.NewGuid():N}");
        var sourceRoot = Path.Combine(Path.GetTempPath(), $"lorex-test-source-{Guid.NewGuid():N}");
        var service = new SkillService(new RegistryService(new GitService()));

        try
        {
            var localDir = Path.Combine(projectRoot, ".lorex", "skills", "local");
            var sourceDir = Path.Combine(sourceRoot, "linked");
            var linkedDir = Path.Combine(projectRoot, ".lorex", "skills", "linked");

            Directory.CreateDirectory(localDir);
            File.WriteAllText(Path.Combine(localDir, "SKILL.md"), "# local");

            Directory.CreateDirectory(sourceDir);
            File.WriteAllText(Path.Combine(sourceDir, "SKILL.md"), "# linked");

            Directory.CreateDirectory(Path.GetDirectoryName(linkedDir)!);
            Directory.CreateSymbolicLink(linkedDir, sourceDir);

            Assert.True(service.RequiresOverwriteApproval(projectRoot, "local"));
            Assert.False(service.RequiresOverwriteApproval(projectRoot, "linked"));
            Assert.False(service.RequiresOverwriteApproval(projectRoot, "missing"));
        }
        finally
        {
            if (Directory.Exists(projectRoot))
                Directory.Delete(projectRoot, recursive: true);
            if (Directory.Exists(sourceRoot))
                Directory.Delete(sourceRoot, recursive: true);
        }
    }
}
