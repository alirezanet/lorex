namespace Lorex.Core.Models;

/// <summary>
/// Result of publishing a skill to a registry.
/// </summary>
public sealed record PublishResult
{
    public required string SkillName { get; init; }

    public required string PublishMode { get; init; }

    public string? BranchName { get; init; }

    public string? BaseBranch { get; init; }

    public string? PullRequestUrl { get; init; }
}
