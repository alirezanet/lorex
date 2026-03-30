using Lorex.Core.Adapters;
using Lorex.Core.Models;
using Lorex.Core.Serialization;

namespace Lorex.Core.Services;

/// <summary>
/// Builds the lorex skill index block and injects it into AI agent config files via the registered adapters.
/// The injected block is delimited by <c>&lt;!-- lorex:start --&gt;</c> and <c>&lt;!-- lorex:end --&gt;</c> HTML comments
/// so it can be replaced idempotently on subsequent runs.
/// </summary>
public sealed class AdapterService
{
    private const string StartMarker = "<!-- lorex:start -->";
    private const string EndMarker = "<!-- lorex:end -->";

    public static readonly IReadOnlyDictionary<string, IAdapter> KnownAdapters =
        new Dictionary<string, IAdapter>(StringComparer.OrdinalIgnoreCase)
        {
            ["copilot"]   = new CopilotAdapter(),
            ["codex"]     = new CodexAdapter(),
            ["openclaw"]  = new OpenClawAdapter(),
            ["cursor"]    = new CursorAdapter(),
            ["claude"]    = new ClaudeAdapter(),
            ["windsurf"]  = new WindsurfAdapter(),
            ["cline"]     = new ClineAdapter(),
            ["roo"]       = new RooAdapter(),
            ["gemini"]    = new GeminiAdapter(),
            ["opencode"]  = new OpenCodeAdapter(),
        };

    /// <summary>
    /// Injects (or replaces) the lorex skill index block in all configured adapters.
    /// </summary>
    public void Compile(string projectRoot, LorexConfig config)
    {
        var indexBlock = BuildIndexBlock(projectRoot, config);

        foreach (var adapterName in config.Adapters)
        {
            if (!KnownAdapters.TryGetValue(adapterName, out var adapter))
                continue;

            InjectIntoFile(adapter.TargetFilePath(projectRoot), indexBlock);
        }
    }

    /// <summary>
    /// Injects (or replaces) the lorex skill index block in a single adapter.
    /// </summary>
    public void CompileTarget(string projectRoot, LorexConfig config, string adapterName)
    {
        if (!KnownAdapters.TryGetValue(adapterName, out var adapter))
            throw new ArgumentException($"Unknown adapter '{adapterName}'. Known adapters: {string.Join(", ", KnownAdapters.Keys)}");

        var indexBlock = BuildIndexBlock(projectRoot, config);
        InjectIntoFile(adapter.TargetFilePath(projectRoot), indexBlock);
    }

    // ── Index building ────────────────────────────────────────────────────────

    internal string BuildIndexBlock(string projectRoot, LorexConfig config)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine(StartMarker);
        sb.AppendLine("## Lorex Skill Index");
        sb.AppendLine();
        sb.AppendLine("Read this index and load the skill files relevant to your current task.");
        sb.AppendLine();

        if (config.InstalledSkills.Length == 0)
        {
            sb.AppendLine("_No skills installed. Run `lorex install <skill>` to add skills._");
        }
        else
        {
            foreach (var skillName in config.InstalledSkills)
            {
                var skillPath = Path.Combine(".lorex", "skills", skillName, "skill.md")
                    .Replace('\\', '/');

                var description = GetSkillDescription(projectRoot, skillName);
                sb.AppendLine($"- **{skillName}**: {description} → `{skillPath}`");

                // List any embedded tools (executables/scripts alongside skill.md)
                var tools = GetEmbeddedTools(projectRoot, skillName);
                if (tools.Length > 0)
                {
                    var toolLinks = string.Join(", ", tools.Select(t =>
                    {
                        var rel = Path.Combine(".lorex", "skills", skillName, Path.GetFileName(t)).Replace('\\', '/');
                        return $"[`{Path.GetFileName(t)}`]({rel})";
                    }));
                    sb.AppendLine($"  _Embedded tools: {toolLinks}_");
                }
            }
        }

        sb.AppendLine();
        sb.Append(EndMarker);
        return sb.ToString();
    }

    // ── File injection ────────────────────────────────────────────────────────

    internal static void InjectIntoFile(string filePath, string indexBlock)
    {
        // Ensure parent directory exists
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        string existing = File.Exists(filePath) ? File.ReadAllText(filePath) : string.Empty;
        string updated = ReplaceOrAppend(existing, indexBlock);
        File.WriteAllText(filePath, updated);
    }

    /// <summary>
    /// Replaces the lorex block if present, otherwise appends it (with a blank-line separator).
    /// </summary>
    internal static string ReplaceOrAppend(string content, string indexBlock)
    {
        var startIdx = content.IndexOf(StartMarker, StringComparison.Ordinal);
        var endIdx   = content.IndexOf(EndMarker,   StringComparison.Ordinal);

        if (startIdx >= 0 && endIdx >= 0 && endIdx > startIdx)
        {
            // Replace existing block (inclusive of end marker)
            return string.Concat(
                content.AsSpan(0, startIdx),
                indexBlock,
                content.AsSpan(endIdx + EndMarker.Length));
        }

        // Append — ensure exactly one blank line before the block.
        // Always use LF: these files live in shared git repos and must be consistent across platforms.
        if (string.IsNullOrWhiteSpace(content))
            return indexBlock + "\n";

        var trimmed = content.TrimEnd();
        return trimmed + "\n\n" + indexBlock + "\n";
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string GetSkillDescription(string projectRoot, string skillName)
    {
        var skillDir = Path.Combine(projectRoot, ".lorex", "skills", skillName);

        // New format: YAML frontmatter inside skill.md
        var skillMd = Path.Combine(skillDir, "skill.md");
        if (File.Exists(skillMd))
        {
            try
            {
                var yaml = SimpleYamlParser.ExtractFrontmatterYaml(File.ReadAllText(skillMd));
                if (yaml is not null)
                {
                    var dict = SimpleYamlParser.ParseToDictionary(yaml);
                    if (dict.TryGetValue("description", out var d)) return d;
                }
            }
            catch { }
        }

        // Legacy fallback: separate metadata.yaml
        var metaFile = Path.Combine(skillDir, "metadata.yaml");
        if (File.Exists(metaFile))
        {
            try
            {
                var dict = SimpleYamlParser.ParseToDictionary(File.ReadAllText(metaFile));
                if (dict.TryGetValue("description", out var d)) return d;
            }
            catch { }
        }

        return skillName;
    }

    /// <summary>Returns paths of files in the skill directory that are not skill.md or metadata.yaml.</summary>
    private static string[] GetEmbeddedTools(string projectRoot, string skillName)
    {
        var skillDir = Path.Combine(projectRoot, ".lorex", "skills", skillName);
        if (!Directory.Exists(skillDir)) return [];

        return [.. Directory.EnumerateFiles(skillDir)
            .Where(f =>
            {
                var fn = Path.GetFileName(f);
                return !fn.Equals("skill.md", StringComparison.OrdinalIgnoreCase)
                    && !fn.Equals("metadata.yaml", StringComparison.OrdinalIgnoreCase);
            })];
    }
}
