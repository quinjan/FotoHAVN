using System.Text.Json;
using FotoHavn.Protocol.Presentations;

namespace FotoHavn.Protocol.Tests;

public sealed class ProtocolJsonTests
{
    [Fact]
    public void Options_use_camel_case_property_names()
    {
        var json = JsonSerializer.Serialize(new { CommandType = "ready" }, ProtocolJson.Options);

        Assert.Equal("""{"commandType":"ready"}""", json);
    }

    [Fact]
    public void Options_are_read_only_and_compact()
    {
        Assert.True(ProtocolJson.Options.IsReadOnly);
        Assert.False(ProtocolJson.Options.WriteIndented);
    }

    [Fact]
    public void Enums_are_camel_case_strings()
    {
        Assert.Equal("\"guestCycleDetail\"", ProtocolJson.Serialize(OperatorSurface.GuestCycleDetail));
        Assert.Equal(GuestSurface.StartUnavailable, ProtocolJson.Deserialize<GuestSurface>("\"startUnavailable\""));
    }

    [Theory]
    [InlineData("1")]
    [InlineData("\"admin\"")]
    public void Enums_reject_numbers_and_unknown_names(string json)
    {
        Assert.ThrowsAny<JsonException>(() => ProtocolJson.Deserialize<Role>(json));
    }

    [Fact]
    public void Numbers_must_be_numbers()
    {
        Assert.ThrowsAny<JsonException>(() => ProtocolJson.Deserialize<SetCountdownPayload>("""{"seconds":"5"}"""));
    }

    [Fact]
    public void Nulls_are_written_so_snapshots_are_complete()
    {
        Assert.Equal(
            """{"surface":"start","start":null,"overlays":{"exitHold":null,"pinDialog":null,"phaseTimerWarning":null,"startOverConfirmation":null,"notice":null},"fatal":null}""",
            ProtocolJson.Serialize(new GuestPresentation(GuestSurface.Start, null, GuestOverlays.None, null)));
    }

    [Fact]
    public void Non_nullable_fields_reject_null_and_absence()
    {
        Assert.ThrowsAny<JsonException>(() => ProtocolJson.Deserialize<NoticeOverlay>("""{"message":null}"""));
        Assert.ThrowsAny<JsonException>(() => ProtocolJson.Deserialize<NoticeOverlay>("{}"));
        Assert.ThrowsAny<JsonException>(() => ProtocolJson.Deserialize<NoticeOverlay>("null"));
        Assert.ThrowsAny<JsonException>(() => ProtocolJson.Deserialize<NoticeOverlay>(JsonDocument.Parse("null").RootElement));
    }

    [Fact]
    public void Null_values_serialize_as_null()
    {
        Assert.Equal("null", ProtocolJson.Serialize<NoticeOverlay?>(null));
        Assert.Equal(System.Text.Json.JsonValueKind.Null, ProtocolJson.ToElement<NoticeOverlay?>(null).ValueKind);
    }

    [Fact]
    public void Dates_are_iso_8601()
    {
        var row = new EventListRow("e", "E", new DateOnly(2026, 1, 2), null, 0, new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero), false);

        Assert.Contains("\"date\":\"2026-01-02\"", ProtocolJson.Serialize(row), StringComparison.Ordinal);
        Assert.Contains("\"lastUsed\":\"2026-01-02T03:04:05+00:00\"", ProtocolJson.Serialize(row), StringComparison.Ordinal);
    }
}
