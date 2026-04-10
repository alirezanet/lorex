using Lorex.Cli;
using Lorex.Core.Services;
using Spectre.Console;

namespace Lorex.Commands;

/// <summary>Implements <c>lorex publish [skill…]</c>: pushes locally authored skills to the registry, then replaces them with symlinks.</summary>
public static class PublishCommand
{
    private const string GlobalFlag = "--global";

    /// <summary>Runs the command. Returns 0 on success, 1 if any publish failed.</summary>
    public static int Run(string[] args, string? cwd = null, string? homeRoot = null)
    {
        if (args.Any(a => a is "--help" or "-h"))
            return PrintHelp();

        var isGlobal = WantsGlobal(args);
        var projectRoot = isGlobal
            ? GlobalRootLocator.ResolveForExistingGlobal(homeRoot)
            : ProjectRootLocator.ResolveForExistingProject(cwd ?? Directory.GetCurrentDirectory());
        var builtIns = BuiltInSkillService.SkillNames().ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (!RegistryCommandSupport.TryRefreshConfiguredRegistry(projectRoot, out var config))
            return 1;

        List<string> toPublish;

        var skillArgs = args.Where(a => !string.IsNullOrWhiteSpace(a) &&
                                        !string.Equals(a, GlobalFlag,  StringComparison.OrdinalIgnoreCase) &&
                                        !string.Equals(a, "-g",        StringComparison.OrdinalIgnoreCase)).ToArray();

        if (skillArgs.Length > 0)
        {
            // Names supplied on the command line
            toPublish = [];
            foreach (var arg in skillArgs)
            {
                if (builtIns.Contains(arg))
                {
                    AnsiConsole.MarkupLine("[yellow]'{0}' is a built-in lorex skill — it cannot be published.[/]", arg);
                    AnsiConsole.MarkupLine("[dim]Run [bold]lorex create <new-name>[/] to scaffold a custom version.[/]");
                    return 1;
                }
                toPublish.Add(arg);
            }
        }
        else
        {
            // Interactive multi-select: local skills + registry-installed skills with uncommitted changes
            var publishable = ServiceFactory.Skills.PublishableSkills(projectRoot, ServiceFactory.Git).ToArray();

            if (publishable.Length == 0)
            {
                AnsiConsole.MarkupLine("[red]No skills with pending changes to publish.[/] Edit a skill or run [bold]lorex create[/] to scaffold a new one.");
                return 1;
            }

            var metadata = SkillPickerTui.ReadInstalledMetadata(projectRoot, publishable);
            toPublish = SkillPickerTui.Run(metadata, [], title: "Publish Skills");

            if (toPublish.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]Nothing selected.[/]");
                return 0;
            }
        }

        var failed = false;
        foreach (var skillName in toPublish)
        {
            try
            {
                // Version bump check: warn if local version matches what's already in the registry.
                // Skip for registry-installed (symlinked) skills — the local path and registry path
                // resolve to the same file, so the comparison would always be a false positive.
                var localSkillDir = ServiceFactory.Skills.SkillDir(projectRoot, skillName);
                var isRegistryLinked = new DirectoryInfo(localSkillDir).LinkTarget is not null;
                var localEntryPath = Core.Services.SkillFileConvention.ResolveEntryPath(localSkillDir);
                if (!isRegistryLinked && localEntryPath is not null)
                {
                    string? localVersion = null;
                    try { localVersion = Core.Serialization.SimpleYamlParser.ParseSkillMetadataFromMarkdown(File.ReadAllText(localEntryPath)).Version; }
                    catch { /* best-effort */ }

                    if (localVersion is not null)
                    {
                        var registrySkillPath = ServiceFactory.Registry.FindSkillPath(config.Registry!.Url, skillName, refresh: false);
                        if (registrySkillPath is not null)
                        {
                            var registryEntryPath = Core.Services.SkillFileConvention.ResolveEntryPath(registrySkillPath);
                            string? registryVersion = null;
                            if (registryEntryPath is not null)
                                try { registryVersion = Core.Serialization.SimpleYamlParser.ParseSkillMetadataFromMarkdown(File.ReadAllText(registryEntryPath)).Version; }
                                catch { /* best-effort */ }

                            if (registryVersion is not null &&
                                string.Equals(localVersion, registryVersion, StringComparison.OrdinalIgnoreCase))
                            {
                                AnsiConsole.MarkupLine(
                                    "[yellow]⚠[/]  Version [bold]{0}[/] of '[bold]{1}[/]' matches the current registry version.",
                                    Markup.Escape(localVersion), Markup.Escape(skillName));
                                AnsiConsole.MarkupLine("[dim]Bump the version in SKILL.md before publishing.[/]");

                                var proceed = AnsiConsole.Prompt(
                                    new SelectionPrompt<string>()
                                        .Title("Continue anyway?")
                                        .AddChoices("No, abort", "Yes, publish anyway"));

                                if (string.Equals(proceed, "No, abort", StringComparison.Ordinal))
                                {
                                    AnsiConsole.MarkupLine("[dim]Publish aborted.[/]");
                                    failed = true;
                                    continue;
                                }
                            }
                        }
                    }
                }

                Core.Models.PublishResult result = null!;
                AnsiConsole.Status()
                    .Start($"Publishing [bold]{skillName}[/]...", ctx =>
                    {
                        ctx.Spinner(Spinner.Known.Dots);
                        result = ServiceFactory.Skills.PublishSkill(projectRoot, skillName, ServiceFactory.Git);
                    });

                if (string.Equals(result.PublishMode, Core.Models.RegistryPublishModes.Direct, StringComparison.OrdinalIgnoreCase))
                {
                    AnsiConsole.MarkupLine("[green]✓[/] Published [bold]{0}[/] directly to the registry.", skillName);
                }
                else
                {
                    AnsiConsole.MarkupLine(
                        "[green]✓[/] Prepared [bold]{0}[/] for review on branch [bold]{1}[/] targeting [bold]{2}[/].",
                        skillName,
                        Markup.Escape(result.BranchName ?? string.Empty),
                        Markup.Escape(result.BaseBranch ?? string.Empty));

                    if (!string.IsNullOrWhiteSpace(result.PullRequestUrl))
                        AnsiConsole.MarkupLine("[dim]Open a PR:[/] {0}", Markup.Escape(result.PullRequestUrl));
                }
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine("[red]✗[/] {0}: {1}", skillName, Markup.Escape(ex.Message));
                failed = true;
            }
        }

        return failed ? 1 : 0;
    }

    private static bool WantsGlobal(string[] args) =>
        args.Any(a => string.Equals(a, GlobalFlag, StringComparison.OrdinalIgnoreCase) ||
                      string.Equals(a, "-g",       StringComparison.OrdinalIgnoreCase));

    private static int PrintHelp() => HelpPrinter.Print(
        "lorex publish [<skill>…] [-g]",
        "Push local skills to the registry. Running without arguments opens an interactive picker.\nDirect registries publish immediately; pull-request registries prepare a review branch.",
        options:
        [
            ("<skill>…",      "Skill names to publish"),
            ("-g, --global",  "Operate on the global lorex root (~/.lorex)"),
            ("-h, --help",    "Show this help"),
        ],
        examples:
        [
            ("Interactive picker",              "lorex publish"),
            ("Publish a specific skill",        "lorex publish my-skill"),
            ("Publish from global lorex root",  "lorex publish -g"),
        ]);
}
