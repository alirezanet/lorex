using Lorex.Core.Models;
using Lorex.Core.Services;

namespace Lorex.Tests;

public sealed class SkillServiceTests
{
    [Fact]
    public void DiscoverInstalledSkillNames_ReturnsExistingLorexSkillDirectories()
    {
        var projectRoot = Path.Combine(Path.GetTempPath(), $"lorex-test-{Guid.NewGuid():N}");

        try
        {
            Directory.CreateDirectory(Path.Combine(projectRoot, ".lorex", "skills", "lorex"));
            Directory.CreateDirectory(Path.Combine(projectRoot, ".lorex", "skills", "lorex-contributing"));

            var service = new SkillService(new RegistryService(new GitService()));
            var discovered = service.DiscoverInstalledSkillNames(projectRoot);

            Assert.Contains("lorex", discovered);
            Assert.Contains("lorex-contributing", discovered);
        }
        finally
        {
            if (Directory.Exists(projectRoot))
                Directory.Delete(projectRoot, recursive: true);
        }
    }
}
