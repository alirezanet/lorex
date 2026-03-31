using Lorex.Core.Models;
using Spectre.Console;

namespace Lorex.Cli;

internal static class ArtifactOverwritePrompts
{
    internal static (List<string> approved, List<string> skipped) ResolveApprovedOverrides(
        string projectRoot,
        ArtifactKind kind,
        IEnumerable<string> artifactNames,
        Func<string, string> promptFactory)
    {
        var approved = new List<string>();
        var skipped = new List<string>();

        foreach (var artifactName in artifactNames.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!ServiceFactory.Artifacts.RequiresOverwriteApproval(projectRoot, kind, artifactName))
            {
                approved.Add(artifactName);
                continue;
            }

            var confirmed = AnsiConsole.Confirm(promptFactory(artifactName), defaultValue: false);
            if (confirmed)
                approved.Add(artifactName);
            else
                skipped.Add(artifactName);
        }

        return (approved, skipped);
    }
}
