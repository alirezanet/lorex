using Lorex.Core.Services;
using Spectre.Console;

namespace Lorex.Commands;

/// <summary>Implements <c>lorex list</c>: fetches the registry cache and displays all available skills with their install status.</summary>
public static class ListCommand
{
    /// <summary>Runs the command. Returns 0 on success, 1 on failure.</summary>
    public static int Run(string[] args)
    {
        var projectRoot = Directory.GetCurrentDirectory();

        try
        {
            var config = ServiceFactory.Skills.ReadConfig(projectRoot);

            if (config.Registry is null)
            {
                AnsiConsole.MarkupLine("[yellow]No registry configured[/] — lorex is running in local-only mode.");
                AnsiConsole.MarkupLine("[dim]Run [bold]lorex init <url>[/] to connect a registry and browse available skills.[/]");
                return 0;
            }

            IReadOnlyList<Core.Models.SkillMetadata> available = [];
            AnsiConsole.Status()
                .Start("Fetching registry…", ctx =>
                {
                    ctx.Spinner(Spinner.Known.Dots);
                    available = ServiceFactory.Registry.ListAvailableSkills(config.Registry);
                });

            if (available.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]No skills found in the registry.[/]");
                return 0;
            }

            var installed = new HashSet<string>(config.InstalledSkills, StringComparer.OrdinalIgnoreCase);

            var table = new Table()
                .Border(TableBorder.Rounded)
                .AddColumn("[bold]Skill[/]")
                .AddColumn("[bold]Description[/]")
                .AddColumn("[bold]Version[/]")
                .AddColumn("[bold]Tags[/]")
                .AddColumn("[bold]Status[/]");

            foreach (var skill in available.OrderBy(s => s.Name))
            {
                var status = installed.Contains(skill.Name)
                    ? "[green]installed[/]"
                    : "[dim]available[/]";

                table.AddRow(
                    skill.Name,
                    skill.Description,
                    skill.Version,
                    string.Join(", ", skill.Tags),
                    status);
            }

            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[dim]Registry:[/] [bold]{0}[/]", Markup.Escape(config.Registry));
            AnsiConsole.Write(table);
            AnsiConsole.MarkupLine("[dim]Run [bold]lorex install <skill>[/] to install a skill.[/]");
            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine("[red]Error:[/] {0}", Markup.Escape(ex.Message));
            return 1;
        }
    }
}
