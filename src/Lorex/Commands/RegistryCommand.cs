using Lorex.Cli;
using Lorex.Core.Models;
using Lorex.Core.Services;
using Spectre.Console;

namespace Lorex.Commands;

/// <summary>
/// Implements <c>lorex registry</c>: interactively updates the connected registry policy.
/// </summary>
public static class RegistryCommand
{
    public static int Run(string[] args, string? cwd = null)
    {
        if (args.Any(a => a is "--help" or "-h"))
            return PrintHelp();

        if (args.Length > 0)
        {
            AnsiConsole.MarkupLine("[red]Unknown arguments:[/] {0}", Markup.Escape(string.Join(' ', args)));
            AnsiConsole.MarkupLine("[dim]Run [bold]lorex registry --help[/] for usage.[/]");
            return 1;
        }

        var projectRoot = ProjectRootLocator.ResolveForExistingProject(cwd ?? Directory.GetCurrentDirectory());

        if (!RegistryCommandSupport.TryRefreshConfiguredRegistry(projectRoot, out LorexConfig config))
            return 1;

        var registryConfig = config.Registry!;
        var currentPolicy = registryConfig.Policy;

        AnsiConsole.MarkupLine("[bold]Configuring registry:[/] [dim]{0}[/]", registryConfig.Url);
        AnsiConsole.MarkupLine("[dim]Current publish mode:[/] [bold]{0}[/]", Markup.Escape(currentPolicy.PublishMode));
        AnsiConsole.MarkupLine("[dim]Current base branch:[/] [bold]{0}[/]", Markup.Escape(currentPolicy.BaseBranch));
        if (string.Equals(currentPolicy.PublishMode, RegistryPublishModes.PullRequest, StringComparison.OrdinalIgnoreCase))
            AnsiConsole.MarkupLine("[dim]Current PR branch prefix:[/] [bold]{0}[/]", Markup.Escape(currentPolicy.PrBranchPrefix));
        AnsiConsole.WriteLine();

        var publishMode = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[bold]How should contributors publish skills to this registry?[/]")
                .UseConverter(RegistryPolicyPrompts.RenderChoice)
                .AddChoices(RegistryPolicyPrompts.OrderedChoices(currentPolicy.PublishMode)));

        var baseBranch = AnsiConsole.Prompt(
            new TextPrompt<string>("[bold]Base branch for registry writes and review branches[/]")
                .DefaultValue(currentPolicy.BaseBranch)
                .AllowEmpty());

        var prBranchPrefix = currentPolicy.PrBranchPrefix;
        if (string.Equals(publishMode, RegistryPolicyPrompts.PullRequestChoice, StringComparison.Ordinal))
        {
            prBranchPrefix = AnsiConsole.Prompt(
                new TextPrompt<string>("[bold]Prefix for lorex review branches[/]")
                    .DefaultValue(currentPolicy.PrBranchPrefix)
                    .AllowEmpty());
        }

        // ── Recommended taps ─────────────────────────────────────────────────
        var updatedRecommendedTaps = currentPolicy.RecommendedTaps;

