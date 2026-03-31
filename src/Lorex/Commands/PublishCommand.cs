using Lorex.Cli;
using Lorex.Core.Models;
using Lorex.Core.Services;
using Spectre.Console;

namespace Lorex.Commands;

/// <summary>Implements <c>lorex publish [artifact…]</c>: pushes locally authored artifacts to the registry.</summary>
public static class PublishCommand
{
    public static int Run(string[] args)
    {
        var projectRoot = ProjectRootLocator.ResolveForExistingProject(Directory.GetCurrentDirectory());

        ArtifactCliSupport.ParsedArtifactType parsedType;
        try
        {
            parsedType = ArtifactCliSupport.ParseArtifactTypeOrDefault(args);
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine("[red]Error:[/] {0}", Markup.Escape(ex.Message));
            return 1;
        }

        args = parsedType.RemainingArgs;

        var interactive = args.Length == 0;
        var kind = interactive && !parsedType.HasExplicitType
            ? ArtifactCliSupport.PromptForArtifactKind("publish")
            : parsedType.Kind;
        var builtIns = kind == ArtifactKind.Skill
            ? BuiltInSkillService.SkillNames().ToHashSet(StringComparer.OrdinalIgnoreCase)
            : [];

        if (!RegistryCommandSupport.TryRefreshConfiguredRegistry(projectRoot, out _))
            return 1;

        List<string> toPublish;
        if (args.Length > 0)
        {
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
            var local = ServiceFactory.Artifacts.LocalOnlyArtifacts(projectRoot, kind).ToArray();
            if (local.Length == 0)
            {
                AnsiConsole.MarkupLine(
                    "[red]No local {0} to publish.[/] Ask your AI agent to create one, or run [bold]lorex create{1}[/] to scaffold it.",
                    kind.DisplayNamePlural(),
                    kind == ArtifactKind.Skill ? string.Empty : " --type prompt");
                return 1;
            }

            toPublish = AnsiConsole.Prompt(
                new MultiSelectionPrompt<string>()
                    .Title($"[bold]Which local {kind.DisplayNamePlural()} do you want to publish?[/]")
                    .InstructionsText("[dim](Space to select, Enter to confirm)[/]")
                    .AddChoices(local));

            if (toPublish.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]Nothing selected.[/]");
                return 0;
            }
        }

        var failed = false;
        foreach (var artifactName in toPublish)
        {
            try
            {
                PublishResult result = null!;
                AnsiConsole.Status()
                    .Start($"Publishing [bold]{artifactName}[/]...", ctx =>
                    {
                        ctx.Spinner(Spinner.Known.Dots);
                        result = ServiceFactory.Artifacts.PublishArtifact(projectRoot, kind, artifactName, ServiceFactory.Git);
                    });

                if (string.Equals(result.PublishMode, RegistryPublishModes.Direct, StringComparison.OrdinalIgnoreCase))
                {
                    AnsiConsole.MarkupLine("[green]✓[/] Published [bold]{0}[/] directly to the registry.", artifactName);
                }
                else
                {
                    AnsiConsole.MarkupLine(
                        "[green]✓[/] Prepared [bold]{0}[/] for review on branch [bold]{1}[/] targeting [bold]{2}[/].",
                        artifactName,
                        Markup.Escape(result.BranchName ?? string.Empty),
                        Markup.Escape(result.BaseBranch ?? string.Empty));

                    if (!string.IsNullOrWhiteSpace(result.PullRequestUrl))
                        AnsiConsole.MarkupLine("[dim]Open a PR:[/] {0}", Markup.Escape(result.PullRequestUrl));
                }
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine("[red]✗[/] {0}: {1}", artifactName, Markup.Escape(ex.Message));
                failed = true;
            }
        }

        return failed ? 1 : 0;
    }
}
