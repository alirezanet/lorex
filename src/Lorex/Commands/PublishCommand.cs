using Lorex.Core.Services;
using Spectre.Console;

namespace Lorex.Commands;

/// <summary>Implements <c>lorex publish [skill…]</c>: pushes locally authored skills to the registry, then replaces them with symlinks.</summary>
public static class PublishCommand
{
    /// <summary>Runs the command. Returns 0 on success, 1 if any publish failed.</summary>
    public static int Run(string[] args)
    {
        var projectRoot = ProjectRootLocator.ResolveForExistingProject(Directory.GetCurrentDirectory());
        var builtIns = BuiltInSkillService.SkillNames().ToHashSet(StringComparer.OrdinalIgnoreCase);

        try
        {
            Lorex.Core.Models.LorexConfig cfg = null!;
            AnsiConsole.Status()
                .Start("Refreshing registry policy...", ctx =>
                {
                    ctx.Spinner(Spinner.Known.Dots);
                    cfg = ServiceFactory.Skills.RefreshRegistryPolicy(projectRoot);
                });

            if (cfg.Registry is null)
            {
                AnsiConsole.MarkupLine("[red]No registry configured.[/] lorex is running in local-only mode.");
                AnsiConsole.MarkupLine("[dim]Run [bold]lorex init <url>[/] to connect a registry before publishing.[/]");
                return 1;
            }
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine("[red]Error:[/] {0}", Markup.Escape(ex.Message));
            return 1;
        }

        List<string> toPublish;

        if (args.Length > 0)
        {
            // Names supplied on the command line
            toPublish = [];
            foreach (var arg in args.Where(a => !string.IsNullOrWhiteSpace(a)))
            {
                if (builtIns.Contains(arg))
                {
                    AnsiConsole.MarkupLine("[yellow]'{0}' is a built-in lorex skill — it cannot be published.[/]", arg);
                    AnsiConsole.MarkupLine("[dim]Run [bold]lorex create <new-name>[/] to scaffold a custom version.[/]");
                    return 1;
                }
                toPublish.Add(arg);
            }
        }
        else
        {
            // Interactive multi-select
            var local = ServiceFactory.Skills.LocalOnlySkills(projectRoot).ToArray();

            if (local.Length == 0)
            {
                AnsiConsole.MarkupLine("[red]No local skills to publish.[/] Ask your AI agent to create one, or run [bold]lorex create[/] to scaffold it.");
                return 1;
            }

            toPublish = AnsiConsole.Prompt(
                new MultiSelectionPrompt<string>()
                    .Title("[bold]Which local skills do you want to publish?[/]")
                    .InstructionsText("[dim](Space to select, Enter to confirm)[/]")
                    .AddChoices(local));

            if (toPublish.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]Nothing selected.[/]");
                return 0;
            }
        }

        var failed = false;
        foreach (var skillName in toPublish)
        {
            try
            {
                Core.Models.PublishResult result = null!;
                AnsiConsole.Status()
                    .Start($"Publishing [bold]{skillName}[/]...", ctx =>
                    {
                        ctx.Spinner(Spinner.Known.Dots);
                        result = ServiceFactory.Skills.PublishSkill(projectRoot, skillName, ServiceFactory.Git);
                    });

                if (string.Equals(result.PublishMode, Core.Models.RegistryPublishModes.Direct, StringComparison.OrdinalIgnoreCase))
                {
                    AnsiConsole.MarkupLine("[green]✓[/] Published [bold]{0}[/] directly to the registry.", skillName);
                }
                else
                {
                    AnsiConsole.MarkupLine(
                        "[green]✓[/] Prepared [bold]{0}[/] for review on branch [bold]{1}[/] targeting [bold]{2}[/].",
                        skillName,
                        Markup.Escape(result.BranchName ?? string.Empty),
                        Markup.Escape(result.BaseBranch ?? string.Empty));

                    if (!string.IsNullOrWhiteSpace(result.PullRequestUrl))
                        AnsiConsole.MarkupLine("[dim]Open a PR:[/] {0}", Markup.Escape(result.PullRequestUrl));
                }
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine("[red]✗[/] {0}: {1}", skillName, Markup.Escape(ex.Message));
                failed = true;
            }
        }

        return failed ? 1 : 0;
    }
}
