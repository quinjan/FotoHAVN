using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using FotoHavn.Core.Hardening;
using FotoHavn.Host.Windows.Logging;
using Microsoft.Extensions.Logging;

namespace FotoHavn.Host.Windows.Tests;

/// <summary>The host log (M10): redacted by allow-list, with the Event and Guest Cycle ids from the brain's scope, and never a personal value or an exception message.</summary>
public sealed partial class JsonLinesLoggerProviderTests
{
    private const string EventName = "Studio open day";
    private const string Pin = "9137";
    private const string Venue = "Harbour Hall";
    private static readonly string DataFolder = Path.Combine(Path.GetTempPath(), "fotohavn-log-test", "Data");

    private static (ILogger Logger, StringWriter Output, JsonLinesLoggerProvider Provider) Create(string category = "FotoHavn.Test")
    {
        var output = new StringWriter();
        var provider = new JsonLinesLoggerProvider(output, DataFolder);
        return (provider.CreateLogger(category), output, provider);
    }

    private static void Write(ILogger logger, string template, Exception? exception, params (string Name, object? Value)[] properties)
    {
        List<KeyValuePair<string, object?>> state = [.. properties.Select(property => new KeyValuePair<string, object?>(property.Name, property.Value)), new("{OriginalFormat}", template)];
        logger.Log(LogLevel.Information, new EventId(1, "test"), (IReadOnlyList<KeyValuePair<string, object?>>)state, exception, static (_, _) => "UNREDACTED MESSAGE");
    }

