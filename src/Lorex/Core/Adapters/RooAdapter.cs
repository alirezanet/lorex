namespace Lorex.Core.Adapters;

/// <summary>Adapter for Roo Code — projects lorex skills into Code mode rule files.</summary>
public sealed class RooAdapter : IAdapter
{
    public string Name => "roo";

    public AdapterProjection GetProjection(string projectRoot) =>
        new RooRulesProjection(Path.Combine(projectRoot, ".roo", "rules-code"));

    public bool DetectExisting(string projectRoot) =>
        Directory.Exists(Path.Combine(projectRoot, ".roo", "rules-code")) ||
        File.Exists(Path.Combine(projectRoot, ".roorules"));

    public IReadOnlyList<string> LegacyPaths(string projectRoot) =>
        [Path.Combine(projectRoot, ".roorules")];
}
