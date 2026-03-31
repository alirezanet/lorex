namespace Lorex.Core.Models;

/// <summary>
/// Result of updating the registry policy manifest.
/// </summary>
public sealed record RegistryPolicyUpdateResult
{
    /// <summary>How the update was submitted.</summary>
    public required string PublishMode { get; init; }

    /// <summary>The requested new policy.</summary>
    public required RegistryPolicy Policy { get; init; }

    /// <summary>The review branch name when the update was submitted through a pull request workflow.</summary>
    public string? BranchName { get; init; }

    /// <summary>The base branch targeted by the update.</summary>
    public string? BaseBranch { get; init; }

    /// <summary>A compare URL when the registry host is recognized.</summary>
    public string? PullRequestUrl { get; init; }
}
