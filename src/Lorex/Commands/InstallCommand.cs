using Lorex.Cli;
using Lorex.Core.Models;
using Lorex.Core.Services;
using Spectre.Console;

namespace Lorex.Commands;

/// <summary>Implements <c>lorex install [skill…]</c>: installs one or more skills from the registry into the current project.</summary>
public static class InstallCommand
{
    private const string AllFlag         = "--all";
    private const string RecommendedFlag = "--recommended";
    private const string GlobalFlag      = "--global";
    private const string SearchFlag      = "--search";
    private const string TagFlag         = "--tag";

    private static bool IsUrl(string arg) =>
        arg.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
        arg.StartsWith("http://",  StringComparison.OrdinalIgnoreCase) ||
        arg.StartsWith("git@",     StringComparison.OrdinalIgnoreCase);


    /// <summary>Runs the command. Returns 0 on success, 1 on failure.</summary>
    public static int Run(string[] args, string? cwd = null, string? homeRoot = null)
    {
        var isGlobal = WantsGlobal(args);

        if (isGlobal && OperatingSystem.IsWindows() && !WindowsDevModeHelper.IsSymlinkAvailable())
        {
            if (!WindowsDevModeHelper.EnsureSymlinkOrElevate())
                return 0; // elevated process was launched
        }

        var projectRoot = isGlobal
            ? GlobalRootLocator.ResolveForExistingGlobal(homeRoot)
            : ProjectRootLocator.ResolveForExistingProject(cwd ?? Directory.GetCurrentDirectory());

        try
        {
            var cfg = ServiceFactory.Skills.ReadConfig(projectRoot);
            if (cfg.Registry is null && cfg.Taps.Length == 0)
            {
                RegistryCommandSupport.PrintNoRegistryConfigured();
                return 1;
            }

            var installAll         = WantsAll(args);
            var installRecommended = WantsRecommended(args);
            var allRequestedSkills = ParseSkillNames(args);
            var search             = ParseSearch(args);
            var tag                = ParseTag(args);

            // Separate URL-style args (direct install) from named skills
            var urlInstalls    = allRequestedSkills.Where(IsUrl).ToList();
            var requestedSkills = allRequestedSkills.Where(s => !IsUrl(s)).ToList();

            if ((installAll || installRecommended) && (requestedSkills.Count > 0 || urlInstalls.Count > 0))
            {
                AnsiConsole.MarkupLine("[red]Usage:[/] lorex install [[bold]<skill>...[/]] [[bold]--all[/]] [[bold]--recommended[/]]");
                AnsiConsole.MarkupLine("[dim]Use explicit skill names or one install mode flag, not both.[/]");
                return 1;
            }

            // Handle direct URL installs
            if (urlInstalls.Count > 0)
            {
                var installedFromUrls = new List<string>();
                foreach (var url in urlInstalls)
                {
                    var installedName = "";
                    AnsiConsole.Status()
                        .Start($"Installing from URL…", ctx =>
                        {
                            ctx.Spinner(Spinner.Known.Dots);
                            installedName = ServiceFactory.Skills.InstallSkillFromUrl(projectRoot, url, ServiceFactory.Git);
                        });
                    AnsiConsole.MarkupLine($"[green]✓[/] Installed [bold]{Markup.Escape(installedName)}[/] [dim](copied from url)[/]");
                    installedFromUrls.Add(installedName);
                }

                if (requestedSkills.Count == 0 && !installAll && !installRecommended)
                {
                    // Re-project and exit — nothing else to install
                    AnsiConsole.Status().Start("Projecting skills…", ctx =>
                    {
                        ctx.Spinner(Spinner.Known.Dots);
                        var cfg = ServiceFactory.Skills.ReadConfig(projectRoot);
                        ServiceFactory.Adapters.Project(projectRoot, cfg);
                    });
                    return 0;
                }
            }

            if (installAll && installRecommended)
            {
                AnsiConsole.MarkupLine("[red]Usage:[/] lorex install [[bold]<skill>...[/]] [[bold]--all[/]] [[bold]--recommended[/]]");
                AnsiConsole.MarkupLine("[dim]Use [bold]--all[/] or [bold]--recommended[/], not both.[/]");
                return 1;
            }

            IReadOnlyList<SkillMetadata>? available = null;
            Dictionary<string, string>  skillSources = [];

            if (installAll)
            {
                (available, skillSources) = FetchAllSkills(cfg);
                requestedSkills = ServiceFactory.RegistrySkills.GetInstallableSkillNames(available, cfg);
            }
            else if (installRecommended)
            {
                (available, skillSources) = FetchAllSkills(cfg);
                requestedSkills = ServiceFactory.RegistrySkills.GetRecommendedSkillNames(projectRoot, available, cfg);

                if (requestedSkills.Count == 0)
                {
                    AnsiConsole.MarkupLine("[yellow]No recommended skills found for this project.[/]");
                    return 0;
                }
            }
            else if (requestedSkills.Count == 0)
            {
                (requestedSkills, skillSources) = PromptForSkills(projectRoot, cfg, search, tag);
            }
            else
            {
                // Named skills — discover sources so we can route tap vs registry
                (available, skillSources) = FetchAllSkills(cfg);
            }

            if (requestedSkills.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]Nothing selected.[/]");
                return 0;
            }

            // Split requested skills by source: registry vs tap
            var tapSkillNames      = requestedSkills.Where(s => skillSources.TryGetValue(s, out var src) && src.StartsWith("tap:", StringComparison.OrdinalIgnoreCase)).ToList();
            var registrySkillNames = requestedSkills.Except(tapSkillNames, StringComparer.OrdinalIgnoreCase).ToList();

            // Overwrite prompts (only for registry skills — tap skills use the same symlink logic)
            var (approvedRegistry, skippedSkills) = registrySkillNames.Count > 0
                ? SkillOverwritePrompts.ResolveApprovedOverrides(
                    projectRoot,
                    registrySkillNames,
                    skillName => $"Overwrite local skill [bold]{Markup.Escape(skillName)}[/] with the registry version?")
                : (registrySkillNames, []);

            // Build tap skill → (TapConfig, source path) map
            var tapSkillPaths = new Dictionary<string, (Core.Models.TapConfig Tap, string SourcePath)>(StringComparer.OrdinalIgnoreCase);
            foreach (var skillName in tapSkillNames)
            {
                var tapName = skillSources[skillName]["tap:".Length..];
                var tap = cfg.Taps.FirstOrDefault(t => string.Equals(t.Name, tapName, StringComparison.OrdinalIgnoreCase));
                if (tap is null) continue;
                var sourcePath = ServiceFactory.Taps.FindSkillPath(tap, skillName);
                if (sourcePath is not null) tapSkillPaths[skillName] = (tap, sourcePath);
            }

            if (approvedRegistry.Count == 0 && tapSkillPaths.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]Nothing selected.[/]");
                return 0;
            }

