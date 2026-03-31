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
        "sync"      => SyncCommand.Run(rest),
        "show"      => ShowCommand.Run(rest),
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

static int PrintVersion()
{
    var version = typeof(Program).Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
        ?.InformationalVersion ?? "unknown";
    // Strip any build metadata suffix (e.g. +commit hash)
    var plus = version.IndexOf('+');
    if (plus >= 0) version = version[..plus];
    AnsiConsole.WriteLine(version);
    return 0;
}

static int PrintHelp()
{
    var grid = new Grid()
        .AddColumn(new GridColumn().Width(14))
        .AddColumn(new GridColumn().Width(28))
        .AddColumn();

    void Row(string cmd, string args, string desc)
        => grid.AddRow($"  [bold]{cmd}[/]", $"[dim]{args}[/]", desc);

    Row("init",      "[[<url>]] [[--local]] [[--adapters a,b]]", "Configure a registry (or run local-only) and set up this project");
    Row("install",   "[[<artifact>…]] [[--all]] [[--recommended]] [[--type skill|prompt]]", "Install registry artifacts, or choose interactively");
    Row("uninstall", "[[<artifact>…]] [[--all]] [[--type skill|prompt]]", "Remove installed artifacts, or choose interactively");
    Row("list",      "[[--type skill|prompt]]", "List registry artifacts of one type");
    Row("status",    "[[--type skill|prompt]]", "Show installed artifacts and their state");
    Row("sync",      "[[--type skill|prompt]]", "Pull latest shared artifacts from the registry");
    Row("create",    "[[<name>]] [[-d desc]] [[-t tags]] [[-o owner]] [[--type skill|prompt]]", "Scaffold a new local artifact");
    Row("publish",   "[[<artifact>…]] [[--type skill|prompt]]", "Push local artifacts to the registry");
    Row("show",      "prompt <name>", "Print a canonical prompt for manual use");
    Row("registry",  "",                   "Interactively configure the connected registry policy");
    Row("refresh",   "[[--target adapter]] [[--type skill|prompt]]", "Re-project lorex artifacts into native agent locations");

    AnsiConsole.WriteLine();
    AnsiConsole.Write(new FigletText("lorex").Color(Color.Blue));
    AnsiConsole.MarkupLine("[dim]v0.0.1 — Teach your AI agents once. Reuse everywhere.[/]");
    AnsiConsole.WriteLine();
    AnsiConsole.MarkupLine("[bold]USAGE[/]  lorex [dim]<command>[/] [dim][[args]][/]");
    AnsiConsole.WriteLine();
    AnsiConsole.MarkupLine("[bold]COMMANDS[/]");
    AnsiConsole.Write(grid);
    AnsiConsole.WriteLine();
    AnsiConsole.MarkupLine("[dim]Run [bold]lorex <command> --help[/] for command-specific help.[/]");
    AnsiConsole.WriteLine();
    return 0;
}

static int UnknownCommand(string name)
{
    AnsiConsole.MarkupLine("[red]Unknown command '[/]{0}[red]'[/]. Run [bold]lorex --help[/] for available commands.", Markup.Escape(name));
    return 1;
}
