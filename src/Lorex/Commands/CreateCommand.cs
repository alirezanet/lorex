using Lorex.Core.Services;
using Spectre.Console;

namespace Lorex.Commands;

/// <summary>Implements <c>lorex create</c>: scaffolds a new skill into <c>.lorex/skills/</c> for local authoring.</summary>
public static class CreateCommand
{
    /// <summary>Runs the command. Returns 0 on success, 1 on failure.</summary>
    /// <remarks>
    /// Non-interactive: <c>lorex create &lt;name&gt; [--description "…"] [--tags a,b] [--owner "…"]</c><br/>
    /// Interactive:     <c>lorex create</c>
    /// Legacy alias:    <c>lorex generate</c>
    /// </remarks>
    public static int Run(string[] args)
    {
        var projectRoot = ProjectRootLocator.ResolveForExistingProject(Directory.GetCurrentDirectory());

        // ── Parse flags ───────────────────────────────────────────────────────
        string? nameArg = null, descArg = null, tagsArg = null, ownerArg = null;
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--description" or "-d" when i + 1 < args.Length: descArg  = args[++i]; break;
                case "--tags"        or "-t" when i + 1 < args.Length: tagsArg  = args[++i]; break;
                case "--owner"       or "-o" when i + 1 < args.Length: ownerArg = args[++i]; break;
                default:
                    if (!args[i].StartsWith('-')) nameArg = args[i];
                    break;
            }
        }

        AnsiConsole.MarkupLine("[bold]Create a new skill[/]");
        AnsiConsole.WriteLine();

        string name, description, owner;
        string[] tags;

        // Resolve name — arg or prompt
        if (nameArg is not null)
        {
            name = nameArg.Trim().ToLowerInvariant().Replace(' ', '-');
            if (string.IsNullOrWhiteSpace(name))
            {
                AnsiConsole.MarkupLine("[red]Name cannot be empty.[/]");
                return 1;
            }
        }
        else
        {
            var nameInput = AnsiConsole.Ask<string>("[bold]Skill name[/] [dim](kebab-case, e.g. auth-overview):[/]");
            if (string.IsNullOrWhiteSpace(nameInput))
            {
                AnsiConsole.MarkupLine("[red]Name cannot be empty.[/]");
                return 1;
            }
            name = nameInput.Trim().ToLowerInvariant().Replace(' ', '-');
        }

        // Remaining fields — use flag if provided, otherwise prompt
        description = descArg  ?? AnsiConsole.Ask<string>("[bold]Short description:[/]");
        var tagsInput = tagsArg ?? AnsiConsole.Ask<string>("[bold]Tags[/] [dim](comma-separated, e.g. auth, security):[/]", string.Empty);
        owner       = ownerArg ?? AnsiConsole.Ask<string>("[bold]Owner[/] [dim](team or individual name):[/]", string.Empty);
        tags        = tagsInput.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        try
        {
            ServiceFactory.Skills.ScaffoldSkill(projectRoot, name, description, tags, owner);

            var config = ServiceFactory.Skills.ReadConfig(projectRoot);
            ServiceFactory.Adapters.Project(projectRoot, config);

            var skillDir = ServiceFactory.Skills.SkillDir(projectRoot, name);
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[green]✓[/] Skill created at [dim]{0}[/]", skillDir);
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[bold]Next steps:[/]");
            AnsiConsole.MarkupLine("  1. Ask your AI agent to author [bold]{0}/{1}[/], or edit it yourself", skillDir, SkillFileConvention.CanonicalFileName);
            AnsiConsole.MarkupLine("  2. The skill is already active locally and projected into configured agent integrations");
            AnsiConsole.MarkupLine("  3. When ready to share, run [bold]lorex publish {0}[/] to push it to the registry", name);
            AnsiConsole.WriteLine();
            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine("[red]Error:[/] {0}", Markup.Escape(ex.Message));
            return 1;
        }
    }
}
