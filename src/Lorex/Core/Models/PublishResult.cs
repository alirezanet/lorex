namespace Lorex.Core.Models;

/// <summary>
/// Result of publishing an artifact to a registry.
/// </summary>
public sealed record PublishResult
{
    public required ArtifactKind ArtifactKind { get; init; }

    public required string ArtifactName { get; init; }

    public required string PublishMode { get; init; }

    public string? BranchName { get; init; }

    public string? BaseBranch { get; init; }

    public string? PullRequestUrl { get; init; }
}
