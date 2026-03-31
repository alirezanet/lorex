using Lorex.Cli;
using Lorex.Core.Models;
using Lorex.Core.Services;
using Spectre.Console;

namespace Lorex.Commands;

/// <summary>
/// Implements <c>lorex refresh [--target adapter]</c>: re-projects lorex skills into native agent surfaces
/// without fetching from the registry.
/// </summary>
public static class RefreshCommand
{
    /// <summary>Runs the command. Returns 0 on success, 1 on failure.</summary>
    public static int Run(string[] args)
    {
        // Parse optional --target <adapter>
        string? target = null;
        ArtifactKind? kind = null;
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (args[i] is "--target" or "-t")
            {
                target = args[i + 1];
            }
            else if (args[i] == "--type")
            {
                if (!ArtifactKindExtensions.TryParseCliValue(args[i + 1], out var parsedKind))
                {
                    AnsiConsole.MarkupLine("[red]Error:[/] Unsupported artifact type '{0}'. Expected `skill` or `prompt`.", Markup.Escape(args[i + 1]));
                    return 1;
                }

                kind = parsedKind;
            }
        }

        var projectRoot = ProjectRootLocator.ResolveForExistingProject(Directory.GetCurrentDirectory());

        try
        {
            var config = ServiceFactory.Artifacts.ReadConfig(projectRoot);

            if (target is not null)
                ServiceFactory.Adapters.ProjectTarget(projectRoot, config, target, kind);
            else
                ServiceFactory.Adapters.Project(projectRoot, config, kind);

            var kindLabel = kind is null ? "all artifact types" : kind.Value.DisplayNamePlural();
            AnsiConsole.MarkupLine("[green]✓[/] Lorex projections refreshed for [bold]{0}[/] [dim]({1})[/].", target ?? "all adapters", kindLabel);
            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine("[red]Error:[/] {0}", Markup.Escape(ex.Message));
            return 1;
        }
    }
}
