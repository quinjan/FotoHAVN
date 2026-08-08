using Microsoft.Win32;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;

namespace FotoHavn.UiVerificationHost;

public sealed record PinnedEnvironment(
    int WindowsBuild,
    string OsArchitecture,
    string ProcessArchitecture,
    string DotnetSdk,
    string Culture,
    string UiCulture,
    int Dpi,
    string Theme,
    string FontSmoothing)
{
    public static EnvironmentEvidence Verify(string repositoryRoot, bool allowDrift)
    {
        var path = Path.Combine(repositoryRoot, "tools", "FotoHavn.UiVerificationHost", "pinned-environment.json");
        var pinned = JsonSerializer.Deserialize<PinnedEnvironment>(File.ReadAllText(path), JsonOptions)
            ?? throw new InvalidDataException("The pinned environment manifest is empty.");
        var actual = Capture();
        var differences = new List<string>();
        Compare(nameof(WindowsBuild), pinned.WindowsBuild, actual.WindowsBuild, differences);
        Compare(nameof(OsArchitecture), pinned.OsArchitecture, actual.OsArchitecture, differences);
        Compare(nameof(ProcessArchitecture), pinned.ProcessArchitecture, actual.ProcessArchitecture, differences);
        Compare(nameof(DotnetSdk), pinned.DotnetSdk, actual.DotnetSdk, differences);
        Compare(nameof(Culture), pinned.Culture, actual.Culture, differences);
        Compare(nameof(UiCulture), pinned.UiCulture, actual.UiCulture, differences);
        Compare(nameof(Dpi), pinned.Dpi, actual.Dpi, differences);
        Compare(nameof(Theme), pinned.Theme, actual.Theme, differences);
        Compare(nameof(FontSmoothing), pinned.FontSmoothing, actual.FontSmoothing, differences);
        if (differences.Count > 0 && !allowDrift)
        {
            throw new InvalidOperationException(
                "This machine does not match pinned-environment.json:" + Environment.NewLine +
                string.Join(Environment.NewLine, differences.Select(item => $"- {item}")));
        }

        return new(
            pinned,
            actual,
            differences,
            Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant(),
            differences.Count == 0);
    }

    private static PinnedEnvironment Capture()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("The FotoHAVN UI verification host requires Windows.");
        }

        using var personalization = Registry.CurrentUser.OpenSubKey(
            @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
        var lightTheme = personalization?.GetValue("AppsUseLightTheme") as int? != 0;
        using var desktop = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop");
        var smoothing = desktop?.GetValue("FontSmoothing")?.ToString() == "2" &&
            desktop.GetValue("FontSmoothingType")?.ToString() == "2"
                ? "ClearType"
                : "Other";

        return new(
            Environment.OSVersion.Version.Build,
            RuntimeInformation.OSArchitecture.ToString(),
            RuntimeInformation.ProcessArchitecture.ToString(),
            DotnetVersion(),
            CultureInfo.CurrentCulture.Name,
            CultureInfo.CurrentUICulture.Name,
            checked((int)GetDpiForSystem()),
            lightTheme ? "Light" : "Dark",
            smoothing);
    }

    private static string DotnetVersion()
    {
        using var process = Process.Start(new ProcessStartInfo("dotnet", "--version")
        {
            RedirectStandardOutput = true,
            UseShellExecute = false,
        }) ?? throw new InvalidOperationException("Could not start dotnet --version.");
        var version = process.StandardOutput.ReadToEnd().Trim();
        process.WaitForExit();
        return version;
    }

    private static void Compare<T>(string name, T expected, T actual, List<string> differences)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            differences.Add($"{name}: expected '{expected}', found '{actual}'");
        }
    }

    [DllImport("user32.dll")]
    private static extern uint GetDpiForSystem();

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}

public sealed record EnvironmentEvidence(
    PinnedEnvironment Pinned,
    PinnedEnvironment Actual,
    IReadOnlyList<string> Differences,
    string ManifestSha256,
    bool IsPinned);
