using Lorex.Cli;
using Lorex.Core.Services;
using Spectre.Console;

namespace Lorex.Commands;

/// <summary>Implements <c>lorex sync</c>: pulls the registry cache so all symlinked skills reflect the latest content.</summary>
public static class SyncCommand
{
    private const string GlobalFlag = "--global";

    /// <summary>Runs the command. Returns 0 on success, 1 on failure.</summary>
    public static int Run(string[] args, string? cwd = null, string? homeRoot = null)
    {
        if (args.Any(a => a is "--help" or "-h"))
            return PrintHelp();

        var isGlobal = args.Any(a =>
            string.Equals(a, GlobalFlag, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(a, "-g",       StringComparison.OrdinalIgnoreCase));

        if (isGlobal && OperatingSystem.IsWindows() && !WindowsDevModeHelper.IsSymlinkAvailable())
            WindowsDevModeHelper.EnsureSymlinkOrElevate();

        var projectRoot = isGlobal
            ? GlobalRootLocator.ResolveForExistingGlobal(homeRoot)
            : ProjectRootLocator.ResolveForExistingProject(cwd ?? Directory.GetCurrentDirectory());

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

                // ── Remove skills deleted from the registry ───────────────────
                // Broken symlinks with no registry source are never recoverable — always clean up.
                var staleSkills = ServiceFactory.Skills.FindStaleRegistrySkills(projectRoot);
                if (staleSkills.Count > 0)
                {
                    foreach (var skillName in staleSkills)
                        ServiceFactory.Skills.UninstallSkill(projectRoot, skillName);

                    // Clean up adapter projections for removed skills immediately.
                    ServiceFactory.Adapters.Project(projectRoot, ServiceFactory.Skills.ReadConfig(projectRoot));

                    AnsiConsole.MarkupLine(
                        "[yellow]Removed {0} stale skill{1} (deleted from registry):[/]",
                        staleSkills.Count, staleSkills.Count == 1 ? "" : "s");
                    foreach (var skillName in staleSkills)
                        AnsiConsole.MarkupLine("  [dim]•[/] {0}", Markup.Escape(skillName));
                }

                var overwriteCandidates = refreshedConfig.InstalledSkills
                    .Where(skillName =>
                        ServiceFactory.Skills.RequiresOverwriteApproval(projectRoot, skillName)
                        && ServiceFactory.Registry.FindSkillPath(refreshedConfig.Registry!.Url, skillName, refresh: false) is not null)
                    .ToList();

                var (approvedOverwriteSkills, skippedOverwriteSkills) = SkillOverwritePrompts.ResolveApprovedOverrides(
                    projectRoot,
                    overwriteCandidates,
                    skillName => $"Sync will replace local skill [bold]{Markup.Escape(skillName)}[/] with the registry version. Continue?");

                var oldVersions = refreshedConfig.InstalledSkillVersions;

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

                var newVersions = ServiceFactory.Skills.ReadConfig(projectRoot).InstalledSkillVersions;
                var changedSkills = updated
                    .Where(name =>
                    {
                        var oldVer = oldVersions.TryGetValue(name, out var ov) ? ov : null;
                        var newVer = newVersions.TryGetValue(name, out var nv) ? nv : null;
                        return oldVer != newVer;
                    })
                    .ToList();

                if (changedSkills.Count == 0 && skippedOverwriteSkills.Count == 0)
                    AnsiConsole.MarkupLine("[green]✓[/] Registry up to date.");
                else
                {
                    if (changedSkills.Count > 0)
                    {
                        AnsiConsole.MarkupLine("[green]✓[/] Registry synced — [bold]{0}[/] skill(s) updated:", changedSkills.Count);
                        foreach (var name in changedSkills)
                            AnsiConsole.MarkupLine("  • {0}", name);
                    }
                    else
                    {
                        AnsiConsole.MarkupLine("[green]✓[/] Registry up to date.");
                    }
                }

                foreach (var name in skippedOverwriteSkills)
                    AnsiConsole.MarkupLine("[yellow]Skipped[/] [bold]{0}[/] [dim](kept existing local skill)[/]", name);
            }

            // ── Notify about new registry-recommended taps ───────────────────
            var latestConfig = ServiceFactory.Skills.ReadConfig(projectRoot);
            if (latestConfig.Registry is not null)
            {
                var policy = ServiceFactory.Registry.ReadRegistryPolicy(latestConfig.Registry.Url, refresh: false);
                if (policy?.RecommendedTaps is { Length: > 0 } recommended)
                {
                    var configuredUrls = new HashSet<string>(
                        latestConfig.Taps.Select(t => t.Url), StringComparer.OrdinalIgnoreCase);
                    var newTaps = recommended.Where(t => !configuredUrls.Contains(t.Url)).ToList();
                    if (newTaps.Count > 0)
                    {
                        var tapList = string.Join(", ", newTaps.Select(t => Markup.Escape(t.Name)));
                        AnsiConsole.MarkupLine(
                            "[blue]ℹ[/] This registry recommends {0} new tap source{1}: [bold]{2}[/]",
                            newTaps.Count, newTaps.Count == 1 ? "" : "s", tapList);
                        AnsiConsole.MarkupLine(
                            "[dim]Run [bold]lorex tap add <url>[/] to add them, or [bold]lorex init[/] to configure interactively.[/]");
                    }
                }
            }

            // ── Tap sync ─────────────────────────────────────────────────────
            if (latestConfig.Taps.Length > 0)
            {
                IReadOnlyList<string> syncedTaps = [];
                AnsiConsole.Status()
                    .Start("Syncing taps…", ctx =>
                    {
                        ctx.Spinner(Spinner.Known.Dots);
                        syncedTaps = ServiceFactory.Taps.SyncAll(latestConfig);
                    });

                if (syncedTaps.Count == 0)
                    AnsiConsole.MarkupLine("[green]✓[/] Taps up to date.");
                else
                {
                    AnsiConsole.MarkupLine("[green]✓[/] Taps synced — [bold]{0}[/] updated:", syncedTaps.Count);
                    foreach (var name in syncedTaps)
                        AnsiConsole.MarkupLine("  • {0}", Markup.Escape(name));
                }

                // ── Restore missing tap skill symlinks ────────────────────────
                // After a fresh clone the tap caches now exist but the gitignored
                // symlinks in .lorex/skills/ are gone. Recreate them.
                latestConfig = ServiceFactory.Skills.ReadConfig(projectRoot);
                var missingTapSkills = new Dictionary<string, (Core.Models.TapConfig Tap, string SourcePath)>(
                    StringComparer.OrdinalIgnoreCase);

                foreach (var skillName in latestConfig.InstalledSkills)
                {
                    if (Directory.Exists(ServiceFactory.Skills.SkillDir(projectRoot, skillName))) continue;

                    if (!latestConfig.InstalledSkillSources.TryGetValue(skillName, out var src)) continue;
                    if (!src.StartsWith("tap:", StringComparison.OrdinalIgnoreCase)) continue;

                    var tapName   = src["tap:".Length..];
                    var tap       = latestConfig.Taps.FirstOrDefault(t =>
                        string.Equals(t.Name, tapName, StringComparison.OrdinalIgnoreCase));
                    if (tap is null) continue;

                    var sourcePath = ServiceFactory.Taps.FindSkillPath(tap, skillName);
                    if (sourcePath is null) continue;

                    missingTapSkills[skillName] = (tap, sourcePath);
                }

                if (missingTapSkills.Count > 0)
                {
                    IReadOnlyList<string> restored = [];
                    AnsiConsole.Status()
                        .Start("Restoring tap skills…", ctx =>
                        {
                            ctx.Spinner(Spinner.Known.Dots);
                            restored = ServiceFactory.Skills.InstallTapSkillsBatch(projectRoot, missingTapSkills);

                            if (restored.Count > 0)
                            {
                                ctx.Status("Projecting skills into native agent locations…");
                                ServiceFactory.Adapters.Project(projectRoot, ServiceFactory.Skills.ReadConfig(projectRoot));
                            }
                        });

                    if (restored.Count > 0)
                    {
                        AnsiConsole.MarkupLine("[green]✓[/] Restored [bold]{0}[/] tap skill(s):", restored.Count);
                        foreach (var name in restored)
                            AnsiConsole.MarkupLine("  • {0}", name);
                    }
                }
            }

            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine("[red]Error:[/] {0}", Markup.Escape(ex.Message));
            return 1;
        }
    }

    private static int PrintHelp() => HelpPrinter.Print(
        "lorex sync [-g]",
        "Pull the latest skill versions from the registry and all taps,\nand restore any missing symlinks (e.g. after a fresh clone).",
        options:
        [
            ("-g, --global", "Operate on the global lorex root (~/.lorex)"),
            ("-h, --help",   "Show this help"),
        ],
        examples:
        [
            ("", "lorex sync"),
            ("", "lorex sync --global"),
        ]);
}
