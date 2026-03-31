namespace Lorex.Core.Services;

/// <summary>
/// Resolves the lorex project root from the current working directory or one of its ancestors.
/// </summary>
internal static class ProjectRootLocator
{
    private const string LorexConfigRelativePath = ".lorex/lorex.json";

    internal static string ResolveForExistingProject(string startDirectory)
    {
        var root = FindNearestInitializedRoot(startDirectory);
        if (root is not null)
            return root;

        throw new FileNotFoundException(
            "lorex is not initialised in this directory. Run `lorex init` first.",
            Path.Combine(Path.GetFullPath(startDirectory), ".lorex", "lorex.json"));
    }

    internal static string ResolveForInit(string startDirectory) =>
        FindNearestInitializedRoot(startDirectory) ?? Path.GetFullPath(startDirectory);

    internal static string? FindNearestInitializedRoot(string startDirectory)
    {
        var current = new DirectoryInfo(Path.GetFullPath(startDirectory));
        while (current is not null)
        {
            var configPath = Path.Combine(current.FullName, ".lorex", "lorex.json");
            if (File.Exists(configPath))
                return current.FullName;

            current = current.Parent;
        }

        return null;
    }
}
