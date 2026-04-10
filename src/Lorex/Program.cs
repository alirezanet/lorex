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

    AnsiConsole.Write(new Rule("[bold]COMMANDS[/]").LeftJustified().RuleStyle("blue dim"));
    var commandsGrid = new Grid()
        .AddColumn(new GridColumn().Width(12))
        .AddColumn();
    commandsGrid.AddRow("  [bold deepskyblue3]init[/]",      "[dim]Configure lorex for this project or globally[/]");
    commandsGrid.AddRow("  [bold deepskyblue3]install[/]",   "[dim]Install skills from the registry, taps, or a URL[/]");
    commandsGrid.AddRow("  [bold deepskyblue3]uninstall[/]", "[dim]Remove installed skills[/]");
    commandsGrid.AddRow("  [bold deepskyblue3]create[/]",    "[dim]Scaffold a new skill for authoring[/]");
    commandsGrid.AddRow("  [bold deepskyblue3]list[/]",      "[dim]Browse and filter available skills[/]");
    commandsGrid.AddRow("  [bold deepskyblue3]status[/]",    "[dim]Show installed skills and their state[/]");
    commandsGrid.AddRow("  [bold deepskyblue3]sync[/]",      "[dim]Pull latest skill versions from registry and taps[/]");
    commandsGrid.AddRow("  [bold deepskyblue3]publish[/]",   "[dim]Push local skills to the registry[/]");
    commandsGrid.AddRow("  [bold deepskyblue3]refresh[/]",   "[dim]Re-project skills into native agent locations[/]");
    commandsGrid.AddRow("  [bold deepskyblue3]registry[/]",  "[dim]Configure the connected registry policy[/]");
    commandsGrid.AddRow("  [bold deepskyblue3]tap[/]",       "[dim]Manage read-only skill sources[/]");
    AnsiConsole.Write(commandsGrid);
    AnsiConsole.WriteLine();

    AnsiConsole.Write(new Rule("[bold]FLAGS[/]").LeftJustified().RuleStyle("blue dim"));
    var flagsGrid = new Grid()
        .AddColumn(new GridColumn().Width(20))
        .AddColumn();
    flagsGrid.AddRow("  [bold]-g[/][dim], --global[/]",  "[dim]Operate on the global lorex root ([bold]~/.lorex/[/])[/]");
    flagsGrid.AddRow("  [bold]-h[/][dim], --help[/]",    "[dim]Show help for a command[/]");
    flagsGrid.AddRow("  [bold]-v[/][dim], --version[/]", "[dim]Show version[/]");
    AnsiConsole.Write(flagsGrid);
    AnsiConsole.WriteLine();

    AnsiConsole.MarkupLine("[dim]Run [bold]lorex <command> --help[/] for command-specific flags and examples.[/]");
    AnsiConsole.WriteLine();
    return 0;
}

static int UnknownCommand(string name)
{
    AnsiConsole.MarkupLine("[red]Unknown command '[/]{0}[red]'[/]. Run [bold]lorex --help[/] for available commands.", Markup.Escape(name));
    return 1;
}
