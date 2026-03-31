namespace Lorex.Core.Models;

/// <summary>
/// Installed project artifacts grouped by kind.
/// </summary>
public sealed record ArtifactCollection
{
    public string[] Skills { get; init; } = [];

    public string[] Prompts { get; init; } = [];

    public string[] Get(ArtifactKind kind) => kind switch
    {
        ArtifactKind.Skill => Skills,
        ArtifactKind.Prompt => Prompts,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    public ArtifactCollection With(ArtifactKind kind, IEnumerable<string> names)
    {
        var normalized = names
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return kind switch
        {
            ArtifactKind.Skill => this with { Skills = normalized },
            ArtifactKind.Prompt => this with { Prompts = normalized },
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };
    }
}
