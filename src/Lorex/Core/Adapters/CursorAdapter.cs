namespace Lorex.Core.Adapters;

/// <summary>Adapter for Cursor — projects lorex skills into <c>.cursor/rules</c>.</summary>
public sealed class CursorAdapter : IAdapter
{
    public string Name => "cursor";

    public AdapterProjection GetProjection(string projectRoot) =>
        new CursorRulesProjection(Path.Combine(projectRoot, ".cursor", "rules"));

    public bool DetectExisting(string projectRoot) =>
        Directory.Exists(Path.Combine(projectRoot, ".cursor", "rules")) ||
        File.Exists(Path.Combine(projectRoot, ".cursorrules"));

    public IReadOnlyList<string> LegacyPaths(string projectRoot) =>
        [Path.Combine(projectRoot, ".cursorrules")];
}
