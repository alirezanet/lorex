using Lorex.Core.Services;
using Spectre.Console;

namespace Lorex.Commands;

/// <summary>Implements <c>lorex install [skill…]</c>: installs one or more skills from the registry into the current project.</summary>
public static class InstallCommand
{
    private const string AllFlag = "--all";
    private const string PromptInstallAll = "Install all available skills";
    private const string PromptChooseSpecific = "Choose specific skills";

    /// <summary>Runs the command. Returns 0 on success, 1 on failure.</summary>
    public static int Run(string[] args)
    {
        var projectRoot = ProjectRootLocator.ResolveForExistingProject(Directory.GetCurrentDirectory());

        try
        {
            var cfg = ServiceFactory.Skills.ReadConfig(projectRoot);
            if (cfg.Registry is null)
            {
                AnsiConsole.MarkupLine("[red]No registry configured.[/] lorex is running in local-only mode.");
                AnsiConsole.MarkupLine("[dim]Run [bold]lorex init <url>[/] to connect a registry, then try again.[/]");
                return 1;
            }

            var installAll = WantsAll(args);
            var requestedSkills = ParseSkillNames(args);

            if (installAll && requestedSkills.Count > 0)
            {
                AnsiConsole.MarkupLine("[red]Usage:[/] lorex install [[bold]<skill>...[/]] [[bold]--all[/]]");
                AnsiConsole.MarkupLine("[dim]Use explicit skill names or [bold]--all[/], not both.[/]");
                return 1;
            }

            if (installAll)
            {
                requestedSkills = GetInstallableSkillNames(FetchAvailableSkills(cfg), cfg);
            }
            else if (requestedSkills.Count == 0)
            {
                requestedSkills = PromptForSkills(cfg);
            }

            if (requestedSkills.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]Nothing selected.[/]");
                return 0;
            }

            AnsiConsole.Status()
                .Start("Installing skills...", ctx =>
                {
                    foreach (var skillName in requestedSkills)
                    {
                        ctx.Status($"Installing [bold]{skillName}[/]...");
                        ServiceFactory.Skills.InstallSkill(projectRoot, skillName);
                    }

                    ctx.Spinner(Spinner.Known.Dots);
                    ctx.Status("Projecting skills into native agent locations...");
                    var config = ServiceFactory.Skills.ReadConfig(projectRoot);
                    ServiceFactory.Adapters.Project(projectRoot, config);
                });

            foreach (var skillName in requestedSkills)
            {
                AnsiConsole.MarkupLine($"[green]✓[/] Installed [bold]{skillName}[/] [dim](symlinked)[/]");
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

    internal static List<string> ParseSkillNames(string[] args) =>
        [.. args
            .Where(a => !string.IsNullOrWhiteSpace(a) && !string.Equals(a, AllFlag, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)];

    internal static List<string> GetInstallableSkillNames(
        IReadOnlyList<Core.Models.SkillMetadata> available,
        Core.Models.LorexConfig cfg) =>
        [.. available
            .Where(skill => !cfg.InstalledSkills.Contains(skill.Name, StringComparer.OrdinalIgnoreCase))
            .OrderBy(skill => skill.Name, StringComparer.OrdinalIgnoreCase)
            .Select(skill => skill.Name)];

    private static List<string> PromptForSkills(Core.Models.LorexConfig cfg)
    {
        var available = FetchAvailableSkills(cfg);
        if (available.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No skills found in the registry.[/]");
            return [];
        }

        var choices = available
            .Where(skill => !cfg.InstalledSkills.Contains(skill.Name, StringComparer.OrdinalIgnoreCase))
            .OrderBy(skill => skill.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (choices.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]All skills in the registry are already installed.[/]");
            AnsiConsole.MarkupLine("[dim]Run [bold]lorex sync[/] to update them, or [bold]lorex install <skill>[/] to reinstall one explicitly.[/]");
            return [];
        }

        var selectionMode = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[bold]How do you want to install skills?[/]")
                .AddChoices(PromptInstallAll, PromptChooseSpecific));

        if (string.Equals(selectionMode, PromptInstallAll, StringComparison.Ordinal))
            return [.. choices.Select(skill => skill.Name)];

        var selected = AnsiConsole.Prompt(
            new MultiSelectionPrompt<Core.Models.SkillMetadata>()
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

    private static IReadOnlyList<Core.Models.SkillMetadata> FetchAvailableSkills(Core.Models.LorexConfig cfg)
    {
        IReadOnlyList<Core.Models.SkillMetadata> available = [];
        AnsiConsole.Status()
            .Start("Fetching registry…", ctx =>
            {
                ctx.Spinner(Spinner.Known.Dots);
                available = ServiceFactory.Registry.ListAvailableSkills(cfg.Registry!.Url);
            });

        return available;
    }
}
