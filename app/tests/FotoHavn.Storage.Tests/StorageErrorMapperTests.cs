using FotoHavn.Core.Storage;
using Microsoft.Data.Sqlite;

namespace FotoHavn.Storage.Tests;

public sealed class StorageErrorMapperTests
{
    [Theory]
    [InlineData(unchecked((int)0x80070070), StorageErrorKind.DiskFull)]
    [InlineData(unchecked((int)0x80070027), StorageErrorKind.DiskFull)]
    [InlineData(unchecked((int)0x80070020), StorageErrorKind.Locked)]
    [InlineData(unchecked((int)0x80070021), StorageErrorKind.Locked)]
    [InlineData(unchecked((int)0x80070005), StorageErrorKind.AccessDenied)]
    [InlineData(unchecked((int)0x80070002), StorageErrorKind.NotFound)]
    [InlineData(unchecked((int)0x80070003), StorageErrorKind.NotFound)]
    [InlineData(unchecked((int)0x80070057), StorageErrorKind.Unknown)]
    [InlineData(0, StorageErrorKind.Unknown)]
    public void An_io_exception_maps_by_its_hresult(int hResult, StorageErrorKind expected)
    {
        Assert.Equal(expected, StorageErrorMapper.Map(new IOException("x", hResult)));
    }

    [Fact]
    public void Unauthorized_access_is_access_denied()
    {
        Assert.Equal(StorageErrorKind.AccessDenied, StorageErrorMapper.Map(new UnauthorizedAccessException()));
    }

    [Fact]
    public void Missing_files_and_folders_are_not_found_whatever_their_hresult()
    {
        Assert.Equal(StorageErrorKind.NotFound, StorageErrorMapper.Map(new FileNotFoundException()));
        Assert.Equal(StorageErrorKind.NotFound, StorageErrorMapper.Map(new DirectoryNotFoundException()));
    }

    [Theory]
    [InlineData(13, StorageErrorKind.DiskFull)]
    [InlineData(5, StorageErrorKind.Locked)]
    [InlineData(6, StorageErrorKind.Locked)]
    [InlineData(11, StorageErrorKind.Corrupt)]
    [InlineData(26, StorageErrorKind.Corrupt)]
    [InlineData(14, StorageErrorKind.AccessDenied)]
    [InlineData(8, StorageErrorKind.NotWritable)]
    [InlineData(19, StorageErrorKind.Conflict)]
    [InlineData(1, StorageErrorKind.Unknown)]
    [InlineData(10, StorageErrorKind.Unknown)]
    public void A_sqlite_exception_maps_by_its_result_code(int code, StorageErrorKind expected)
    {
        Assert.Equal(expected, StorageErrorMapper.Map(new SqliteException("x", code)));
    }

    [Theory]
    [InlineData(13 | (2 << 8), StorageErrorKind.DiskFull)]
    [InlineData(5 | (1 << 8), StorageErrorKind.Locked)]
    [InlineData(19 | (1555 << 8), StorageErrorKind.Conflict)]
    public void An_extended_sqlite_code_maps_by_its_primary_code(int code, StorageErrorKind expected)
    {
        Assert.Equal(expected, StorageErrorMapper.Map(new SqliteException("x", code)));
    }

    [Fact]
    public void A_storage_exception_keeps_its_kind()
    {
        Assert.Equal(StorageErrorKind.Corrupt, StorageErrorMapper.Map(new StorageException(StorageErrorKind.Corrupt, "x")));
    }

    [Fact]
    public void Other_exceptions_are_unknown()
    {
        Assert.Equal(StorageErrorKind.Unknown, StorageErrorMapper.Map(new InvalidOperationException()));
        Assert.Equal(StorageErrorKind.Unknown, StorageErrorMapper.Map(new IOException("x")));
    }

    [Fact]
    public void Map_refuses_null()
    {
        Assert.Throws<ArgumentNullException>(() => StorageErrorMapper.Map(null!));
        Assert.Throws<ArgumentNullException>(() => StorageErrorMapper.ToStorageException(null!));
    }

    [Fact]
    public void Wrapping_keeps_the_cause_and_hides_the_path_from_the_message()
    {
        var cause = new IOException(@"The process cannot access the file 'C:\Users\Ana Lindqvist\Data\fotohavn.db'.", unchecked((int)0x80070020));

        var wrapped = StorageErrorMapper.ToStorageException(cause);

        Assert.Equal(StorageErrorKind.Locked, wrapped.Kind);
        Assert.Same(cause, wrapped.InnerException);
        Assert.DoesNotContain("Lindqvist", wrapped.Message, StringComparison.Ordinal);
        Assert.Equal(StorageErrorMapper.Describe(StorageErrorKind.Locked), wrapped.Message);
    }

    [Fact]
    public void Wrapping_a_storage_exception_returns_it_unchanged()
    {
        var original = new StorageException(StorageErrorKind.Conflict, "x");

        Assert.Same(original, StorageErrorMapper.ToStorageException(original));
    }

    [Fact]
    public void Every_kind_has_a_distinct_non_personal_message()
    {
        var messages = Enum.GetValues<StorageErrorKind>().Select(StorageErrorMapper.Describe).ToList();

        Assert.Equal(messages.Count, messages.Distinct().Count());
        Assert.All(messages, message => Assert.DoesNotContain('\\', message));
        Assert.Equal("Storage failed.", StorageErrorMapper.Describe(StorageErrorKind.Unknown));
    }
}
