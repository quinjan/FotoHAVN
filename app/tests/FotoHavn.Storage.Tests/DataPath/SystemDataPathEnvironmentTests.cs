using FotoHavn.Storage.DataPath;
using FotoHavn.Storage.Tests.Support;

namespace FotoHavn.Storage.Tests.DataPath;

/// <summary>The thin real-OS wrapper and the probe's real-folder smoke test.</summary>
public sealed class SystemDataPathEnvironmentTests : IDisposable
{
    private readonly ScratchFolder _scratch = new();
    private readonly SystemDataPathEnvironment _environment = SystemDataPathEnvironment.Instance;

    public void Dispose() => _scratch.Dispose();

    [Fact]
    public void It_reads_an_environment_variable_and_treats_blank_as_unset()
    {
        const string Name = "FOTOHAVN_TEST_PROBE_VARIABLE";
        try
        {
            Environment.SetEnvironmentVariable(Name, "value");
            Assert.Equal("value", _environment.GetEnvironmentVariable(Name));
            Environment.SetEnvironmentVariable(Name, "   ");
            Assert.Null(_environment.GetEnvironmentVariable(Name));
            Environment.SetEnvironmentVariable(Name, null);
            Assert.Null(_environment.GetEnvironmentVariable(Name));
        }
        finally
        {
            Environment.SetEnvironmentVariable(Name, null);
        }
    }

    [Fact]
    public void It_reads_files_and_answers_null_for_a_missing_or_invalid_path()
    {
        var path = _scratch.Combine("info.json");
        File.WriteAllText(path, "{}");

        Assert.Equal("{}", _environment.ReadAllText(path));
        Assert.Null(_environment.ReadAllText(_scratch.Combine("missing.json")));
        Assert.Null(_environment.ReadAllText("bad\0path"));
    }

    [Fact]
    public void It_checks_whether_a_path_exists()
    {
        Assert.True(_environment.PathExists(_scratch.Path));
        Assert.False(_environment.PathExists(_scratch.Combine("missing")));
        Assert.False(_environment.PathExists("bad\0path"));
    }

    [Fact]
    public void It_reports_a_known_drive_type_for_the_scratch_drive_and_unknown_for_a_bad_root()
    {
        var root = System.IO.Path.GetPathRoot(_scratch.Path)!;

        Assert.NotEqual(DriveType.Unknown, _environment.GetDriveType(root));
        Assert.Equal(DriveType.Unknown, _environment.GetDriveType("bad\0root"));
    }

    [Fact]
    public void It_reads_a_volume_label_without_throwing()
    {
        var root = System.IO.Path.GetPathRoot(_scratch.Path)!;

        var label = _environment.GetVolumeLabel(root);

        Assert.True(label is null || label.Length > 0);
        Assert.Null(_environment.GetVolumeLabel("bad\0root"));
    }

    [Fact]
    public void It_reads_attributes_and_answers_null_for_a_missing_path()
    {
        var attributes = _environment.GetAttributes(_scratch.Path);

        Assert.NotNull(attributes);
        Assert.True(attributes.Value.HasFlag(FileAttributes.Directory));
        Assert.Null(_environment.GetAttributes(_scratch.Combine("missing")));
    }

    [Fact]
    public void It_writes_and_removes_a_probe_file_and_reports_failure_for_a_missing_folder()
    {
        Assert.True(_environment.TryWriteProbeFile(_scratch.Path));
        Assert.Empty(Directory.GetFileSystemEntries(_scratch.Path));
        Assert.False(_environment.TryWriteProbeFile(_scratch.Combine("missing")));
    }

    [Fact]
    public void The_real_probe_finds_a_scratch_folder_writable_and_leaves_nothing_behind()
    {
        var facts = new WindowsDataPathProbe().Probe(_scratch.Path);

        Assert.True(facts.CanWrite);
        Assert.False(facts.IsUnc);
        Assert.Empty(Directory.GetFileSystemEntries(_scratch.Path));
    }
}
