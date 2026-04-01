using System.Text.Json;
using Lorex.Core.Adapters;
using Lorex.Core.Models;
using Lorex.Core.Serialization;

namespace Lorex.Core.Services;

/// <summary>
/// Projects lorex skills into each configured agent's native integration surface.
/// </summary>
public sealed class AdapterService
{
    public static readonly IReadOnlyDictionary<string, IAdapter> KnownAdapters =
        new Dictionary<string, IAdapter>(StringComparer.OrdinalIgnoreCase)
        {
            ["copilot"] = new CopilotAdapter(),
            ["codex"] = new CodexAdapter(),
            ["cursor"] = new CursorAdapter(),
            ["claude"] = new ClaudeAdapter(),
            ["windsurf"] = new WindsurfAdapter(),
            ["cline"] = new ClineAdapter(),
            ["roo"] = new RooAdapter(),
            ["gemini"] = new GeminiAdapter(),
            ["opencode"] = new OpenCodeAdapter(),
        };

    /// <summary>
    /// Projects skills into every configured adapter.
    /// </summary>
    public void Project(string projectRoot, LorexConfig config)
    {
        foreach (var adapterName in config.Adapters)
        {
            if (!KnownAdapters.TryGetValue(adapterName, out var adapter))
                continue;

            ProjectAdapter(projectRoot, config, adapter);
        }
    }

    /// <summary>
    /// Projects skills into a single adapter.
    /// </summary>
    public void ProjectTarget(string projectRoot, LorexConfig config, string adapterName)
    {
        if (!KnownAdapters.TryGetValue(adapterName, out var adapter))
            throw new ArgumentException($"Unknown adapter '{adapterName}'. Known adapters: {string.Join(", ", KnownAdapters.Keys)}");

        ProjectAdapter(projectRoot, config, adapter);
    }

    /// <summary>
    /// Returns the paths this adapter manages in the current project.
    /// </summary>
    public IReadOnlyList<string> DescribeTargets(string projectRoot, string adapterName)
    {
        if (!KnownAdapters.TryGetValue(adapterName, out var adapter))
            throw new ArgumentException($"Unknown adapter '{adapterName}'. Known adapters: {string.Join(", ", KnownAdapters.Keys)}");

        return adapter.GetProjection(projectRoot) switch
        {
            SkillDirectoryProjection skillRoot => [skillRoot.RootPath],
            CursorRulesProjection cursor => [cursor.RulesDirectory],
            RooRulesProjection roo => [roo.RulesDirectory],
            GeminiContextProjection gemini => [gemini.SettingsPath],
            _ => []
        };
    }

    internal string RenderCursorRule(string projectRoot, string skillName)
    {
        var description = GetSkillDescription(projectRoot, skillName);
        var body = GetSkillBody(projectRoot, skillName);
        return
            $"---\n" +
            $"description: \"{EscapeYamlScalar(description)}\"\n" +
            "alwaysApply: false\n" +
            "---\n\n" +
            body +
            "\n";
    }

    internal string RenderRooRule(string projectRoot, string skillName)
    {
        var description = GetSkillDescription(projectRoot, skillName);
        var body = GetSkillBody(projectRoot, skillName);
        return
            $"# {skillName}\n\n" +
            $"Use this lorex skill when the task matches: {description}\n\n" +
            body +
            "\n";
    }

    internal void UpdateGeminiSettings(string projectRoot, LorexConfig config, string settingsPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);

        using var existing = ReadJsonDocument(settingsPath);
        var root = existing?.RootElement;
        JsonElement? existingContext = null;
        if (root is { ValueKind: JsonValueKind.Object } rootObject &&
            rootObject.TryGetProperty("context", out var contextElement) &&
            contextElement.ValueKind == JsonValueKind.Object)
        {
            existingContext = contextElement;
        }

        var fileNames = ReadStringValues(existingContext, "fileName", StringComparer.Ordinal);
        fileNames.Add(SkillFileConvention.CanonicalFileName);
        fileNames.Add(SkillFileConvention.LegacyFileName);

