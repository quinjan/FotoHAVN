using System.Globalization;
using FotoHavn.Protocol.Testing;

namespace FotoHavn.Host.Windows;

/// <summary>
/// The host command line. <c>--remote-debugging-port</c>, <c>--e2e-camera</c>, and
/// <c>--e2e-storage-fault &lt;diskFull|accessDenied|locked&gt;</c> are read in every build so the
/// command line stays the same, but only E2E builds act on them. <c>--webview2-runtime</c> names a Fixed Version
/// WebView2 runtime folder; without it the host uses <c>runtime/webview2</c> beside the executable when present, and
/// the Evergreen runtime otherwise.
/// <c>--smoke</c> (no value, read in every build) starts the app as usual and exits <c>HostExitCodes.Success</c> once the Operator UI has
/// finished its handshake, or <c>HostExitCodes.SmokeTimeout</c> if that takes too long: the field build's own check.
/// </summary>
internal sealed record HostOptions(string? DataFolder, int? RemoteDebuggingPort, string? E2ECamera, string? WebView2RuntimeFolder = null, StorageFaultKind? E2EStorageFault = null, bool Smoke = false)
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
                case "--smoke":
                    options = inlineValue is null
                        ? options with { Smoke = true }
                        : throw new HostOptionsException("Option '--smoke' takes no value.");
                    break;
                case "--e2e-storage-fault":
                    options = options with { E2EStorageFault = ParseFault(ReadValue(args, ref index, name, inlineValue)) };
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

    private static StorageFaultKind ParseFault(string value) =>
        Enum.TryParse<StorageFaultKind>(value, ignoreCase: true, out var kind) && Enum.IsDefined(kind) && !int.TryParse(value, out _)
            ? kind
            : throw new HostOptionsException($"'{value}' is not a storage fault for --e2e-storage-fault (diskFull, accessDenied, or locked).");

    private static int ParsePort(string value) =>
        int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var port) && port is > 0 and <= 65535
            ? port
            : throw new HostOptionsException($"'{value}' is not a valid port for --remote-debugging-port.");
}

internal sealed class HostOptionsException(string message) : Exception(message);