            AnsiConsole.Status()
                .Start("Installing skills...", ctx =>
                {
                    ctx.Spinner(Spinner.Known.Dots);

                    if (approvedRegistry.Count > 0)
                        ServiceFactory.Skills.InstallSkillsBatch(
                            projectRoot,
                            approvedRegistry,
                            shouldOverwrite: skillName => ServiceFactory.Skills.RequiresOverwriteApproval(projectRoot, skillName));

                    if (tapSkillPaths.Count > 0)
                        ServiceFactory.Skills.InstallTapSkillsBatch(
                            projectRoot,
                            tapSkillPaths,
                            shouldOverwrite: skillName => ServiceFactory.Skills.RequiresOverwriteApproval(projectRoot, skillName));

                    ctx.Status("Projecting skills into native agent locations...");
                    var config = ServiceFactory.Skills.ReadConfig(projectRoot);
                    ServiceFactory.Adapters.Project(projectRoot, config);
                });

            foreach (var skillName in approvedRegistry)
                AnsiConsole.MarkupLine($"[green]✓[/] Installed [bold]{Markup.Escape(skillName)}[/] [dim](symlinked)[/]");

            foreach (var skillName in tapSkillPaths.Keys)
                AnsiConsole.MarkupLine($"[green]✓[/] Installed [bold]{Markup.Escape(skillName)}[/] [dim](symlinked from tap)[/]");

