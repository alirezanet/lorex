using Lorex.Cli;
using Lorex.Core.Models;
using Lorex.Core.Services;
using Spectre.Console;

namespace Lorex.Commands;

/// <summary>Implements <c>lorex list</c>: fetches the registry cache and displays available skills with optional search, tag filter, and pagination.</summary>
public static class ListCommand
{
    private const string SearchFlag   = "--search";
    private const string TagFlag      = "--tag";
    private const string PageFlag     = "--page";
    private const string PageSizeFlag = "--page-size";
    private const string GlobalFlag   = "--global";
    private const int    DefaultPageSize = 25;

    /// <summary>Runs the command. Returns 0 on success, 1 on failure.</summary>
    public static int Run(string[] args, string? cwd = null, string? homeRoot = null)
    {
        var isGlobal = args.Any(a =>
            string.Equals(a, GlobalFlag, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(a, "-g",       StringComparison.OrdinalIgnoreCase));

        var projectRoot = isGlobal
            ? GlobalRootLocator.ResolveForExistingGlobal(homeRoot)
            : ProjectRootLocator.ResolveForExistingProject(cwd ?? Directory.GetCurrentDirectory());

        try
        {
            var config = ServiceFactory.Skills.ReadConfig(projectRoot);

            if (config.Registry is null && config.Taps.Length == 0)
            {
                AnsiConsole.MarkupLine("[yellow]No registry or taps configured[/] — lorex is running in local-only mode.");
                AnsiConsole.MarkupLine("[dim]Run [bold]lorex init <url>[/] to connect a registry, or [bold]lorex tap add <url>[/] to add a skill source.[/]");
                return 0;
            }

            var search   = ParseSearch(args);
            var tag      = ParseTag(args);
            var page     = ParsePage(args);
            var pageSize = ParsePageSize(args);

            // TUI mode: interactive terminal with no paging flags → launch the browser
            var hasPagingFlags = args.Any(a =>
                string.Equals(a, PageFlag,     StringComparison.OrdinalIgnoreCase) ||
                string.Equals(a, PageSizeFlag, StringComparison.OrdinalIgnoreCase));

            IReadOnlyList<SkillMetadata> available = [];
            Dictionary<string, string> skillSources = [];
            AnsiConsole.Status()
                .Start("Fetching registry…", ctx =>
                {
                    ctx.Spinner(Spinner.Known.Dots);
                    (available, skillSources) = ServiceFactory.RegistrySkills.ListAllSkills(config);
                });

            if (available.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]No skills found in the registry or configured taps.[/]");
                return 0;
            }

            var installed = new HashSet<string>(config.InstalledSkills, StringComparer.OrdinalIgnoreCase);
            var installedVersions = config.InstalledSkillVersions;
            var recommended = isGlobal
                ? []
                : ServiceFactory.RegistrySkills.GetRecommendedSkillNames(projectRoot, available, config);
            var recommendedSet = recommended.ToHashSet(StringComparer.OrdinalIgnoreCase);

            // Launch interactive TUI when stdout is a terminal and no pagination flags given
            if (!hasPagingFlags && !Console.IsOutputRedirected && !Console.IsInputRedirected)
            {
                SkillBrowserTui.Run(available, installed, installedVersions, recommendedSet, skillSources, search, tag);
                return 0;
            }

            // Sort: recommended first → registry before tap → alphabetical
            var sorted = available
                .OrderByDescending(s => recommendedSet.Contains(s.Name))
                .ThenByDescending(s => !IsTapSkill(s.Name, skillSources))
                .ThenBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            // Apply search/tag filter
            var filtered = ServiceFactory.RegistrySkills.FilterBySearch(sorted, search, tag);

            var totalCount = filtered.Count;
            var hasFilter  = !string.IsNullOrWhiteSpace(search) || !string.IsNullOrWhiteSpace(tag);

            if (totalCount == 0)
            {
                AnsiConsole.MarkupLine(hasFilter
                    ? "[yellow]No skills match your filter.[/] Try a different [bold]--search[/] or [bold]--tag[/] value."
                    : "[yellow]No skills found in the registry.[/]");
                return 0;
            }

            // Pagination
            List<SkillMetadata> pageItems;
            int totalPages;
            int startIndex;
            int endIndex;

            if (pageSize > 0)
            {
                totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
                page       = Math.Min(page, totalPages);
                startIndex = (page - 1) * pageSize + 1;
                endIndex   = Math.Min(page * pageSize, totalCount);
                pageItems  = [.. filtered.Skip((page - 1) * pageSize).Take(pageSize)];
            }
            else
            {
                // page-size 0 = show all
                totalPages = 1;
                page       = 1;
                startIndex = 1;
                endIndex   = totalCount;
                pageItems  = [.. filtered];
            }

            AnsiConsole.WriteLine();
            if (config.Registry is not null)
                AnsiConsole.MarkupLine("[dim]Registry:[/] [bold]{0}[/]", Markup.Escape(config.Registry.Url));

            if (hasFilter)
            {
                var filterDesc = string.Join(", ",
                    new[] {
                        search is not null ? $"search: {search}" : null,
                        tag    is not null ? $"tag: {tag}"       : null,
                    }.Where(x => x is not null));
                AnsiConsole.MarkupLine("[dim]Filter:[/] [bold]{0}[/]", Markup.Escape(filterDesc));
            }

            if (recommended.Count > 0)
                AnsiConsole.MarkupLine("[dim]Recommended for this project:[/] [bold]{0}[/]", Markup.Escape(string.Join(", ", recommended)));

            AnsiConsole.MarkupLine("[dim]Showing {0}–{1} of {2} skill{3}[/]",
                startIndex, endIndex, totalCount, totalCount == 1 ? "" : "s");

            var hasTapSkills = skillSources.Values.Any(s =>
                s.StartsWith("tap:", StringComparison.OrdinalIgnoreCase));

            var table = new Table()
                .Border(TableBorder.Rounded)
                .AddColumn("[bold]Skill[/]")
                .AddColumn("[bold]Description[/]")
                .AddColumn("[bold]Version[/]")
                .AddColumn("[bold]Tags[/]");

            if (hasTapSkills)
                table.AddColumn("[bold]Source[/]");

            table.AddColumn("[bold]Status[/]");

            foreach (var skill in pageItems)
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

                if (hasTapSkills)
                {
                    var sourceLabel = GetSourceLabel(skill.Name, skillSources);
                    table.AddRow(
                        skill.Name,
                        skill.Description,
                        skill.Version,
                        string.Join(", ", skill.Tags),
                        sourceLabel,
                        status);
                }
                else
                {
                    table.AddRow(
                        skill.Name,
                        skill.Description,
                        skill.Version,
                        string.Join(", ", skill.Tags),
                        status);
                }
            }

            AnsiConsole.Write(table);

            if (pageSize > 0 && totalCount > pageSize)
                AnsiConsole.MarkupLine("[dim]Page {0} of {1} — use [bold]--page <n>[/] to navigate, [bold]--page-size 0[/] to show all.[/]",
                    page, totalPages);

            AnsiConsole.MarkupLine("[dim]Run [bold]lorex install[/] to choose skills interactively, [bold]lorex install --recommended[/] to install suggested skills, or [bold]lorex install <skill>[/] to install one directly.[/]");
            AnsiConsole.MarkupLine("[dim]Use [bold]--search <text>[/] or [bold]--tag <tag>[/] to filter, [bold]--page <n>[/] and [bold]--page-size <n>[/] to paginate.[/]");
            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine("[red]Error:[/] {0}", Markup.Escape(ex.Message));
            return 1;
        }
    }

    internal static string? ParseSearch(string[] args)   => ArgParser.FlagValue(args, SearchFlag);
    internal static string? ParseTag(string[] args)       => ArgParser.FlagValue(args, TagFlag);
    internal static int     ParsePage(string[] args)      => Math.Max(1, ArgParser.IntFlagValue(args, PageFlag, 1));
    internal static int     ParsePageSize(string[] args)  => ArgParser.IntFlagValue(args, PageSizeFlag, DefaultPageSize);

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

    private static bool IsTapSkill(string name, Dictionary<string, string> sources) =>
        sources.TryGetValue(name, out var src) &&
        src.StartsWith("tap:", StringComparison.OrdinalIgnoreCase);

    private static string GetSourceLabel(string name, Dictionary<string, string> sources)
    {
        if (!sources.TryGetValue(name, out var src))
            return "[dim]registry[/]";
        if (src.StartsWith("tap:", StringComparison.OrdinalIgnoreCase))
            return $"[blue]tap: {Markup.Escape(src["tap:".Length..])}[/]";
        if (src.StartsWith("url:", StringComparison.OrdinalIgnoreCase))
            return "[dim]url[/]";
        return "[dim]registry[/]";
    }
}
