using Lorex.Core.Adapters;
using Lorex.Core.Models;
using Lorex.Core.Services;
using Spectre.Console;

namespace Lorex.Commands;

/// <summary>Implements <c>lorex init</c>: configures a skill registry, selects adapters, and projects built-in skills into native agent locations.</summary>
public static class InitCommand
{
    /// <summary>Runs the command. Returns 0 on success, 1 on failure.</summary>
    /// <remarks>
    /// Non-interactive usage: <c>lorex init &lt;url&gt; [--adapters copilot,codex]</c><br/>
    /// Local-only (no registry): <c>lorex init --local [--adapters copilot,codex]</c><br/>
    /// Interactive usage:     <c>lorex init</c> (prompts for URL and adapters; Enter to skip registry)
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

        AnsiConsole.MarkupLine("[bold]Initialising lorex in:[/] [dim]{0}[/]", projectRoot);
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
            // Interactive — allow skipping with empty input
            const string AddNew = "+ Add new registry…";
            const string SkipOption = "- Skip (local-only mode, no registry)";
            while (true)
            {
                var knownRegistries = ServiceFactory.Skills.ReadGlobalConfig().Registries;

                string input;
                if (knownRegistries.Length > 0)
                {
                    var choices = knownRegistries.Append(AddNew).Append(SkipOption).ToList();
                    var selected = AnsiConsole.Prompt(
                        new SelectionPrompt<string>()
                            .Title("[bold]Skill registry[/] [dim](select, add new, or skip):[/]")
                            .AddChoices(choices));

                    input = selected == AddNew
                        ? AnsiConsole.Ask<string>("[bold]Registry URL[/] [dim](git repo — SSH or HTTPS):[/]")
                        : selected;
                }
                else
                {
                    input = AnsiConsole.Prompt(
                        new TextPrompt<string>("[bold]Skill registry URL[/] [dim](git repo — SSH or HTTPS, or press Enter to skip):[/]")
                            .AllowEmpty());
                }

                if (input == SkipOption || string.IsNullOrWhiteSpace(input))
                {
                    registryUrl = null;
                    break;
                }

                string? probeError = null;
                AnsiConsole.Status().Start("Verifying registry…", ctx =>
                {
                    ctx.Spinner(Spinner.Known.Dots);
                    probeError = ServiceFactory.Git.ProbeRemote(input.Trim());
                });

                if (probeError is null)
                {
                    registryUrl = input.Trim();
                    break;
                }

                AnsiConsole.MarkupLine("[red]Cannot reach registry:[/] {0}", Markup.Escape(probeError));
                AnsiConsole.MarkupLine("[dim]Fix the URL, check your network, or press Enter to skip.[/]");
                AnsiConsole.WriteLine();
            }
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
            // Always prompt — pre-select detected adapters (or defaults) as a helpful starting point
            var defaultAdapters = detected.Count > 0 ? detected : ["copilot", "codex"];

            var prompt = new MultiSelectionPrompt<string>()
                .Title("[bold]Which AI agent integrations should lorex maintain?[/]")
                .AddChoices(adapterChoices);

            foreach (var a in defaultAdapters)
                prompt.Select(a);

            selectedAdapters = AnsiConsole.Prompt(prompt);
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
}

