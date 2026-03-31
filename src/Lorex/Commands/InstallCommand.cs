using Lorex.Cli;
using Lorex.Core.Models;
using Lorex.Core.Services;
using Spectre.Console;

namespace Lorex.Commands;

/// <summary>Implements <c>lorex install [skill…]</c>: installs one or more skills from the registry into the current project.</summary>
public static class InstallCommand
{
    private const string AllFlag = "--all";
    private const string RecommendedFlag = "--recommended";
    private const string PromptInstallRecommended = "Install recommended skills";
    private const string PromptInstallAll = "Install all available skills";
    private const string PromptChooseSpecific = "Choose specific skills";

    /// <summary>Runs the command. Returns 0 on success, 1 on failure.</summary>
    public static int Run(string[] args)
    {
        var projectRoot = ProjectRootLocator.ResolveForExistingProject(Directory.GetCurrentDirectory());

        try
        {
            if (!RegistryCommandSupport.TryReadConfiguredRegistry(projectRoot, out var cfg))
                return 1;

            var installAll = WantsAll(args);
            var installRecommended = WantsRecommended(args);
            var requestedSkills = ParseSkillNames(args);

            if ((installAll || installRecommended) && requestedSkills.Count > 0)
            {
                AnsiConsole.MarkupLine("[red]Usage:[/] lorex install [[bold]<skill>...[/]] [[bold]--all[/]] [[bold]--recommended[/]]");
                AnsiConsole.MarkupLine("[dim]Use explicit skill names or one install mode flag, not both.[/]");
                return 1;
            }

            if (installAll && installRecommended)
            {
                AnsiConsole.MarkupLine("[red]Usage:[/] lorex install [[bold]<skill>...[/]] [[bold]--all[/]] [[bold]--recommended[/]]");
                AnsiConsole.MarkupLine("[dim]Use [bold]--all[/] or [bold]--recommended[/], not both.[/]");
                return 1;
            }

            IReadOnlyList<SkillMetadata>? available = null;

            if (installAll)
            {
                available = FetchAvailableSkills(cfg);
                requestedSkills = ServiceFactory.RegistrySkills.GetInstallableSkillNames(available, cfg);
            }
            else if (installRecommended)
            {
                available = FetchAvailableSkills(cfg);
                requestedSkills = ServiceFactory.RegistrySkills.GetRecommendedSkillNames(projectRoot, available, cfg);

                if (requestedSkills.Count == 0)
                {
                    AnsiConsole.MarkupLine("[yellow]No recommended skills found for this project.[/]");
                    return 0;
                }
            }
            else if (requestedSkills.Count == 0)
            {
                requestedSkills = PromptForSkills(projectRoot, cfg);
            }

            if (requestedSkills.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]Nothing selected.[/]");
                return 0;
            }

            var (approvedSkills, skippedSkills) = SkillOverwritePrompts.ResolveApprovedOverrides(
                projectRoot,
                requestedSkills,
                skillName => $"Overwrite local skill [bold]{Markup.Escape(skillName)}[/] with the registry version?");

            if (approvedSkills.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]Nothing selected.[/]");
                return 0;
            }

            AnsiConsole.Status()
                .Start("Installing skills...", ctx =>
                {
                    foreach (var skillName in approvedSkills)
                    {
                        ctx.Status($"Installing [bold]{skillName}[/]...");
                        ServiceFactory.Skills.InstallSkill(
                            projectRoot,
                            skillName,
                            overwriteLocalSkill: ServiceFactory.Skills.RequiresOverwriteApproval(projectRoot, skillName));
                    }

                    ctx.Spinner(Spinner.Known.Dots);
                    ctx.Status("Projecting skills into native agent locations...");
                    var config = ServiceFactory.Skills.ReadConfig(projectRoot);
                    ServiceFactory.Adapters.Project(projectRoot, config);
                });

            foreach (var skillName in approvedSkills)
            {
                AnsiConsole.MarkupLine($"[green]✓[/] Installed [bold]{skillName}[/] [dim](symlinked)[/]");
            }

            foreach (var skillName in skippedSkills)
                AnsiConsole.MarkupLine("[yellow]Skipped[/] [bold]{0}[/] [dim](kept existing local skill)[/]", skillName);

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

    internal static List<string> ParseSkillNames(string[] args) =>
        [.. args
            .Where(a =>
                !string.IsNullOrWhiteSpace(a)
                && !string.Equals(a, AllFlag, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(a, RecommendedFlag, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)];

    private static List<string> PromptForSkills(string projectRoot, LorexConfig cfg)
    {
        var available = FetchAvailableSkills(cfg);
        if (available.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No skills found in the registry.[/]");
            return [];
        }

        var installableNames = ServiceFactory.RegistrySkills.GetInstallableSkillNames(available, cfg);
        var installableSet = installableNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var choices = available
            .Where(skill => installableSet.Contains(skill.Name))
            .OrderBy(skill => skill.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (choices.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]All skills in the registry are already installed.[/]");
            AnsiConsole.MarkupLine("[dim]Run [bold]lorex sync[/] to update them, or [bold]lorex install <skill>[/] to reinstall one explicitly.[/]");
            return [];
        }

        var recommended = ServiceFactory.RegistrySkills.GetRecommendedSkillNames(projectRoot, available, cfg);

        var selectionMode = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[bold]How do you want to install skills?[/]")
                .AddChoices(recommended.Count > 0
                    ? [PromptInstallRecommended, PromptInstallAll, PromptChooseSpecific]
                    : [PromptInstallAll, PromptChooseSpecific]));

        if (string.Equals(selectionMode, PromptInstallRecommended, StringComparison.Ordinal))
            return recommended;

        if (string.Equals(selectionMode, PromptInstallAll, StringComparison.Ordinal))
            return [.. choices.Select(skill => skill.Name)];

        var selected = AnsiConsole.Prompt(
            new MultiSelectionPrompt<SkillMetadata>()
                .Title("[bold]Which skills do you want to install?[/]")
                .InstructionsText("[dim](Space to select, Enter to confirm)[/]")
                .UseConverter(skill =>
                {
                    var description = string.IsNullOrWhiteSpace(skill.Description)
                        ? string.Empty
                        : $" [dim]- {Markup.Escape(skill.Description)}[/]";
                    return $"[bold]{Markup.Escape(skill.Name)}[/]{description}";
                })
                .AddChoices(choices));

        return [.. selected.Select(skill => skill.Name)];
    }

    private static IReadOnlyList<SkillMetadata> FetchAvailableSkills(LorexConfig cfg)
    {
        IReadOnlyList<SkillMetadata> available = [];
        AnsiConsole.Status()
            .Start("Fetching registry…", ctx =>
            {
                ctx.Spinner(Spinner.Known.Dots);
                available = ServiceFactory.RegistrySkills.ListAvailableSkills(cfg);
            });

        return available;
    }
}
