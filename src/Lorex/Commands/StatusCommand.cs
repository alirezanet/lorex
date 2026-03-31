using Lorex.Core.Services;
using Spectre.Console;

namespace Lorex.Commands;

/// <summary>Implements <c>lorex status</c>: shows the current project's registry, adapters, and installed skill link states.</summary>
public static class StatusCommand
{
    /// <summary>Runs the command. Returns 0 on success, 1 on failure.</summary>
    public static int Run(string[] args)
    {
        var projectRoot = ProjectRootLocator.ResolveForExistingProject(Directory.GetCurrentDirectory());

        try
        {
            var config = ServiceFactory.Skills.ReadConfig(projectRoot);

            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[bold]Project:[/] [dim]{0}[/]", Markup.Escape(projectRoot));
            AnsiConsole.MarkupLine("[bold]Registry:[/] [dim]{0}[/]", config.Registry is null ? "(none — local-only mode)" : Markup.Escape(config.Registry));
            AnsiConsole.MarkupLine("[bold]Adapters:[/] [dim]{0}[/]",
                config.Adapters.Length > 0 ? string.Join(", ", config.Adapters) : "none");
            AnsiConsole.WriteLine();

            if (config.Adapters.Length > 0)
            {
                var adapterTable = new Table()
                    .Border(TableBorder.Rounded)
                    .AddColumn("[bold]Adapter[/]")
                    .AddColumn("[bold]Target[/]");

                foreach (var adapterName in config.Adapters)
                {
                    foreach (var target in ServiceFactory.Adapters.DescribeTargets(projectRoot, adapterName))
                        adapterTable.AddRow(adapterName, Markup.Escape(target));
                }

                AnsiConsole.Write(adapterTable);
                AnsiConsole.WriteLine();
            }

            if (config.InstalledSkills.Length == 0)
            {
                AnsiConsole.MarkupLine("[dim]No skills installed. Run [bold]lorex list[/] to browse available skills.[/]");
                return 0;
            }

            var table = new Table()
                .Border(TableBorder.Rounded)
                .AddColumn("[bold]Skill[/]")
                .AddColumn("[bold]Link type[/]")
                .AddColumn("[bold]Path[/]");

            foreach (var name in config.InstalledSkills)
            {
                var dir = ServiceFactory.Skills.SkillDir(projectRoot, name);
                var (linkType, style) = GetLinkInfo(dir);
                table.AddRow(name, $"[{style}]{linkType}[/]", Markup.Escape(dir));
            }

            AnsiConsole.Write(table);
            AnsiConsole.MarkupLine(
                "[dim]Run [bold]lorex sync[/] to pull the latest versions, or [bold]lorex list[/] to browse.[/]");
            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine("[red]Error:[/] {0}", Markup.Escape(ex.Message));
            return 1;
        }
    }

    /// <summary>Determines the link type and display style for a skill directory.</summary>
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
