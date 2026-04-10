using Lorex.Cli;
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
    public static int Run(string[] args, string? cwd = null)
    {
        if (args.Any(a => a is "--help" or "-h"))
            return PrintHelp();

        // Parse optional --target <adapter>
        string? target = null;
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (args[i] is "--target" or "-t")
            {
                target = args[i + 1];
                break;
            }
        }

        var projectRoot = ProjectRootLocator.ResolveForExistingProject(cwd ?? Directory.GetCurrentDirectory());

        try
        {
            var config = ServiceFactory.Skills.ReadConfig(projectRoot);

            if (target is not null)
                ServiceFactory.Adapters.ProjectTarget(projectRoot, config, target);
            else
                ServiceFactory.Adapters.Project(projectRoot, config);

            AnsiConsole.MarkupLine("[green]✓[/] Lorex projections refreshed for [bold]{0}[/].", target ?? "all adapters");
            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine("[red]Error:[/] {0}", Markup.Escape(ex.Message));
            return 1;
        }
    }

    private static int PrintHelp() => HelpPrinter.Print(
        "lorex refresh [--target <adapter>]",
        "Re-project lorex skills into native agent locations without fetching from the registry.\nUseful after adding a new adapter or when projections are out of sync.",
        options:
        [
            ("-t, --target <adapter>", "Re-project a single adapter only"),
            ("-h, --help",             "Show this help"),
        ],
        examples:
        [
            ("Refresh all adapters",        "lorex refresh"),
            ("Refresh only Claude adapter", "lorex refresh --target claude"),
        ]);
}
