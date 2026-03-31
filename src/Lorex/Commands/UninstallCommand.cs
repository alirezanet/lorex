using Lorex.Core.Services;
using Spectre.Console;

namespace Lorex.Commands;

/// <summary>Implements <c>lorex uninstall &lt;skill&gt;</c>: removes an installed skill from the current project.</summary>
public static class UninstallCommand
{
    /// <summary>Runs the command. Returns 0 on success, 1 on failure.</summary>
    public static int Run(string[] args)
    {
        if (args.Length == 0)
        {
            AnsiConsole.MarkupLine("[red]Usage:[/] lorex uninstall [bold]<skill>[/]");
            return 1;
        }

        var skillName = args[0];
        var projectRoot = ProjectRootLocator.ResolveForExistingProject(Directory.GetCurrentDirectory());

        try
        {
            var config = ServiceFactory.Skills.ReadConfig(projectRoot);
            if (!config.InstalledSkills.Contains(skillName, StringComparer.OrdinalIgnoreCase))
            {
                AnsiConsole.MarkupLine("[yellow]Skill '[/]{0}[yellow]' is not installed in this project.[/]",
                    Markup.Escape(skillName));
                return 1;
            }

            ServiceFactory.Skills.UninstallSkill(projectRoot, skillName);

            // Re-project adapter outputs so the removed skill disappears from native agent integrations
            var updated = ServiceFactory.Skills.ReadConfig(projectRoot);
            ServiceFactory.Adapters.Project(projectRoot, updated);

            AnsiConsole.MarkupLine("[green]✓[/] Uninstalled [bold]{0}[/]", Markup.Escape(skillName));
            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine("[red]Error:[/] {0}", Markup.Escape(ex.Message));
            return 1;
        }
    }
}