        if (config.Taps.Length > 0)
        {
            var currentRecommendedUrls = new HashSet<string>(
                currentPolicy.RecommendedTaps?.Select(t => t.Url) ?? [],
                StringComparer.OrdinalIgnoreCase);

            AnsiConsole.WriteLine();
            var tapPrompt = new MultiSelectionPrompt<TapConfig>()
                .Title("[bold]Which local taps should this registry recommend to all connected projects?[/]")
                .InstructionsText("[dim](Space to toggle, Enter to confirm, Enter with none = no recommendations)[/]")
                .NotRequired()
                .UseConverter(t =>
                    $"[bold]{Markup.Escape(t.Name)}[/] [dim]— {Markup.Escape(t.Url)}" +
                    (t.Root is not null ? $" ({Markup.Escape(t.Root)})" : "") + "[/]")
                .AddChoices(config.Taps);

            foreach (var tap in config.Taps.Where(t => currentRecommendedUrls.Contains(t.Url)))
                tapPrompt.Select(tap);

            var selectedTaps = AnsiConsole.Prompt(tapPrompt);
            updatedRecommendedTaps = selectedTaps.Count > 0 ? [.. selectedTaps] : null;
        }
        else if (currentPolicy.RecommendedTaps is { Length: > 0 })
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine(
                "[dim]This registry currently recommends [bold]{0}[/] tap(s). Add local taps with [bold]lorex tap add[/] to manage recommendations.[/]",
                currentPolicy.RecommendedTaps.Length);
        }

        var updatedPolicy = RegistryPolicyPrompts.BuildPolicy(publishMode, baseBranch, prBranchPrefix) with
        {
            RecommendedTaps = updatedRecommendedTaps,
        };

        if (PolicyEquals(updatedPolicy, currentPolicy))
        {
            AnsiConsole.MarkupLine("[yellow]No registry policy changes to apply.[/]");
            return 0;
        }

        try
        {
            RegistryPolicyUpdateResult result = null!;
            AnsiConsole.Status()
                .Start("Updating registry policy...", ctx =>
                {
                    ctx.Spinner(Spinner.Known.Dots);
                    result = ServiceFactory.Registry.UpdateRegistryPolicy(registryConfig.Url, updatedPolicy);
                });

            if (string.Equals(result.PublishMode, RegistryPublishModes.Direct, StringComparison.OrdinalIgnoreCase))
            {
                var updatedConfig = config with
                {
                    Registry = registryConfig with { Policy = updatedPolicy }
                };
                ServiceFactory.Skills.WriteConfig(projectRoot, updatedConfig);

                AnsiConsole.MarkupLine(
                    "[green]✓[/] Updated [bold]{0}[/] directly in the registry.",
                    RegistryService.RegistryManifestFileName);
            }
            else
            {
                AnsiConsole.MarkupLine(
                    "[green]✓[/] Prepared a registry policy update on branch [bold]{0}[/] targeting [bold]{1}[/].",
                    Markup.Escape(result.BranchName ?? string.Empty),
                    Markup.Escape(result.BaseBranch ?? string.Empty));

                if (!string.IsNullOrWhiteSpace(result.PullRequestUrl))
                    AnsiConsole.MarkupLine("[dim]Open a PR:[/] {0}", Markup.Escape(result.PullRequestUrl));

                AnsiConsole.MarkupLine(
                    "[dim]The project keeps using the current registry policy until that change is merged and you run [bold]lorex sync[/].[/]");
            }

            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine("[red]Error:[/] {0}", Markup.Escape(ex.Message));
            return 1;
        }
    }

    private static int PrintHelp() => HelpPrinter.Print(
        "lorex registry",
        "Interactively update the connected registry's publish policy and recommended taps.\nDirect registries update immediately; pull-request registries prepare a review branch.",
        options:
        [
            ("-h, --help", "Show this help"),
        ]);

    /// <summary>
    /// Value-equality for <see cref="RegistryPolicy"/> that correctly compares the
    /// <see cref="RegistryPolicy.RecommendedTaps"/> array by content (not reference).
    /// </summary>
    private static bool PolicyEquals(RegistryPolicy a, RegistryPolicy b)
    {
        if (!string.Equals(a.PublishMode,    b.PublishMode,    StringComparison.OrdinalIgnoreCase)) return false;
        if (!string.Equals(a.BaseBranch,     b.BaseBranch,     StringComparison.OrdinalIgnoreCase)) return false;
        if (!string.Equals(a.PrBranchPrefix, b.PrBranchPrefix, StringComparison.OrdinalIgnoreCase)) return false;

        var aTaps = a.RecommendedTaps ?? [];
        var bTaps = b.RecommendedTaps ?? [];
        if (aTaps.Length != bTaps.Length) return false;

        // Order-sensitive: same taps in same order is considered equal
        return aTaps.Zip(bTaps).All(pair =>
            string.Equals(pair.First.Name, pair.Second.Name, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(pair.First.Url,  pair.Second.Url,  StringComparison.OrdinalIgnoreCase) &&
            string.Equals(pair.First.Root, pair.Second.Root, StringComparison.OrdinalIgnoreCase));
    }
}
