using Lorex.Core.Services;
using Spectre.Console;

namespace Lorex.Commands;

/// <summary>Implements <c>lorex sync</c>: pulls the registry cache so all symlinked skills reflect the latest content.</summary>
public static class SyncCommand
{
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

            IReadOnlyList<string> updated = [];
            List<string> skipped = [];
            Lorex.Core.Models.LorexConfig refreshedConfig = cfg;

            AnsiConsole.Status()
                .Start("Refreshing registry...", ctx =>
                {
                    ctx.Spinner(Spinner.Known.Dots);
                    refreshedConfig = ServiceFactory.Skills.RefreshRegistryPolicy(projectRoot);
                });

            var overwriteCandidates = refreshedConfig.InstalledSkills
                .Where(skillName =>
                    ServiceFactory.Skills.RequiresOverwriteApproval(projectRoot, skillName)
                    && ServiceFactory.Registry.FindSkillPath(refreshedConfig.Registry!.Url, skillName, refresh: false) is not null)
                .ToList();

            var (approvedOverwriteSkills, skippedOverwriteSkills) = SkillOverwritePrompts.ResolveApprovedOverrides(
                projectRoot,
                overwriteCandidates,
                skillName => $"Sync will replace local skill [bold]{Markup.Escape(skillName)}[/] with the registry version. Continue?");
            skipped = skippedOverwriteSkills;

            AnsiConsole.Status()
                .Start("Syncing skills from registry...", ctx =>
                {
                    ctx.Spinner(Spinner.Known.Dots);
                    updated = ServiceFactory.Skills.SyncSkills(projectRoot, approvedOverwriteSkills, refreshRegistry: false);

                    if (updated.Count > 0)
                    {
                        ctx.Status("Projecting skills into native agent locations...");
                        var config = ServiceFactory.Skills.ReadConfig(projectRoot);
                        ServiceFactory.Adapters.Project(projectRoot, config);
                    }
                });

            if (updated.Count == 0 && skipped.Count == 0)
                AnsiConsole.MarkupLine("[green]✓[/] All skills are up to date.");
            else
            {
                if (updated.Count > 0)
                {
                    AnsiConsole.MarkupLine("[green]✓[/] Registry pulled — [bold]{0}[/] skill(s) reflect latest content:", updated.Count);
                    foreach (var name in updated)
                        AnsiConsole.MarkupLine("  • {0}", name);
                    AnsiConsole.MarkupLine("[dim](Symlinked skills updated automatically via cache pull.)[/]");
                }
                else
                {
                    AnsiConsole.MarkupLine("[green]✓[/] Registry pulled.");
                }
            }

            foreach (var name in skipped)
                AnsiConsole.MarkupLine("[yellow]Skipped[/] [bold]{0}[/] [dim](kept existing local skill)[/]", name);

            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine("[red]Error:[/] {0}", Markup.Escape(ex.Message));
            return 1;
        }
    }
}
