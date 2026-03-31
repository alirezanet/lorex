using System.Reflection;

namespace Lorex.Core.Services;

/// <summary>
/// Provides access to skills that are bundled inside the lorex binary as embedded resources.
/// These are installed automatically on <c>lorex init</c> so every user's agent immediately
/// knows the lorex skill format.
/// </summary>
internal static class BuiltInSkillService
{
    private const string ResourcePrefix = "Lorex.Resources.";

    /// <summary>
    /// Returns all built-in skill names (derived from embedded resource filenames).
    /// </summary>
    internal static IEnumerable<string> SkillNames()
    {
        return Assembly.GetExecutingAssembly()
            .GetManifestResourceNames()
            .Where(n => n.StartsWith(ResourcePrefix) && n.EndsWith(".md"))
            .Select(n => n[ResourcePrefix.Length..^".md".Length]);
    }

    /// <summary>
    /// Reads the content of a built-in skill. Returns null if not found.
    /// </summary>
    internal static string? ReadSkillContent(string skillName)
    {
        var resourceName = $"{ResourcePrefix}{skillName}.md";
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName);
        if (stream is null) return null;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <summary>
    /// Installs all built-in skills into <c>.lorex/skills/</c> of the project.
    /// Writes the canonical <c>SKILL.md</c> file directly (no registry or symlink needed).
    /// Skips any skill that is already installed via the registry.
    /// </summary>
    internal static List<string> InstallAll(string projectRoot, Lorex.Core.Models.LorexConfig config)
    {
        var installed = new List<string>();

        foreach (var skillName in SkillNames())
        {
            // Don't overwrite a registry-installed skill with the same name
            if (config.InstalledSkills.Contains(skillName)) continue;

            var content = ReadSkillContent(skillName);
            if (content is null) continue;

            var skillDir = Path.Combine(projectRoot, ".lorex", "skills", skillName);
            Directory.CreateDirectory(skillDir);
            if (SkillFileConvention.ResolveEntryPath(skillDir) is not null)
                continue;

            File.WriteAllText(SkillFileConvention.CanonicalPath(skillDir), content);
            installed.Add(skillName);
        }

        return installed;
    }
}
