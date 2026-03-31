using Lorex.Core.Models;
using Spectre.Console;

namespace Lorex.Cli;

internal static class ArtifactCliSupport
{
    internal sealed record ParsedArtifactType(bool HasExplicitType, ArtifactKind Kind, string[] RemainingArgs);
    internal sealed record ParsedOptionalArtifactType(bool HasExplicitType, ArtifactKind? Kind, string[] RemainingArgs);

    internal static ParsedArtifactType ParseArtifactTypeOrDefault(string[] args)
    {
        var parsed = ParseOptionalArtifactType(args);
        return new ParsedArtifactType(parsed.HasExplicitType, parsed.Kind ?? ArtifactKind.Skill, parsed.RemainingArgs);
    }

    internal static ParsedOptionalArtifactType ParseOptionalArtifactType(string[] args)
    {
        ArtifactKind? kind = null;
        var hasExplicitType = false;
        var remaining = new List<string>();

        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] == "--type")
            {
                if (i + 1 >= args.Length)
                    throw new InvalidOperationException("Missing value for --type. Expected `skill` or `prompt`.");

                if (!ArtifactKindExtensions.TryParseCliValue(args[i + 1], out var parsedKind))
                    throw new InvalidOperationException($"Unsupported artifact type '{args[i + 1]}'. Expected `skill` or `prompt`.");

                kind = parsedKind;
                hasExplicitType = true;
                i++;
                continue;
            }

            remaining.Add(args[i]);
        }

        return new ParsedOptionalArtifactType(hasExplicitType, kind, [.. remaining]);
    }

    internal static ArtifactKind PromptForArtifactKind(string action)
    {
        var choice = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title($"[bold]Which artifact type do you want to {Markup.Escape(action)}?[/]")
                .AddChoices("Skill", "Prompt"));

        return string.Equals(choice, "Prompt", StringComparison.Ordinal)
            ? ArtifactKind.Prompt
            : ArtifactKind.Skill;
    }
}
