using Lorex.Cli;
using Lorex.Core.Services;
using Spectre.Console;

namespace Lorex.Commands;

/// <summary>Implements <c>lorex uninstall &lt;skill&gt;</c>: removes an installed skill from the current project.</summary>
public static class UninstallCommand
{
    private const string AllFlag = "--all";
    private const string PromptUninstallAll = "Uninstall all installed skills";
    private const string PromptChooseSpecific = "Choose specific skills";

    /// <summary>Runs the command. Returns 0 on success, 1 on failure.</summary>
    public static int Run(string[] args)
    {
        var projectRoot = ProjectRootLocator.ResolveForExistingProject(Directory.GetCurrentDirectory());

        try
        {
            var config = ServiceFactory.Skills.ReadConfig(projectRoot);
            var uninstallAll = WantsAll(args);
            var requestedSkills = ParseSkillNames(args);

            if (uninstallAll && requestedSkills.Count > 0)
            {
                AnsiConsole.MarkupLine("[red]Usage:[/] lorex uninstall [[bold]<skill>...[/]] [[bold]--all[/]]");
                AnsiConsole.MarkupLine("[dim]Use explicit skill names or [bold]--all[/], not both.[/]");
                return 1;
            }

            if (uninstallAll)
            {
                requestedSkills = GetInstalledSkillNames(config);
            }
            else if (requestedSkills.Count == 0)
            {
                requestedSkills = PromptForSkills(config);
            }

            if (requestedSkills.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]Nothing selected.[/]");
                return 0;
            }

            var missingSkills = requestedSkills
                .Where(skill => !config.InstalledSkills.Contains(skill, StringComparer.OrdinalIgnoreCase))
                .ToList();

            if (missingSkills.Count > 0)
            {
                foreach (var skillName in missingSkills)
                {
                    AnsiConsole.MarkupLine("[yellow]Skill '[/]{0}[yellow]' is not installed in this project.[/]",
                        Markup.Escape(skillName));
                }

                return 1;
            }

            foreach (var skillName in requestedSkills)
                ServiceFactory.Skills.UninstallSkill(projectRoot, skillName);

            // Re-project adapter outputs so the removed skill disappears from native agent integrations
            var updated = ServiceFactory.Skills.ReadConfig(projectRoot);
            ServiceFactory.Adapters.Project(projectRoot, updated);

            foreach (var skillName in requestedSkills)
                AnsiConsole.MarkupLine("[green]✓[/] Uninstalled [bold]{0}[/]", Markup.Escape(skillName));

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

    internal static List<string> ParseSkillNames(string[] args) =>
        [.. args
            .Where(a => !string.IsNullOrWhiteSpace(a) && !string.Equals(a, AllFlag, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)];

    internal static List<string> GetInstalledSkillNames(Core.Models.LorexConfig config) =>
        [.. config.InstalledSkills.OrderBy(name => name, StringComparer.OrdinalIgnoreCase)];

    private static List<string> PromptForSkills(Core.Models.LorexConfig config)
    {
        var installedSkills = GetInstalledSkillNames(config);
        if (installedSkills.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No skills are installed in this project.[/]");
            return [];
        }

        var selectionMode = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[bold]How do you want to uninstall skills?[/]")
                .AddChoices(PromptUninstallAll, PromptChooseSpecific));

        if (string.Equals(selectionMode, PromptUninstallAll, StringComparison.Ordinal))
            return installedSkills;

        return AnsiConsole.Prompt(
            new MultiSelectionPrompt<string>()
                .Title("[bold]Which skills do you want to uninstall?[/]")
                .InstructionsText("[dim](Space to select, Enter to confirm)[/]")
                .AddChoices(installedSkills));
    }
}
