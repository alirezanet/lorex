using Lorex.Core.Models;

namespace Lorex.Core.Adapters;

/// <summary>Adapter for Roo Code — projects lorex skills into Code mode rule files.</summary>
public sealed class RooAdapter : IAdapter
{
    public string Name => "roo";

    public AdapterProjection? GetProjection(string projectRoot, ArtifactKind kind) => kind switch
    {
        ArtifactKind.Skill => new RooRulesProjection(Path.Combine(projectRoot, ".roo", "rules-code")),
        ArtifactKind.Prompt => new PromptProjection(Path.Combine(projectRoot, ".roo", "commands"), PromptProjectionStyle.Roo),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    public bool DetectExisting(string projectRoot) =>
        Directory.Exists(Path.Combine(projectRoot, ".roo", "rules-code")) ||
        File.Exists(Path.Combine(projectRoot, ".roorules"));

    public IReadOnlyList<string> LegacyPaths(string projectRoot) =>
        [Path.Combine(projectRoot, ".roorules")];
}
