using Lorex.Cli;
using Lorex.Core.Models;
using Lorex.Core.Services;
using Spectre.Console;

namespace Lorex.Commands;

/// <summary>Implements <c>lorex show prompt &lt;name&gt;</c>: prints the canonical lorex prompt for manual use.</summary>
public static class ShowCommand
{
    public static int Run(string[] args)
    {
        if (args.Length > 0 && args.Any(arg => arg is "--help" or "-h"))
            return PrintHelp();

        if (args.Length != 2 || !string.Equals(args[0], "prompt", StringComparison.OrdinalIgnoreCase))
        {
            AnsiConsole.MarkupLine("[red]Usage:[/] lorex show prompt [bold]<name>[/]");
            return 1;
        }

        var promptName = args[1];
        var projectRoot = ProjectRootLocator.ResolveForExistingProject(Directory.GetCurrentDirectory());

        try
        {
            var metadata = ServiceFactory.Artifacts.ReadArtifactMetadata(projectRoot, ArtifactKind.Prompt, promptName);
            var body = ServiceFactory.Artifacts.ReadArtifactBody(projectRoot, ArtifactKind.Prompt, promptName);

            Console.WriteLine("---");
            Console.WriteLine($"name: {metadata.Name}");
            Console.WriteLine($"description: {metadata.Description}");
            Console.WriteLine($"version: {metadata.Version}");
            Console.WriteLine($"tags: {string.Join(", ", metadata.Tags)}");
            Console.WriteLine($"owner: {metadata.Owner}");
            Console.WriteLine("---");
            Console.WriteLine();
            Console.Write(body);
            if (!body.EndsWith('\n'))
                Console.WriteLine();

            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine("[red]Error:[/] {0}", Markup.Escape(ex.Message));
            return 1;
        }
    }

    private static int PrintHelp()
    {
        AnsiConsole.MarkupLine("[bold]USAGE[/]  lorex show prompt [bold]<name>[/]");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[dim]Print the canonical prompt metadata and body from .lorex/prompts for manual use in adapters without native projection support.[/]");
        return 0;
    }
}
