using FotoHavn.Core.Storage;
using FotoHavn.Storage.DataPath;

namespace FotoHavn.Storage.Tests.DataPath;

/// <summary>A fake operating system for <see cref="WindowsDataPathProbe"/>.</summary>
internal sealed class FakeDataPathEnvironment : IDataPathEnvironment
{
    public Dictionary<string, string> Variables { get; } = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, string> Files { get; } = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, DriveType> DriveTypes { get; } = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, string> VolumeLabels { get; } = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, FileAttributes> Attributes { get; } = new(StringComparer.OrdinalIgnoreCase);

    public bool CanWrite { get; set; } = true;

    public string? ProbedDirectory { get; private set; }

    public string? GetEnvironmentVariable(string name) => Variables.GetValueOrDefault(name);

    public string? ReadAllText(string path) => Files.GetValueOrDefault(path);

    public bool PathExists(string path) => Attributes.ContainsKey(path) || Files.ContainsKey(path);

    public DriveType GetDriveType(string root) => DriveTypes.GetValueOrDefault(root, DriveType.Fixed);

    public string? GetVolumeLabel(string root) => VolumeLabels.GetValueOrDefault(root);

    public FileAttributes? GetAttributes(string path) => Attributes.TryGetValue(path, out var value) ? value : null;

    public bool TryWriteProbeFile(string directory)
    {
        ProbedDirectory = directory;
        return CanWrite;
    }
}

/// <summary>An environment where every read fails, to prove the probe never throws.</summary>
internal sealed class ThrowingDataPathEnvironment : IDataPathEnvironment
{
    public string? GetEnvironmentVariable(string name) => throw new IOException("boom");

    public string? ReadAllText(string path) => throw new IOException("boom");

    public bool PathExists(string path) => throw new IOException("boom");

    public DriveType GetDriveType(string root) => throw new IOException("boom");

    public string? GetVolumeLabel(string root) => throw new IOException("boom");

    public FileAttributes? GetAttributes(string path) => throw new IOException("boom");

    public bool TryWriteProbeFile(string directory) => throw new IOException("boom");
}

public sealed class WindowsDataPathProbeTests
{
    private const string BoothData = @"C:\Booth\FOTOHAVN\Data";

    private static DataPathFacts Probe(FakeDataPathEnvironment environment, string path = BoothData) =>
        new WindowsDataPathProbe(environment).Probe(path);

    [Fact]
    public void A_plain_local_folder_has_no_flags()
    {
        var environment = new FakeDataPathEnvironment();

        var facts = Probe(environment);

        Assert.Equal(new DataPathFacts(DriveType.Fixed, false, false, null, false, true), facts);
        Assert.Equal(BoothData, environment.ProbedDirectory);
    }

