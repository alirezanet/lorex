using Lorex.Core.Services;
using Spectre.Console;

namespace Lorex.Commands;

/// <summary>Implements <c>lorex install [skill…]</c>: installs one or more skills from the registry into the current project.</summary>
public static class InstallCommand
{
    /// <summary>Runs the command. Returns 0 on success, 1 on failure.</summary>
    public static int Run(string[] args)
    {
        var projectRoot = Directory.GetCurrentDirectory();

        try
        {
            var cfg = ServiceFactory.Skills.ReadConfig(projectRoot);
            if (cfg.Registry is null)
            {
                AnsiConsole.MarkupLine("[red]No registry configured.[/] lorex is running in local-only mode.");
                AnsiConsole.MarkupLine("[dim]Run [bold]lorex init <url>[/] to connect a registry, then try again.[/]");
                return 1;
            }

            var requestedSkills = args.Length > 0
                ? [.. args
                    .Where(a => !string.IsNullOrWhiteSpace(a))
                    .Distinct(StringComparer.OrdinalIgnoreCase)]
                : PromptForSkills(cfg);

            if (requestedSkills.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]Nothing selected.[/]");
                return 0;
            }

            var results = new List<(string SkillName, bool UsedSymlink)>();
            AnsiConsole.Status()
                .Start("Installing skills...", ctx =>
                {
                    foreach (var skillName in requestedSkills)
                    {
                        ctx.Status($"Installing [bold]{skillName}[/]...");
                        var usedSymlink = ServiceFactory.Skills.InstallSkill(projectRoot, skillName);
                        results.Add((skillName, usedSymlink));
                    }

                    ctx.Spinner(Spinner.Known.Dots);
                    ctx.Status("Compiling skill index...");
                    var config = ServiceFactory.Skills.ReadConfig(projectRoot);
                    ServiceFactory.Adapters.Compile(projectRoot, config);
                });

            var printedCopyGuidance = false;
            foreach (var (skillName, usedSymlink) in results)
            {
                if (usedSymlink)
                {
                    AnsiConsole.MarkupLine($"[green]✓[/] Installed [bold]{skillName}[/] [dim](symlinked)[/]");
                }
                else
                {
                    AnsiConsole.MarkupLine($"[green]✓[/] Installed [bold]{skillName}[/] [yellow](copied)[/]");

                    // Give the user actionable guidance if Developer Mode is the reason symlinks failed.
                    if (!printedCopyGuidance && OperatingSystem.IsWindows() && !WindowsDevModeHelper.IsSymlinkAvailable())
                    {
                        WindowsDevModeHelper.PrintDevModeGuidance();
                        WindowsDevModeHelper.OfferToOpenSettings();
                        printedCopyGuidance = true;
                    }
                }
            }

            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine("[red]Error:[/] {0}", Markup.Escape(ex.Message));
            return 1;
        }
    }

    private static List<string> PromptForSkills(Core.Models.LorexConfig cfg)
    {
        IReadOnlyList<Core.Models.SkillMetadata> available = [];
        AnsiConsole.Status()
            .Start("Fetching registry…", ctx =>
            {
                ctx.Spinner(Spinner.Known.Dots);
                available = ServiceFactory.Registry.ListAvailableSkills(cfg.Registry!);
            });

        if (available.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No skills found in the registry.[/]");
            return [];
        }

        var installed = new HashSet<string>(cfg.InstalledSkills, StringComparer.OrdinalIgnoreCase);
        var choices = available
            .Where(skill => !installed.Contains(skill.Name))
            .OrderBy(skill => skill.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (choices.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]All skills in the registry are already installed.[/]");
            AnsiConsole.MarkupLine("[dim]Run [bold]lorex sync[/] to update them, or [bold]lorex install <skill>[/] to reinstall one explicitly.[/]");
            return [];
        }

        var selected = AnsiConsole.Prompt(
            new MultiSelectionPrompt<Core.Models.SkillMetadata>()
                .Title("[bold]Which skills do you want to install?[/]")
                .InstructionsText("[dim](Space to select, Enter to confirm)[/]")
                .UseConverter(skill =>
                {
                    var description = string.IsNullOrWhiteSpace(skill.Description)
                        ? string.Empty
                        : $" [dim]- {Markup.Escape(skill.Description)}[/]";
                    return $"[bold]{Markup.Escape(skill.Name)}[/]{description}";
                })
                .AddChoices(choices));

        return [.. selected.Select(skill => skill.Name)];
    }
}
