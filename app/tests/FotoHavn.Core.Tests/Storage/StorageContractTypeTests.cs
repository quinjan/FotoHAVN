using FotoHavn.Core.Storage;

namespace FotoHavn.Core.Tests.Storage;

public sealed class StorageContractTypeTests
{
    [Fact]
    public void A_storage_exception_carries_its_kind()
    {
        var inner = new IOException("disk");

        var exception = new StorageException(StorageErrorKind.DiskFull, "The disk is full.", inner);

        Assert.Equal(StorageErrorKind.DiskFull, exception.Kind);
        Assert.Equal("The disk is full.", exception.Message);
        Assert.Same(inner, exception.InnerException);
    }

    [Fact]
    public void The_standard_constructors_default_to_unknown()
    {
        Assert.Equal(StorageErrorKind.Unknown, new StorageException().Kind);
        Assert.Equal("Storage failed.", new StorageException().Message);
        Assert.Equal("x", new StorageException("x").Message);
        Assert.Equal(StorageErrorKind.Unknown, new StorageException("x").Kind);
        var inner = new InvalidOperationException();
        Assert.Same(inner, new StorageException("x", inner).InnerException);
        Assert.Equal(StorageErrorKind.Unknown, new StorageException("x", inner).Kind);
    }

    [Fact]
    public void The_error_kinds_are_the_documented_set()
    {
        Assert.Equal(
            ["DiskFull", "AccessDenied", "Locked", "NotLocalDisk", "CloudSynced", "NotWritable", "IntegrityFailed", "Corrupt", "NotFound", "Conflict", "Unknown"],
            Enum.GetNames<StorageErrorKind>());
    }

    [Fact]
    public void An_empty_report_has_no_repairs()
    {
        Assert.False(ReconcileReport.Empty.HasRepairs);
    }

    [Fact]
    public void A_report_has_repairs_when_any_count_or_problem_is_set()
    {
        var empty = ReconcileReport.Empty;

        Assert.True((empty with { StagingFilesDeleted = 1 }).HasRepairs);
        Assert.True((empty with { OrphanFilesDeleted = 1 }).HasRepairs);
        Assert.True((empty with { ArtifactProblems = 1 }).HasRepairs);
        Assert.True((empty with { CyclesMarkedUnrecoverable = 1 }).HasRepairs);
        Assert.True((empty with { CyclesClosedAsAppClosed = 1 }).HasRepairs);
        Assert.True((empty with { GuestCyclesDeleted = 1 }).HasRepairs);
        Assert.True((empty with { EventsDeleted = 1 }).HasRepairs);
        Assert.True((empty with { DiscardedFilesDeleted = 1 }).HasRepairs);
        Assert.True((empty with { OrphanThumbnailsDeleted = 1 }).HasRepairs);
        Assert.True((empty with { Problems = ["A Capture file is missing."] }).HasRepairs);
    }

    [Theory]
    [InlineData(DataPathStatus.NotLocalDisk, StorageErrorKind.NotLocalDisk)]
    [InlineData(DataPathStatus.CloudSynced, StorageErrorKind.CloudSynced)]
    [InlineData(DataPathStatus.NotWritable, StorageErrorKind.NotWritable)]
    public void A_refusal_maps_to_its_error_kind(DataPathStatus status, StorageErrorKind kind)
    {
        var verdict = new DataPathVerdict(status, "reason");

        Assert.False(verdict.IsOk);
        Assert.Equal(kind, verdict.ToErrorKind());
    }

    [Fact]
    public void An_ok_verdict_is_not_an_error()
    {
        var verdict = new DataPathVerdict(DataPathStatus.Ok, "Local disk.");

        Assert.True(verdict.IsOk);
        Assert.Throws<InvalidOperationException>(() => verdict.ToErrorKind());
    }

    [Fact]
    public void The_data_path_facts_record_what_the_probe_saw()
    {
        var facts = new DataPathFacts(DriveType.Fixed, false, true, "OneDrive", false, true);

        Assert.Equal(DriveType.Fixed, facts.DriveType);
        Assert.True(facts.IsUnderKnownCloudSyncRoot);
        Assert.Equal("OneDrive", facts.CloudSyncProvider);
    }
}
