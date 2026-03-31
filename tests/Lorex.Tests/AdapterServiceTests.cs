using System.Text.Json.Nodes;
using Lorex.Core.Models;
using Lorex.Core.Services;

namespace Lorex.Tests;

public sealed class AdapterServiceTests
{
    private static LorexConfig MakeConfig(params string[] skills) => new()
    {
        Registry = "https://github.com/test/registry",
        Adapters = ["codex"],
        InstalledSkills = skills,
    };

    [Fact]
    public void RemoveLegacyBlock_WhenMarkersPresent_RemovesLorexSection()
    {
        const string existing = """
            Before

            <!-- lorex:start -->
            old block
            <!-- lorex:end -->

            After
            """;

        var updated = AdapterService.RemoveLegacyBlock(existing);

        Assert.DoesNotContain("old block", updated);
        Assert.Contains("Before", updated);
        Assert.Contains("After", updated);
    }

    [Fact]
    public void CleanupLegacyFile_WhenOnlyLorexBlockExists_DeletesFile()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"lorex-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var filePath = Path.Combine(tempDir, "AGENTS.md");

        try
        {
            File.WriteAllText(filePath, """
                <!-- lorex:start -->
                block
                <!-- lorex:end -->
                """);

            AdapterService.CleanupLegacyFile(filePath);

            Assert.False(File.Exists(filePath));
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void RenderCursorRule_UsesDescriptionAndBodyFromSkill()
    {
        var service = new AdapterService();
        var projectRoot = Path.Combine(Path.GetTempPath(), $"lorex-test-{Guid.NewGuid():N}");

        try
        {
            var skillDir = Path.Combine(projectRoot, ".lorex", "skills", "auth-overview");
            Directory.CreateDirectory(skillDir);
            File.WriteAllText(Path.Combine(skillDir, "SKILL.md"),
                """
                ---
                name: auth-overview
                description: Auth flows and constraints
                version: 1.0.0
                ---

                # auth-overview

                Always validate tokens.
                """);

            var rendered = service.RenderCursorRule(projectRoot, "auth-overview");

            Assert.Contains("description: \"Auth flows and constraints\"", rendered);
            Assert.Contains("Always validate tokens.", rendered);
            Assert.DoesNotContain("version: 1.0.0", rendered);
        }
        finally
        {
            if (Directory.Exists(projectRoot))
                Directory.Delete(projectRoot, recursive: true);
        }
    }

    [Fact]
    public void UpdateGeminiSettings_PopulatesLorexSkillDirectories()
    {
        var service = new AdapterService();
        var projectRoot = Path.Combine(Path.GetTempPath(), $"lorex-test-{Guid.NewGuid():N}");

        try
        {
            var settingsPath = Path.Combine(projectRoot, ".gemini", "settings.json");
            var config = MakeConfig("lorex", "architecture");

            service.UpdateGeminiSettings(projectRoot, config, settingsPath);

            var root = JsonNode.Parse(File.ReadAllText(settingsPath))!.AsObject();
            var context = root["context"]!.AsObject();
            var fileNames = context["fileName"]!.AsArray().Select(node => node!.GetValue<string>()).ToArray();
            var includeDirectories = context["includeDirectories"]!.AsArray().Select(node => node!.GetValue<string>()).ToArray();

            Assert.Contains("SKILL.md", fileNames);
            Assert.Contains("skill.md", fileNames);
            Assert.Contains(".lorex/skills/lorex", includeDirectories);
            Assert.Contains(".lorex/skills/architecture", includeDirectories);
            Assert.True(context["loadFromIncludeDirectories"]!.GetValue<bool>());
        }
        finally
        {
            if (Directory.Exists(projectRoot))
                Directory.Delete(projectRoot, recursive: true);
        }
    }

    [Fact]
    public void IsLorexManagedProjection_IsTrueOnlyForSymlinkIntoLorexSkills()
    {
        var projectRoot = Path.Combine(Path.GetTempPath(), $"lorex-test-{Guid.NewGuid():N}");

        try
        {
            var lorexSkillsRoot = Path.Combine(projectRoot, ".lorex", "skills");
            var sourceDir = Path.Combine(lorexSkillsRoot, "lorex");
            var targetRoot = Path.Combine(projectRoot, ".claude", "skills");
            var targetDir = Path.Combine(targetRoot, "lorex");

            Directory.CreateDirectory(sourceDir);
            Directory.CreateDirectory(targetRoot);
            File.WriteAllText(Path.Combine(sourceDir, "SKILL.md"), "# lorex");

            var relativeTarget = Path.GetRelativePath(targetRoot, sourceDir);
            Directory.CreateSymbolicLink(targetDir, relativeTarget);

            Assert.True(AdapterService.IsLorexManagedProjection(targetDir, lorexSkillsRoot));

            Directory.Delete(targetDir);
            Directory.CreateDirectory(targetDir);
            File.WriteAllText(Path.Combine(targetDir, "SKILL.md"), "# user");

            Assert.False(AdapterService.IsLorexManagedProjection(targetDir, lorexSkillsRoot));
        }
        finally
        {
            if (Directory.Exists(projectRoot))
                Directory.Delete(projectRoot, recursive: true);
        }
    }
}
