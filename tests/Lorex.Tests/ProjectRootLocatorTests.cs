using Lorex.Core.Services;

namespace Lorex.Tests;

public sealed class ProjectRootLocatorTests
{
    [Fact]
    public void ResolveForExistingProject_FindsNearestAncestorWithLorexConfig()
    {
        var root = Path.Combine(Path.GetTempPath(), $"lorex-root-{Guid.NewGuid():N}");
        var nested = Path.Combine(root, "src", "Feature", "Deep");

        try
        {
            Directory.CreateDirectory(Path.Combine(root, ".lorex"));
            File.WriteAllText(Path.Combine(root, ".lorex", "lorex.json"), "{}");
            Directory.CreateDirectory(nested);

            var resolved = ProjectRootLocator.ResolveForExistingProject(nested);

            Assert.Equal(root, resolved);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ResolveForInit_UsesCurrentDirectoryWhenNoInitializedProjectExists()
    {
        var root = Path.Combine(Path.GetTempPath(), $"lorex-root-{Guid.NewGuid():N}");

        try
        {
            Directory.CreateDirectory(root);

            var resolved = ProjectRootLocator.ResolveForInit(root);

            Assert.Equal(root, resolved);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }
}
