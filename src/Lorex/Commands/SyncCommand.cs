using Lorex.Cli;
using Lorex.Core.Models;
using Lorex.Core.Services;
using Spectre.Console;

namespace Lorex.Commands;

/// <summary>Implements <c>lorex sync</c>: pulls the registry cache so installed registry artifacts reflect the latest content.</summary>
public static class SyncCommand
{
    /// <summary>Runs the command. Returns 0 on success, 1 on failure.</summary>
    public static int Run(string[] args)
    {
        var projectRoot = ProjectRootLocator.ResolveForExistingProject(Directory.GetCurrentDirectory());

        try
        {
            var parsedType = ArtifactCliSupport.ParseOptionalArtifactType(args);
            if (parsedType.RemainingArgs.Length > 0)
            {
                AnsiConsole.MarkupLine("[red]Usage:[/] lorex sync [[bold]--type skill|prompt[/]]");
                return 1;
            }

            var kindFilter = parsedType.Kind;
            if (!RegistryCommandSupport.TryReadConfiguredRegistry(projectRoot, out var cfg))
                return 1;

            var updatedByKind = new Dictionary<ArtifactKind, IReadOnlyList<string>>();
            var skippedByKind = new Dictionary<ArtifactKind, List<string>>();
            Lorex.Core.Models.LorexConfig refreshedConfig = cfg;

            if (!RegistryCommandSupport.TryRefreshConfiguredRegistry(projectRoot, out refreshedConfig, "Refreshing registry..."))
                return 1;

            var kinds = kindFilter is null
                ? new[] { ArtifactKind.Skill, ArtifactKind.Prompt }
                : new[] { kindFilter.Value };
            var hasUpdates = false;

            AnsiConsole.Status()
                .Start("Syncing artifacts from registry...", ctx =>
                {
                    ctx.Spinner(Spinner.Known.Dots);

                    foreach (var kind in kinds)
                    {
                        var overwriteCandidates = refreshedConfig.Artifacts.Get(kind)
                            .Where(artifactName =>
                                ServiceFactory.Artifacts.RequiresOverwriteApproval(projectRoot, kind, artifactName)
                                && ServiceFactory.Registry.FindArtifactPath(refreshedConfig.Registry!.Url, kind, artifactName, refresh: false) is not null)
                            .ToList();

                        var (approvedOverwriteArtifacts, skippedOverwriteArtifacts) = ArtifactOverwritePrompts.ResolveApprovedOverrides(
                            projectRoot,
                            kind,
                            overwriteCandidates,
                            artifactName => $"Sync will replace local {kind.DisplayName()} [bold]{Markup.Escape(artifactName)}[/] with the registry version. Continue?");

                        skippedByKind[kind] = skippedOverwriteArtifacts;

                        ctx.Status($"Syncing {kind.DisplayNamePlural()}...");
                        var updated = ServiceFactory.Artifacts.SyncArtifacts(projectRoot, kind, approvedOverwriteArtifacts, refreshRegistry: false);
                        updatedByKind[kind] = updated;
                        hasUpdates |= updated.Count > 0;
                    }

                    if (!hasUpdates)
                        return;

                    ctx.Status("Projecting artifacts into native agent locations...");
                    var config = ServiceFactory.Artifacts.ReadConfig(projectRoot);
                    ServiceFactory.Adapters.Project(projectRoot, config, kindFilter);
                });

            var skippedCount = skippedByKind.Values.Sum(names => names.Count);
            if (!hasUpdates && skippedCount == 0)
            {
                var kindLabel = kindFilter is null ? "artifacts" : kindFilter.Value.DisplayNamePlural();
                AnsiConsole.MarkupLine("[green]✓[/] All {0} are up to date.", kindLabel);
                return 0;
            }

            if (!hasUpdates)
            {
                AnsiConsole.MarkupLine("[green]✓[/] Registry pulled.");
            }
            else
            {
                foreach (var kind in kinds)
                {
                    if (!updatedByKind.TryGetValue(kind, out var updated) || updated.Count == 0)
                        continue;

                    AnsiConsole.MarkupLine("[green]✓[/] [bold]{0}[/] {1} reflect the latest registry content:", updated.Count, kind.DisplayNamePlural());
                    foreach (var name in updated)
                        AnsiConsole.MarkupLine("  • {0}", name);
                }

                AnsiConsole.MarkupLine("[dim](Registry-backed artifacts update automatically through the shared cache.)[/]");
            }

            foreach (var kind in kinds)
            {
                if (!skippedByKind.TryGetValue(kind, out var skipped))
                    continue;

                foreach (var name in skipped)
                    AnsiConsole.MarkupLine("[yellow]Skipped[/] [bold]{0}[/] [dim](kept existing local {1})[/]", name, kind.DisplayName());
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
