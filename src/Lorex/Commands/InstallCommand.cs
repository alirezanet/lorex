using Lorex.Core.Services;
using Spectre.Console;

namespace Lorex.Commands;

/// <summary>Implements <c>lorex install &lt;skill&gt;</c>: installs a skill from the registry into the current project.</summary>
public static class InstallCommand
{
    /// <summary>Runs the command. Returns 0 on success, 1 on failure.</summary>
    public static int Run(string[] args)
    {
        if (args.Length == 0)
        {
            AnsiConsole.MarkupLine("[red]Usage:[/] lorex install [bold]<skill>[/]");
            return 1;
        }

        var skillName = args[0];
        var projectRoot = Directory.GetCurrentDirectory();

        try
        {
            var cfg = ServiceFactory.Skills.ReadConfig(projectRoot);
            if (cfg.Registry is null)
            {
                AnsiConsole.MarkupLine("[red]No registry configured.[/] lorex is running in local-only mode.");
                AnsiConsole.MarkupLine("[dim]Run [bold]lorex init <url>[/] to connect a registry, then try again.[/]");
                return 1;
            }
            bool usedSymlink = false;
            AnsiConsole.Status()
                .Start($"Installing [bold]{skillName}[/]...", ctx =>
                {
                    ctx.Spinner(Spinner.Known.Dots);
                    usedSymlink = ServiceFactory.Skills.InstallSkill(projectRoot, skillName);

                    ctx.Status("Compiling skill index...");
                    var config = ServiceFactory.Skills.ReadConfig(projectRoot);
                    ServiceFactory.Adapters.Compile(projectRoot, config);
                });

            if (usedSymlink)
            {
                AnsiConsole.MarkupLine($"[green]✓[/] Installed [bold]{skillName}[/] [dim](symlinked)[/]");
            }
            else
            {
                AnsiConsole.MarkupLine($"[green]✓[/] Installed [bold]{skillName}[/] [yellow](copied)[/]");

                // Give the user actionable guidance if Developer Mode is the reason symlinks failed.
                if (OperatingSystem.IsWindows() && !WindowsDevModeHelper.IsSymlinkAvailable())
                {
                    WindowsDevModeHelper.PrintDevModeGuidance();
                    WindowsDevModeHelper.OfferToOpenSettings();
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
}
