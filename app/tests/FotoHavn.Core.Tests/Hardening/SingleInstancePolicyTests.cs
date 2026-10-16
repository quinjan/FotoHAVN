using FotoHavn.Core.Hardening;

namespace FotoHavn.Core.Tests.Hardening;

public sealed class SingleInstancePolicyTests
{
    private const string Folder = @"C:\FOTOHAVN\Data";

    [Theory]
    [InlineData(@"C:\FOTOHAVN\Data\")]
    [InlineData(@"C:\FOTOHAVN\Data\\")]
    [InlineData(@"c:\fotohavn\data")]
    [InlineData(@"C:/FOTOHAVN/Data")]
    [InlineData(@"C:\FOTOHAVN\Other\..\Data")]
    [InlineData(@"C:\FOTOHAVN\.\Data")]
    [InlineData(@"\\?\C:\FOTOHAVN\Data")]
    public void Spellings_of_the_same_folder_share_their_names(string spelling)
    {
        Assert.Equal(SingleInstancePolicy.NamesFor(Folder), SingleInstancePolicy.NamesFor(spelling));
    }

    [Fact]
    public void The_key_is_upper_case_with_backslashes_and_no_trailing_separator()
    {
        Assert.Equal(@"C:\FOTOHAVN\DATA", SingleInstancePolicy.Normalize(@"c:/fotohavn/data//"));
    }

    [Fact]
    public void A_drive_root_keeps_its_separator()
    {
        Assert.Equal(@"C:\", SingleInstancePolicy.Normalize(@"c:\"));
    }

    [Fact]
    public void A_long_path_unc_prefix_is_removed()
    {
        Assert.Equal(@"\\SERVER\SHARE\DATA", SingleInstancePolicy.Normalize(@"\\?\UNC\server\share\data\"));
    }

    [Fact]
    public void A_relative_path_is_made_absolute()
    {
        Assert.Equal(Path.GetFullPath("Data").ToUpperInvariant(), SingleInstancePolicy.Normalize("Data"));
    }

    [Theory]
    [InlineData(@"C:\FOTOHAVN\Data2")]
    [InlineData(@"C:\FOTOHAVN")]
    [InlineData(@"D:\FOTOHAVN\Data")]
    [InlineData(@"C:\FOTOHAVN\Data\E2E-1")]
    public void Different_folders_get_different_names(string other)
    {
        var first = SingleInstancePolicy.NamesFor(Folder);
        var second = SingleInstancePolicy.NamesFor(other);

        Assert.NotEqual(first.MutexName, second.MutexName);
        Assert.NotEqual(first.ActivationEventName, second.ActivationEventName);
    }

    [Fact]
    public void Many_parallel_instance_folders_never_collide()
    {
        var names = Enumerable.Range(0, 200).Select(index => SingleInstancePolicy.NamesFor($@"C:\Temp\fotohavn-e2e-{index}\Data").MutexName).ToList();

        Assert.Equal(names.Count, names.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void The_names_are_session_local_with_sixteen_hex_digits()
    {
        var names = SingleInstancePolicy.NamesFor(Folder);

        Assert.Matches(@"^Local\\FOTOHAVN-[0-9a-f]{16}$", names.MutexName);
        Assert.Equal(names.MutexName + "-activate", names.ActivationEventName);
        Assert.Equal(@"C:\FOTOHAVN\DATA", names.DataFolderKey);
        Assert.True(names.MutexName.Length < 260);
    }

    [Fact]
    public void The_names_are_stable()
    {
        Assert.Equal(SingleInstancePolicy.NamesFor(Folder).MutexName, SingleInstancePolicy.NamesFor(Folder).MutexName);
    }

    [Fact]
    public void The_first_launch_proceeds_and_a_second_activates_the_running_one()
    {
        Assert.Equal(SingleInstanceDecision.Proceed, SingleInstancePolicy.Decide(ownsMutex: true));
        Assert.Equal(SingleInstanceDecision.ActivateExisting, SingleInstancePolicy.Decide(ownsMutex: false));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void A_missing_folder_is_refused(string? folder)
    {
        Assert.ThrowsAny<ArgumentException>(() => SingleInstancePolicy.NamesFor(folder!));
    }

    [Fact]
    public void The_exit_codes_are_distinct_and_the_single_instance_one_is_defined()
    {
        int[] codes = [HostExitCodes.Success, HostExitCodes.Failure, HostExitCodes.InvalidArguments, HostExitCodes.AlreadyRunning, HostExitCodes.SmokeTimeout];

        Assert.Equal(codes.Length, codes.Distinct().Count());
        Assert.Equal(3, HostExitCodes.AlreadyRunning);
    }
}
