using Lorex.Core.Serialization;

namespace Lorex.Tests;

public sealed class SimpleYamlParserTests
{
    [Fact]
    public void ParseToDictionary_BasicKeyValue_ReturnsCorrectEntries()
    {
        const string yaml = """
            name: auth-overview
            description: Authentication service overview
            version: 1.2.3
            """;

        var dict = SimpleYamlParser.ParseToDictionary(yaml);

        Assert.Equal("auth-overview", dict["name"]);
        Assert.Equal("Authentication service overview", dict["description"]);
        Assert.Equal("1.2.3", dict["version"]);
    }

    [Fact]
    public void ParseToDictionary_CommentsAndBlankLines_AreIgnored()
    {
        const string yaml = """
            # This is a comment
            name: my-skill

            description: Some desc
            """;

        var dict = SimpleYamlParser.ParseToDictionary(yaml);

        Assert.Equal(2, dict.Count);
        Assert.Equal("my-skill", dict["name"]);
    }

    [Fact]
    public void ParseToDictionary_ValueContainsColon_PreservesRemainder()
    {
        const string yaml = "description: See https://example.com for details";

        var dict = SimpleYamlParser.ParseToDictionary(yaml);

        Assert.Equal("See https://example.com for details", dict["description"]);
    }

    [Fact]
    public void ParseArtifactMetadata_FullYaml_MapsAllFields()
    {
        const string yaml = """
            name: auth-overview
            description: Authentication service overview, supported flows and constraints
            version: 1.0.0
            tags: auth, security, identity
            owner: platform-team
            """;

        var meta = SimpleYamlParser.ParseArtifactMetadata(yaml);

        Assert.Equal("auth-overview", meta.Name);
        Assert.Equal("Authentication service overview, supported flows and constraints", meta.Description);
        Assert.Equal("1.0.0", meta.Version);
        Assert.Equal(["auth", "security", "identity"], meta.Tags);
        Assert.Equal("platform-team", meta.Owner);
    }

    [Fact]
    public void ParseArtifactMetadata_MissingOptionalFields_UsesDefaults()
    {
        const string yaml = """
            name: minimal-skill
            description: A minimal skill
            """;

        var meta = SimpleYamlParser.ParseArtifactMetadata(yaml);

        Assert.Equal("1.0.0", meta.Version);
        Assert.Empty(meta.Tags);
        Assert.Equal(string.Empty, meta.Owner);
    }

    [Fact]
    public void ParseArtifactMetadata_MissingRequiredField_ThrowsInvalidDataException()
    {
        const string yaml = "description: No name here";

        Assert.Throws<InvalidDataException>(() => SimpleYamlParser.ParseArtifactMetadata(yaml));
    }

    [Fact]
    public void ParseArtifactMetadata_TagsWithExtraWhitespace_AreTrimmed()
    {
        const string yaml = """
            name: test
            description: desc
            tags:  auth ,  security ,   identity
            """;

        var meta = SimpleYamlParser.ParseArtifactMetadata(yaml);

        Assert.Equal(["auth", "security", "identity"], meta.Tags);
    }

    [Fact]
    public void ParseToDictionary_KeysAreCaseInsensitive()
    {
        const string yaml = "Name: my-skill\nDESCRIPTION: some desc";

        var dict = SimpleYamlParser.ParseToDictionary(yaml);

        Assert.Equal("my-skill", dict["name"]);
        Assert.Equal("some desc", dict["description"]);
    }

    // ── Frontmatter ───────────────────────────────────────────────────────────

    [Fact]
    public void ExtractFrontmatterYaml_WithFrontmatter_ReturnsYamlContent()
    {
        const string markdown = """
            ---
            name: auth-overview
            description: Auth flows
            version: 1.0.0
            ---

            # auth-overview

            Content here.
            """;

        var yaml = SimpleYamlParser.ExtractFrontmatterYaml(markdown);

        Assert.NotNull(yaml);
        Assert.Contains("name: auth-overview", yaml);
        Assert.Contains("description: Auth flows", yaml);
        Assert.DoesNotContain("---",    yaml);
        Assert.DoesNotContain("Content here", yaml);
    }

    [Fact]
    public void ExtractFrontmatterYaml_NoFrontmatter_ReturnsNull()
    {
        const string markdown = "# Just a heading\n\nSome content.";

        var yaml = SimpleYamlParser.ExtractFrontmatterYaml(markdown);

        Assert.Null(yaml);
    }

    [Fact]
    public void ExtractFrontmatterYaml_UnclosedDelimiter_ReturnsNull()
    {
        const string markdown = "---\nname: test\n# no closing delimiter";

        var yaml = SimpleYamlParser.ExtractFrontmatterYaml(markdown);

        Assert.Null(yaml);
    }

    [Fact]
    public void ParseArtifactMetadataFromMarkdown_WithFrontmatter_MapsAllFields()
    {
        const string markdown = """
            ---
            name: checkout-flow
            description: Checkout lifecycle and payment edge cases
            version: 2.0.0
            tags: checkout, payments, orders
            owner: commerce-team
            ---

            # checkout-flow

            Content.
            """;

        var meta = SimpleYamlParser.ParseArtifactMetadataFromMarkdown(markdown);

        Assert.Equal("checkout-flow", meta.Name);
        Assert.Equal("Checkout lifecycle and payment edge cases", meta.Description);
        Assert.Equal("2.0.0", meta.Version);
        Assert.Equal(["checkout", "payments", "orders"], meta.Tags);
        Assert.Equal("commerce-team", meta.Owner);
    }

    [Fact]
    public void ParseArtifactMetadataFromMarkdown_NoFrontmatter_ThrowsInvalidDataException()
    {
        const string markdown = "# heading\n\nNo frontmatter here.";

        Assert.Throws<InvalidDataException>(() =>
            SimpleYamlParser.ParseArtifactMetadataFromMarkdown(markdown));
    }
}
