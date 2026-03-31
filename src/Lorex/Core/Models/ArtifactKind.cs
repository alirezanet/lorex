namespace Lorex.Core.Models;

/// <summary>
/// The two first-class artifact kinds that lorex manages.
/// </summary>
public enum ArtifactKind
{
    Skill,
    Prompt,
}

public static class ArtifactKindExtensions
{
    public static string CliValue(this ArtifactKind kind) => kind switch
    {
        ArtifactKind.Skill => "skill",
        ArtifactKind.Prompt => "prompt",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    public static string DisplayName(this ArtifactKind kind) => kind switch
    {
        ArtifactKind.Skill => "skill",
        ArtifactKind.Prompt => "prompt",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    public static string DisplayNamePlural(this ArtifactKind kind) => kind switch
    {
        ArtifactKind.Skill => "skills",
        ArtifactKind.Prompt => "prompts",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    public static string Title(this ArtifactKind kind) => kind switch
    {
        ArtifactKind.Skill => "Skill",
        ArtifactKind.Prompt => "Prompt",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    public static string FolderName(this ArtifactKind kind) => kind switch
    {
        ArtifactKind.Skill => "skills",
        ArtifactKind.Prompt => "prompts",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    public static string CanonicalFileName(this ArtifactKind kind) => kind switch
    {
        ArtifactKind.Skill => "SKILL.md",
        ArtifactKind.Prompt => "PROMPT.md",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    public static string? LegacyFileName(this ArtifactKind kind) => kind switch
    {
        ArtifactKind.Skill => "skill.md",
        ArtifactKind.Prompt => null,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    public static bool TryParseCliValue(string? value, out ArtifactKind kind)
    {
        kind = ArtifactKind.Skill;

        if (string.IsNullOrWhiteSpace(value))
            return false;

        if (string.Equals(value, "skill", StringComparison.OrdinalIgnoreCase))
        {
            kind = ArtifactKind.Skill;
            return true;
        }

        if (string.Equals(value, "prompt", StringComparison.OrdinalIgnoreCase))
        {
            kind = ArtifactKind.Prompt;
            return true;
        }

        return false;
    }
}
