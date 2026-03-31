using Lorex.Cli;
using Lorex.Core.Models;
using Lorex.Core.Services;
using Spectre.Console;

namespace Lorex.Commands;

/// <summary>Implements <c>lorex status</c>: shows the current project's registry, adapters, and installed artifact link states.</summary>
public static class StatusCommand
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
                AnsiConsole.MarkupLine("[red]Usage:[/] lorex status [[bold]--type skill|prompt[/]]");
                return 1;
            }

            var kindFilter = parsedType.Kind;
            var config = ServiceFactory.Artifacts.ReadConfig(projectRoot);

            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[bold]Project:[/] [dim]{0}[/]", Markup.Escape(projectRoot));
            AnsiConsole.MarkupLine("[bold]Registry:[/] [dim]{0}[/]", config.Registry is null ? "(none — local-only mode)" : Markup.Escape(config.Registry.Url));
            if (config.Registry is not null)
            {
                var policy = config.Registry.Policy;
                if (string.Equals(policy.PublishMode, Lorex.Core.Models.RegistryPublishModes.PullRequest, StringComparison.OrdinalIgnoreCase))
                {
                    AnsiConsole.MarkupLine(
                        "[bold]Publish mode:[/] [dim]{0}[/] [dim](base branch: {1})[/]",
                        Markup.Escape(policy.PublishMode),
                        Markup.Escape(policy.BaseBranch));
                }
                else
                {
                    AnsiConsole.MarkupLine("[bold]Publish mode:[/] [dim]{0}[/]", Markup.Escape(policy.PublishMode));
                }
            }
            AnsiConsole.MarkupLine("[bold]Adapters:[/] [dim]{0}[/]",
                config.Adapters.Length > 0 ? string.Join(", ", config.Adapters) : "none");
            AnsiConsole.WriteLine();

            if (config.Adapters.Length > 0)
            {
                var adapterTable = new Table()
                    .Border(TableBorder.Rounded)
                    .AddColumn("[bold]Adapter[/]")
                    .AddColumn("[bold]Artifact type[/]")
                    .AddColumn("[bold]Target[/]");

                foreach (var adapterName in config.Adapters)
                {
                    foreach (var target in ServiceFactory.Adapters.DescribeTargets(projectRoot, adapterName, kindFilter))
                        adapterTable.AddRow(adapterName, target.Kind.DisplayName(), Markup.Escape(target.Path));
                }

                AnsiConsole.Write(adapterTable);
                AnsiConsole.WriteLine();
            }

            var kinds = kindFilter is null
                ? new[] { ArtifactKind.Skill, ArtifactKind.Prompt }
                : new[] { kindFilter.Value };
            var installedAny = kinds.Any(kind => config.Artifacts.Get(kind).Length > 0);

            if (!installedAny)
            {
                var kindLabel = kindFilter is null ? "artifacts" : kindFilter.Value.DisplayNamePlural();
                AnsiConsole.MarkupLine("[dim]No {0} installed. Run [bold]lorex list[/] to browse registry skills or [bold]lorex create --type prompt[/] to scaffold a prompt.[/]",
                    kindLabel);
                return 0;
            }

            var table = new Table()
                .Border(TableBorder.Rounded)
                .AddColumn("[bold]Type[/]")
                .AddColumn("[bold]Name[/]")
                .AddColumn("[bold]Link type[/]")
                .AddColumn("[bold]Path[/]");

            foreach (var kind in kinds)
            {
                foreach (var name in config.Artifacts.Get(kind).OrderBy(name => name, StringComparer.OrdinalIgnoreCase))
                {
                    var dir = ServiceFactory.Artifacts.ArtifactDir(projectRoot, kind, name);
                    var (linkType, style) = GetLinkInfo(dir);
                    table.AddRow(kind.DisplayName(), name, $"[{style}]{linkType}[/]", Markup.Escape(dir));
                }
            }

            AnsiConsole.Write(table);
            AnsiConsole.MarkupLine(
                "[dim]Run [bold]lorex sync[/] to pull the latest shared artifacts, [bold]lorex list[/] to browse skills, or [bold]lorex list --type prompt[/] to browse prompts.[/]");
            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine("[red]Error:[/] {0}", Markup.Escape(ex.Message));
            return 1;
        }
    }

    /// <summary>Determines the link type and display style for an artifact directory.</summary>
    private static (string label, string style) GetLinkInfo(string dir)
    {
        if (!Directory.Exists(dir))
            return ("missing", "red");

        var info = new DirectoryInfo(dir);
        if (info.LinkTarget is not null)
        {
            var targetOk = Directory.Exists(info.LinkTarget);
            return targetOk ? ("symlink", "green") : ("broken symlink", "red");
        }

        return ("local", "yellow");
    }
}
