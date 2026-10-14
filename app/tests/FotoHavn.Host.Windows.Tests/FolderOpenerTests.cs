using System.Diagnostics;
using FotoHavn.Host.Windows.Screens;
using Microsoft.Extensions.Logging;

namespace FotoHavn.Host.Windows.Tests;

public sealed class FolderOpenerTests
{
    private const string DataRoot = @"C:\FOTOHAVN\Data";
    private const string EventFolder = @"C:\FOTOHAVN\Data\Events\6f1c2d3e-0000-4000-8000-000000000001";

    [Theory]
    [InlineData(EventFolder, EventFolder)]
    [InlineData(EventFolder + @"\", EventFolder)]
    [InlineData(@"c:\fotohavn\data\events\abc", @"c:\fotohavn\data\events\abc")]
    [InlineData(@"C:\FOTOHAVN\Data\Events\abc\..\def", @"C:\FOTOHAVN\Data\Events\def")]
    [InlineData(@"C:/FOTOHAVN/Data/Events/abc", @"C:\FOTOHAVN\Data\Events\abc")]
    [InlineData(@"C:\FOTOHAVN\Data\Event Folder (1)\a b", @"C:\FOTOHAVN\Data\Event Folder (1)\a b")]
    public void A_path_inside_the_Data_folder_is_accepted(string path, string expected)
    {
        Assert.Equal(expected, FolderOpener.Resolve(path, DataRoot));
    }

    [Theory]
    [InlineData(@"C:\FOTOHAVN\Data")]
    [InlineData(@"C:\FOTOHAVN\Data\")]
    [InlineData(@"C:\FOTOHAVN")]
    [InlineData(@"C:\Windows\System32")]
    [InlineData(@"C:\FOTOHAVN\Data2\Events\abc")]
    [InlineData(@"C:\FOTOHAVN\DataEvents")]
    [InlineData(@"C:\FOTOHAVN\Data\..\Secrets")]
    [InlineData(@"C:\FOTOHAVN\Data\Events\..\..\Other")]
    [InlineData(@"D:\FOTOHAVN\Data\Events\abc")]
    [InlineData(@"\\server\share\FOTOHAVN\Data\Events\abc")]
    [InlineData(@"\\?\C:\Windows")]
    [InlineData(@"Events\abc")]
    [InlineData(@"..\Events\abc")]
    [InlineData(@"\Events\abc")]
    [InlineData(@"C:Events\abc")]
    [InlineData("C:\\FOTOHAVN\\Data\\Events\\a|b")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Anything_else_is_refused(string? path)
    {
        Assert.Null(FolderOpener.Resolve(path, DataRoot));
    }

    [Fact]
    public void The_Data_root_may_itself_be_relative_or_have_a_trailing_separator()
    {
        Assert.Equal(EventFolder, FolderOpener.Resolve(EventFolder, @"C:\FOTOHAVN\Data\"));
        Assert.Equal(EventFolder, FolderOpener.Resolve(EventFolder, @"C:\FOTOHAVN\Other\..\Data"));
        Assert.Throws<ArgumentException>(() => FolderOpener.Resolve(EventFolder, " "));
    }

    [Fact]
    public void A_field_build_creates_a_missing_folder_and_starts_Explorer_with_one_argument()
    {
        var host = new Host(launch: true) { Exists = false };

        host.Opener.Open(EventFolder);

        Assert.Equal([EventFolder], host.Created);
        var info = Assert.Single(host.Started);
        Assert.Equal(@"C:\Windows\explorer.exe", info.FileName);
        Assert.True(info.UseShellExecute);
        Assert.Equal([EventFolder], info.ArgumentList);
        Assert.Equal(string.Empty, info.Arguments);
        Assert.Contains(host.Logger.Entries, entry => entry.Message.Contains("started", StringComparison.Ordinal) && entry.Message.Contains(EventFolder, StringComparison.Ordinal));
    }

    [Fact]
    public void An_existing_folder_is_not_created_again()
    {
        var host = new Host(launch: true) { Exists = true };

        host.Opener.Open(EventFolder);

        Assert.Empty(host.Created);
        Assert.Single(host.Started);
    }

    [Fact]
    public void A_path_with_spaces_and_shell_characters_stays_one_argument()
    {
        var host = new Host(launch: true);
        var tricky = @"C:\FOTOHAVN\Data\Events\a b & c (1) %PATH%";

        host.Opener.Open(tricky);

        var info = Assert.Single(host.Started);
        Assert.Equal([tricky], info.ArgumentList);
    }

    [Fact]
    public void An_E2E_build_validates_and_logs_but_launches_nothing()
    {
        var host = new Host(launch: false);

        host.Opener.Open(EventFolder);

        Assert.Empty(host.Started);
        Assert.Empty(host.Created);
        Assert.Contains(host.Logger.Entries, entry => entry.Message.Contains("not launched", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void A_path_outside_the_Data_folder_is_refused_and_logged(bool launch)
    {
        var host = new Host(launch);

        host.Opener.Open(@"C:\Windows\System32");

        Assert.Empty(host.Started);
        Assert.Empty(host.Created);
        var entry = Assert.Single(host.Logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Contains("Refused", entry.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_failure_to_start_Explorer_is_logged_not_thrown()
    {
        var host = new Host(launch: true) { StartFails = true };

        host.Opener.Open(EventFolder);

        Assert.Contains(host.Logger.Entries, entry => entry.Level == LogLevel.Warning && entry.Message.Contains("could not open", StringComparison.Ordinal));
    }

    [Fact]
    public void A_failure_to_create_the_folder_is_logged_and_Explorer_is_not_started()
    {
        var host = new Host(launch: true) { Exists = false, CreateFails = true };

        host.Opener.Open(EventFolder);

        Assert.Empty(host.Started);
        Assert.Contains(host.Logger.Entries, entry => entry.Level == LogLevel.Warning);
    }

    [Fact]
    public void The_defaults_start_the_real_Explorer_and_the_build_decides_whether_to_launch()
    {
        var root = System.IO.Path.Combine(AppContext.BaseDirectory, "folder-opener-data");
        var opener = new FolderOpener(root, launchExplorer: false, new RecordingLogger());

        opener.Open(System.IO.Path.Combine(root, "Events", "e1"));
        opener.Open(@"C:\Windows");

        Assert.False(Directory.Exists(System.IO.Path.Combine(root, "Events", "e1")));
#if E2E_HOOKS
        Assert.False(HostBuild.LaunchesExplorer);
#else
        Assert.True(HostBuild.LaunchesExplorer);
#endif
        Assert.Throws<ArgumentException>(() => new FolderOpener(" ", false, new RecordingLogger()));
        Assert.Throws<ArgumentNullException>(() => new FolderOpener(DataRoot, false, null!));
    }

    private sealed class Host
    {
        public Host(bool launch)
        {
            Opener = new FolderOpener(
                DataRoot,
                launch,
                Logger,
                info =>
                {
                    if (StartFails)
                    {
                        throw new InvalidOperationException("no explorer");
                    }

                    Started.Add(info);
                },
                _ => Exists,
                path =>
                {
                    if (CreateFails)
                    {
                        throw new IOException("read-only");
                    }

                    Created.Add(path);
                },
                @"C:\Windows\explorer.exe");
        }

        public FolderOpener Opener { get; }

        public RecordingLogger Logger { get; } = new();

        public List<ProcessStartInfo> Started { get; } = [];

        public List<string> Created { get; } = [];

        public bool Exists { get; set; } = true;

        public bool StartFails { get; set; }

        public bool CreateFails { get; set; }
    }
}
