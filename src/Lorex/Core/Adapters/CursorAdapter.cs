using Lorex.Core.Models;

namespace Lorex.Core.Adapters;

/// <summary>Adapter for Cursor — projects lorex skills into <c>.cursor/rules</c>.</summary>
public sealed class CursorAdapter : IAdapter
{
    public string Name => "cursor";

    public AdapterProjection? GetProjection(string projectRoot, ArtifactKind kind) => kind switch
    {
        ArtifactKind.Skill => new CursorRulesProjection(Path.Combine(projectRoot, ".cursor", "rules")),
        ArtifactKind.Prompt => new PromptProjection(Path.Combine(projectRoot, ".cursor", "commands"), PromptProjectionStyle.Cursor),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    public bool DetectExisting(string projectRoot) =>
        Directory.Exists(Path.Combine(projectRoot, ".cursor", "rules")) ||
        File.Exists(Path.Combine(projectRoot, ".cursorrules"));

    public IReadOnlyList<string> LegacyPaths(string projectRoot) =>
        [Path.Combine(projectRoot, ".cursorrules")];
}
