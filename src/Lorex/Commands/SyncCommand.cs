using Lorex.Cli;
using Lorex.Core.Services;
using Spectre.Console;

namespace Lorex.Commands;

/// <summary>Implements <c>lorex sync</c>: pulls the registry cache so all symlinked skills reflect the latest content.</summary>
public static class SyncCommand
{
    private const string GlobalFlag = "--global";

    /// <summary>Runs the command. Returns 0 on success, 1 on failure.</summary>
    public static int Run(string[] args)
    {
        var isGlobal = args.Any(a => string.Equals(a, GlobalFlag, StringComparison.OrdinalIgnoreCase));

        if (isGlobal && OperatingSystem.IsWindows() && !WindowsDevModeHelper.IsSymlinkAvailable())
        {
            if (!WindowsDevModeHelper.EnsureSymlinkOrElevate())
                return 0; // elevated process was launched
        }

        var projectRoot = isGlobal
            ? GlobalRootLocator.ResolveForExistingGlobal()
            : ProjectRootLocator.ResolveForExistingProject(Directory.GetCurrentDirectory());

        try
        {
            var cfg = ServiceFactory.Skills.ReadConfig(projectRoot);
            if (cfg.Registry is null && cfg.Taps.Length == 0)
            {
                RegistryCommandSupport.PrintNoRegistryConfigured();
                return 1;
            }

            // ── Registry sync ────────────────────────────────────────────────
            if (cfg.Registry is not null)
            {
                Lorex.Core.Models.LorexConfig refreshedConfig;
                if (!RegistryCommandSupport.TryRefreshConfiguredRegistry(projectRoot, out refreshedConfig, "Refreshing registry...", forceRefresh: true))
                    return 1;

                var overwriteCandidates = refreshedConfig.InstalledSkills
                    .Where(skillName =>
                        ServiceFactory.Skills.RequiresOverwriteApproval(projectRoot, skillName)
                        && ServiceFactory.Registry.FindSkillPath(refreshedConfig.Registry!.Url, skillName, refresh: false) is not null)
                    .ToList();

                var (approvedOverwriteSkills, skippedOverwriteSkills) = SkillOverwritePrompts.ResolveApprovedOverrides(
                    projectRoot,
                    overwriteCandidates,
                    skillName => $"Sync will replace local skill [bold]{Markup.Escape(skillName)}[/] with the registry version. Continue?");

                IReadOnlyList<string> updated = [];
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

                if (updated.Count == 0 && skippedOverwriteSkills.Count == 0)
                    AnsiConsole.MarkupLine("[green]✓[/] All registry skills are up to date.");
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

                foreach (var name in skippedOverwriteSkills)
                    AnsiConsole.MarkupLine("[yellow]Skipped[/] [bold]{0}[/] [dim](kept existing local skill)[/]", name);
            }

            // ── Tap sync ─────────────────────────────────────────────────────
            var latestConfig = ServiceFactory.Skills.ReadConfig(projectRoot);
            if (latestConfig.Taps.Length > 0)
            {
                IReadOnlyList<string> syncedTaps = [];
                AnsiConsole.Status()
                    .Start("Syncing taps…", ctx =>
                    {
                        ctx.Spinner(Spinner.Known.Dots);
                        syncedTaps = ServiceFactory.Taps.SyncAll(latestConfig);
                    });

                if (syncedTaps.Count > 0)
                    AnsiConsole.MarkupLine(
                        $"[green]✓[/] Taps synced: {string.Join(", ", syncedTaps.Select(Markup.Escape))}");
                else
                    AnsiConsole.MarkupLine("[green]✓[/] All taps are up to date.");
            }

            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine("[red]Error:[/] {0}", Markup.Escape(ex.Message));
            return 1;
        }
    }
}
