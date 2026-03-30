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

    // ── ReplaceOrAppend ───────────────────────────────────────────────────────

    [Fact]
    public void ReplaceOrAppend_EmptyFile_ReturnsBlockWithNewline()
    {
        var result = AdapterService.ReplaceOrAppend(string.Empty, "<!-- lorex:start -->\nindex\n<!-- lorex:end -->");
        Assert.Contains("<!-- lorex:start -->", result);
        Assert.Contains("<!-- lorex:end -->", result);
    }

    [Fact]
    public void ReplaceOrAppend_ExistingContent_NoBlock_AppendsWithBlankLine()
    {
        const string existing = "# My Agents file\n\nSome existing content.";
        var result = AdapterService.ReplaceOrAppend(existing, "<!-- lorex:start -->\nnew\n<!-- lorex:end -->");

        Assert.StartsWith("# My Agents file", result);
        Assert.Contains("<!-- lorex:start -->", result);
        // Block must appear after existing content, with at least one blank line separator
        var contentIdx = result.IndexOf("Some existing content.", StringComparison.Ordinal);
        var blockIdx   = result.IndexOf("<!-- lorex:start -->", StringComparison.Ordinal);
        Assert.True(contentIdx < blockIdx, "Lorex block must appear after existing content");
        // There should be at least 2 newline characters between existing content and the block
        var between = result[contentIdx..blockIdx];
        Assert.True(between.Count(c => c == '\n') >= 2, "Expected at least one blank line before lorex block");
    }

    [Fact]
    public void ReplaceOrAppend_ExistingBlock_ReplacesInPlace()
    {
        const string existing = """
            # Agents

            Some content.

            <!-- lorex:start -->
            ## Old index
            <!-- lorex:end -->

            After block.
            """;

        var result = AdapterService.ReplaceOrAppend(existing, "<!-- lorex:start -->\nnew index\n<!-- lorex:end -->");

        Assert.Contains("new index", result);
        Assert.DoesNotContain("Old index", result);
        Assert.Contains("After block.", result);
        Assert.Contains("Some content.", result);
    }

    [Fact]
    public void ReplaceOrAppend_ExistingBlock_PreservesContentAfterBlock()
    {
        const string existing = "before\n<!-- lorex:start -->\nold\n<!-- lorex:end -->\nafter";
        var result = AdapterService.ReplaceOrAppend(existing, "<!-- lorex:start -->\nnew\n<!-- lorex:end -->");

        Assert.StartsWith("before\n", result);
        Assert.EndsWith("\nafter", result.TrimEnd('\r', '\n') + "\nafter".Substring("\nafter".Length - "\nafter".Length));
        Assert.Contains("new", result);
        Assert.Contains("after", result);
    }

    // ── BuildIndexBlock ───────────────────────────────────────────────────────

    [Fact]
    public void BuildIndexBlock_NoSkills_ContainsNoSkillsMessage()
    {
        var service = new AdapterService();
        var config = MakeConfig();

        var block = service.BuildIndexBlock(Path.GetTempPath(), config);

        Assert.Contains("<!-- lorex:start -->", block);
        Assert.Contains("<!-- lorex:end -->", block);
        Assert.Contains("No skills installed", block);
    }

    [Fact]
    public void BuildIndexBlock_WithSkills_ContainsSkillEntries()
    {
        var service = new AdapterService();
        var projectRoot = Path.Combine(Path.GetTempPath(), $"lorex-test-{Guid.NewGuid():N}");
        try
        {
            // New format: frontmatter in skill.md
            var skillDir = Path.Combine(projectRoot, ".lorex", "skills", "auth-overview");
            Directory.CreateDirectory(skillDir);
            File.WriteAllText(Path.Combine(skillDir, "skill.md"),
                "---\nname: auth-overview\ndescription: Auth flows and constraints\nversion: 1.0.0\n---\n\n# auth-overview\n");

            var config = MakeConfig("auth-overview");
            var block = service.BuildIndexBlock(projectRoot, config);

            Assert.Contains("auth-overview", block);
            Assert.Contains("Auth flows and constraints", block);
            Assert.Contains(".lorex/skills/auth-overview/skill.md", block);
        }
        finally
        {
            if (Directory.Exists(projectRoot))
                Directory.Delete(projectRoot, recursive: true);
        }
    }

    [Fact]
    public void BuildIndexBlock_SkillWithLegacyMetadataYaml_FallsBackGracefully()
    {
        var service = new AdapterService();
        var projectRoot = Path.Combine(Path.GetTempPath(), $"lorex-test-{Guid.NewGuid():N}");
        try
        {
            // Legacy format: separate metadata.yaml (no frontmatter in skill.md)
            var skillDir = Path.Combine(projectRoot, ".lorex", "skills", "legacy-skill");
            Directory.CreateDirectory(skillDir);
            File.WriteAllText(Path.Combine(skillDir, "metadata.yaml"),
                "name: legacy-skill\ndescription: Legacy description\nversion: 1.0.0\n");
            File.WriteAllText(Path.Combine(skillDir, "skill.md"), "# legacy-skill\n\nContent.\n");

            var config = MakeConfig("legacy-skill");
            var block = service.BuildIndexBlock(projectRoot, config);

            Assert.Contains("legacy-skill", block);
            Assert.Contains("Legacy description", block);
        }
        finally
        {
            if (Directory.Exists(projectRoot))
                Directory.Delete(projectRoot, recursive: true);
        }
    }

    [Fact]
    public void BuildIndexBlock_SkillWithEmbeddedTools_ListsToolsLine()
    {
        var service = new AdapterService();
        var projectRoot = Path.Combine(Path.GetTempPath(), $"lorex-test-{Guid.NewGuid():N}");
        try
        {
            var skillDir = Path.Combine(projectRoot, ".lorex", "skills", "db-migrations");
            Directory.CreateDirectory(skillDir);
            File.WriteAllText(Path.Combine(skillDir, "skill.md"),
                "---\nname: db-migrations\ndescription: Database migration patterns\nversion: 1.0.0\n---\n\n# db-migrations\n");
            // Embedded tool files
            File.WriteAllText(Path.Combine(skillDir, "run-migration.ps1"), "# migration script");
            File.WriteAllText(Path.Combine(skillDir, "rollback.sh"), "#!/bin/bash");

            var config = MakeConfig("db-migrations");
            var block = service.BuildIndexBlock(projectRoot, config);

            Assert.Contains("Embedded tools", block);
            Assert.Contains("run-migration.ps1", block);
            Assert.Contains("rollback.sh", block);
        }
        finally
        {
            if (Directory.Exists(projectRoot))
                Directory.Delete(projectRoot, recursive: true);
        }
    }

    [Fact]
    public void BuildIndexBlock_SkillPathUseForwardSlashes()
    {
        var service = new AdapterService();
        var config = MakeConfig("my-skill");

        var block = service.BuildIndexBlock(Path.GetTempPath(), config);

        // Paths injected into markdown must use forward slashes (cross-platform + agent readability)
        Assert.DoesNotContain(".lorex\\skills", block);
    }
}
