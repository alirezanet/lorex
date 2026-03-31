using Lorex.Cli;
using Lorex.Core.Models;
using Lorex.Core.Services;
using Spectre.Console;

namespace Lorex.Commands;

/// <summary>Implements <c>lorex create</c>: scaffolds a new local artifact into the canonical lorex store.</summary>
public static class CreateCommand
{
    /// <summary>Runs the command. Returns 0 on success, 1 on failure.</summary>
    public static int Run(string[] args)
    {
        var projectRoot = ProjectRootLocator.ResolveForExistingProject(Directory.GetCurrentDirectory());

        ArtifactCliSupport.ParsedArtifactType parsedType;
        try
        {
            parsedType = ArtifactCliSupport.ParseArtifactTypeOrDefault(args);
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine("[red]Error:[/] {0}", Markup.Escape(ex.Message));
            return 1;
        }

        args = parsedType.RemainingArgs;

        string? nameArg = null, descArg = null, tagsArg = null, ownerArg = null;
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--description" or "-d" when i + 1 < args.Length:
                    descArg = args[++i];
                    break;
                case "--tags" when i + 1 < args.Length:
                    tagsArg = args[++i];
                    break;
                case "--owner" or "-o" when i + 1 < args.Length:
                    ownerArg = args[++i];
                    break;
                default:
                    if (!args[i].StartsWith('-'))
                        nameArg = args[i];
                    break;
            }
        }

        var kind = parsedType.HasExplicitType || nameArg is not null
            ? parsedType.Kind
            : ArtifactCliSupport.PromptForArtifactKind("create");

        AnsiConsole.MarkupLine("[bold]Create a new {0}[/]", kind.DisplayName());
        AnsiConsole.WriteLine();

        string name;
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
            var prompt = $"[bold]{kind.Title()} name[/] [dim](kebab-case, e.g. auth-overview):[/]";
            var nameInput = AnsiConsole.Ask<string>(prompt);
            if (string.IsNullOrWhiteSpace(nameInput))
            {
                AnsiConsole.MarkupLine("[red]Name cannot be empty.[/]");
                return 1;
            }

            name = nameInput.Trim().ToLowerInvariant().Replace(' ', '-');
        }

        var description = descArg ?? AnsiConsole.Ask<string>("[bold]Short description:[/]");
        var tagsInput = tagsArg ?? AnsiConsole.Ask<string>("[bold]Tags[/] [dim](comma-separated, e.g. auth, security):[/]", string.Empty);
        var owner = ownerArg ?? AnsiConsole.Ask<string>("[bold]Owner[/] [dim](team or individual name):[/]", string.Empty);
        var tags = tagsInput.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        try
        {
            ServiceFactory.Artifacts.ScaffoldArtifact(projectRoot, kind, name, description, tags, owner);

            var config = ServiceFactory.Artifacts.ReadConfig(projectRoot);
            ServiceFactory.Adapters.Project(projectRoot, config);

            var artifactDir = ServiceFactory.Artifacts.ArtifactDir(projectRoot, kind, name);
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[green]✓[/] {0} created at [dim]{1}[/]", kind.Title(), artifactDir);
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[bold]Next steps:[/]");
            AnsiConsole.MarkupLine("  1. Ask your AI agent to author [bold]{0}/{1}[/], or edit it yourself", artifactDir, kind.CanonicalFileName());
            AnsiConsole.MarkupLine("  2. The {0} is already active locally and projected where supported", kind.DisplayName());
            var publishCommand = kind == ArtifactKind.Skill
                ? $"lorex publish {name}"
                : $"lorex publish --type prompt {name}";
            AnsiConsole.MarkupLine("  3. When ready to share, run [bold]{0}[/] to push it to the registry", publishCommand);
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
