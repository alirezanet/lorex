using Lorex.Cli;
using Lorex.Core.Adapters;
using Lorex.Core.Models;
using Lorex.Core.Services;
using Spectre.Console;

namespace Lorex.Commands;

/// <summary>Implements <c>lorex init</c>: configures a skill registry, selects adapters, and projects built-in skills into native agent locations.</summary>
public static class InitCommand
{
    private const string AddNewRegistry = "+ Enter a new registry URL";
    private const string UseLocalOnly = "- Keep this repo local-only";
    private const string GlobalFlag = "--global";

    /// <summary>Runs the command. Returns 0 on success, 1 on failure.</summary>
    /// <remarks>
    /// Non-interactive usage: <c>lorex init &lt;url&gt; [--adapters copilot,codex]</c><br/>
    /// Local-only (no registry): <c>lorex init --local [--adapters copilot,codex]</c><br/>
    /// Global (user-level) usage: <c>lorex init --global [&lt;url&gt;] [--adapters a,b]</c><br/>
    /// Interactive usage:     <c>lorex init</c> (guided setup for registry and adapters)
    /// </remarks>
    public static int Run(string[] args, string? cwd = null, string? homeRoot = null)
    {
        var isGlobal = args.Any(a => string.Equals(a, GlobalFlag, StringComparison.OrdinalIgnoreCase));

        if (isGlobal && OperatingSystem.IsWindows() && !WindowsDevModeHelper.IsSymlinkAvailable())
        {
            if (!WindowsDevModeHelper.EnsureSymlinkOrElevate())
                return 0; // elevated process was launched
        }

        var projectRoot = isGlobal
            ? (homeRoot ?? GlobalRootLocator.GetGlobalRoot())
            : ProjectRootLocator.ResolveForInit(cwd ?? Directory.GetCurrentDirectory());

        // ── Parse flags ───────────────────────────────────────────────────────
        // Accept: lorex init <url> [--adapters a,b,c] [--global]
        string? urlArg = null;
        string[]? adapterArg = null;
        bool localOnly = false;

        for (int i = 0; i < args.Length; i++)
        {
            if ((args[i] == "--adapters" || args[i] == "-a") && i + 1 < args.Length)
                adapterArg = args[++i].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            else if (args[i] == "--local")
                localOnly = true;
            else if (!args[i].StartsWith('-'))
                urlArg = args[i];
        }

        bool nonInteractive = urlArg is not null || localOnly;

        if (isGlobal)
            AnsiConsole.MarkupLine("[bold]Initialising lorex globally at:[/] [dim]{0}[/]", Path.Combine(projectRoot, ".lorex"));
        else
            AnsiConsole.MarkupLine("[bold]Initialising lorex for project:[/] [dim]{0}[/]", projectRoot);
        AnsiConsole.WriteLine();

        // ── Registry URL ──────────────────────────────────────────────────────
        string? registryUrl;
        RegistryPolicy? registryPolicy = null;

        if (localOnly)
        {
            registryUrl = null;
        }
        else if (urlArg is not null)
        {
            registryUrl = urlArg.Trim();
            string? probeError = null;
            AnsiConsole.Status().Start("Verifying registry…", ctx =>
            {
                ctx.Spinner(Spinner.Known.Dots);
                probeError = ServiceFactory.Git.ProbeRemote(registryUrl);
            });

            if (probeError is not null)
            {
                AnsiConsole.MarkupLine("[red]Cannot reach registry:[/] {0}", Markup.Escape(probeError));
                return 1;
            }

            AnsiConsole.Status().Start("Loading registry policy...", ctx =>
            {
                ctx.Spinner(Spinner.Known.Dots);
                registryPolicy = ServiceFactory.Registry.ReadRegistryPolicy(registryUrl, forceRefresh: true);
            });

            if (registryPolicy is null)
            {
                if (Console.IsInputRedirected || Console.IsOutputRedirected)
                {
                    throw new InvalidOperationException(
                        $"Registry '{registryUrl}' is missing {RegistryService.RegistryManifestFileName}. Run `lorex init` interactively to initialize it.");
                }

                registryPolicy = ResolveRegistryPolicyInteractive(registryUrl);
            }
        }
        else
        {
            registryUrl = PromptForRegistryInteractive(homeRoot);
            if (registryUrl is not null)
                registryPolicy = ResolveRegistryPolicyInteractive(registryUrl);
        }

        // ── Adapter selection ─────────────────────────────────────────────────
        var detected = AdapterService.KnownAdapters.Values
            .Where(a => a.DetectExisting(projectRoot))
            .Select(a => a.Name)
            .ToList();

        var adapterChoices = AdapterService.KnownAdapters.Keys.ToList();

        List<string> selectedAdapters;
        if (adapterArg is not null)
        {
            // --adapters was explicitly provided — skip prompt
            selectedAdapters = [.. adapterArg];
            var invalid = selectedAdapters.Except(adapterChoices).ToList();
            if (invalid.Count > 0)
            {
                AnsiConsole.MarkupLine("[red]Unknown adapter(s):[/] {0}", Markup.Escape(string.Join(", ", invalid)));
                AnsiConsole.MarkupLine("[dim]Valid adapters: {0}[/]", string.Join(", ", adapterChoices));
                return 1;
            }
        }
        else
        {
            selectedAdapters = PromptForAdaptersInteractive(projectRoot, detected, adapterChoices);
        }

        if (selectedAdapters.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No adapters selected — lorex will not project skills into any agent-specific location.[/]");
        }

        // ── Write lorex.json ──────────────────────────────────────────────────
        // Preserve installed skills, versions, sources, and taps from any existing config so that
        // re-running lorex init (e.g. to change adapters) doesn't orphan previously installed skills
        // and so that lorex sync can restore symlinks after a fresh clone.
        LorexConfig? prior = null;
        try { prior = ServiceFactory.Skills.ReadConfig(projectRoot); } catch { }

        var config = new LorexConfig
        {
            Registry = registryUrl is null
                ? null
                : new RegistryConfig
                {
                    Url = registryUrl,
                    Policy = registryPolicy ?? throw new InvalidOperationException("Registry policy is missing."),
                },
            Adapters = [.. selectedAdapters],
            InstalledSkills        = prior?.InstalledSkills        ?? [],
            InstalledSkillVersions = prior?.InstalledSkillVersions ?? new Dictionary<string, string>(),
            InstalledSkillSources  = prior?.InstalledSkillSources  ?? new Dictionary<string, string>(),
            Taps                   = prior?.Taps                   ?? [],
        };

        ServiceFactory.Skills.WriteConfig(projectRoot, config);
        if (registryUrl is not null)
            ServiceFactory.Skills.SaveGlobalRegistry(registryUrl, homeRoot);

        // ── Install built-in skills (bundled in the binary) ───────────────────
        var builtIns = BuiltInSkillService.InstallAll(projectRoot, config);
        var discoveredSkills = ServiceFactory.Skills.DiscoverInstalledSkillNames(projectRoot);
        if (builtIns.Count > 0 || discoveredSkills.Count > 0)
        {
            config = config with
            {
                InstalledSkills = [.. config.InstalledSkills
                    .Concat(builtIns)
                    .Concat(discoveredSkills)
                    .Distinct(StringComparer.OrdinalIgnoreCase)]
            };
            ServiceFactory.Skills.WriteConfig(projectRoot, config);
        }

        ServiceFactory.Skills.TrackInstalledVersions(projectRoot, config.InstalledSkills);
        config = ServiceFactory.Skills.ReadConfig(projectRoot);

        // ── Project built-in skills into native agent surfaces ────────────────
        ServiceFactory.Adapters.Project(projectRoot, config);

        if (!nonInteractive && registryUrl is not null)
        {
            var recommendedToInstall = FindRecommendedSkills(projectRoot, config);
            if (recommendedToInstall.Count > 0)
            {
                var installRecommended = AnsiConsole.Prompt(
                    new SelectionPrompt<string>()
                        .Title($"[bold]Install recommended registry skills for this project?[/] [dim]({string.Join(", ", recommendedToInstall)})[/]")
                        .AddChoices("Yes", "No"));

                if (string.Equals(installRecommended, "Yes", StringComparison.Ordinal))
                {
                    var (approvedSkills, skippedSkills) = SkillOverwritePrompts.ResolveApprovedOverrides(
                        projectRoot,
                        recommendedToInstall,
                        skillName => $"Overwrite local skill [bold]{Markup.Escape(skillName)}[/] with the registry version?");

                    if (approvedSkills.Count == 0)
                        goto FinishInit;

                    AnsiConsole.Status()
                        .Start("Installing recommended skills...", ctx =>
                        {
                            foreach (var skillName in approvedSkills)
                            {
                                ctx.Spinner(Spinner.Known.Dots);
                                ctx.Status($"Installing [bold]{skillName}[/]...");
                                ServiceFactory.Skills.InstallSkill(
                                    projectRoot,
                                    skillName,
                                    overwriteLocalSkill: ServiceFactory.Skills.RequiresOverwriteApproval(projectRoot, skillName));
                            }

                            ctx.Status("Projecting skills into native agent locations...");
                            var updatedConfig = ServiceFactory.Skills.ReadConfig(projectRoot);
                            ServiceFactory.Adapters.Project(projectRoot, updatedConfig);
                            config = updatedConfig;
                        });

                    foreach (var skillName in skippedSkills)
                        AnsiConsole.MarkupLine("[yellow]Skipped[/] [bold]{0}[/] [dim](kept existing local skill)[/]", skillName);
                }
            }
        }

        // ── Recommended taps from the registry ───────────────────────────────
        if (!Console.IsInputRedirected && registryPolicy is { RecommendedTaps: { Length: > 0 } recommendedTaps })
        {
            config = ServiceFactory.Skills.ReadConfig(projectRoot);
            var configuredUrls = new HashSet<string>(config.Taps.Select(t => t.Url), StringComparer.OrdinalIgnoreCase);
            var newTaps = recommendedTaps.Where(t => !configuredUrls.Contains(t.Url)).ToList();

            if (newTaps.Count > 0)
            {
                AnsiConsole.WriteLine();
                var tapPrompt = new MultiSelectionPrompt<TapConfig>()
                    .Title("[bold]This registry recommends tap sources. Which do you want to add?[/]")
                    .InstructionsText("[dim](Space to toggle, Enter to confirm, Enter with none = skip)[/]")
                    .NotRequired()
                    .UseConverter(t =>
                        $"[bold]{Markup.Escape(t.Name)}[/] [dim]— {Markup.Escape(t.Url)}" +
                        (t.Root is not null ? $" ({Markup.Escape(t.Root)})" : "") + "[/]")
                    .AddChoices(newTaps);

                // Pre-select all recommended taps — user deselects what they don't want
                foreach (var tap in newTaps)
                    tapPrompt.Select(tap);

                var selectedTaps = AnsiConsole.Prompt(tapPrompt);

                foreach (var tap in selectedTaps)
                {
                    try
                    {
                        IReadOnlyList<SkillMetadata> found = [];
                        AnsiConsole.Status().Start($"Adding tap {Markup.Escape(tap.Name)}…", ctx =>
                        {
                            ctx.Spinner(Spinner.Known.Dots);
                            found = ServiceFactory.Taps.Add(projectRoot, ServiceFactory.Skills, tap.Url, tap.Name, tap.Root);
                        });
                        AnsiConsole.MarkupLine(
                            "[green]✓[/] Tap [bold]{0}[/] added [dim]({1} skill{2} found)[/]",
                            Markup.Escape(tap.Name), found.Count, found.Count == 1 ? "" : "s");
                    }
                    catch (Exception ex)
                    {
                        AnsiConsole.MarkupLine(
                            "[yellow]Warning:[/] Could not add tap [bold]{0}[/]: {1}",
                            Markup.Escape(tap.Name), Markup.Escape(ex.Message));
                    }
                }

                config = ServiceFactory.Skills.ReadConfig(projectRoot);
            }
        }

FinishInit:
        var remainingRegistrySkills = registryUrl is not null
            ? FindRemainingRegistrySkills(projectRoot, config)
            : [];
        var remainingRecommendedSkills = registryUrl is not null
            ? FindRecommendedSkills(projectRoot, config)
            : [];

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[green]✓[/] lorex initialised. Native agent projections updated:");
        foreach (var name in selectedAdapters)
        {
            foreach (var target in ServiceFactory.Adapters.DescribeTargets(projectRoot, name))
                AnsiConsole.MarkupLine("  [dim]{0}[/]", target);
        }
        if (builtIns.Count > 0)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[dim]Built-in skills installed:[/]");
            foreach (var s in builtIns)
                AnsiConsole.MarkupLine("  [dim]•[/] {0}", s);
        }
        var missingSkillCount = config.InstalledSkills
            .Count(s => !Directory.Exists(ServiceFactory.Skills.SkillDir(projectRoot, s)));

