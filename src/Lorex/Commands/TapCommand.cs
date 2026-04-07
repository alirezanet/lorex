using Lorex.Cli;
using Lorex.Core.Services;
using Spectre.Console;

namespace Lorex.Commands;

/// <summary>Implements <c>lorex tap &lt;subcommand&gt;</c>: add, remove, list, and sync read-only skill sources (taps).</summary>
public static class TapCommand
{
    /// <summary>Runs the command. Returns 0 on success, 1 on failure.</summary>
    public static int Run(string[] args, string? cwd = null, string? homeRoot = null)
    {
        var sub = args.Length > 0 ? args[0] : "--help";
        var rest = args.Length > 1 ? args[1..] : [];

        return sub switch
        {
            "add"             => Add(rest, cwd, homeRoot),
            "remove"          => Remove(rest, cwd, homeRoot),
            "list"            => List(rest, cwd, homeRoot),
            "sync"            => Sync(rest, cwd, homeRoot),
            "--help" or "-h"  => PrintHelp(),
            _                 => UnknownSubcommand(sub),
        };
    }

    // ── lorex tap add <url> [--name <name>] [--root <path>] [--global] ───────

    private static int Add(string[] args, string? cwd, string? homeRoot)
    {
        var isGlobal = WantsGlobal(args);
        var url = args.FirstOrDefault(a => !a.StartsWith("-", StringComparison.Ordinal));
        if (string.IsNullOrWhiteSpace(url))
        {
            AnsiConsole.MarkupLine("[red]Usage:[/] lorex tap add [bold]<url>[/] [[bold]--name <name>[/]] [[bold]--root <path>[/]] [[bold]--global[/]]");
            return 1;
        }

        var name = ArgParser.FlagValue(args, "--name") ?? DeriveNameFromUrl(url);
        var root = ArgParser.FlagValue(args, "--root");

        var projectRoot = isGlobal
            ? GlobalRootLocator.ResolveForExistingGlobal(homeRoot)
            : ProjectRootLocator.ResolveForExistingProject(cwd ?? Directory.GetCurrentDirectory());

        try
        {
            IReadOnlyList<Lorex.Core.Models.SkillMetadata> skills = [];
            AnsiConsole.Status()
                .Start($"Cloning tap '{Markup.Escape(name)}'…", ctx =>
                {
                    ctx.Spinner(Spinner.Known.Dots);
                    skills = ServiceFactory.Taps.Add(projectRoot, ServiceFactory.Skills, url, name, root);
                });

            AnsiConsole.MarkupLine(
                $"[green]✓[/] Tap [bold]{Markup.Escape(name)}[/] added — " +
                $"[bold]{skills.Count}[/] skill{(skills.Count == 1 ? "" : "s")} found.");
            AnsiConsole.MarkupLine("[dim]Run [bold]lorex list[/] to browse, or [bold]lorex install[/] to pick and install skills.[/]");
            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine("[red]Error:[/] {0}", Markup.Escape(ex.Message));
            return 1;
        }
    }

    // ── lorex tap remove <name> [--global] ───────────────────────────────────

    private static int Remove(string[] args, string? cwd, string? homeRoot)
    {
        var isGlobal = WantsGlobal(args);
        var name = args.FirstOrDefault(a => !a.StartsWith("-", StringComparison.Ordinal));
        if (string.IsNullOrWhiteSpace(name))
        {
            AnsiConsole.MarkupLine("[red]Usage:[/] lorex tap remove [bold]<name>[/] [[bold]--global[/]]");
            return 1;
        }

        var projectRoot = isGlobal
            ? GlobalRootLocator.ResolveForExistingGlobal(homeRoot)
            : ProjectRootLocator.ResolveForExistingProject(cwd ?? Directory.GetCurrentDirectory());

        try
        {
            ServiceFactory.Taps.Remove(projectRoot, ServiceFactory.Skills, name);
            AnsiConsole.MarkupLine($"[green]✓[/] Tap [bold]{Markup.Escape(name)}[/] removed.");
            AnsiConsole.MarkupLine("[dim](The local cache was kept — other projects using this tap are unaffected.)[/]");
            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine("[red]Error:[/] {0}", Markup.Escape(ex.Message));
            return 1;
        }
    }

    // ── lorex tap list [--global] ─────────────────────────────────────────────

    private static int List(string[] args, string? cwd, string? homeRoot)
    {
        var isGlobal = WantsGlobal(args);
        var projectRoot = isGlobal
            ? GlobalRootLocator.ResolveForExistingGlobal(homeRoot)
            : ProjectRootLocator.ResolveForExistingProject(cwd ?? Directory.GetCurrentDirectory());

        try
        {
            var config = ServiceFactory.Skills.ReadConfig(projectRoot);

            if (config.Taps.Length == 0)
            {
                AnsiConsole.MarkupLine("[yellow]No taps configured.[/]");
                AnsiConsole.MarkupLine("[dim]Add one: [bold]lorex tap add <url>[/][/]");
                return 0;
            }

            var taps = ServiceFactory.Taps.List(config);
            var table = new Table()
                .Border(TableBorder.Rounded)
                .AddColumn("[bold]Name[/]")
                .AddColumn("[bold]URL[/]")
                .AddColumn("[bold]Skills[/]")
                .AddColumn("[bold]Root[/]")
                .AddColumn("[bold]Status[/]");

            foreach (var (tap, count, isCached) in taps)
            {
                table.AddRow(
                    Markup.Escape(tap.Name),
                    Markup.Escape(tap.Url),
                    isCached ? count.ToString() : "[dim]?[/]",
                    tap.Root is not null ? Markup.Escape(tap.Root) : "[dim](auto)[/]",
                    isCached ? "[green]cached[/]" : "[yellow]not cached[/]");
            }

            AnsiConsole.Write(table);
            AnsiConsole.MarkupLine("[dim]Run [bold]lorex tap sync[/] to pull latest skill versions from all taps.[/]");
            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine("[red]Error:[/] {0}", Markup.Escape(ex.Message));
            return 1;
        }
    }

