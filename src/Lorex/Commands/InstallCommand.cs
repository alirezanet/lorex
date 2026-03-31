using Lorex.Cli;
using Lorex.Core.Models;
using Lorex.Core.Services;
using Spectre.Console;

namespace Lorex.Commands;

/// <summary>Implements <c>lorex install [artifact…]</c>: installs one or more registry artifacts into the current project.</summary>
public static class InstallCommand
{
    private const string AllFlag = "--all";
    private const string RecommendedFlag = "--recommended";
    private const string PromptInstallRecommended = "Install recommended artifacts";
    private const string PromptInstallAll = "Install all available artifacts";
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
            if (!RegistryCommandSupport.TryReadConfiguredRegistry(projectRoot, out var cfg))
                return 1;

            var installAll = WantsAll(args);
            var installRecommended = WantsRecommended(args);
            var requestedArtifacts = ParseArtifactNames(args);

            if ((installAll || installRecommended) && requestedArtifacts.Count > 0)
            {
                PrintUsage();
                AnsiConsole.MarkupLine("[dim]Use explicit artifact names or one install mode flag, not both.[/]");
                return 1;
            }

            if (installAll && installRecommended)
            {
                PrintUsage();
                AnsiConsole.MarkupLine("[dim]Use [bold]--all[/] or [bold]--recommended[/], not both.[/]");
                return 1;
            }

            var interactive = !installAll && !installRecommended && requestedArtifacts.Count == 0;
            var kind = interactive && !parsedType.HasExplicitType
                ? ArtifactCliSupport.PromptForArtifactKind("install")
                : parsedType.Kind;

            IReadOnlyList<ArtifactMetadata>? available = null;

            if (installAll)
            {
                available = FetchAvailableArtifacts(cfg, kind);
                requestedArtifacts = ServiceFactory.RegistryArtifacts.GetInstallableArtifactNames(available, cfg, kind);
            }
            else if (installRecommended)
            {
                available = FetchAvailableArtifacts(cfg, kind);
                requestedArtifacts = ServiceFactory.RegistryArtifacts.GetRecommendedArtifactNames(projectRoot, available, cfg, kind);

                if (requestedArtifacts.Count == 0)
                {
                    AnsiConsole.MarkupLine("[yellow]No recommended {0} found for this project.[/]", kind.DisplayNamePlural());
                    return 0;
                }
            }
            else if (interactive)
            {
                requestedArtifacts = PromptForArtifacts(projectRoot, cfg, kind);
            }

