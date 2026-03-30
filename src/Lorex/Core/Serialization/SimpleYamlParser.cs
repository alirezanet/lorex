using Lorex.Core.Models;

namespace Lorex.Core.Serialization;

/// <summary>
/// Minimal YAML parser for lorex skill files.
/// Supports:
///   - Standalone key: value YAML
///   - YAML frontmatter between --- delimiters at the top of a markdown file
///   - tags: val1, val2, val3 (comma-separated → string[])
///   - Blank lines and # comments are ignored
/// </summary>
public static class SimpleYamlParser
{
    // ── Frontmatter ───────────────────────────────────────────────────────────

    /// <summary>
    /// Extracts the YAML string from between --- delimiters at the start of a markdown file.
    /// Returns null if no frontmatter block is present.
    /// </summary>
    public static string? ExtractFrontmatterYaml(string markdownContent)
    {
        var lines = markdownContent.Split('\n');
        if (lines.Length < 2 || lines[0].Trim() != "---")
            return null;

        for (var i = 1; i < lines.Length; i++)
        {
            if (lines[i].Trim() == "---")
                return string.Join('\n', lines[1..i]).Trim();
        }

        return null; // no closing ---
    }

    /// <summary>Parses skill metadata from YAML frontmatter in a markdown file.</summary>
    public static SkillMetadata ParseSkillMetadataFromMarkdown(string markdownContent)
    {
        var yaml = ExtractFrontmatterYaml(markdownContent)
            ?? throw new InvalidDataException(
                "Markdown file has no YAML frontmatter. Expected --- delimiters at the top of the file.");
        return ParseSkillMetadata(yaml);
    }

    // ── Standalone YAML ───────────────────────────────────────────────────────

    public static SkillMetadata ParseSkillMetadata(string yaml)
    {
        var dict = ParseToDictionary(yaml);

        return new SkillMetadata
        {
            Name = GetRequired(dict, "name"),
            Description = GetRequired(dict, "description"),
            Version = dict.TryGetValue("version", out var v) ? v : "1.0.0",
            Tags = dict.TryGetValue("tags", out var t)
                ? t.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                : [],
            Owner = dict.TryGetValue("owner", out var o) ? o : string.Empty,
        };
    }

    public static Dictionary<string, string> ParseToDictionary(string yaml)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var rawLine in yaml.AsSpan().EnumerateLines())
        {
            var line = rawLine.Trim();

            if (line.IsEmpty || line[0] == '#')
                continue;

            var colonIndex = line.IndexOf(':');
            if (colonIndex <= 0)
                continue;

            var key = line[..colonIndex].Trim().ToString();
            var value = line[(colonIndex + 1)..].Trim().ToString();

            result[key] = value;
        }

        return result;
    }

    private static string GetRequired(Dictionary<string, string> dict, string key)
    {
        if (!dict.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
            throw new InvalidDataException($"Required YAML field '{key}' is missing or empty.");
        return value;
    }
}
