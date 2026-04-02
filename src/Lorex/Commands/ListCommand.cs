using Lorex.Cli;
using Lorex.Core.Services;
using Spectre.Console;

namespace Lorex.Commands;

/// <summary>Implements <c>lorex list</c>: fetches the registry cache and displays all available skills with their install status.</summary>
public static class ListCommand
{
    /// <summary>Runs the command. Returns 0 on success, 1 on failure.</summary>
    public static int Run(string[] args)
    {
        var projectRoot = ProjectRootLocator.ResolveForExistingProject(Directory.GetCurrentDirectory());

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
                    available = ServiceFactory.RegistrySkills.ListAvailableSkills(config);
                });

            if (available.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]No skills found in the registry.[/]");
                return 0;
            }

            var installed = new HashSet<string>(config.InstalledSkills, StringComparer.OrdinalIgnoreCase);
            var installedVersions = config.InstalledSkillVersions;
            var recommended = ServiceFactory.RegistrySkills.GetRecommendedSkillNames(projectRoot, available, config);
            var recommendedSet = recommended.ToHashSet(StringComparer.OrdinalIgnoreCase);

            var table = new Table()
                .Border(TableBorder.Rounded)
                .AddColumn("[bold]Skill[/]")
                .AddColumn("[bold]Description[/]")
                .AddColumn("[bold]Version[/]")
                .AddColumn("[bold]Tags[/]")
                .AddColumn("[bold]Status[/]");

            foreach (var skill in available
                .OrderByDescending(s => recommendedSet.Contains(s.Name))
                .ThenBy(s => s.Name, StringComparer.OrdinalIgnoreCase))
            {
                string status;
                if (installed.Contains(skill.Name))
                {
                    status = HasUpdate(skill.Version, installedVersions, skill.Name)
                        ? "[yellow]update available[/]"
                        : "[green]installed[/]";
                }
                else
                {
                    status = recommendedSet.Contains(skill.Name)
                        ? "[blue]recommended[/]"
                        : "[dim]available[/]";
                }

                table.AddRow(
                    skill.Name,
                    skill.Description,
                    skill.Version,
                    string.Join(", ", skill.Tags),
                    status);
            }

            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[dim]Registry:[/] [bold]{0}[/]", Markup.Escape(config.Registry.Url));
            if (recommended.Count > 0)
                AnsiConsole.MarkupLine("[dim]Recommended for this project:[/] [bold]{0}[/]", Markup.Escape(string.Join(", ", recommended)));
            AnsiConsole.Write(table);
            AnsiConsole.MarkupLine("[dim]Run [bold]lorex install[/] to choose skills interactively, [bold]lorex install --recommended[/] to install suggested skills, or [bold]lorex install <skill>[/] to install one directly.[/]");
            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine("[red]Error:[/] {0}", Markup.Escape(ex.Message));
            return 1;
        }
    }

    internal static bool HasUpdate(string registryVersion, Dictionary<string, string> installedVersions, string skillName)
    {
        if (!installedVersions.TryGetValue(skillName, out var installedVersion))
            return false;

        if (System.Version.TryParse(registryVersion, out var rv) &&
            System.Version.TryParse(installedVersion, out var iv))
            return rv > iv;

        // Fall back to string comparison — different strings means unknown state, not an update
        return false;
    }
}
