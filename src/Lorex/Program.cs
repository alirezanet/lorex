using System.Reflection;
using Lorex.Commands;
using Spectre.Console;

// Ctrl+C → exit cleanly with code 130 (standard Unix convention for SIGINT)
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = false;           // let the process die normally
    AnsiConsole.WriteLine();    // tidy up the cursor line
    Environment.Exit(130);
};

var command = args.Length > 0 ? args[0] : "--help";
var rest = args.Length > 1 ? args[1..] : [];

try
{
    return command switch
    {
        "init"      => InitCommand.Run(rest),
        "install"   => InstallCommand.Run(rest),
        "uninstall" => UninstallCommand.Run(rest),
        "create" or "generate" => CreateCommand.Run(rest),
        "publish"   => PublishCommand.Run(rest),
        "registry"  => RegistryCommand.Run(rest),
        "tap"       => TapCommand.Run(rest),
        "sync"      => SyncCommand.Run(rest),
        "list"      => ListCommand.Run(rest),
        "status"    => StatusCommand.Run(rest),
        "refresh"   => RefreshCommand.Run(rest),
        "--version" or "-v" => PrintVersion(),
        "--help" or "-h" => PrintHelp(),
        _ => UnknownCommand(command),
    };
}
catch (OperationCanceledException)
{
    AnsiConsole.MarkupLine("[dim]Cancelled.[/]");
    return 130;
}
catch (Exception ex)
{
    AnsiConsole.MarkupLine("[red]Unexpected error:[/] {0}", Markup.Escape(ex.Message));
    return 1;
}

static string GetVersion()
{
    var version = typeof(Program).Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
        ?.InformationalVersion ?? "unknown";
    // Strip any build metadata suffix (e.g. +commit hash)
    var plus = version.IndexOf('+');
    if (plus >= 0) version = version[..plus];
    return version;
}

static int PrintVersion()
{
    AnsiConsole.WriteLine(GetVersion());
    return 0;
}

static int PrintHelp()
{
    AnsiConsole.WriteLine();
    AnsiConsole.Write(new FigletText("lorex").Color(Color.Blue));
    AnsiConsole.MarkupLine($"[dim]v{GetVersion()} — Teach your AI agents once. Reuse everywhere.[/]");
    AnsiConsole.WriteLine();
    AnsiConsole.MarkupLine("[bold]USAGE[/]  lorex [dim]<command> [[args]][/]");
    AnsiConsole.WriteLine();

    Grid MakeGrid() => new Grid()
        .AddColumn(new GridColumn().Width(12))
        .AddColumn(new GridColumn().Width(36))
        .AddColumn();

    void Row(Grid g, string cmd, string args, string desc)
        => g.AddRow($"  [bold deepskyblue3]{cmd}[/]", $"[grey]{args}[/]", $"[dim]{desc}[/]");

    void Section(string title, Action<Grid> rows)
    {
        AnsiConsole.Write(new Rule($"[bold]{title}[/]").LeftJustified().RuleStyle("blue dim"));
        var g = MakeGrid();
        rows(g);
        AnsiConsole.Write(g);
        AnsiConsole.WriteLine();
    }

    Section("Local", g =>
    {
        Row(g, "init",    "[[<url>]] [[--local]] [[--global]] [[--adapters a,b]]",  "Configure a registry and set up this project or global skills");
        Row(g, "create",  "[[<name>]] [[-d desc]] [[-t tags]] [[-o owner]]",   "Scaffold a new skill for AI/manual authoring");
        Row(g, "status",  "",                                                   "Show installed skills and their state");
        Row(g, "refresh", "[[--target adapter]]",                               "Re-project lorex skills into native agent locations");
    });

    Section("Registry", g =>
    {
        Row(g, "install",   "[[<skill|url>…]] [[--all]] [[--recommended]] [[--search <text>]] [[--tag <tag>]] [[--global]]", "Install skills from registry, taps, or a URL");
        Row(g, "uninstall", "[[<skill>…]] [[--all]]",                                                                        "Remove installed skills, or choose interactively");
        Row(g, "list",      "[[--search <text>]] [[--tag <tag>]] [[--page <n>]] [[--page-size <n>]]",                        "Browse and filter skills available in the registry and taps");
        Row(g, "sync",      "[[--global]]",                                                                                  "Pull latest skill versions from the registry and all taps");
        Row(g, "publish",   "[[<skill>…]]",                                                                                  "Push local skills to the registry");
        Row(g, "registry",  "",                                                                                               "Interactively configure the connected registry policy");
    });

    Section("Taps", g =>
    {
        Row(g, "tap add",    "<url> [[--name <name>]] [[--root <path>]]", "Add a read-only skill source (any git repo)");
        Row(g, "tap remove", "<name>",                                    "Remove a tap");
        Row(g, "tap list",   "",                                          "List configured taps with skill counts");
        Row(g, "tap sync",   "[[<name>]]",                                "Pull latest from all taps (or one)");
    });

    AnsiConsole.MarkupLine("[dim]Run [bold]lorex <command> --help[/] for command-specific help.[/]");
    AnsiConsole.WriteLine();
    return 0;
}

static int UnknownCommand(string name)
{
    AnsiConsole.MarkupLine("[red]Unknown command '[/]{0}[red]'[/]. Run [bold]lorex --help[/] for available commands.", Markup.Escape(name));
    return 1;
}
