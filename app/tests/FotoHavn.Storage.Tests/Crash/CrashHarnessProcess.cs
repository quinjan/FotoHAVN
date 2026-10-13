using System.Diagnostics;
using System.Text.Json;

namespace FotoHavn.Storage.Tests.Crash;

/// <summary>What one run of the crash harness did.</summary>
public sealed record HarnessResult(int ExitCode, string Output, string Errors, string MarkerPath)
{
    public bool Crashed => File.Exists(MarkerPath);

    /// <summary>The crash marker the harness wrote just before it killed itself.</summary>
    public JsonElement Marker => JsonDocument.Parse(File.ReadAllText(MarkerPath)).RootElement.Clone();

    public string MarkerStep => Marker.GetProperty("step").GetString()!;

    public string? MarkerCycleId => Marker.GetProperty("cycleId").GetString();

    /// <summary>Every crash point the harness reached up to and including the one it crashed at, in order.</summary>
    public IReadOnlyList<string> MarkerReached => Marker.GetProperty("reached").EnumerateArray().Select(item => item.GetString()!).ToList();

    /// <summary>The cycle the harness worked on, from the marker (a crash) or its output (a clean run).</summary>
    public string? CycleId => Crashed
        ? MarkerCycleId
        : JsonDocument.Parse(Output).RootElement.GetProperty("cycleId").GetString();

    public IReadOnlyList<string> CreatedIds => JsonDocument.Parse(Output).RootElement.GetProperty("created").EnumerateArray().Select(item => item.GetString()!).ToList();
}

/// <summary>Starts <c>FotoHavn.Storage.CrashHarness</c> as a child process on a Data folder.</summary>
public static class CrashHarnessProcess
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(90);

    /// <summary>The harness is copied beside the test assembly by the test project's build (<c>crash-harness/</c>).</summary>
    public static string HarnessDll { get; } = Path.Combine(AppContext.BaseDirectory, "crash-harness", "FotoHavn.Storage.CrashHarness.dll");

    public static string DotnetHost { get; } = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") is { Length: > 0 } host ? host : "dotnet";

    public static async Task<HarnessResult> RunAsync(string dataRoot, string scenario, string crashAt, string? eventId = null, string? cycleId = null)
    {
        Assert.True(File.Exists(HarnessDll), $"The crash harness isn't built beside the tests: {HarnessDll}");
        var start = new ProcessStartInfo(DotnetHost)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        start.ArgumentList.Add(HarnessDll);
        foreach (var argument in new[] { "--data", dataRoot, "--scenario", scenario, "--crash-at", crashAt })
        {
            start.ArgumentList.Add(argument);
        }

        if (eventId is not null)
        {
            start.ArgumentList.Add("--event");
            start.ArgumentList.Add(eventId);
        }

        if (cycleId is not null)
        {
            start.ArgumentList.Add("--cycle");
            start.ArgumentList.Add(cycleId);
        }

        using var process = Process.Start(start) ?? throw new InvalidOperationException("The crash harness didn't start.");
        var output = process.StandardOutput.ReadToEndAsync();
        var errors = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(Timeout);
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException($"The crash harness ({scenario} at {crashAt}) didn't finish within {Timeout.TotalSeconds:0} s.");
        }

        return new HarnessResult(
            process.ExitCode,
            await output,
            await errors,
            Path.Combine(Path.GetDirectoryName(dataRoot)!, "crash-marker.json"));
    }
}
