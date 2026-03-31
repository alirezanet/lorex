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

    /// <summary>Runs the command. Returns 0 on success, 1 on failure.</summary>
    /// <remarks>
    /// Non-interactive usage: <c>lorex init &lt;url&gt; [--adapters copilot,codex]</c><br/>
    /// Local-only (no registry): <c>lorex init --local [--adapters copilot,codex]</c><br/>
    /// Interactive usage:     <c>lorex init</c> (guided setup for registry and adapters)
    /// </remarks>
    public static int Run(string[] args)
    {
        var projectRoot = ProjectRootLocator.ResolveForInit(Directory.GetCurrentDirectory());

        // ── Parse flags ───────────────────────────────────────────────────────
        // Accept: lorex init <url> [--adapters a,b,c]
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

        AnsiConsole.MarkupLine("[bold]Initialising lorex for project:[/] [dim]{0}[/]", projectRoot);
        AnsiConsole.WriteLine();

        // ── Registry URL ──────────────────────────────────────────────────────
        string? registryUrl;

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
        }
        else
        {
            registryUrl = PromptForRegistryInteractive();
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
        var config = new LorexConfig
        {
            Registry = registryUrl,
            Adapters = [.. selectedAdapters],
            InstalledSkills = [],
        };

        ServiceFactory.Skills.WriteConfig(projectRoot, config);
        if (registryUrl is not null)
            ServiceFactory.Skills.SaveGlobalRegistry(registryUrl);

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

        // ── Project built-in skills into native agent surfaces ────────────────
        ServiceFactory.Adapters.Project(projectRoot, config);

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
        if (registryUrl is null)
            AnsiConsole.MarkupLine("[dim]Running in local-only mode. Ask your AI agent to create a skill for this project, or run [bold]lorex create[/] to scaffold one.[/]");
        else
            AnsiConsole.MarkupLine("[dim]Run [/][bold]lorex install <skill>[/][dim] to add your first skill.[/]");
        return 0;
    }

    internal static List<string> GetDefaultAdapters(IEnumerable<string> detectedAdapters)
    {
        var detected = detectedAdapters
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return detected.Count > 0 ? detected : ["copilot", "codex", "claude"];
    }

    private static string? PromptForRegistryInteractive()
    {
        while (true)
        {
            var knownRegistries = ServiceFactory.Skills.ReadGlobalConfig().Registries;

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
}

