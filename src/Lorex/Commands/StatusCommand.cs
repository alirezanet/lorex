using Lorex.Cli;
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
                    .AddColumn("[bold]Target[/]");

                foreach (var adapterName in config.Adapters)
                {
                    foreach (var target in ServiceFactory.Adapters.DescribeTargets(projectRoot, adapterName))
                        adapterTable.AddRow(adapterName, Markup.Escape(target));
                }

                AnsiConsole.Write(adapterTable);
                AnsiConsole.WriteLine();
            }

            // ── Taps ─────────────────────────────────────────────────────────
            if (config.Taps.Length > 0)
            {
                AnsiConsole.MarkupLine("[bold]Taps:[/] [dim]{0} configured[/]", config.Taps.Length);
                var tapTable = new Table()
                    .Border(TableBorder.Rounded)
                    .AddColumn("[bold]Name[/]")
                    .AddColumn("[bold]URL[/]")
                    .AddColumn("[bold]Root[/]");

                foreach (var tap in config.Taps)
                {
                    tapTable.AddRow(
                        Markup.Escape(tap.Name),
                        Markup.Escape(tap.Url),
                        tap.Root is not null ? Markup.Escape(tap.Root) : "[dim](repo root)[/]");
                }

                AnsiConsole.Write(tapTable);
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
                .AddColumn("[bold]Version[/]")
                .AddColumn("[bold]Source[/]")
                .AddColumn("[bold]Link type[/]")
                .AddColumn("[bold]Path[/]");

            foreach (var name in config.InstalledSkills)
            {
                var dir = ServiceFactory.Skills.SkillDir(projectRoot, name);
                var (linkType, style) = GetLinkInfo(dir);
                var version = ServiceFactory.Skills.GetInstalledSkillVersion(projectRoot, name, config);
                var sourceLabel = GetSourceLabel(name, config.InstalledSkillSources, style);
                table.AddRow(name, version, sourceLabel, $"[{style}]{linkType}[/]", Markup.Escape(dir));
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

    /// <summary>Returns a Spectre-marked-up source label for the installed-skills table.</summary>
    private static string GetSourceLabel(string name, Dictionary<string, string> sources, string linkStyle)
    {
        if (sources.TryGetValue(name, out var src))
        {
            if (src.StartsWith("tap:", StringComparison.OrdinalIgnoreCase))
                return $"[blue]tap: {Markup.Escape(src["tap:".Length..])}[/]";
            if (src.StartsWith("url:", StringComparison.OrdinalIgnoreCase))
                return "[dim]url[/]";
            if (string.Equals(src, "registry", StringComparison.OrdinalIgnoreCase))
                return "[dim]registry[/]";
        }

        // No source entry — infer from link type (symlink = registry, local = local/built-in)
        return linkStyle == "green" ? "[dim]registry[/]" : "[dim]local[/]";
    }
}
