namespace Lorex.Core.Models;

/// <summary>
/// Registry-owned contribution policy stored in <c>.lorex-registry.json</c>.
/// </summary>
public sealed record RegistryPolicy
{
    /// <summary>Schema version for the registry manifest.</summary>
    public int Version { get; init; } = 1;

    /// <summary>How skills may be contributed back to the registry.</summary>
    public string PublishMode { get; init; } = RegistryPublishModes.PullRequest;

    /// <summary>Base branch for pull requests when <see cref="PublishMode"/> is pull-request.</summary>
    public string BaseBranch { get; init; } = "main";

    /// <summary>Prefix to use for PR branches created by lorex.</summary>
    public string PrBranchPrefix { get; init; } = "lorex/";
}
