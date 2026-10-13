using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace FotoHavn.Host.Windows.Logging;

/// <summary>
/// Writes one JSON object per line to the host log. The event name comes from the <see cref="EventId"/>, so
/// <c>HostLog.UiReady</c> writes <c>{"event":"ready", ...}</c>.
/// </summary>
internal sealed class JsonLinesLoggerProvider : ILoggerProvider
{
    private static readonly JsonWriterOptions WriterOptions = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    private readonly Lock _gate = new();
    private readonly StreamWriter _writer;
    private bool _disposed;

    public JsonLinesLoggerProvider(string logFile)
    {
        var stream = new FileStream(logFile, FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
        _writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)) { AutoFlush = true };
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

    private sealed class JsonLinesLogger(JsonLinesLoggerProvider provider, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            using var buffer = new MemoryStream();
            using (var json = new Utf8JsonWriter(buffer, WriterOptions))
            {
                json.WriteStartObject();
                json.WriteString("timestamp", DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture));
                json.WriteString("level", logLevel.ToString());
                json.WriteString("category", category);
                json.WriteString("event", eventId.Name ?? eventId.Id.ToString(CultureInfo.InvariantCulture));
                json.WriteString("message", formatter(state, exception));
                if (state is IReadOnlyList<KeyValuePair<string, object?>> properties)
                {
                    foreach (var (key, value) in properties)
                    {
                        if (key == "{OriginalFormat}" || key.Length == 0)
                        {
                            continue;
                        }

                        json.WritePropertyName(JsonNamingPolicy.CamelCase.ConvertName(key));
                        WriteValue(json, value);
                    }
                }

                if (exception is not null)
                {
                    json.WriteString("exception", exception.ToString());
                }

                json.WriteEndObject();
            }

            provider.WriteLine(Encoding.UTF8.GetString(buffer.ToArray()));
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