        var includeDirectories = ReadStringValues(existingContext, "includeDirectories", StringComparer.OrdinalIgnoreCase)
            .Where(path => !path.Replace('\\', '/').StartsWith(".lorex/skills/", StringComparison.OrdinalIgnoreCase))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var skillName in config.InstalledSkills)
            includeDirectories.Add(Path.Combine(".lorex", "skills", skillName).Replace('\\', '/'));

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();

            if (root is { ValueKind: JsonValueKind.Object } rootElement)
            {
                foreach (var property in rootElement.EnumerateObject())
                {
                    if (property.NameEquals("context"))
                        continue;

                    property.WriteTo(writer);
                }
            }

            writer.WritePropertyName("context");
            writer.WriteStartObject();

            if (existingContext is { ValueKind: JsonValueKind.Object } contextObject)
            {
                foreach (var property in contextObject.EnumerateObject())
                {
                    if (property.NameEquals("fileName") ||
                        property.NameEquals("includeDirectories") ||
                        property.NameEquals("loadFromIncludeDirectories"))
                    {
                        continue;
                    }

                    property.WriteTo(writer);
                }
            }

            WriteStringArray(writer, "fileName", fileNames.OrderBy(name => name, StringComparer.Ordinal));
            WriteStringArray(writer, "includeDirectories", includeDirectories.OrderBy(path => path, StringComparer.Ordinal));
            writer.WriteBoolean("loadFromIncludeDirectories", true);

            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        File.WriteAllText(settingsPath, System.Text.Encoding.UTF8.GetString(stream.ToArray()) + "\n");
    }

    private void ProjectAdapter(string projectRoot, LorexConfig config, IAdapter adapter)
    {
        switch (adapter.GetProjection(projectRoot))
        {
            case SkillDirectoryProjection projection:
                ProjectSkillDirectory(projectRoot, config, projection.RootPath);
                break;

            case CursorRulesProjection projection:
                ProjectCursorRules(projectRoot, config, projection.RulesDirectory);
                break;

            case RooRulesProjection projection:
                ProjectRooRules(projectRoot, config, projection.RulesDirectory);
                break;

            case GeminiContextProjection projection:
                UpdateGeminiSettings(projectRoot, config, projection.SettingsPath);
                break;
        }
    }

    private void ProjectSkillDirectory(string projectRoot, LorexConfig config, string rootPath)
    {
        Directory.CreateDirectory(rootPath);

        var installed = config.InstalledSkills.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var lorexSkillsRoot = Path.Combine(projectRoot, ".lorex", "skills");

        foreach (var dir in Directory.EnumerateDirectories(rootPath))
        {
            var name = Path.GetFileName(dir);
            if (!installed.Contains(name) && IsLorexManagedProjection(dir, lorexSkillsRoot))
                Directory.Delete(dir, recursive: true);
        }

        foreach (var skillName in config.InstalledSkills)
        {
            var sourceDir = Path.Combine(projectRoot, ".lorex", "skills", skillName);
            if (!Directory.Exists(sourceDir))
                continue;

            var targetDir = Path.Combine(rootPath, skillName);
            EnsureSkillProjection(projectRoot, sourceDir, targetDir, lorexSkillsRoot);
        }
    }

    private void ProjectCursorRules(string projectRoot, LorexConfig config, string rulesDirectory)
    {
        Directory.CreateDirectory(rulesDirectory);
        RemoveProjectedFiles(rulesDirectory, "*.mdc");

        foreach (var skillName in config.InstalledSkills)
        {
            var path = Path.Combine(rulesDirectory, $"lorex-{skillName}.mdc");
            File.WriteAllText(path, RenderCursorRule(projectRoot, skillName));
        }
    }

    private void ProjectRooRules(string projectRoot, LorexConfig config, string rulesDirectory)
    {
        Directory.CreateDirectory(rulesDirectory);
        RemoveProjectedFiles(rulesDirectory, "*.md");

        foreach (var skillName in config.InstalledSkills)
        {
            var path = Path.Combine(rulesDirectory, $"lorex-{skillName}.md");
            File.WriteAllText(path, RenderRooRule(projectRoot, skillName));
        }
    }

    private static void EnsureSkillProjection(string projectRoot, string sourceDir, string targetDir, string lorexSkillsRoot)
    {
        if (SkillFileConvention.ResolveEntryPath(sourceDir) is null)
            throw new InvalidOperationException($"Skill directory '{sourceDir}' is missing {SkillFileConvention.CanonicalFileName}.");

        if (Directory.Exists(targetDir))
        {
            if (!IsLorexManagedProjection(targetDir, lorexSkillsRoot))
                return;

            var currentTarget = ResolveDirectoryLinkTarget(targetDir);
            if (currentTarget is not null &&
                PathsEqual(currentTarget, sourceDir))
            {
                return;
            }

            Directory.Delete(targetDir, recursive: true);
        }

        if (!TryCreateDirectorySymlink(projectRoot, targetDir, sourceDir))
            throw new InvalidOperationException(
                $"Lorex requires symlink support for native skill projections. Failed to create '{targetDir}'.");
    }

    private static bool TryCreateDirectorySymlink(string projectRoot, string linkPath, string targetPath)
    {
        try
        {
            Directory.CreateSymbolicLink(linkPath, GetSymlinkTarget(projectRoot, linkPath, targetPath));
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static bool TryCreateFileSymlink(string projectRoot, string linkPath, string targetPath)
    {
        try
        {
            File.CreateSymbolicLink(linkPath, GetSymlinkTarget(projectRoot, linkPath, targetPath));
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    internal static bool IsLorexManagedProjection(string directoryPath, string lorexSkillsRoot)
    {
        if (!Directory.Exists(directoryPath))
            return false;

        var target = ResolveDirectoryLinkTarget(directoryPath);
        return target is not null && IsWithin(target, lorexSkillsRoot);
    }

    private static string? ResolveDirectoryLinkTarget(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
            return null;

        var info = new DirectoryInfo(directoryPath);
        var target = info.LinkTarget;
        if (target is null)
            return null;

        if (Path.IsPathRooted(target))
            return Path.GetFullPath(target);

        var parent = info.Parent?.FullName
            ?? throw new InvalidOperationException($"Cannot determine parent directory for '{directoryPath}'.");
        return Path.GetFullPath(Path.Combine(parent, target));
    }

    private static void RemoveProjectedFiles(string directory, string searchPattern)
    {
        foreach (var file in Directory.EnumerateFiles(directory, searchPattern))
        {
            if (Path.GetFileName(file).StartsWith("lorex-", StringComparison.OrdinalIgnoreCase))
                File.Delete(file);
        }
    }

    private static JsonDocument? ReadJsonDocument(string path)
    {
        if (!File.Exists(path))
            return null;

        var json = File.ReadAllText(path);
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static HashSet<string> ReadStringValues(JsonElement? parent, string propertyName, IEqualityComparer<string> comparer)
    {
        var values = new HashSet<string>(comparer);
        if (parent is not { ValueKind: JsonValueKind.Object } parentObject ||
            !parentObject.TryGetProperty(propertyName, out var property))
        {
            return values;
        }

        switch (property.ValueKind)
        {
            case JsonValueKind.Array:
                foreach (var item in property.EnumerateArray())
                {
                    var value = item.GetString();
                    if (!string.IsNullOrWhiteSpace(value))
                        values.Add(value);
                }
                break;

            case JsonValueKind.String:
                var single = property.GetString();
                if (!string.IsNullOrWhiteSpace(single))
                    values.Add(single);
                break;
        }

        return values;
    }

    internal static string GetSymlinkTarget(string projectRoot, string linkPath, string targetPath)
    {
        var fullProjectRoot = Path.GetFullPath(projectRoot)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var fullLinkPath = Path.GetFullPath(linkPath);
        var fullTargetPath = Path.GetFullPath(targetPath);

        if (IsWithin(fullLinkPath, fullProjectRoot) && IsWithin(fullTargetPath, fullProjectRoot))
        {
            var linkDirectory = Path.GetDirectoryName(fullLinkPath)
                ?? throw new InvalidOperationException($"Cannot determine parent directory for '{linkPath}'.");
            return Path.GetRelativePath(linkDirectory, fullTargetPath);
        }

        return fullTargetPath;
    }

    private static void WriteStringArray(Utf8JsonWriter writer, string propertyName, IEnumerable<string> values)
    {
        writer.WritePropertyName(propertyName);
        writer.WriteStartArray();
        foreach (var value in values)
            writer.WriteStringValue(value);
        writer.WriteEndArray();
    }

    private static string EscapeYamlScalar(string value) =>
        value.Replace("\\", "\\\\").Replace("\"", "\\\"");

    private static bool IsWithin(string path, string root)
    {
        var normalizedPath = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var normalizedRoot = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        if (normalizedPath.Equals(normalizedRoot, StringComparison.OrdinalIgnoreCase))
            return true;

        var prefix = normalizedRoot + Path.DirectorySeparatorChar;
        return normalizedPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }

    private static bool PathsEqual(string left, string right) =>
        Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Equals(
                Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase);

    private static string GetSkillDescription(string projectRoot, string skillName)
    {
        var skillDir = Path.Combine(projectRoot, ".lorex", "skills", skillName);

        var entryPath = SkillFileConvention.ResolveEntryPath(skillDir);
        if (entryPath is not null)
        {
            try
            {
                var yaml = SimpleYamlParser.ExtractFrontmatterYaml(File.ReadAllText(entryPath));
                if (yaml is not null)
                {
                    var dict = SimpleYamlParser.ParseToDictionary(yaml);
                    if (dict.TryGetValue("description", out var description))
                        return description;
                }
            }
            catch
            {
                // Fall through to legacy metadata.yaml.
            }
        }

        var metadataPath = Path.Combine(skillDir, "metadata.yaml");
        if (File.Exists(metadataPath))
        {
            try
            {
                var dict = SimpleYamlParser.ParseToDictionary(File.ReadAllText(metadataPath));
                if (dict.TryGetValue("description", out var description))
                    return description;
            }
            catch
            {
                // Ignore malformed legacy metadata.
            }
        }

        return skillName;
    }

    private static string GetSkillBody(string projectRoot, string skillName)
    {
        var skillDir = Path.Combine(projectRoot, ".lorex", "skills", skillName);
        var entryPath = SkillFileConvention.ResolveEntryPath(skillDir)
            ?? throw new InvalidOperationException($"Skill '{skillName}' is missing {SkillFileConvention.CanonicalFileName}.");

        return SkillFileConvention.ExtractBody(File.ReadAllText(entryPath));
    }
}
