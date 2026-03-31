using Lorex.Cli;
using Lorex.Core.Models;
using Lorex.Core.Services;
using Spectre.Console;

namespace Lorex.Commands;

/// <summary>Implements <c>lorex list</c>: fetches the registry cache and displays available artifacts of one kind.</summary>
public static class ListCommand
{
    public static int Run(string[] args)
    {
        var projectRoot = ProjectRootLocator.ResolveForExistingProject(Directory.GetCurrentDirectory());

        ArtifactCliSupport.ParsedArtifactType parsedType;
        try
        {
            parsedType = ArtifactCliSupport.ParseArtifactTypeOrDefault(args);
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine("[red]Error:[/] {0}", Markup.Escape(ex.Message));
            return 1;
        }

        var kind = parsedType.Kind;

        try
        {
            var config = ServiceFactory.Artifacts.ReadConfig(projectRoot);

            if (config.Registry is null)
            {
                AnsiConsole.MarkupLine("[yellow]No registry configured[/] — lorex is running in local-only mode.");
                AnsiConsole.MarkupLine("[dim]Run [bold]lorex init <url>[/] to connect a registry and browse available artifacts.[/]");
                return 0;
            }

            IReadOnlyList<ArtifactMetadata> available = [];
            AnsiConsole.Status()
                .Start("Fetching registry…", ctx =>
                {
                    ctx.Spinner(Spinner.Known.Dots);
                    available = ServiceFactory.RegistryArtifacts.ListAvailableArtifacts(config, kind);
                });

            if (available.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]No {0} found in the registry.[/]", kind.DisplayNamePlural());
                return 0;
            }

            var installed = new HashSet<string>(config.Artifacts.Get(kind), StringComparer.OrdinalIgnoreCase);
            var recommended = ServiceFactory.RegistryArtifacts.GetRecommendedArtifactNames(projectRoot, available, config, kind);
            var recommendedSet = recommended.ToHashSet(StringComparer.OrdinalIgnoreCase);

            var table = new Table()
                .Border(TableBorder.Rounded)
                .AddColumn($"[bold]{kind.Title()}[/]")
                .AddColumn("[bold]Description[/]")
                .AddColumn("[bold]Version[/]")
                .AddColumn("[bold]Tags[/]")
                .AddColumn("[bold]Status[/]");

            foreach (var artifact in available
                .OrderByDescending(a => recommendedSet.Contains(a.Name))
                .ThenBy(a => a.Name, StringComparer.OrdinalIgnoreCase))
            {
                var status = installed.Contains(artifact.Name)
                    ? "[green]installed[/]"
                    : recommendedSet.Contains(artifact.Name)
                        ? "[blue]recommended[/]"
                        : "[dim]available[/]";

                table.AddRow(
                    artifact.Name,
                    artifact.Description,
                    artifact.Version,
                    string.Join(", ", artifact.Tags),
                    status);
            }

            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[dim]Registry:[/] [bold]{0}[/]", Markup.Escape(config.Registry.Url));
            AnsiConsole.MarkupLine("[dim]Artifact type:[/] [bold]{0}[/]", kind.DisplayNamePlural());
            if (recommended.Count > 0)
                AnsiConsole.MarkupLine("[dim]Recommended for this project:[/] [bold]{0}[/]", Markup.Escape(string.Join(", ", recommended)));
            AnsiConsole.Write(table);

            var installHint = kind == ArtifactKind.Skill
                ? "lorex install"
                : "lorex install --type prompt";
            AnsiConsole.MarkupLine(
                "[dim]Run [bold]{0}[/] to choose {1} interactively, [bold]{0} --recommended[/] to install suggested ones, or [bold]{0} <name>[/] to install one directly.[/]",
                installHint,
                kind.DisplayNamePlural());
            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine("[red]Error:[/] {0}", Markup.Escape(ex.Message));
            return 1;
        }
    }
}
