using Lorex.Core.Services;
using Spectre.Console;

namespace Lorex.Commands;

/// <summary>
/// Implements <c>lorex refresh [--target adapter]</c>: re-injects the skill index into agent config files
/// without fetching from the registry. Useful after manually editing a skill or changing adapter selection.
/// </summary>
public static class RefreshCommand
{
    /// <summary>Runs the command. Returns 0 on success, 1 on failure.</summary>
    public static int Run(string[] args)
    {
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

        var projectRoot = Directory.GetCurrentDirectory();

        try
        {
            var config = ServiceFactory.Skills.ReadConfig(projectRoot);

            if (target is not null)
                ServiceFactory.Adapters.CompileTarget(projectRoot, config, target);
            else
                ServiceFactory.Adapters.Compile(projectRoot, config);

            AnsiConsole.MarkupLine("[green]✓[/] Skill index refreshed in [bold]{0}[/].", target ?? "all adapters");
            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine("[red]Error:[/] {0}", Markup.Escape(ex.Message));
            return 1;
        }
    }
}