            foreach (var skillName in skippedSkills)
                AnsiConsole.MarkupLine("[yellow]Skipped[/] [bold]{0}[/] [dim](kept existing local skill)[/]", skillName);

            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine("[red]Error:[/] {0}", Markup.Escape(ex.Message));
            if (OperatingSystem.IsWindows() && !WindowsDevModeHelper.IsSymlinkAvailable())
            {
                WindowsDevModeHelper.PrintDevModeGuidance();
                WindowsDevModeHelper.OfferToOpenSettings();
            }
            return 1;
        }
    }

    internal static bool WantsAll(string[] args) =>
        args.Any(a => string.Equals(a, AllFlag, StringComparison.OrdinalIgnoreCase));

    internal static bool WantsRecommended(string[] args) =>
        args.Any(a => string.Equals(a, RecommendedFlag, StringComparison.OrdinalIgnoreCase));

    internal static bool WantsGlobal(string[] args) =>
        args.Any(a => string.Equals(a, GlobalFlag, StringComparison.OrdinalIgnoreCase));

    internal static string? ParseSearch(string[] args) => ArgParser.FlagValue(args, SearchFlag);
    internal static string? ParseTag(string[] args)    => ArgParser.FlagValue(args, TagFlag);

    /// <summary>
    /// Returns the skill names from <paramref name="args"/>, excluding all flags (any arg starting with <c>--</c>)
    /// and the value arguments that follow value-carrying flags (<c>--search</c>, <c>--tag</c>).
    /// </summary>
    internal static List<string> ParseSkillNames(string[] args)
    {
        var result = new List<string>();
        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (string.IsNullOrWhiteSpace(arg)) continue;
            if (IsValueFlag(arg)) { i++; continue; }  // skip flag and its value
            if (arg.StartsWith("--", StringComparison.Ordinal)) continue;
            result.Add(arg);
        }
        return [.. result.Distinct(StringComparer.OrdinalIgnoreCase)];
    }

    private static bool IsValueFlag(string arg) =>
        string.Equals(arg, SearchFlag, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(arg, TagFlag,    StringComparison.OrdinalIgnoreCase);

    private static (List<string> SelectedSkills, Dictionary<string, string> Sources) PromptForSkills(
        string projectRoot, LorexConfig cfg,
        string? preSearch, string? preTag)
    {
        var (available, skillSources) = FetchAllSkills(cfg);
        if (available.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No skills found in the registry or configured taps.[/]");
            return ([], []);
        }

        var installableNames = ServiceFactory.RegistrySkills.GetInstallableSkillNames(available, cfg);
        var installableSet   = installableNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var choices = available
            .Where(skill => installableSet.Contains(skill.Name))
            .ToList();

        if (choices.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]All skills are already installed.[/]");
            AnsiConsole.MarkupLine("[dim]Run [bold]lorex sync[/] to update them, or [bold]lorex install <skill>[/] to reinstall one explicitly.[/]");
            return ([], []);
        }

        var recommended    = ServiceFactory.RegistrySkills.GetRecommendedSkillNames(projectRoot, available, cfg);
        var recommendedSet = recommended.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var selected = SkillPickerTui.Run(choices, recommendedSet, skillSources, preSearch, preTag);
        return (selected, skillSources);
    }

    private static (IReadOnlyList<SkillMetadata> Skills, Dictionary<string, string> Sources) FetchAllSkills(LorexConfig cfg)
    {
        IReadOnlyList<SkillMetadata> available = [];
        Dictionary<string, string>  sources   = [];
        AnsiConsole.Status()
            .Start("Fetching registry…", ctx =>
            {
                ctx.Spinner(Spinner.Known.Dots);
                (available, sources) = ServiceFactory.RegistrySkills.ListAllSkills(cfg);
            });

        return (available, sources);
    }
}
