using Lorex.Commands;
using Lorex.Cli;
using Lorex.Core.Models;
using Lorex.Core.Services;

namespace Lorex.Tests;

public sealed class CommandArgumentTests
{
    [Fact]
    public void InstallCommand_ParseArtifactNames_FiltersAllFlagAndDeduplicates()
    {
        var parsed = InstallCommand.ParseArtifactNames(["auth", "--all", "Auth", "api"]);

        Assert.Equal(["auth", "api"], parsed);
    }

    [Fact]
    public void InstallCommand_ParseArtifactNames_FiltersRecommendedFlag()
    {
        var parsed = InstallCommand.ParseArtifactNames(["auth", "--recommended", "api"]);

        Assert.Equal(["auth", "api"], parsed);
    }

    [Fact]
    public void RegistryArtifactQueryService_GetInstallableArtifactNames_ExcludesAlreadyInstalledSkills()
    {
        var config = new LorexConfig
        {
            Artifacts = new ArtifactCollection { Skills = ["auth"] },
        };
        var service = new RegistryArtifactQueryService(new RegistryService(new GitService()), new GitService());

        var available = new[]
        {
            new ArtifactMetadata { Name = "api", Description = "API" },
            new ArtifactMetadata { Name = "auth", Description = "Auth" },
            new ArtifactMetadata { Name = "build", Description = "Build" },
        };

        var installable = service.GetInstallableArtifactNames(available, config, ArtifactKind.Skill);

        Assert.Equal(["api", "build"], installable);
    }

    [Fact]
    public void RegistryArtifactQueryService_IsRecommendedForProject_MatchesProjectTags()
    {
        var service = new RegistryArtifactQueryService(new RegistryService(new GitService()), new GitService());

        var matchingSkill = new ArtifactMetadata
        {
            Name = "api",
            Description = "API",
            Tags = ["alirezanet/lorex"],
        };
        var unrelatedSkill = new ArtifactMetadata
        {
            Name = "build",
            Description = "Build",
            Tags = ["dotnet"],
        };

        Assert.True(service.IsRecommendedForProject(matchingSkill, ["alirezanet/lorex", "lorex"]));
        Assert.False(service.IsRecommendedForProject(unrelatedSkill, ["alirezanet/lorex", "lorex"]));
    }

    [Fact]
    public void RegistryArtifactQueryService_NormalizeProjectTag_LowercasesAndNormalizesSlashes()
    {
        var normalized = RegistryArtifactQueryService.NormalizeProjectTag("AliRezaNet\\Lorex");

        Assert.Equal("alirezanet/lorex", normalized);
    }

    [Fact]
    public void UninstallCommand_ParseArtifactNames_FiltersAllFlagAndDeduplicates()
    {
        var parsed = UninstallCommand.ParseArtifactNames(["auth", "--all", "Auth", "api"]);

        Assert.Equal(["auth", "api"], parsed);
    }

    [Fact]
    public void UninstallCommand_GetInstalledArtifactNames_ReturnsSortedArtifactNames()
    {
        var config = new LorexConfig
        {
            Artifacts = new ArtifactCollection { Skills = ["zeta", "alpha", "beta"] },
        };

        var installed = UninstallCommand.GetInstalledArtifactNames(config, ArtifactKind.Skill);

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
    public void ArtifactService_SanitizeBranchSegment_NormalizesUnsafeCharacters()
    {
        var sanitized = Lorex.Core.Services.ArtifactService.SanitizeBranchSegment("Auth Logic/Flow!");

        Assert.Equal("auth-logic-flow", sanitized);
    }

    [Fact]
    public void ArtifactCliSupport_ParseArtifactTypeOrDefault_DefaultsToSkillWithoutConsumingTagFlag()
    {
        var parsed = ArtifactCliSupport.ParseArtifactTypeOrDefault(["conventions", "-t", "dotnet,backend"]);

        Assert.False(parsed.HasExplicitType);
        Assert.Equal(ArtifactKind.Skill, parsed.Kind);
        Assert.Equal(["conventions", "-t", "dotnet,backend"], parsed.RemainingArgs);
    }

    [Fact]
    public void ArtifactCliSupport_ParseArtifactTypeOrDefault_ParsesExplicitPromptType()
    {
        var parsed = ArtifactCliSupport.ParseArtifactTypeOrDefault(["--type", "prompt", "review"]);

        Assert.True(parsed.HasExplicitType);
        Assert.Equal(ArtifactKind.Prompt, parsed.Kind);
        Assert.Equal(["review"], parsed.RemainingArgs);
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
    public void ArtifactService_RequiresOverwriteApproval_IsTrueForLocalDirectoryAndFalseForSymlink()
    {
        var projectRoot = Path.Combine(Path.GetTempPath(), $"lorex-test-{Guid.NewGuid():N}");
        var sourceRoot = Path.Combine(Path.GetTempPath(), $"lorex-test-source-{Guid.NewGuid():N}");
        var service = new ArtifactService(new RegistryService(new GitService()));

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

            Assert.True(service.RequiresOverwriteApproval(projectRoot, ArtifactKind.Skill, "local"));
            Assert.False(service.RequiresOverwriteApproval(projectRoot, ArtifactKind.Skill, "linked"));
            Assert.False(service.RequiresOverwriteApproval(projectRoot, ArtifactKind.Skill, "missing"));
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
