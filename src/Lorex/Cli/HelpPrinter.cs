using Spectre.Console;

namespace Lorex.Cli;

/// <summary>
/// Renders consistent USAGE / DESCRIPTION / OPTIONS / EXAMPLES help output for lorex commands.
/// </summary>
internal static class HelpPrinter
{
    /// <summary>Prints a formatted help page and returns 0 for use as a command return value.</summary>
    /// <param name="usage">Usage synopsis, e.g. <c>lorex install [&lt;skill&gt;…] [--all] [-g]</c>.</param>
    /// <param name="description">One or two sentences. Use <c>\n</c> for line breaks.</param>
    /// <param name="options">Option rows shown in an OPTIONS section. Null = section omitted.</param>
    /// <param name="examples">Example rows. Empty <c>Comment</c> = no comment line printed. Null = section omitted.</param>
    /// <param name="subcommands">Subcommand rows shown in a SUBCOMMANDS section before OPTIONS. Null = section omitted.</param>
    internal static int Print(
        string usage,
        string description,
        (string Flags, string Description)[]? options = null,
        (string Comment, string Command)[]? examples = null,
        (string Signature, string Description)[]? subcommands = null)
    {
        AnsiConsole.MarkupLine("[bold]USAGE[/]");
        AnsiConsole.MarkupLine("  {0}", Markup.Escape(usage));
        AnsiConsole.WriteLine();

        AnsiConsole.MarkupLine("[bold]DESCRIPTION[/]");
        foreach (var line in description.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
            AnsiConsole.MarkupLine("  {0}", Markup.Escape(line));
        AnsiConsole.WriteLine();

        if (subcommands is { Length: > 0 })
        {
            AnsiConsole.MarkupLine("[bold]SUBCOMMANDS[/]");
            var subWidth = Math.Max(subcommands.Max(s => s.Signature.Length) + 4, 20);
            var subGrid = new Grid()
                .AddColumn(new GridColumn().Width(subWidth))
                .AddColumn();
            foreach (var (sig, desc) in subcommands)
                subGrid.AddRow(
                    $"  [bold deepskyblue3]{Markup.Escape(sig)}[/]",
                    $"[dim]{Markup.Escape(desc)}[/]");
            AnsiConsole.Write(subGrid);
            AnsiConsole.WriteLine();
        }

        if (options is { Length: > 0 })
        {
            AnsiConsole.MarkupLine("[bold]OPTIONS[/]");
            var optWidth = Math.Max(options.Max(o => o.Flags.Length) + 4, 24);
            var optGrid = new Grid()
                .AddColumn(new GridColumn().Width(optWidth))
                .AddColumn();
            foreach (var (flags, desc) in options)
                optGrid.AddRow(
                    $"  [bold]{Markup.Escape(flags)}[/]",
                    $"[dim]{Markup.Escape(desc)}[/]");
            AnsiConsole.Write(optGrid);
            AnsiConsole.WriteLine();
        }

        if (examples is { Length: > 0 })
        {
            AnsiConsole.MarkupLine("[bold]EXAMPLES[/]");
            foreach (var (comment, command) in examples)
            {
                if (!string.IsNullOrEmpty(comment))
                    AnsiConsole.MarkupLine("  [dim]# {0}[/]", Markup.Escape(comment));
                AnsiConsole.MarkupLine("  {0}", Markup.Escape(command));
            }
            AnsiConsole.WriteLine();
        }

        return 0;
    }
}
