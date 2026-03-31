using Lorex.Core.Models;

namespace Lorex.Core.Adapters;

/// <summary>
/// Describes how lorex projects installed artifacts into an agent's native integration surface.
/// </summary>
public abstract record AdapterProjection(ArtifactKind Kind);

/// <summary>
/// Projects each lorex skill into a native skill directory for the target agent.
/// </summary>
/// <param name="RootPath">Absolute path to the agent's skill root directory.</param>
public sealed record SkillDirectoryProjection(string RootPath) : AdapterProjection(ArtifactKind.Skill);

/// <summary>
/// Projects each lorex skill into a Cursor rule file.
/// </summary>
/// <param name="RulesDirectory">Absolute path to the Cursor rules directory.</param>
public sealed record CursorRulesProjection(string RulesDirectory) : AdapterProjection(ArtifactKind.Skill);

/// <summary>
/// Projects lorex skills into Roo Code's mode-specific rules directory.
/// </summary>
/// <param name="RulesDirectory">Absolute path to the Roo rules directory.</param>
public sealed record RooRulesProjection(string RulesDirectory) : AdapterProjection(ArtifactKind.Skill);

/// <summary>
/// Updates Gemini CLI settings so its context loader reads lorex skills directly.
/// </summary>
/// <param name="SettingsPath">Absolute path to the Gemini project settings file.</param>
public sealed record GeminiContextProjection(string SettingsPath) : AdapterProjection(ArtifactKind.Skill);

/// <summary>
/// Generated prompt file projection for adapters that consume repo-local command or prompt files.
/// </summary>
public sealed record PromptProjection(
    string RootPath,
    PromptProjectionStyle Style,
    string? AuxiliaryPath = null) : AdapterProjection(ArtifactKind.Prompt);

public enum PromptProjectionStyle
{
    Copilot,
    Claude,
    Cursor,
    Workflow,
    Roo,
    Gemini,
    OpenCode,
}

public sealed record AdapterTargetDescription(ArtifactKind Kind, string Path);
