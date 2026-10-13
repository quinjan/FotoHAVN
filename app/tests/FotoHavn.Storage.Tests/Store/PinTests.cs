using FotoHavn.Core.Storage;
using FotoHavn.Storage.Tests.Support;

namespace FotoHavn.Storage.Tests.Store;

public sealed class PinTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_PIN_is_stored_as_a_pbkdf2_hash_with_a_salt_and_at_least_210000_iterations()
    {
        await using var harness = await StoreHarness.OpenedAsync(hasher: new Pbkdf2PinHasher());
        var record = Sample.Event();
        await harness.Store.CreateEventAsync(record, Ct);

        await harness.Store.SetPinAsync(record.Id, "4821", Ct);

        var row = harness.Query("SELECT pin_hash, pin_salt, pin_iterations FROM events;", reader => (reader.GetFieldValue<byte[]>(0), reader.GetFieldValue<byte[]>(1), reader.GetInt32(2)))[0];
        Assert.Equal(Pbkdf2PinHasher.HashLength, row.Item1.Length);
        Assert.Equal(Pbkdf2PinHasher.SaltLength, row.Item2.Length);
        Assert.True(row.Item3 >= 210_000);
        Assert.DoesNotContain("4821", Convert.ToHexString(row.Item1), StringComparison.Ordinal);
        Assert.True((await harness.Store.GetEventAsync(record.Id, Ct))!.HasPin);
    }

    [Fact]
    public async Task Verify_accepts_the_PIN_and_rejects_another_and_a_changed_PIN_replaces_the_old()
    {
        await using var harness = await StoreHarness.OpenedAsync(hasher: new Pbkdf2PinHasher());
        var record = Sample.Event();
        await harness.Store.CreateEventAsync(record, Ct);
        await harness.Store.SetPinAsync(record.Id, "1234", Ct);

        Assert.True(await harness.Store.VerifyPinAsync(record.Id, "1234", Ct));
        Assert.False(await harness.Store.VerifyPinAsync(record.Id, "1235", Ct));
        Assert.False(await harness.Store.VerifyPinAsync(record.Id, "123", Ct));
        await harness.Store.SetPinAsync(record.Id, "9999", Ct);
        Assert.False(await harness.Store.VerifyPinAsync(record.Id, "1234", Ct));
        Assert.True(await harness.Store.VerifyPinAsync(record.Id, "9999", Ct));
    }

    [Fact]
    public async Task Two_Events_with_the_same_PIN_get_different_salts_and_hashes()
    {
        await using var harness = await StoreHarness.OpenedAsync(hasher: new Pbkdf2PinHasher());
        var first = Sample.Event();
        var second = Sample.Event();
        await harness.Store.CreateEventAsync(first, Ct);
        await harness.Store.CreateEventAsync(second, Ct);

        await harness.Store.SetPinAsync(first.Id, "1111", Ct);
        await harness.Store.SetPinAsync(second.Id, "1111", Ct);

        var rows = harness.Query("SELECT hex(pin_salt), hex(pin_hash) FROM events;", reader => (reader.GetString(0), reader.GetString(1)));
        Assert.NotEqual(rows[0].Item1, rows[1].Item1);
        Assert.NotEqual(rows[0].Item2, rows[1].Item2);
    }

    [Fact]
    public async Task Verify_is_false_when_no_PIN_is_set_and_a_PIN_survives_an_update()
    {
        await using var harness = await StoreHarness.OpenedAsync();
        var record = Sample.Event();
        await harness.Store.CreateEventAsync(record, Ct);

        Assert.False(await harness.Store.VerifyPinAsync(record.Id, "0000", Ct));
        await harness.Store.SetPinAsync(record.Id, "0000", Ct);
        await harness.Store.UpdateEventAsync(record with { Name = "Changed" }, Ct);

        Assert.True(await harness.Store.VerifyPinAsync(record.Id, "0000", Ct));
    }

    [Fact]
    public async Task PIN_calls_on_a_missing_or_deleting_Event_are_NotFound_and_an_empty_PIN_is_refused()
    {
        await using var harness = await StoreHarness.OpenedAsync();
        var record = Sample.Event();
        await harness.Store.CreateEventAsync(record, Ct);
        var missing = StorageIds.NewId();

        Assert.Equal(StorageErrorKind.NotFound, (await Assert.ThrowsAsync<StorageException>(() => harness.Store.SetPinAsync(missing, "1234", Ct))).Kind);
        Assert.Equal(StorageErrorKind.NotFound, (await Assert.ThrowsAsync<StorageException>(() => harness.Store.VerifyPinAsync(missing, "1234", Ct))).Kind);
        _ = await Assert.ThrowsAsync<ArgumentException>(() => harness.Store.SetPinAsync(record.Id, string.Empty, Ct));
        _ = await Assert.ThrowsAsync<ArgumentException>(() => harness.Store.VerifyPinAsync(record.Id, string.Empty, Ct));
        _ = await Assert.ThrowsAsync<ArgumentException>(() => harness.Store.SetPinAsync("bad", "1234", Ct));
        await harness.Store.MarkEventDeletingAsync(record.Id, Ct);
        Assert.Equal(StorageErrorKind.NotFound, (await Assert.ThrowsAsync<StorageException>(() => harness.Store.VerifyPinAsync(record.Id, "1234", Ct))).Kind);
        await harness.Store.DrainAsync(Ct);
    }
}
