using Lorex.Cli;
using Lorex.Core.Services;
using Spectre.Console;

namespace Lorex.Commands;

/// <summary>Implements <c>lorex publish [skill…]</c>: pushes locally authored skills to the registry, then replaces them with symlinks.</summary>
public static class PublishCommand
{
    /// <summary>Runs the command. Returns 0 on success, 1 if any publish failed.</summary>
    public static int Run(string[] args, string? cwd = null)
    {
        if (args.Any(a => a is "--help" or "-h"))
            return PrintHelp();

        var projectRoot = ProjectRootLocator.ResolveForExistingProject(cwd ?? Directory.GetCurrentDirectory());
        var builtIns = BuiltInSkillService.SkillNames().ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (!RegistryCommandSupport.TryRefreshConfiguredRegistry(projectRoot, out var config))
            return 1;

        List<string> toPublish;

        if (args.Length > 0)
        {
            // Names supplied on the command line
            toPublish = [];
            foreach (var arg in args.Where(a => !string.IsNullOrWhiteSpace(a)))
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
            // Interactive multi-select
            var local = ServiceFactory.Skills.LocalOnlySkills(projectRoot).ToArray();

            if (local.Length == 0)
            {
                AnsiConsole.MarkupLine("[red]No local skills to publish.[/] Ask your AI agent to create one, or run [bold]lorex create[/] to scaffold it.");
                return 1;
            }

            var metadata = SkillPickerTui.ReadInstalledMetadata(projectRoot, local);
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
                // Version bump check: warn if local version matches what's already in the registry
                var localSkillDir = ServiceFactory.Skills.SkillDir(projectRoot, skillName);
                var localEntryPath = Core.Services.SkillFileConvention.ResolveEntryPath(localSkillDir);
                if (localEntryPath is not null)
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

    private static int PrintHelp() => HelpPrinter.Print(
        "lorex publish [<skill>…]",
        "Push local skills to the registry. Running without arguments opens an interactive picker.\nDirect registries publish immediately; pull-request registries prepare a review branch.",
        options:
        [
            ("<skill>…",   "Skill names to publish"),
            ("-h, --help", "Show this help"),
        ],
        examples:
        [
            ("Interactive picker",       "lorex publish"),
            ("Publish a specific skill", "lorex publish my-skill"),
        ]);
}
