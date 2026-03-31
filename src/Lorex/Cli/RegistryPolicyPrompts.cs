using Lorex.Core.Models;

namespace Lorex.Cli;

internal static class RegistryPolicyPrompts
{
    internal const string DirectPublishChoice = "Direct publish";
    internal const string PullRequestChoice = "Publish via pull request";
    internal const string ReadOnlyChoice = "Read-only";

    internal static IReadOnlyList<string> OrderedChoices(string? currentPublishMode)
    {
        var currentChoice = ChoiceForPublishMode(currentPublishMode);

        return
        [
            currentChoice,
            .. new[] { PullRequestChoice, DirectPublishChoice, ReadOnlyChoice }
                .Where(choice => !string.Equals(choice, currentChoice, StringComparison.Ordinal))
        ];
    }

    internal static string RenderChoice(string choice) => choice switch
    {
        PullRequestChoice => "[bold]Publish via pull request[/] [dim]- contributors push review branches instead of writing directly[/]",
        DirectPublishChoice => "[bold]Direct publish[/] [dim]- contributors commit and push straight to the registry[/]",
        ReadOnlyChoice => "[bold]Read-only[/] [dim]- skills can be installed and synced, but publishing is blocked[/]",
        _ => choice,
    };

    internal static RegistryPolicy BuildPolicy(string publishModeChoice, string baseBranch, string prBranchPrefix)
    {
        var normalizedBaseBranch = string.IsNullOrWhiteSpace(baseBranch) ? "main" : baseBranch.Trim();
        var normalizedPrefix = string.IsNullOrWhiteSpace(prBranchPrefix) ? "lorex/" : prBranchPrefix.Trim();

        return publishModeChoice switch
        {
            PullRequestChoice => new RegistryPolicy
            {
                PublishMode = RegistryPublishModes.PullRequest,
                BaseBranch = normalizedBaseBranch,
                PrBranchPrefix = normalizedPrefix,
            },
            DirectPublishChoice => new RegistryPolicy
            {
                PublishMode = RegistryPublishModes.Direct,
                BaseBranch = normalizedBaseBranch,
                PrBranchPrefix = normalizedPrefix,
            },
            ReadOnlyChoice => new RegistryPolicy
            {
                PublishMode = RegistryPublishModes.ReadOnly,
                BaseBranch = normalizedBaseBranch,
                PrBranchPrefix = normalizedPrefix,
            },
            _ => throw new InvalidOperationException($"Unknown publish mode choice '{publishModeChoice}'."),
        };
    }

    private static string ChoiceForPublishMode(string? publishMode) => publishMode switch
    {
        var mode when string.Equals(mode, RegistryPublishModes.Direct, StringComparison.OrdinalIgnoreCase)
            => DirectPublishChoice,
        var mode when string.Equals(mode, RegistryPublishModes.ReadOnly, StringComparison.OrdinalIgnoreCase)
            => ReadOnlyChoice,
        _ => PullRequestChoice,
    };
}