            if (requestedArtifacts.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]Nothing selected.[/]");
                return 0;
            }

            var (approvedArtifacts, skippedArtifacts) = ArtifactOverwritePrompts.ResolveApprovedOverrides(
                projectRoot,
                kind,
                requestedArtifacts,
                artifactName => $"Overwrite local {kind.DisplayName()} [bold]{Markup.Escape(artifactName)}[/] with the registry version?");

            if (approvedArtifacts.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]Nothing selected.[/]");
                return 0;
            }

            AnsiConsole.Status()
                .Start($"Installing {kind.DisplayNamePlural()}...", ctx =>
                {
                    foreach (var artifactName in approvedArtifacts)
                    {
                        ctx.Status($"Installing [bold]{artifactName}[/]...");
                        ServiceFactory.Artifacts.InstallArtifact(
                            projectRoot,
                            kind,
                            artifactName,
                            overwriteLocalArtifact: ServiceFactory.Artifacts.RequiresOverwriteApproval(projectRoot, kind, artifactName));
                    }

                    ctx.Spinner(Spinner.Known.Dots);
                    ctx.Status("Projecting artifacts into native agent locations...");
                    var config = ServiceFactory.Artifacts.ReadConfig(projectRoot);
                    ServiceFactory.Adapters.Project(projectRoot, config);
                });

            foreach (var artifactName in approvedArtifacts)
                AnsiConsole.MarkupLine("[green]✓[/] Installed [bold]{0}[/] [dim](symlinked)[/]", artifactName);

            foreach (var artifactName in skippedArtifacts)
            {
                AnsiConsole.MarkupLine(
                    "[yellow]Skipped[/] [bold]{0}[/] [dim](kept existing local {1})[/]",
                    artifactName,
                    kind.DisplayName());
            }

            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine("[red]Error:[/] {0}", Markup.Escape(ex.Message));
            if (OperatingSystem.IsWindows() && !WindowsDevModeHelper.IsSymlinkAvailable())
            {
                WindowsDevModeHelper.PrintDevModeGuidance();
                WindowsDevModeHelper.OfferToOpenSettings();
            }
            return 1;
        }
    }

    internal static bool WantsAll(string[] args) =>
        args.Any(a => string.Equals(a, AllFlag, StringComparison.OrdinalIgnoreCase));

    internal static bool WantsRecommended(string[] args) =>
        args.Any(a => string.Equals(a, RecommendedFlag, StringComparison.OrdinalIgnoreCase));

    internal static List<string> ParseArtifactNames(string[] args) =>
        [.. args
            .Where(a =>
                !string.IsNullOrWhiteSpace(a)
                && !string.Equals(a, AllFlag, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(a, RecommendedFlag, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)];

    private static void PrintUsage()
    {
        AnsiConsole.MarkupLine("[red]Usage:[/] lorex install [[bold]<artifact>...[/]] [[bold]--all[/]] [[bold]--recommended[/]] [[bold]--type skill|prompt[/]]");
    }

    private static List<string> PromptForArtifacts(string projectRoot, LorexConfig cfg, ArtifactKind kind)
    {
        var available = FetchAvailableArtifacts(cfg, kind);
        if (available.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No {0} found in the registry.[/]", kind.DisplayNamePlural());
            return [];
        }

        var installableNames = ServiceFactory.RegistryArtifacts.GetInstallableArtifactNames(available, cfg, kind);
        var installableSet = installableNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var choices = available
            .Where(artifact => installableSet.Contains(artifact.Name))
            .OrderBy(artifact => artifact.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (choices.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]All {0} in the registry are already installed.[/]", kind.DisplayNamePlural());
            AnsiConsole.MarkupLine("[dim]Run [bold]lorex sync[/] to update them, or [bold]lorex install --type {0} <name>[/] to reinstall one explicitly.[/]", kind.CliValue());
            return [];
        }

        var recommended = ServiceFactory.RegistryArtifacts.GetRecommendedArtifactNames(projectRoot, available, cfg, kind);
        var selectionMode = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title($"[bold]How do you want to install {kind.DisplayNamePlural()}?[/]")
                .AddChoices(recommended.Count > 0
                    ? [PromptInstallRecommended, PromptInstallAll, PromptChooseSpecific]
                    : [PromptInstallAll, PromptChooseSpecific]));

        if (string.Equals(selectionMode, PromptInstallRecommended, StringComparison.Ordinal))
            return recommended;

        if (string.Equals(selectionMode, PromptInstallAll, StringComparison.Ordinal))
            return [.. choices.Select(artifact => artifact.Name)];

        var selected = AnsiConsole.Prompt(
            new MultiSelectionPrompt<ArtifactMetadata>()
                .Title($"[bold]Which {kind.DisplayNamePlural()} do you want to install?[/]")
                .InstructionsText("[dim](Space to select, Enter to confirm)[/]")
                .UseConverter(artifact =>
                {
                    var description = string.IsNullOrWhiteSpace(artifact.Description)
                        ? string.Empty
                        : $" [dim]- {Markup.Escape(artifact.Description)}[/]";
                    return $"[bold]{Markup.Escape(artifact.Name)}[/]{description}";
                })
                .AddChoices(choices));

        return [.. selected.Select(artifact => artifact.Name)];
    }

    private static IReadOnlyList<ArtifactMetadata> FetchAvailableArtifacts(LorexConfig cfg, ArtifactKind kind)
    {
        IReadOnlyList<ArtifactMetadata> available = [];
        AnsiConsole.Status()
            .Start("Fetching registry…", ctx =>
            {
                ctx.Spinner(Spinner.Known.Dots);
                available = ServiceFactory.RegistryArtifacts.ListAvailableArtifacts(cfg, kind);
            });

        return available;
    }
}
