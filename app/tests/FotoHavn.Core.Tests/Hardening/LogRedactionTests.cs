using FotoHavn.Core.Hardening;
using FotoHavn.Protocol;
using Microsoft.Extensions.Logging;

namespace FotoHavn.Core.Tests.Hardening;

public sealed class LogRedactionTests
{
    private const string Data = @"C:\FOTOHAVN\Data";
    private const string Redacted = LogRedaction.RedactedText;

    private static ScrubbedLogEntry Scrub(string template, params (string Name, object? Value)[] properties)
    {
        List<KeyValuePair<string, object?>> state = [.. properties.Select(property => new KeyValuePair<string, object?>(property.Name, property.Value)), new(LogRedaction.OriginalFormatKey, template)];
        return LogRedaction.Scrub(state, Data);
    }

    // Ids

    [Theory]
    [InlineData("EventId", "6f1c2a8e-3b4d-4e5f-9a0b-1c2d3e4f5a6b")]
    [InlineData("EventId", "event-1")]
    [InlineData("GuestCycleId", "cycle-1")]
    [InlineData("CaptureId", "capture-20261014-071910-123.jpg")]
    [InlineData("TemplateId", "signature-strip")]
    [InlineData("LookId", "naturale-v1")]
    [InlineData("eventid", "event-1")]
    public void Ids_pass_through(string name, string id)
    {
        Assert.Equal(id, LogRedaction.ScrubValue(name, id, Data));
    }

    [Theory]
    [InlineData("Lindqvist wedding")]
    [InlineData("Ana Lindqvist")]
    [InlineData("")]
    [InlineData("-starts-with-a-dash")]
    [InlineData("a/b")]
    [InlineData("0123456789012345678901234567890123456789012345678901234567890123456789")]
    public void Something_that_is_not_an_id_is_redacted_even_in_an_id_property(string notAnId)
    {
        Assert.Equal(Redacted, LogRedaction.ScrubValue("EventId", notAnId, Data));
    }

    [Fact]
    public void A_guid_value_passes_as_an_id()
    {
        var id = Guid.NewGuid();

        Assert.Equal(id, LogRedaction.ScrubValue("EventId", id, Data));
    }

    // What may never be logged

    [Theory]
    [InlineData("EventName")]
    [InlineData("Name")]
    [InlineData("Customer")]
    [InlineData("Venue")]
    [InlineData("Location")]
    [InlineData("Notes")]
    [InlineData("Pin")]
    [InlineData("Digits")]
    [InlineData("Password")]
    [InlineData("Message")]
    [InlineData("Detail")]
    [InlineData("Content")]
    [InlineData("Image")]
    [InlineData("Bytes")]
    [InlineData("GuestName")]
    [InlineData("SomethingNobodyListed")]
    public void A_property_that_is_not_listed_is_redacted(string name)
    {
        Assert.Equal(LogFieldKind.Redacted, LogRedaction.Classify(name));
        Assert.Equal(Redacted, LogRedaction.ScrubValue(name, "Lindqvist wedding", Data));
    }

    [Fact]
    public void A_redacted_property_never_reaches_the_message_or_the_properties()
    {
        var entry = Scrub("Saved {EventName} for {Customer} (PIN {Pin}) as {EventId}", ("EventName", "Lindqvist wedding"), ("Customer", "Ana Lindqvist"), ("Pin", "9137"), ("EventId", "event-1"));

        Assert.Equal($"Saved {Redacted} for {Redacted} (PIN {Redacted}) as event-1", entry.Message);
        var everything = entry.Message + string.Concat(entry.Properties.Select(property => property.Key + "=" + property.Value));
        Assert.DoesNotContain("Lindqvist", everything, StringComparison.Ordinal);
        Assert.DoesNotContain("9137", everything, StringComparison.Ordinal);
    }

    [Fact]
    public void A_redacted_value_of_any_type_is_replaced()
    {
        Assert.Equal(Redacted, LogRedaction.ScrubValue("Pin", 9137, Data));
        Assert.Equal(Redacted, LogRedaction.ScrubValue("Content", new byte[] { 1, 2, 3 }, Data));
    }

    [Fact]
    public void Null_stays_null_in_every_kind()
    {
        foreach (var name in new[] { "EventId", "Role", "Uri", "Path", "DataFolder", "Pin" })
        {
            Assert.Null(LogRedaction.ScrubValue(name, null, Data));
        }
    }

    // Plain values

    [Fact]
    public void Numbers_flags_and_enums_pass_untouched()
    {
        Assert.Equal(30.5, LogRedaction.ScrubValue("Fps", 30.5, Data));
        Assert.Equal(1920, LogRedaction.ScrubValue("Width", 1920, Data));
        Assert.Equal(true, LogRedaction.ScrubValue("SingleScreen", true, Data));
        Assert.Equal(Role.Guest, LogRedaction.ScrubValue("Role", Role.Guest, Data));
        Assert.Equal(-2147024891, LogRedaction.ScrubValue("HResult", -2147024891, Data));
    }

