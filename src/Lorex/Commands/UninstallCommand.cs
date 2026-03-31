using Lorex.Cli;
using Lorex.Core.Models;
using Lorex.Core.Services;
using Spectre.Console;

namespace Lorex.Commands;

/// <summary>Implements <c>lorex uninstall</c>: removes installed artifacts from the current project.</summary>
public static class UninstallCommand
{
    private const string AllFlag = "--all";
    private const string PromptUninstallAll = "Uninstall all installed artifacts";
    private const string PromptChooseSpecific = "Choose specific artifacts";

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

        try
        {
            var config = ServiceFactory.Artifacts.ReadConfig(projectRoot);
            var uninstallAll = WantsAll(args);
            var requestedArtifacts = ParseArtifactNames(args);
            var interactive = !uninstallAll && requestedArtifacts.Count == 0;
            var kind = interactive && !parsedType.HasExplicitType
                ? ArtifactCliSupport.PromptForArtifactKind("uninstall")
                : parsedType.Kind;

            if (uninstallAll && requestedArtifacts.Count > 0)
            {
                AnsiConsole.MarkupLine("[red]Usage:[/] lorex uninstall [[bold]<artifact>...[/]] [[bold]--all[/]] [[bold]--type skill|prompt[/]]");
                AnsiConsole.MarkupLine("[dim]Use explicit artifact names or [bold]--all[/], not both.[/]");
                return 1;
            }

            if (uninstallAll)
            {
                requestedArtifacts = GetInstalledArtifactNames(config, kind);
            }
            else if (interactive)
            {
                requestedArtifacts = PromptForArtifacts(config, kind);
            }

            if (requestedArtifacts.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]Nothing selected.[/]");
                return 0;
            }

            var installed = config.Artifacts.Get(kind);
            var missingArtifacts = requestedArtifacts
                .Where(artifact => !installed.Contains(artifact, StringComparer.OrdinalIgnoreCase))
                .ToList();

            if (missingArtifacts.Count > 0)
            {
                foreach (var artifactName in missingArtifacts)
                {
                    AnsiConsole.MarkupLine(
                        "[yellow]{0} '[/]{1}[yellow]' is not installed in this project.[/]",
                        kind.Title(),
                        Markup.Escape(artifactName));
                }

                return 1;
            }

            foreach (var artifactName in requestedArtifacts)
                ServiceFactory.Artifacts.UninstallArtifact(projectRoot, kind, artifactName);

            var updated = ServiceFactory.Artifacts.ReadConfig(projectRoot);
            ServiceFactory.Adapters.Project(projectRoot, updated);

            foreach (var artifactName in requestedArtifacts)
                AnsiConsole.MarkupLine("[green]✓[/] Uninstalled [bold]{0}[/]", Markup.Escape(artifactName));

            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine("[red]Error:[/] {0}", Markup.Escape(ex.Message));
            return 1;
        }
    }

    internal static bool WantsAll(string[] args) =>
        args.Any(a => string.Equals(a, AllFlag, StringComparison.OrdinalIgnoreCase));

    internal static List<string> ParseArtifactNames(string[] args) =>
        [.. args
            .Where(a => !string.IsNullOrWhiteSpace(a) && !string.Equals(a, AllFlag, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)];

    internal static List<string> GetInstalledArtifactNames(LorexConfig config, ArtifactKind kind) =>
        [.. config.Artifacts.Get(kind).OrderBy(name => name, StringComparer.OrdinalIgnoreCase)];

    private static List<string> PromptForArtifacts(LorexConfig config, ArtifactKind kind)
    {
        var installedArtifacts = GetInstalledArtifactNames(config, kind);
        if (installedArtifacts.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No {0} are installed in this project.[/]", kind.DisplayNamePlural());
            return [];
        }

        var selectionMode = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title($"[bold]How do you want to uninstall {kind.DisplayNamePlural()}?[/]")
                .AddChoices(PromptUninstallAll, PromptChooseSpecific));

        if (string.Equals(selectionMode, PromptUninstallAll, StringComparison.Ordinal))
            return installedArtifacts;

        return AnsiConsole.Prompt(
            new MultiSelectionPrompt<string>()
                .Title($"[bold]Which {kind.DisplayNamePlural()} do you want to uninstall?[/]")
                .InstructionsText("[dim](Space to select, Enter to confirm)[/]")
                .AddChoices(installedArtifacts));
    }
}
