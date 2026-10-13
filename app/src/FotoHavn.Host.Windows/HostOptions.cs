using System.Globalization;

namespace FotoHavn.Host.Windows;

/// <summary>
/// The host command line. <c>--remote-debugging-port</c> and <c>--e2e-camera</c> are read in every build so the
/// command line stays the same, but only E2E builds act on them. <c>--webview2-runtime</c> names a Fixed Version
/// WebView2 runtime folder; without it the host uses <c>runtime/webview2</c> beside the executable when present, and
/// the Evergreen runtime otherwise.
/// </summary>
internal sealed record HostOptions(string? DataFolder, int? RemoteDebuggingPort, string? E2ECamera, string? WebView2RuntimeFolder = null)
{
    public static HostOptions Default { get; } = new(null, null, null);

    public static HostOptions Parse(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);

        var options = Default;
        for (var index = 0; index < args.Count; index++)
        {
            var (name, inlineValue) = Split(args[index]);
            switch (name)
            {
                case "--data":
                    options = options with { DataFolder = ReadValue(args, ref index, name, inlineValue) };
                    break;
                case "--remote-debugging-port":
                    options = options with { RemoteDebuggingPort = ParsePort(ReadValue(args, ref index, name, inlineValue)) };
                    break;
                case "--e2e-camera":
                    options = options with { E2ECamera = ReadValue(args, ref index, name, inlineValue) };
                    break;
                case "--webview2-runtime":
                    options = options with { WebView2RuntimeFolder = ReadValue(args, ref index, name, inlineValue) };
                    break;
                default:
                    throw new HostOptionsException($"Unknown option '{args[index]}'.");
            }
        }

        return options;
    }

    private static (string Name, string? InlineValue) Split(string arg)
    {
        var separator = arg.IndexOf('=', StringComparison.Ordinal);
        return arg.StartsWith("--", StringComparison.Ordinal) && separator > 0
            ? (arg[..separator], arg[(separator + 1)..])
            : (arg, null);
    }

    private static string ReadValue(IReadOnlyList<string> args, ref int index, string name, string? inlineValue)
    {
        var value = inlineValue;
        if (value is null)
        {
            if (index + 1 >= args.Count)
            {
                throw new HostOptionsException($"Option '{name}' needs a value.");
            }

            value = args[++index];
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new HostOptionsException($"Option '{name}' needs a value.");
        }

        return value;
    }

    private static int ParsePort(string value) =>
        int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var port) && port is > 0 and <= 65535
            ? port
            : throw new HostOptionsException($"'{value}' is not a valid port for --remote-debugging-port.");
}

internal sealed class HostOptionsException(string message) : Exception(message);