    [Fact]
    public void Plain_text_loses_control_characters_and_is_cut_to_length()
    {
        Assert.Equal("a  b c", LogRedaction.ScrubValue("Check", "a\r\nb\tc", Data));
        var cut = (string)LogRedaction.ScrubValue("Check", new string('x', LogRedaction.MaxTextLength + 50), Data)!;
        Assert.Equal(LogRedaction.MaxTextLength + 1, cut.Length);
        Assert.EndsWith("…", cut, StringComparison.Ordinal);
    }

    [Fact]
    public void An_object_is_written_as_its_text()
    {
        Assert.Equal("{ X = 1 }", LogRedaction.ScrubValue("Bounds", new { X = 1 }, Data));
    }

    [Fact]
    public void Install_folders_are_written_in_full()
    {
        Assert.Equal(@"C:\FOTOHAVN\Data", LogRedaction.ScrubValue("DataFolder", @"C:\FOTOHAVN\Data", Data));
        Assert.Equal(@"C:\app\runtime\webview2", LogRedaction.ScrubValue("Runtime", @"C:\app\runtime\webview2", Data));
        Assert.Equal(LogFieldKind.InstallPath, LogRedaction.Classify("UserDataFolder"));
    }

    // Paths

    [Theory]
    [InlineData(@"C:\FOTOHAVN\Data\Events\event-1", "Events/event-1")]
    [InlineData(@"c:\fotohavn\data\Events\event-1\guest-cycles\cycle-1\capture-1.jpg", "Events/event-1/guest-cycles/cycle-1/capture-1.jpg")]
    [InlineData(@"C:\FOTOHAVN\Data\", ".")]
    [InlineData(@"C:\FOTOHAVN\Data", ".")]
    [InlineData(@"C:/FOTOHAVN/Data/Cache/thumbnails/a.jpg", "Cache/thumbnails/a.jpg")]
    public void A_path_inside_the_Data_folder_is_written_relative_to_it(string path, string expected)
    {
        Assert.Equal(expected, LogRedaction.ToDataRelativePath(Data, path));
        Assert.Equal(expected, LogRedaction.ScrubValue("Path", path, Data));
    }

    [Theory]
    [InlineData(@"C:\Users\ana\Pictures\wedding.jpg")]
    [InlineData(@"C:\FOTOHAVN\Data2\Events")]
    [InlineData(@"C:\FOTOHAVN\Data\..\Secrets\a.txt")]
    [InlineData(@"D:\FOTOHAVN\Data\Events")]
    public void A_path_outside_the_Data_folder_is_replaced(string path)
    {
        Assert.Equal(LogRedaction.OutsideDataFolder, LogRedaction.ToDataRelativePath(Data, path));
    }

    [Theory]
    [InlineData(null, @"C:\a")]
    [InlineData("", @"C:\a")]
    [InlineData(Data, "   ")]
    [InlineData(Data, "bad\0path")]
    public void An_unusable_path_is_redacted(string? dataFolder, string path)
    {
        Assert.Equal(Redacted, LogRedaction.ToDataRelativePath(dataFolder, path));
    }

    [Fact]
    public void A_null_path_is_null()
    {
        Assert.Null(LogRedaction.ToDataRelativePath(Data, null));
    }

    [Fact]
    public void Without_a_Data_folder_every_path_value_is_redacted()
    {
        Assert.Equal(Redacted, LogRedaction.ScrubValue("Path", @"C:\FOTOHAVN\Data\Events", null));
    }

    // Addresses

    [Theory]
    [InlineData("https://app.fotohavn/index.html?pin=9137#top", "https://app.fotohavn/index.html")]
    [InlineData("https://user:secret@media.fotohavn:8443/events/a.jpg?x=1", "https://media.fotohavn:8443/events/a.jpg")]
    [InlineData("mailto:ana@example.com", "mailto://example.com")]
    public void An_address_loses_its_user_info_query_and_fragment(string uri, string expected)
    {
        Assert.Equal(expected, LogRedaction.ScrubValue("Uri", uri, Data));
        Assert.Equal(expected, LogRedaction.ScrubValue("Source", uri, Data));
    }

    [Fact]
    public void Something_that_is_not_an_address_is_redacted()
    {
        Assert.Equal(Redacted, LogRedaction.ScrubValue("Uri", "Lindqvist wedding", Data));
    }

    // Rendering

    [Fact]
    public void The_message_is_rendered_from_the_redacted_values()
    {
        var entry = Scrub("Opening {Count} displays for {Role} on {DisplayId}: {Missing}", ("Count", 2), ("Role", Role.Guest), ("DisplayId", "virtual-guest"));

        Assert.Equal($"Opening 2 displays for Guest on virtual-guest: {Redacted}", entry.Message);
        Assert.Equal(["Count", "Role", "DisplayId"], entry.Properties.Select(property => property.Key));
    }

    [Fact]
    public void Escaped_braces_alignment_formats_and_destructuring_marks_are_understood()
    {
        var entry = Scrub("{{literal}} {Count,5} {Fps:N1} {@Role} {$Kind} }", ("Count", 7), ("Fps", 29.5), ("Role", Role.Operator), ("Kind", "diskFull"));

        Assert.Equal("{literal} 7 29.5 Operator diskFull }", entry.Message);
    }

    [Fact]
    public void A_null_value_renders_as_null()
    {
        Assert.Equal("Reason (null)", Scrub("Reason {Reason}", ("Reason", null)).Message);
    }

    [Fact]
    public void An_unclosed_brace_is_kept_as_text()
    {
        Assert.Equal("a { b", Scrub("a { b").Message);
    }

    [Fact]
    public void State_without_a_template_has_its_message_redacted()
    {
        var entry = LogRedaction.Scrub([new KeyValuePair<string, object?>("EventId", "event-1"), new KeyValuePair<string, object?>("", "x")], Data);

        Assert.Equal(Redacted, entry.Message);
        Assert.Equal([new KeyValuePair<string, object?>("EventId", "event-1")], entry.Properties);
    }

    [Fact]
    public void The_first_value_of_a_repeated_name_fills_the_message()
    {
        Assert.Equal("event-1 event-1", Scrub("{EventId} {EventId}", ("EventId", "event-1"), ("EventId", "event-2")).Message);
    }

    [Fact]
    public void A_message_logged_with_LoggerMessage_is_scrubbed_end_to_end()
    {
        var provider = new CapturingProvider();
        var logger = provider.CreateLogger("test");

        BrainLog.EventEnded(logger, "6f1c2a8e-3b4d-4e5f-9a0b-1c2d3e4f5a6b");
        BrainLog.EventEnded(logger, "Lindqvist wedding");

        Assert.Equal("Event 6f1c2a8e-3b4d-4e5f-9a0b-1c2d3e4f5a6b ended.", LogRedaction.Scrub(provider.States[0], Data).Message);
        var scrubbed = LogRedaction.Scrub(provider.States[1], Data);
        Assert.Equal($"Event {Redacted} ended.", scrubbed.Message);
        Assert.DoesNotContain("Lindqvist", scrubbed.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Classify_and_Scrub_refuse_null()
    {
        Assert.Throws<ArgumentNullException>(() => LogRedaction.Classify(null!));
        Assert.Throws<ArgumentNullException>(() => LogRedaction.Scrub(null!, Data));
        Assert.Throws<ArgumentNullException>(() => LogRedaction.DescribeException(null!));
    }

    // Exceptions

    [Fact]
    public void An_exception_is_described_without_its_message()
    {
        var exception = Throw(new InvalidOperationException("Could not read C:\\Users\\ana\\Lindqvist wedding\\IMG_0001.jpg"));

        var text = LogRedaction.DescribeException(exception);

        Assert.StartsWith("System.InvalidOperationException", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Lindqvist", text, StringComparison.Ordinal);
        Assert.DoesNotContain("IMG_0001", text, StringComparison.Ordinal);
        Assert.Contains(nameof(Throw), text, StringComparison.Ordinal);
        Assert.DoesNotContain(".cs:line", text, StringComparison.Ordinal);
    }

    [Fact]
    public void An_exception_shows_its_HRESULT_and_inner_exceptions()
    {
        var exception = Throw(new IOException("secret", new UnauthorizedAccessException("also secret")));

        var text = LogRedaction.DescribeException(exception);

        Assert.Contains("System.IO.IOException (HRESULT 0x", text, StringComparison.Ordinal);
        Assert.Contains("System.UnauthorizedAccessException (HRESULT 0x80070005)", text, StringComparison.Ordinal);
        Assert.DoesNotContain("secret", text, StringComparison.Ordinal);
    }

    [Fact]
    public void An_aggregate_exception_lists_every_inner_exception()
    {
        var text = LogRedaction.DescribeException(new AggregateException("secret", new InvalidOperationException("one"), new TimeoutException("two")));

        Assert.Contains("System.InvalidOperationException", text, StringComparison.Ordinal);
        Assert.Contains("System.TimeoutException", text, StringComparison.Ordinal);
        Assert.DoesNotContain("secret", text, StringComparison.Ordinal);
    }

    [Fact]
    public void A_very_deep_chain_of_inner_exceptions_stops()
    {
        Exception chain = new InvalidOperationException("0");
        for (var depth = 1; depth < 30; depth++)
        {
            chain = new InvalidOperationException(depth.ToString(System.Globalization.CultureInfo.InvariantCulture), chain);
        }

        var lines = LogRedaction.DescribeException(chain).Split(Environment.NewLine).Count(line => line.TrimStart().StartsWith("System.InvalidOperationException", StringComparison.Ordinal));

        Assert.Equal(9, lines);
    }

    private static Exception Throw(Exception exception)
    {
        try
        {
            throw exception;
        }
        catch (Exception caught)
        {
            return caught;
        }
    }

    private sealed class CapturingProvider : ILoggerProvider
    {
        public List<IReadOnlyList<KeyValuePair<string, object?>>> States { get; } = [];

        public ILogger CreateLogger(string categoryName) => new Capturing(this);

        public void Dispose()
        {
        }

        private sealed class Capturing(CapturingProvider owner) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
                owner.States.Add((IReadOnlyList<KeyValuePair<string, object?>>)state!);
        }
    }
}
