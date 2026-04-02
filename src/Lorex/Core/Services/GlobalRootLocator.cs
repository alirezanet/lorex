namespace Lorex.Core.Services;

/// <summary>
/// Resolves the global lorex root at <c>~/.lorex</c>, independent of any project directory.
/// Used when commands are invoked with <c>--global</c> to install skills and project them into
/// user-level agent locations (<c>~/.claude/skills</c>, <c>~/.gemini/settings.json</c>, etc.)
/// instead of a specific project.
/// </summary>
internal static class GlobalRootLocator
{
    private const string LorexConfigRelativePath = ".lorex/lorex.json";

    /// <summary>
    /// Returns the user's home directory. This serves as the project-root equivalent for global
    /// operations: skills are stored at <c>~/.lorex/skills/</c> and the config at <c>~/.lorex/lorex.json</c>.
    /// </summary>
    internal static string GetGlobalRoot() =>
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    /// <summary>
    /// Returns <c>true</c> when <c>~/.lorex/lorex.json</c> exists, i.e. global lorex is initialised.
    /// </summary>
    /// <param name="homeRoot">Overrides the home directory; defaults to <see cref="GetGlobalRoot"/> (used in tests).</param>
    internal static bool IsInitialized(string? homeRoot = null)
    {
        var root = homeRoot ?? GetGlobalRoot();
        return File.Exists(Path.Combine(root, LorexConfigRelativePath));
    }

    /// <summary>
    /// Returns the global root path, or throws <see cref="FileNotFoundException"/> if global lorex is
    /// not yet initialised. Call <see cref="IsInitialized"/> first if you want a non-throwing check.
    /// </summary>
    /// <param name="homeRoot">Overrides the home directory; defaults to <see cref="GetGlobalRoot"/> (used in tests).</param>
    internal static string ResolveForExistingGlobal(string? homeRoot = null)
    {
        var root = homeRoot ?? GetGlobalRoot();
        if (IsInitialized(root))
            return root;

        throw new FileNotFoundException(
            "Global lorex is not initialised. Run `lorex init --global` first.",
            Path.Combine(root, ".lorex", "lorex.json"));
    }
}
