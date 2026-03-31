using Lorex.Core.Serialization;

namespace Lorex.Core.Services;

/// <summary>
/// Centralizes lorex skill file naming and parsing helpers.
/// </summary>
internal static class SkillFileConvention
{
    internal const string CanonicalFileName = "SKILL.md";
    internal const string LegacyFileName = "skill.md";

    internal static string CanonicalPath(string skillDirectory) =>
        Path.Combine(skillDirectory, CanonicalFileName);

    internal static string? ResolveEntryPath(string skillDirectory)
    {
        var canonical = CanonicalPath(skillDirectory);
        if (File.Exists(canonical))
            return canonical;

        var legacy = Path.Combine(skillDirectory, LegacyFileName);
        return File.Exists(legacy) ? legacy : null;
    }

    internal static string ExtractBody(string markdown)
    {
        var yaml = SimpleYamlParser.ExtractFrontmatterYaml(markdown);
        if (yaml is null)
            return markdown.Trim();

        var normalized = markdown.Replace("\r\n", "\n");
        if (!normalized.StartsWith("---\n", StringComparison.Ordinal))
            return markdown.Trim();

        var closingMarker = normalized.IndexOf("\n---\n", 4, StringComparison.Ordinal);
        if (closingMarker < 0)
            return markdown.Trim();

        return normalized[(closingMarker + "\n---\n".Length)..].Trim();
    }
}