    [Theory]
    [InlineData(DriveType.Removable)]
    [InlineData(DriveType.Network)]
    [InlineData(DriveType.CDRom)]
    [InlineData(DriveType.Ram)]
    public void The_drive_type_of_the_path_root_is_reported(DriveType driveType)
    {
        var environment = new FakeDataPathEnvironment();
        environment.DriveTypes[@"E:\"] = driveType;

        var facts = Probe(environment, @"E:\FOTOHAVN\Data");

        Assert.Equal(driveType, facts.DriveType);
        Assert.False(facts.IsUnc);
    }

    [Fact]
    public void A_removable_drive_classifies_as_ok()
    {
        var environment = new FakeDataPathEnvironment();
        environment.DriveTypes[@"E:\"] = DriveType.Removable;

        var verdict = DataPathClassifier.Classify(Probe(environment, @"E:\FOTOHAVN\Data"));

        Assert.Equal(DataPathStatus.Ok, verdict.Status);
    }

    [Fact]
    public void A_mapped_network_drive_classifies_as_not_a_local_disk()
    {
        var environment = new FakeDataPathEnvironment();
        environment.DriveTypes[@"Z:\"] = DriveType.Network;

        var verdict = DataPathClassifier.Classify(Probe(environment, @"Z:\Booth\Data"));

        Assert.Equal(DataPathStatus.NotLocalDisk, verdict.Status);
    }

    [Theory]
    [InlineData(@"\\server\share\FOTOHAVN\Data")]
    [InlineData(@"\\?\UNC\server\share\FOTOHAVN\Data")]
    [InlineData(@"\\?\unc\server\share\FOTOHAVN\Data")]
    public void A_unc_path_is_unc_and_a_network_drive(string path)
    {
        var facts = Probe(new FakeDataPathEnvironment(), path);

        Assert.True(facts.IsUnc);
        Assert.Equal(DriveType.Network, facts.DriveType);
        Assert.Equal(DataPathStatus.NotLocalDisk, DataPathClassifier.Classify(facts).Status);
    }

    [Fact]
    public void An_extended_length_local_path_is_not_unc_and_uses_its_drive()
    {
        var environment = new FakeDataPathEnvironment();
        environment.DriveTypes[@"D:\"] = DriveType.Removable;

        var facts = Probe(environment, @"\\?\D:\Booth\Data");

        Assert.False(facts.IsUnc);
        Assert.Equal(DriveType.Removable, facts.DriveType);
        Assert.Equal(@"D:\Booth\Data", environment.ProbedDirectory);
    }

    [Theory]
    [InlineData(@"\\?\Volume{12345678-1234-1234-1234-123456789012}\Booth\Data")]
    [InlineData(@"\\.\C:\Booth\Data")]
    public void A_device_path_is_not_treated_as_unc(string path)
    {
        Assert.False(Probe(new FakeDataPathEnvironment(), path).IsUnc);
    }

    [Fact]
    public void A_relative_path_is_resolved_against_the_current_folder()
    {
        var environment = new FakeDataPathEnvironment();

        Probe(environment, "Data");

        Assert.Equal(Path.GetFullPath("Data"), environment.ProbedDirectory);
    }

    [Theory]
    [InlineData("OneDrive")]
    [InlineData("OneDriveConsumer")]
    [InlineData("OneDriveCommercial")]
    public void A_path_inside_a_onedrive_folder_is_cloud_synced(string variable)
    {
        var environment = new FakeDataPathEnvironment();
        environment.Variables[variable] = @"C:\Users\Booth\OneDrive - Contoso";

        var facts = Probe(environment, @"C:\Users\Booth\OneDrive - Contoso\FOTOHAVN\Data");

        Assert.True(facts.IsUnderKnownCloudSyncRoot);
        Assert.Equal("OneDrive", facts.CloudSyncProvider);
        Assert.Equal(DataPathStatus.CloudSynced, DataPathClassifier.Classify(facts).Status);
    }

    [Theory]
    [InlineData(@"C:\Users\Booth\OneDrive\")]
    [InlineData(@"c:\users\booth\onedrive")]
    public void The_cloud_root_matches_without_regard_to_case_or_a_trailing_slash(string root)
    {
        var environment = new FakeDataPathEnvironment();
        environment.Variables["OneDrive"] = root;

        Assert.True(Probe(environment, @"C:\Users\Booth\OneDrive\Data").IsUnderKnownCloudSyncRoot);
    }

    [Fact]
    public void The_cloud_root_folder_itself_counts()
    {
        var environment = new FakeDataPathEnvironment();
        environment.Variables["OneDrive"] = @"C:\Users\Booth\OneDrive";

        Assert.True(Probe(environment, @"C:\Users\Booth\OneDrive").IsUnderKnownCloudSyncRoot);
    }

    [Fact]
    public void A_sibling_folder_that_only_shares_the_prefix_is_not_cloud_synced()
    {
        var environment = new FakeDataPathEnvironment();
        environment.Variables["OneDrive"] = @"C:\Users\Booth\OneDrive";

        Assert.False(Probe(environment, @"C:\Users\Booth\OneDrive2\Data").IsUnderKnownCloudSyncRoot);
    }

    [Theory]
    [InlineData(@"C:\")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("C:\\bad\0root")]
    public void A_drive_root_a_blank_or_an_invalid_cloud_root_is_ignored(string root)
    {
        var environment = new FakeDataPathEnvironment();
        environment.Variables["OneDrive"] = root;

        Assert.False(Probe(environment).IsUnderKnownCloudSyncRoot);
    }

    [Theory]
    [InlineData("APPDATA", "personal")]
    [InlineData("LOCALAPPDATA", "business")]
    public void A_path_inside_a_dropbox_folder_is_cloud_synced(string variable, string account)
    {
        var environment = new FakeDataPathEnvironment();
        environment.Variables[variable] = @"C:\Users\Booth\AppData\Roaming";
        environment.Files[@"C:\Users\Booth\AppData\Roaming\Dropbox\info.json"] =
            $$"""{ "{{account}}": { "path": "C:\\Users\\Booth\\Dropbox", "host": 1 } }""";

        var facts = Probe(environment, @"C:\Users\Booth\Dropbox\FOTOHAVN\Data");

        Assert.Equal("Dropbox", facts.CloudSyncProvider);
        Assert.True(facts.IsUnderKnownCloudSyncRoot);
    }

    [Fact]
    public void Every_dropbox_account_in_info_json_is_a_root()
    {
        var environment = new FakeDataPathEnvironment();
        environment.Variables["APPDATA"] = @"C:\Users\Booth\AppData\Roaming";
        environment.Files[@"C:\Users\Booth\AppData\Roaming\Dropbox\info.json"] =
            """{ "personal": { "path": "C:\\Users\\Booth\\Dropbox" }, "business": { "path": "D:\\Team Dropbox" } }""";

        Assert.True(Probe(environment, @"D:\Team Dropbox\Booth\Data").IsUnderKnownCloudSyncRoot);
        Assert.True(Probe(environment, @"C:\Users\Booth\Dropbox\Data").IsUnderKnownCloudSyncRoot);
        Assert.False(Probe(environment, @"D:\Other\Data").IsUnderKnownCloudSyncRoot);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[1, 2]")]
    [InlineData("\"text\"")]
    [InlineData("{}")]
    [InlineData("""{ "personal": 5 }""")]
    [InlineData("""{ "personal": { "host": 1 } }""")]
    [InlineData("""{ "personal": { "path": 5 } }""")]
    [InlineData("""{ "personal": { "path": "   " } }""")]
    [InlineData("""{ "personal": { "path": null } }""")]
    public void A_dropbox_info_json_that_names_no_folder_is_ignored(string json)
    {
        var environment = new FakeDataPathEnvironment();
        environment.Variables["APPDATA"] = @"C:\Users\Booth\AppData\Roaming";
        environment.Files[@"C:\Users\Booth\AppData\Roaming\Dropbox\info.json"] = json;

        Assert.False(Probe(environment).IsUnderKnownCloudSyncRoot);
    }

    [Fact]
    public void A_missing_dropbox_info_json_is_ignored()
    {
        var environment = new FakeDataPathEnvironment();
        environment.Variables["APPDATA"] = @"C:\Users\Booth\AppData\Roaming";
        environment.Variables["LOCALAPPDATA"] = @"C:\Users\Booth\AppData\Local";

        Assert.False(Probe(environment).IsUnderKnownCloudSyncRoot);
    }

    [Fact]
    public void A_google_drive_volume_is_cloud_synced()
    {
        var environment = new FakeDataPathEnvironment();
        environment.VolumeLabels[@"G:\"] = "Google Drive";

        var facts = Probe(environment, @"G:\My Drive\FOTOHAVN\Data");

        Assert.Equal("Google Drive", facts.CloudSyncProvider);
        Assert.True(facts.IsUnderKnownCloudSyncRoot);
    }

    [Fact]
    public void A_volume_label_that_is_not_google_drive_does_not_count_and_a_unc_path_is_not_asked_for_one()
    {
        var environment = new FakeDataPathEnvironment();
        environment.VolumeLabels[@"G:\"] = "Backup";
        environment.VolumeLabels[@"\\server\share"] = "Google Drive";

        Assert.False(Probe(environment, @"G:\Data").IsUnderKnownCloudSyncRoot);
        Assert.False(Probe(environment, @"\\server\share\Data").IsUnderKnownCloudSyncRoot);
    }

    [Theory]
    [InlineData("Google Drive", "Google Drive")]
    [InlineData("iCloudDrive", "iCloud Drive")]
    public void A_path_inside_the_users_google_drive_or_icloud_folder_is_cloud_synced(string folder, string provider)
    {
        var environment = new FakeDataPathEnvironment();
        environment.Variables["USERPROFILE"] = @"C:\Users\Booth";

        var facts = Probe(environment, $@"C:\Users\Booth\{folder}\FOTOHAVN\Data");

        Assert.Equal(provider, facts.CloudSyncProvider);
    }

    [Fact]
    public void A_folder_elsewhere_in_the_profile_is_not_cloud_synced()
    {
        var environment = new FakeDataPathEnvironment();
        environment.Variables["USERPROFILE"] = @"C:\Users\Booth";

        Assert.False(Probe(environment, @"C:\Users\Booth\Documents\FOTOHAVN\Data").IsUnderKnownCloudSyncRoot);
    }

    [Theory]
    [InlineData(FileAttributes.Directory | (FileAttributes)WindowsDataPathProbe.RecallOnDataAccess)]
    [InlineData(FileAttributes.Directory | (FileAttributes)WindowsDataPathProbe.RecallOnOpen)]
    [InlineData(FileAttributes.Directory | (FileAttributes)WindowsDataPathProbe.Pinned)]
    [InlineData(FileAttributes.Directory | (FileAttributes)WindowsDataPathProbe.Unpinned)]
    public void Each_cloud_file_attribute_on_the_folder_is_detected(FileAttributes attributes)
    {
        var environment = new FakeDataPathEnvironment();
        environment.Attributes[BoothData] = attributes;

        var facts = Probe(environment);

        Assert.True(facts.HasCloudFileAttributes);
        Assert.False(facts.IsUnderKnownCloudSyncRoot);
        Assert.Equal(DataPathStatus.CloudSynced, DataPathClassifier.Classify(facts).Status);
    }

    [Fact]
    public void Plain_attributes_are_not_cloud_attributes()
    {
        var environment = new FakeDataPathEnvironment();
        environment.Attributes[BoothData] = FileAttributes.Directory | FileAttributes.Hidden | FileAttributes.ReparsePoint;

        Assert.False(Probe(environment).HasCloudFileAttributes);
    }

    [Fact]
    public void The_attributes_of_the_nearest_existing_ancestor_count_when_the_folder_does_not_exist_yet()
    {
        var environment = new FakeDataPathEnvironment();
        environment.Attributes[@"C:\Booth"] = FileAttributes.Directory | (FileAttributes)WindowsDataPathProbe.Pinned;

        Assert.True(Probe(environment).HasCloudFileAttributes);
    }

    [Fact]
    public void An_existing_folder_whose_attributes_cannot_be_read_has_no_cloud_attributes()
    {
        var environment = new FakeDataPathEnvironment();
        environment.Files[BoothData] = "exists but has no attributes";

        Assert.False(Probe(environment).HasCloudFileAttributes);
    }

    [Fact]
    public void A_path_with_no_existing_ancestor_has_no_cloud_attributes()
    {
        Assert.False(Probe(new FakeDataPathEnvironment()).HasCloudFileAttributes);
    }

    [Fact]
    public void A_folder_that_cannot_be_written_is_reported_and_classifies_as_not_writable()
    {
        var environment = new FakeDataPathEnvironment { CanWrite = false };

        var facts = Probe(environment);

        Assert.False(facts.CanWrite);
        Assert.Equal(DataPathStatus.NotWritable, DataPathClassifier.Classify(facts).Status);
    }

    [Fact]
    public void An_environment_that_fails_everywhere_never_makes_the_probe_throw()
    {
        var facts = new WindowsDataPathProbe(new ThrowingDataPathEnvironment()).Probe(BoothData);

        Assert.Equal(new DataPathFacts(DriveType.Unknown, false, false, null, false, false), facts);
    }

    [Fact]
    public void An_empty_path_the_operating_system_rejects_is_probed_as_unknown_and_unwritable()
    {
        var environment = new FakeDataPathEnvironment { CanWrite = false };

        var facts = Probe(environment, string.Empty);

        Assert.Equal(DriveType.Unknown, facts.DriveType);
        Assert.False(facts.CanWrite);
    }

    [Fact]
    public void Arguments_are_checked()
    {
        Assert.Throws<ArgumentNullException>(() => new WindowsDataPathProbe(null!));
        Assert.Throws<ArgumentNullException>(() => new WindowsDataPathProbe().Probe(null!));
    }
}
