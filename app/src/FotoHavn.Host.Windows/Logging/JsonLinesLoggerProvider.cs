using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using FotoHavn.Core.Hardening;
using Microsoft.Extensions.Logging;

namespace FotoHavn.Host.Windows.Logging;

/// <summary>
/// Writes one JSON object per line to the host log. The event name comes from the <see cref="EventId"/>, so
/// <c>HostLog.UiReady</c> writes <c>{"event":"ready", ...}</c>. Every line passes through <see cref="LogRedaction"/> (M10): properties
/// are written by their allow-listed kind, the message is rendered again from the redacted values, and an exception is written as its
/// type and stack frames, never its message. A logging scope of <c>EventId</c> and <c>GuestCycleId</c> (the brain's) is merged into each
/// line as <c>eventId</c> and <c>guestCycleId</c>.
/// </summary>
internal sealed class JsonLinesLoggerProvider : ILoggerProvider
{
    private static readonly JsonWriterOptions WriterOptions = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    private readonly Lock _gate = new();
    private readonly TextWriter _writer;
    private readonly string? _dataFolder;
    private readonly AsyncLocal<ScopeNode?> _scope = new();
    private bool _disposed;

    public JsonLinesLoggerProvider(string logFile, string? dataFolder)
        : this(OpenAppend(logFile), dataFolder)
    {
    }

    /// <param name="writer">Receives one line per entry; the provider disposes it.</param>
    /// <param name="dataFolder">The Data folder, so path properties are written relative to it.</param>
    public JsonLinesLoggerProvider(TextWriter writer, string? dataFolder)
    {
        ArgumentNullException.ThrowIfNull(writer);
        _writer = writer;
        _dataFolder = dataFolder;
    }

    public ILogger CreateLogger(string categoryName) => new JsonLinesLogger(this, categoryName);

    public void Dispose()
    {
        lock (_gate)
        {
            _disposed = true;
            _writer.Dispose();
        }
    }

    private static StreamWriter OpenAppend(string logFile)
    {
        var stream = new FileStream(logFile, FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
        return new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)) { AutoFlush = true };
    }

    private void WriteLine(string line)
    {
        lock (_gate)
        {
            if (!_disposed)
            {
                _writer.WriteLine(line);
            }
        }
    }

    private ScopeNode BeginScope(KeyValuePair<string, object?>[] properties)
    {
        var node = new ScopeNode(properties, _scope.Value, this);
        _scope.Value = node;
        return node;
    }

    private sealed class ScopeNode(KeyValuePair<string, object?>[] properties, ScopeNode? parent, JsonLinesLoggerProvider provider) : IDisposable
    {
        public KeyValuePair<string, object?>[] Properties { get; } = properties;

        public ScopeNode? Parent { get; } = parent;

        public void Dispose() => provider._scope.Value = Parent;
    }

    private sealed class JsonLinesLogger(JsonLinesLoggerProvider provider, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull =>
            state is IEnumerable<KeyValuePair<string, object?>> properties ? provider.BeginScope([.. properties]) : null;

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            var entry = state is IReadOnlyList<KeyValuePair<string, object?>> properties
                ? LogRedaction.Scrub(properties, provider._dataFolder)
                : new ScrubbedLogEntry(LogRedaction.RedactedText, []);

            using var buffer = new MemoryStream();
            using (var json = new Utf8JsonWriter(buffer, WriterOptions))
            {
                json.WriteStartObject();
                json.WriteString("timestamp", DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture));
                json.WriteString("level", logLevel.ToString());
                json.WriteString("category", category);
                json.WriteString("event", eventId.Name ?? eventId.Id.ToString(CultureInfo.InvariantCulture));
                json.WriteString("message", entry.Message);
                var written = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "timestamp", "level", "category", "event", "message", "exception" };
                foreach (var (key, value) in entry.Properties)
                {
                    WriteProperty(json, written, key, value);
                }

                for (var scope = provider._scope.Value; scope is not null; scope = scope.Parent)
                {
                    foreach (var (key, value) in scope.Properties)
                    {
                        WriteProperty(json, written, key, LogRedaction.ScrubValue(key, value, provider._dataFolder));
                    }
                }

                if (exception is not null)
                {
                    json.WriteString("exception", LogRedaction.DescribeException(exception));
                }

                json.WriteEndObject();
            }

            provider.WriteLine(Encoding.UTF8.GetString(buffer.ToArray()));
        }

        private static void WriteProperty(Utf8JsonWriter json, HashSet<string> written, string key, object? value)
        {
            var name = JsonNamingPolicy.CamelCase.ConvertName(key);
            if (name.Length == 0 || !written.Add(name))
            {
                return;
            }

            json.WritePropertyName(name);
            WriteValue(json, value);
        }

        private static void WriteValue(Utf8JsonWriter json, object? value)
        {
            switch (value)
            {
                case null:
                    json.WriteNullValue();
                    break;
                case bool flag:
                    json.WriteBooleanValue(flag);
                    break;
                case int number:
                    json.WriteNumberValue(number);
                    break;
                case long number:
                    json.WriteNumberValue(number);
                    break;
                case double number:
                    json.WriteNumberValue(number);
                    break;
                default:
                    json.WriteStringValue(Convert.ToString(value, CultureInfo.InvariantCulture));
                    break;
            }
        }
    }
}
