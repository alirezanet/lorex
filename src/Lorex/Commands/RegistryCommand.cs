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
    public static int Run(string[] args)
    {
        if (args.Length > 0 && args.Any(arg => arg is "--help" or "-h"))
            return PrintHelp();

        if (args.Length > 0)
        {
            AnsiConsole.MarkupLine("[red]Unknown arguments:[/] {0}", Markup.Escape(string.Join(' ', args)));
            AnsiConsole.MarkupLine("[dim]Run [bold]lorex registry --help[/] for usage.[/]");
            return 1;
        }

        var projectRoot = ProjectRootLocator.ResolveForExistingProject(Directory.GetCurrentDirectory());

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

        var updatedPolicy = RegistryPolicyPrompts.BuildPolicy(publishMode, baseBranch, prBranchPrefix);

        if (updatedPolicy == currentPolicy)
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
                ServiceFactory.Artifacts.WriteConfig(projectRoot, updatedConfig);

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

    private static int PrintHelp()
    {
        AnsiConsole.MarkupLine("[bold]USAGE[/]  lorex registry");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[dim]Interactively update the connected registry's publish policy.[/]");
        AnsiConsole.MarkupLine("[dim]Direct registries update immediately. Pull-request registries prepare a review branch instead.[/]");
        return 0;
    }
}
