using FotoHavn.Core.Storage;

namespace FotoHavn.Core.Tests.Storage;

public sealed class DataPathClassifierTests
{
    private static DataPathFacts Facts(
        DriveType driveType = DriveType.Fixed,
        bool isUnc = false,
        bool cloudRoot = false,
        string? provider = null,
        bool cloudAttributes = false,
        bool canWrite = true) =>
        new(driveType, isUnc, cloudRoot, provider, cloudAttributes, canWrite);

    [Fact]
    public void A_fixed_writable_disk_is_ok()
    {
        var verdict = DataPathClassifier.Classify(Facts());

        Assert.Equal(DataPathStatus.Ok, verdict.Status);
        Assert.True(verdict.IsOk);
        Assert.Equal(DataPathClassifier.LocalDiskReason, verdict.Reason);
    }

    [Theory]
    [InlineData(DriveType.Unknown)]
    [InlineData(DriveType.Ram)]
    [InlineData(DriveType.NoRootDirectory)]
    public void Other_writable_drive_types_are_ok(DriveType driveType)
    {
        Assert.Equal(DataPathStatus.Ok, DataPathClassifier.Classify(Facts(driveType)).Status);
    }

    [Fact]
    public void A_removable_drive_is_ok_and_says_so()
    {
        var verdict = DataPathClassifier.Classify(Facts(DriveType.Removable));

        Assert.Equal(DataPathStatus.Ok, verdict.Status);
        Assert.Equal(DataPathClassifier.RemovableReason, verdict.Reason);
    }

    [Fact]
    public void A_removable_drive_that_cannot_be_written_is_not_writable()
    {
        Assert.Equal(DataPathStatus.NotWritable, DataPathClassifier.Classify(Facts(DriveType.Removable, canWrite: false)).Status);
    }

    [Fact]
    public void A_mapped_network_drive_is_not_a_local_disk()
    {
        var verdict = DataPathClassifier.Classify(Facts(DriveType.Network));

        Assert.Equal(DataPathStatus.NotLocalDisk, verdict.Status);
        Assert.Equal("The Data folder is on a network drive. Move FOTOHAVN to this computer's own disk.", verdict.Reason);
        Assert.Equal(StorageErrorKind.NotLocalDisk, verdict.ToErrorKind());
    }

    [Fact]
    public void A_unc_path_is_not_a_local_disk_whatever_the_drive_type()
    {
        Assert.Equal(DataPathStatus.NotLocalDisk, DataPathClassifier.Classify(Facts(isUnc: true)).Status);
    }

    [Fact]
    public void Network_wins_over_cloud_and_write_failures()
    {
        var verdict = DataPathClassifier.Classify(Facts(DriveType.Network, cloudRoot: true, provider: "OneDrive", cloudAttributes: true, canWrite: false));

        Assert.Equal(DataPathStatus.NotLocalDisk, verdict.Status);
    }

    [Fact]
    public void A_known_cloud_sync_root_is_cloud_synced_and_names_the_provider()
    {
        var verdict = DataPathClassifier.Classify(Facts(cloudRoot: true, provider: "OneDrive"));

        Assert.Equal(DataPathStatus.CloudSynced, verdict.Status);
        Assert.Equal(
            "The Data folder is inside a OneDrive folder, which can change files while the booth runs. Move FOTOHAVN outside it.",
            verdict.Reason);
        Assert.Equal(StorageErrorKind.CloudSynced, verdict.ToErrorKind());
    }

    [Theory]
    [InlineData("Dropbox", "a Dropbox")]
    [InlineData("Google Drive", "a Google Drive")]
    [InlineData("iCloud Drive", "an iCloud Drive")]
    [InlineData("  Dropbox  ", "a Dropbox")]
    public void The_provider_name_and_its_article_appear_in_the_reason(string provider, string phrase)
    {
        var verdict = DataPathClassifier.Classify(Facts(cloudRoot: true, provider: provider));

        Assert.Contains($"inside {phrase} folder", verdict.Reason);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void A_cloud_root_without_a_provider_name_gets_the_generic_reason(string? provider)
    {
        var verdict = DataPathClassifier.Classify(Facts(cloudRoot: true, provider: provider));

        Assert.Equal(DataPathStatus.CloudSynced, verdict.Status);
        Assert.Contains("inside a cloud-synced folder", verdict.Reason);
    }

    [Fact]
    public void Cloud_file_attributes_alone_are_cloud_synced_and_ignore_a_stray_provider()
    {
        var verdict = DataPathClassifier.Classify(Facts(cloudAttributes: true, provider: "Dropbox"));

        Assert.Equal(DataPathStatus.CloudSynced, verdict.Status);
        Assert.Contains("inside a cloud-synced folder", verdict.Reason);
    }

    [Fact]
    public void Cloud_wins_over_a_write_failure()
    {
        Assert.Equal(DataPathStatus.CloudSynced, DataPathClassifier.Classify(Facts(cloudAttributes: true, canWrite: false)).Status);
    }

    [Fact]
    public void A_folder_that_cannot_be_written_is_not_writable()
    {
        var verdict = DataPathClassifier.Classify(Facts(canWrite: false));

        Assert.Equal(DataPathStatus.NotWritable, verdict.Status);
        Assert.Equal(DataPathClassifier.NotWritableReason, verdict.Reason);
        Assert.Equal(StorageErrorKind.NotWritable, verdict.ToErrorKind());
    }

    [Fact]
    public void Facts_are_required()
    {
        Assert.Throws<ArgumentNullException>(() => DataPathClassifier.Classify(null!));
    }
}