    // ── lorex tap sync [<name>] [--global] ───────────────────────────────────

    private static int Sync(string[] args, string? cwd, string? homeRoot)
    {
        var isGlobal = WantsGlobal(args);
        var tapName = args.FirstOrDefault(a => !a.StartsWith("-", StringComparison.Ordinal));
        var projectRoot = isGlobal
            ? GlobalRootLocator.ResolveForExistingGlobal(homeRoot)
            : ProjectRootLocator.ResolveForExistingProject(cwd ?? Directory.GetCurrentDirectory());

        try
        {
            var config = ServiceFactory.Skills.ReadConfig(projectRoot);

            if (tapName is not null)
            {
                AnsiConsole.Status()
                    .Start($"Syncing tap '{Markup.Escape(tapName)}'…", ctx =>
                    {
                        ctx.Spinner(Spinner.Known.Dots);
                        ServiceFactory.Taps.SyncOne(config, tapName);
                    });

                AnsiConsole.MarkupLine($"[green]✓[/] Tap [bold]{Markup.Escape(tapName)}[/] synced.");
            }
            else
            {
                if (config.Taps.Length == 0)
                {
                    AnsiConsole.MarkupLine("[yellow]No taps configured.[/]");
                    return 0;
                }

                IReadOnlyList<string> updated = [];
                AnsiConsole.Status()
                    .Start("Syncing taps…", ctx =>
                    {
                        ctx.Spinner(Spinner.Known.Dots);
                        updated = ServiceFactory.Taps.SyncAll(config);
                    });

                if (updated.Count == 0)
                    AnsiConsole.MarkupLine("[green]✓[/] All taps up to date.");
                else
                    AnsiConsole.MarkupLine(
                        $"[green]✓[/] Synced [bold]{updated.Count}[/] tap{(updated.Count == 1 ? "" : "s")}: " +
                        string.Join(", ", updated.Select(Markup.Escape)));
            }

            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine("[red]Error:[/] {0}", Markup.Escape(ex.Message));
            return 1;
        }
    }

    // ── Global flag ───────────────────────────────────────────────────────────

    internal static bool WantsGlobal(string[] args) =>
        args.Any(a => string.Equals(a, "--global", StringComparison.OrdinalIgnoreCase) ||
                      string.Equals(a, "-g",       StringComparison.OrdinalIgnoreCase));

    // ── Help ──────────────────────────────────────────────────────────────────

    private static int PrintHelp()
    {
        AnsiConsole.MarkupLine("[bold]lorex tap[/] — manage read-only skill sources");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("  [bold deepskyblue3]add[/]     [grey]<url> [[--name <name>]] [[--root <path>]] [[-g|--global]][/]  Add a tap");
        AnsiConsole.MarkupLine("  [bold deepskyblue3]remove[/]  [grey]<name> [[-g|--global]][/]                                     Remove a tap");
        AnsiConsole.MarkupLine("  [bold deepskyblue3]list[/]    [grey][[-g|--global]][/]                                            List configured taps");
        AnsiConsole.MarkupLine("  [bold deepskyblue3]sync[/]    [grey][[<name>]] [[-g|--global]][/]                                 Pull latest from taps");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[dim]Use [bold]-g[/] / [bold]--global[/] to operate on the global lorex config ([bold]~/.lorex/[/]) instead of the current project.[/]");
        AnsiConsole.MarkupLine("[dim]Skills from taps appear in [bold]lorex list[/] and [bold]lorex install[/] alongside registry skills.[/]");
        return 0;
    }

    private static int UnknownSubcommand(string sub)
    {
        AnsiConsole.MarkupLine(
            "[red]Unknown subcommand '[/]{0}[red]'[/]. Run [bold]lorex tap --help[/] for available subcommands.",
            Markup.Escape(sub));
        return 1;
    }

    // ── Name derivation ───────────────────────────────────────────────────────

    /// <summary>
    /// Derives a short tap name from a URL.
    /// <c>https://github.com/owner/repo</c> → <c>owner</c>.
    /// Falls back to the last URL path segment.
    /// </summary>
    internal static string DeriveNameFromUrl(string url)
    {
        // Normalise SSH → HTTPS
        var u = url
            .Replace("git@github.com:", "https://github.com/", StringComparison.OrdinalIgnoreCase)
            .TrimEnd('/');

        if (u.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
            u = u[..^4];

        try
        {
            var uri = new Uri(u);
            var segments = uri.AbsolutePath
                .Split('/', StringSplitOptions.RemoveEmptyEntries);

            // github.com/owner/repo → owner
            if (segments.Length >= 2)
                return Sanitize(segments[0]);

            if (segments.Length == 1)
                return Sanitize(segments[0]);
        }
        catch { }

        // Fallback: last non-empty path segment
        var last = u.Split('/', '\\')
            .LastOrDefault(s => !string.IsNullOrWhiteSpace(s)) ?? "tap";
        return Sanitize(last);
    }

    private static string Sanitize(string value)
    {
        var s = new string(value
            .Select(c => char.IsLetterOrDigit(c) || c == '-' ? char.ToLowerInvariant(c) : '-')
            .ToArray())
            .Trim('-');
        // Collapse multiple consecutive dashes
        while (s.Contains("--", StringComparison.Ordinal))
            s = s.Replace("--", "-", StringComparison.Ordinal);
        return string.IsNullOrEmpty(s) ? "tap" : s;
    }
}
