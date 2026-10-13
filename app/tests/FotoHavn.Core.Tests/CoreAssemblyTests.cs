using FsCheck.Xunit;

namespace FotoHavn.Core.Tests;

public sealed class CoreAssemblyTests
{
    [Fact]
    public void Name_is_the_brain_assembly()
    {
        Assert.Equal("FotoHavn.Core", CoreAssembly.Name);
    }

    [Property]
    public bool Model_based_tests_run_on_fscheck(int value) => value + 0 == value;
}
