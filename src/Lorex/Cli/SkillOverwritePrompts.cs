using Spectre.Console;

namespace Lorex.Cli;

internal static class SkillOverwritePrompts
{
    internal static (List<string> approved, List<string> skipped) ResolveApprovedOverrides(
        string projectRoot,
        IEnumerable<string> skillNames,
        Func<string, string> promptFactory)
    {
        var approved = new List<string>();
        var skipped = new List<string>();

        foreach (var skillName in skillNames.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!ServiceFactory.Skills.RequiresOverwriteApproval(projectRoot, skillName))
            {
                approved.Add(skillName);
                continue;
            }

            var confirmed = AnsiConsole.Confirm(promptFactory(skillName), defaultValue: false);

            if (confirmed)
                approved.Add(skillName);
            else
                skipped.Add(skillName);
        }

        return (approved, skipped);
    }
}