    private static JsonElement Single(StringWriter output)
    {
        var line = Assert.Single(output.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries));
        return JsonDocument.Parse(line).RootElement.Clone();
    }

    [Fact]
    public void A_line_is_one_JSON_object_with_the_event_name_and_the_redacted_message()
    {
        var (logger, output, provider) = Create();

        Write(logger, "Event {EventId} ended after {Count} cycles", null, ("EventId", "event-1"), ("Count", 3));
        provider.Dispose();

        var line = Single(output);
        Assert.Equal("Information", line.GetProperty("level").GetString());
        Assert.Equal("FotoHavn.Test", line.GetProperty("category").GetString());
        Assert.Equal("test", line.GetProperty("event").GetString());
        Assert.Equal("Event event-1 ended after 3 cycles", line.GetProperty("message").GetString());
        Assert.Equal("event-1", line.GetProperty("eventId").GetString());
        Assert.Equal(3, line.GetProperty("count").GetInt32());
    }

    [Fact]
    public void An_Event_name_a_PIN_and_a_venue_never_reach_the_file()
    {
        var (logger, output, provider) = Create();

        Write(
            logger,
            "Starting {EventName} for {Venue} with PIN {Pin}: {Detail} at {Path}",
            null,
            ("EventName", EventName),
            ("Venue", Venue),
            ("Pin", Pin),
            ("Detail", $"{EventName} {Venue} {Pin}"),
            ("Path", Path.Combine(Path.GetTempPath(), EventName, "photo.jpg")));
        provider.Dispose();

        var text = output.ToString();
        Assert.DoesNotContain(EventName, text, StringComparison.Ordinal);
        Assert.DoesNotContain(Venue, text, StringComparison.Ordinal);
        Assert.DoesNotContain(Pin, text, StringComparison.Ordinal);
        Assert.Contains("Starting [redacted] for [redacted] with PIN [redacted]: [redacted] at [outside the Data folder]", text, StringComparison.Ordinal);
    }

    [Fact]
    public void A_path_inside_the_Data_folder_is_written_relative_to_it_and_an_address_loses_its_query()
    {
        var (logger, output, provider) = Create();

        Write(
            logger,
            "{Path} {Uri} {Source}",
            null,
            ("Path", Path.Combine(DataFolder, "Events", "event-1", "strip.jpg")),
            ("Uri", "https://user:secret@app.fotohavn/media/a.jpg?name=Studio+open+day&pin=9137#top"),
            ("Source", "not an address"));
        provider.Dispose();

        var line = Single(output);
        Assert.Equal("Events/event-1/strip.jpg https://app.fotohavn/media/a.jpg [redacted]", line.GetProperty("message").GetString());
        Assert.DoesNotContain("secret", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void An_exception_is_written_as_its_type_and_frames_never_its_message()
    {
        var (logger, output, provider) = Create();
        Exception thrown;
        try
        {
            throw new InvalidOperationException($"Could not save {EventName} ({Pin}) in {Venue}");
        }
        catch (InvalidOperationException exception)
        {
            thrown = exception;
        }

        Write(logger, "Saving failed", thrown);
        provider.Dispose();

        var line = Single(output);
        var text = line.GetProperty("exception").GetString()!;
        Assert.StartsWith("System.InvalidOperationException", text, StringComparison.Ordinal);
        Assert.DoesNotContain(EventName, output.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(Pin, output.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(Venue, output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void A_line_with_no_template_is_written_without_its_message()
    {
        var (logger, output, provider) = Create();

        logger.Log(LogLevel.Warning, default, $"{EventName} {Pin}", null, static (state, _) => state);
        provider.Dispose();

        var line = Single(output);
        Assert.Equal("[redacted]", line.GetProperty("message").GetString());
        Assert.DoesNotContain(EventName, output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void The_scope_of_the_brain_adds_the_Event_and_Guest_Cycle_ids_to_each_line()
    {
        var (logger, output, provider) = Create();

        using (logger.BeginScope(new List<KeyValuePair<string, object?>> { new("EventId", "event-1"), new("GuestCycleId", "cycle-7") }))
        {
            Write(logger, "A Capture was saved", null);
        }

        Write(logger, "After the scope", null);
        provider.Dispose();

        var lines = output.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries).Select(text => JsonDocument.Parse(text).RootElement).ToArray();
        Assert.Equal("event-1", lines[0].GetProperty("eventId").GetString());
        Assert.Equal("cycle-7", lines[0].GetProperty("guestCycleId").GetString());
        Assert.False(lines[1].TryGetProperty("eventId", out _));
        Assert.False(lines[1].TryGetProperty("guestCycleId", out _));
    }

    [Fact]
    public void Nested_scopes_merge_and_a_property_of_the_line_wins_over_the_scope()
    {
        var (logger, output, provider) = Create();

        using (logger.BeginScope(new List<KeyValuePair<string, object?>> { new("EventId", "event-outer") }))
        using (logger.BeginScope(new List<KeyValuePair<string, object?>> { new("GuestCycleId", "cycle-inner"), new("EventId", "event-inner") }))
        {
            Write(logger, "Event {EventId}", null, ("EventId", "event-own"));
        }

        provider.Dispose();

        var raw = output.ToString();
        var line = Single(output);
        Assert.Equal("event-own", line.GetProperty("eventId").GetString());
        Assert.Equal("cycle-inner", line.GetProperty("guestCycleId").GetString());
        Assert.Equal(1, Regex.Count(raw, "\"eventId\""));
    }

    [Fact]
    public void The_values_of_a_scope_are_redacted_like_any_other()
    {
        var (logger, output, provider) = Create();

        using (logger.BeginScope(new List<KeyValuePair<string, object?>> { new("EventId", EventName), new("GuestCycleId", "cycle 1; drop table"), new("EventName", EventName) }))
        {
            Write(logger, "Something happened", null);
        }

        provider.Dispose();

        var line = Single(output);
        Assert.Equal("[redacted]", line.GetProperty("eventId").GetString());
        Assert.Equal("[redacted]", line.GetProperty("guestCycleId").GetString());
        Assert.DoesNotContain(EventName, output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void A_scope_that_is_not_a_property_list_is_ignored()
    {
        var (logger, _, provider) = Create();

        using var scope = logger.BeginScope("just text");

        Assert.Null(scope);
        provider.Dispose();
    }

    [Fact]
    public void Logging_after_the_provider_is_disposed_writes_nothing_and_does_not_throw()
    {
        var (logger, output, provider) = Create();
        provider.Dispose();

        Write(logger, "Late", null);

        Assert.Empty(output.ToString());
        Assert.False(logger.IsEnabled(LogLevel.None));
        Assert.True(logger.IsEnabled(LogLevel.Trace));
    }

    [Fact]
    public void A_property_cannot_overwrite_the_fixed_fields_and_text_values_are_JSON_strings()
    {
        var (logger, output, provider) = Create();

        Write(logger, "{Count} {Reason} {Role}", null, ("Count", 7L), ("Reason", "message"), ("Role", FotoHavn.Protocol.Role.Guest), ("Scale", 1.5), ("HooksCompiled", true), ("Detail", null));
        provider.Dispose();

        var line = Single(output);
        Assert.Equal("7 message Guest", line.GetProperty("message").GetString());
        Assert.Equal(7, line.GetProperty("count").GetInt64());
        Assert.Equal("Guest", line.GetProperty("role").GetString());
        Assert.Equal(1.5, line.GetProperty("scale").GetDouble());
        Assert.True(line.GetProperty("hooksCompiled").GetBoolean());
        Assert.Equal(JsonValueKind.Null, line.GetProperty("detail").ValueKind);
    }

    [Fact]
    public void The_file_provider_appends_to_the_log_file()
    {
        var folder = Path.Combine(Path.GetTempPath(), "fotohavn-log-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var file = Path.Combine(folder, "fotohavn.jsonl");
        try
        {
            for (var run = 0; run < 2; run++)
            {
                using var provider = new JsonLinesLoggerProvider(file, folder);
                Write(provider.CreateLogger("File"), "Run {Count}", null, ("Count", run));
            }

            Assert.Equal(2, File.ReadAllLines(file).Length);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [GeneratedRegex(@"\{(\w+)\}")]
    private static partial Regex Placeholder();

    private static IEnumerable<MethodInfo> HostLogMethods() =>
        typeof(HostLog).GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(method => method.GetCustomAttribute<LoggerMessageAttribute>() is not null);

    [Fact]
    public void Every_placeholder_of_every_host_log_message_is_on_the_allow_list()
    {
        var methods = HostLogMethods().ToArray();
        Assert.True(methods.Length >= 40);

        foreach (var method in methods)
        {
            var template = method.GetCustomAttribute<LoggerMessageAttribute>()!.Message!;
            foreach (Match placeholder in Placeholder().Matches(template))
            {
                Assert.True(
                    LogRedaction.Classify(placeholder.Groups[1].Value) != LogFieldKind.Redacted,
                    $"{method.Name}: {{{placeholder.Groups[1].Value}}} is not on the log allow-list, so it would be written as [redacted].");
            }
        }
    }

    [Fact]
    public void No_host_log_message_writes_a_hostile_value_passed_as_a_path_address_or_free_text()
    {
        var hostile = $"{EventName} {Pin} {Venue}";
        var (logger, output, provider) = Create();
        foreach (var method in HostLogMethods())
        {
            var arguments = method.GetParameters().Select(parameter => HostileArgument(parameter, logger, hostile)).ToArray();
            method.Invoke(null, arguments);
        }

        provider.Dispose();

        var text = output.ToString();
        Assert.True(text.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries).Length >= 40);
        Assert.DoesNotContain(EventName, text, StringComparison.Ordinal);
        Assert.DoesNotContain(Pin, text, StringComparison.Ordinal);
        Assert.DoesNotContain(Venue, text, StringComparison.Ordinal);
    }

    private static object? HostileArgument(ParameterInfo parameter, ILogger logger, string hostile)
    {
        var type = parameter.ParameterType;
        if (type == typeof(ILogger))
        {
            return logger;
        }

        if (type == typeof(Exception))
        {
            return new InvalidOperationException(hostile);
        }

        if (type == typeof(string))
        {
            // A name the allow-list writes as it is (a hardware name, a version) is not hostile input; the others are.
            var kind = LogRedaction.Classify(char.ToUpperInvariant(parameter.Name![0]) + parameter.Name[1..]);
            return kind is LogFieldKind.Redacted or LogFieldKind.DataPath or LogFieldKind.Uri ? hostile : "benign";
        }

        if (type == typeof(Displays.DisplaySnapshot))
        {
            return Displays.DisplaySnapshot.Empty;
        }

        return type.IsValueType ? Activator.CreateInstance(type) : null;
    }
}
