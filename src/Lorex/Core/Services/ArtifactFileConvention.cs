using Lorex.Core.Models;
using Lorex.Core.Serialization;

namespace Lorex.Core.Services;

/// <summary>
/// Centralizes lorex artifact file naming and parsing helpers.
/// </summary>
internal static class ArtifactFileConvention
{
    internal static string CanonicalPath(ArtifactKind kind, string artifactDirectory) =>
        Path.Combine(artifactDirectory, kind.CanonicalFileName());

    internal static string? ResolveEntryPath(ArtifactKind kind, string artifactDirectory)
    {
        var canonical = CanonicalPath(kind, artifactDirectory);
        if (File.Exists(canonical))
            return canonical;

        var legacyFileName = kind.LegacyFileName();
        if (legacyFileName is null)
            return null;

        var legacy = Path.Combine(artifactDirectory, legacyFileName);
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
