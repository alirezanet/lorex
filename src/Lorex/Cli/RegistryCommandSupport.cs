using Lorex.Core.Models;
using Spectre.Console;

namespace Lorex.Cli;

internal static class RegistryCommandSupport
{
    internal static bool TryReadConfiguredRegistry(string projectRoot, out LorexConfig config)
    {
        config = ServiceFactory.Artifacts.ReadConfig(projectRoot);
        if (config.Registry is not null)
            return true;

        PrintNoRegistryConfigured();
        return false;
    }

    internal static bool TryRefreshConfiguredRegistry(string projectRoot, out LorexConfig config, string statusMessage = "Refreshing registry policy...")
    {
        LorexConfig? loadedConfig = null;

        try
        {
            AnsiConsole.Status()
                .Start(statusMessage, ctx =>
                {
                    ctx.Spinner(Spinner.Known.Dots);
                    loadedConfig = ServiceFactory.Artifacts.RefreshRegistryPolicy(projectRoot);
                });
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine("[red]Error:[/] {0}", Markup.Escape(ex.Message));
            config = null!;
            return false;
        }

        config = loadedConfig ?? throw new InvalidOperationException("Registry refresh did not return a project configuration.");

        if (config.Registry is not null)
            return true;

        PrintNoRegistryConfigured();
        return false;
    }

    internal static void PrintNoRegistryConfigured()
    {
        AnsiConsole.MarkupLine("[red]No registry configured.[/] lorex is running in local-only mode.");
        AnsiConsole.MarkupLine("[dim]Run [bold]lorex init <url>[/] to connect a registry, then try again.[/]");
    }
}