        if (registryUrl is null)
            AnsiConsole.MarkupLine("[dim]Running in local-only mode. Ask your AI agent to create a skill for this project, or run [bold]lorex create[/] to scaffold one.[/]");
        else if (missingSkillCount > 0)
            AnsiConsole.MarkupLine("[dim][bold]{0}[/] installed skill(s) need restoring (registry/tap symlinks are not committed). Run [bold]lorex sync[/] to recreate them.[/]", missingSkillCount);
        else if (remainingRecommendedSkills.Count > 0)
            AnsiConsole.MarkupLine("[dim]This registry still has [bold]{0}[/] recommended skill(s) not installed in this project. Run [bold]lorex install --recommended[/] to add them, [bold]lorex list[/] to browse everything else, and [bold]lorex sync[/] later to refresh installed shared skills.[/]", remainingRecommendedSkills.Count);
        else if (remainingRegistrySkills.Count > 0)
            AnsiConsole.MarkupLine("[dim]This registry has [bold]{0}[/] additional shared skill(s) available. Run [bold]lorex list[/] to browse them, and [bold]lorex sync[/] later to refresh installed shared skills.[/]", remainingRegistrySkills.Count);
        else
            AnsiConsole.MarkupLine("[dim]Run [bold]lorex sync[/] later to refresh installed shared skills.[/]");
        return 0;
    }

    internal static List<string> GetDefaultAdapters(IEnumerable<string> detectedAdapters)
    {
        var detected = detectedAdapters
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return detected.Count > 0 ? detected : ["copilot", "codex"];
    }

    private static string? PromptForRegistryInteractive(string? homeRoot = null)
    {
        while (true)
        {
            var knownRegistries = ServiceFactory.Skills.ReadGlobalConfig(homeRoot).Registries;

            var selected = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[bold]Where should Lorex get shared skills from?[/]")
                    .PageSize(10)
                    .UseConverter(RenderRegistryChoice)
                    .AddChoices(knownRegistries.Length > 0
                        ? [.. knownRegistries, AddNewRegistry, UseLocalOnly]
                        : [AddNewRegistry, UseLocalOnly]));

            if (string.Equals(selected, UseLocalOnly, StringComparison.Ordinal))
                return null;

            var candidateUrl = string.Equals(selected, AddNewRegistry, StringComparison.Ordinal)
                ? PromptForRegistryUrlInput()
                : selected;

            if (string.IsNullOrWhiteSpace(candidateUrl))
                return null;

            var trimmedUrl = candidateUrl.Trim();
            string? probeError = null;
            AnsiConsole.Status().Start("Verifying registry…", ctx =>
            {
                ctx.Spinner(Spinner.Known.Dots);
                probeError = ServiceFactory.Git.ProbeRemote(trimmedUrl);
            });

            if (probeError is null)
                return trimmedUrl;

            AnsiConsole.MarkupLine("[red]Cannot reach registry:[/] {0}", Markup.Escape(probeError));

            var retryChoice = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[bold]What do you want to do next?[/]")
                    .AddChoices("Try another registry", "Use local-only mode"));

            if (string.Equals(retryChoice, "Use local-only mode", StringComparison.Ordinal))
                return null;

            AnsiConsole.WriteLine();
        }
    }

    private static string? PromptForRegistryUrlInput()
    {
        return AnsiConsole.Prompt(
            new TextPrompt<string>("[bold]Registry URL[/] [dim](git repo — SSH or HTTPS; press Enter for local-only):[/]")
                .AllowEmpty());
    }

    private static RegistryPolicy ResolveRegistryPolicyInteractive(string registryUrl)
    {
        RegistryPolicy? existingPolicy = null;
        AnsiConsole.Status().Start("Loading registry policy...", ctx =>
        {
            ctx.Spinner(Spinner.Known.Dots);
            existingPolicy = ServiceFactory.Registry.ReadRegistryPolicy(registryUrl, forceRefresh: true);
        });

        if (existingPolicy is not null)
            return existingPolicy;

        AnsiConsole.MarkupLine(
            "[yellow]This registry does not define a lorex policy yet.[/] Lorex can initialize [bold]{0}[/] in the registry root now.",
            RegistryService.RegistryManifestFileName);

        var publishMode = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[bold]How should contributors publish skills to this registry?[/]")
                .UseConverter(RegistryPolicyPrompts.RenderChoice)
                .AddChoices(RegistryPolicyPrompts.OrderedChoices(RegistryPublishModes.PullRequest)));

        var baseBranch = AnsiConsole.Prompt(
            new TextPrompt<string>("[bold]Initial branch name for this registry[/]")
                .DefaultValue("main")
                .AllowEmpty());

        if (string.IsNullOrWhiteSpace(baseBranch))
            baseBranch = "main";

        var policy = RegistryPolicyPrompts.BuildPolicy(
            publishMode,
            baseBranch,
            new RegistryPolicy().PrBranchPrefix);

        AnsiConsole.Status()
            .Start("Initializing registry policy...", ctx =>
            {
                ctx.Spinner(Spinner.Known.Dots);
                policy = ServiceFactory.Registry.InitializeRegistryPolicy(registryUrl, policy);
            });

        AnsiConsole.MarkupLine(
            "[green]✓[/] Initialized registry policy in [bold]{0}[/] with publish mode [bold]{1}[/].",
            RegistryService.RegistryManifestFileName,
            Markup.Escape(policy.PublishMode));

        return policy;
    }

    private static List<string> PromptForAdaptersInteractive(
        string projectRoot,
        IReadOnlyCollection<string> detectedAdapters,
        IReadOnlyCollection<string> adapterChoices)
    {
        AnsiConsole.MarkupLine("[dim]Lorex will keep the selected native agent paths in sync with [[.lorex/skills/]].[/]");

        var prompt = new MultiSelectionPrompt<string>()
            .Title("[bold]Which agent integrations should Lorex maintain?[/]")
            .InstructionsText("[dim](Space to toggle, Enter to confirm)[/]")
            .PageSize(10)
            .UseConverter(name => RenderAdapterChoice(projectRoot, name, detectedAdapters.Contains(name, StringComparer.OrdinalIgnoreCase)))
            .AddChoices(adapterChoices);

        foreach (var adapter in GetDefaultAdapters(detectedAdapters))
            prompt.Select(adapter);

        return AnsiConsole.Prompt(prompt);
    }

    private static string RenderRegistryChoice(string choice) => choice switch
    {
        AddNewRegistry => "[bold]Enter a new registry URL[/] [dim]- connect this repo to another git-based skill registry[/]",
        UseLocalOnly => "[bold]Keep this repo local-only[/] [dim]- create and manage project-only skills without a shared registry[/]",
        _ => $"[bold]Use saved registry[/] [dim]- {Markup.Escape(choice)}[/]",
    };

    private static string RenderAdapterChoice(string projectRoot, string adapterName, bool detected)
    {
        var targets = ServiceFactory.Adapters.DescribeTargets(projectRoot, adapterName);
        var targetText = string.Join(", ", targets.Select(Markup.Escape));
        var detectionText = detected ? "[green](detected)[/]" : "[dim](not detected)[/]";
        return $"[bold]{Markup.Escape(adapterName)}[/] {detectionText} [dim]- {targetText}[/]";
    }

    private static List<string> FindRecommendedSkills(string projectRoot, LorexConfig config)
    {
        if (config.Registry is null)
            return [];

        IReadOnlyList<SkillMetadata> available = [];
        AnsiConsole.Status()
            .Start("Checking for recommended skills...", ctx =>
            {
                ctx.Spinner(Spinner.Known.Dots);
                available = ServiceFactory.Registry.ListAvailableSkills(config.Registry.Url, refresh: false);
            });

        return ServiceFactory.RegistrySkills.GetRecommendedSkillNames(projectRoot, available, config);
    }

    private static List<string> FindRemainingRegistrySkills(string projectRoot, LorexConfig config)
    {
        if (config.Registry is null)
            return [];

        IReadOnlyList<SkillMetadata> available = [];
        AnsiConsole.Status()
            .Start("Checking registry skills...", ctx =>
            {
                ctx.Spinner(Spinner.Known.Dots);
                available = ServiceFactory.Registry.ListAvailableSkills(config.Registry.Url, refresh: false);
            });

        return ServiceFactory.RegistrySkills.GetInstallableSkillNames(available, config);
    }
}
