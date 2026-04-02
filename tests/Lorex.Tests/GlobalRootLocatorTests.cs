using Lorex.Core.Services;

namespace Lorex.Tests;

public sealed class GlobalRootLocatorTests
{
    [Fact]
    public void GetGlobalRoot_ReturnsUserHomeDirectory()
    {
        var expected = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        Assert.Equal(expected, GlobalRootLocator.GetGlobalRoot());
    }

    [Fact]
    public void IsInitialized_ReturnsFalse_WhenConfigMissing()
    {
        var fakeHome = Path.Combine(Path.GetTempPath(), $"lorex-home-{Guid.NewGuid():N}");

        try
        {
            Directory.CreateDirectory(fakeHome);
            Assert.False(GlobalRootLocator.IsInitialized(fakeHome));
        }
        finally
        {
            if (Directory.Exists(fakeHome))
                Directory.Delete(fakeHome, recursive: true);
        }
    }

    [Fact]
    public void IsInitialized_ReturnsTrue_WhenConfigExists()
    {
        var fakeHome = Path.Combine(Path.GetTempPath(), $"lorex-home-{Guid.NewGuid():N}");

        try
        {
            Directory.CreateDirectory(Path.Combine(fakeHome, ".lorex"));
            File.WriteAllText(Path.Combine(fakeHome, ".lorex", "lorex.json"), "{}");

            Assert.True(GlobalRootLocator.IsInitialized(fakeHome));
        }
        finally
        {
            if (Directory.Exists(fakeHome))
                Directory.Delete(fakeHome, recursive: true);
        }
    }

    [Fact]
    public void ResolveForExistingGlobal_ReturnsHomeRoot_WhenInitialized()
    {
        var fakeHome = Path.Combine(Path.GetTempPath(), $"lorex-home-{Guid.NewGuid():N}");

        try
        {
            Directory.CreateDirectory(Path.Combine(fakeHome, ".lorex"));
            File.WriteAllText(Path.Combine(fakeHome, ".lorex", "lorex.json"), "{}");

            var result = GlobalRootLocator.ResolveForExistingGlobal(fakeHome);

            Assert.Equal(fakeHome, result);
        }
        finally
        {
            if (Directory.Exists(fakeHome))
                Directory.Delete(fakeHome, recursive: true);
        }
    }

    [Fact]
    public void ResolveForExistingGlobal_Throws_WhenNotInitialized()
    {
        var fakeHome = Path.Combine(Path.GetTempPath(), $"lorex-home-{Guid.NewGuid():N}");

        try
        {
            Directory.CreateDirectory(fakeHome);

            var ex = Assert.Throws<FileNotFoundException>(
                () => GlobalRootLocator.ResolveForExistingGlobal(fakeHome));

            Assert.Contains("lorex init --global", ex.Message);
        }
        finally
        {
            if (Directory.Exists(fakeHome))
                Directory.Delete(fakeHome, recursive: true);
        }
    }
}
