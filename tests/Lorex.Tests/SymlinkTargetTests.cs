using Lorex.Core.Services;

namespace Lorex.Tests;

public sealed class SymlinkTargetTests
{
    [Fact]
    public void GetSymlinkTarget_UsesRelativePathWithinProjectRoot()
    {
        var projectRoot = @"C:\repo";
        var linkPath = @"C:\repo\.cline\skills\lorex\SKILL.md";
        var targetPath = @"C:\repo\.lorex\skills\lorex\SKILL.md";

        var target = AdapterService.GetSymlinkTarget(projectRoot, linkPath, targetPath);

        Assert.Equal(@"..\..\..\.lorex\skills\lorex\SKILL.md", target);
    }

    [Fact]
    public void GetSymlinkTarget_UsesAbsolutePathOutsideProjectRoot()
    {
        var projectRoot = @"C:\repo";
        var linkPath = @"C:\repo\.lorex\skills\shared";
        var targetPath = @"C:\Users\AliReza\.lorex\cache\registry\skills\shared";

        var target = AdapterService.GetSymlinkTarget(projectRoot, linkPath, targetPath);

        Assert.Equal(targetPath, target);
    }
}
