using FotoHavn.Host.Windows.WebView;
using Microsoft.Extensions.Logging;

namespace FotoHavn.Host.Windows.Tests;

public sealed class WebViewEnvironmentProviderTests
{
    private const string Executable = "msedgewebview2.exe";

    [Fact]
    public void Without_a_runtime_folder_the_Evergreen_runtime_is_used()
    {
        var selection = WebViewRuntimeSelection.Select(null, _ => throw new InvalidOperationException("must not look at the disk"));

        Assert.Equal(WebViewRuntimeKind.Evergreen, selection.Kind);
        Assert.Null(selection.Folder);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void A_blank_runtime_folder_means_Evergreen(string folder)
    {
        var selection = WebViewRuntimeSelection.Select(folder, _ => throw new InvalidOperationException("must not look at the disk"));

        Assert.Equal(WebViewRuntimeKind.Evergreen, selection.Kind);
        Assert.Null(selection.Folder);
    }

    [Fact]
    public void A_runtime_folder_that_holds_the_executable_is_the_Fixed_Version_runtime()
    {
        var folder = Path.Combine(Path.GetTempPath(), "FOTO HAVN app", "runtime", "webview2");
        var probed = new List<string>();

        var selection = WebViewRuntimeSelection.Select(folder, path =>
        {
            probed.Add(path);
            return true;
        });

        Assert.Equal(WebViewRuntimeKind.FixedVersion, selection.Kind);
        Assert.Equal(folder, selection.Folder);
        Assert.Equal([Path.Combine(folder, Executable)], probed);
    }

    [Fact]
    public void A_runtime_folder_without_the_executable_fails_and_names_the_folder()
    {
        var folder = Path.Combine(Path.GetTempPath(), "missing-runtime");

        var failure = Assert.Throws<InvalidOperationException>(() => WebViewRuntimeSelection.Select(folder, _ => false));

        Assert.Contains(folder, failure.Message, StringComparison.Ordinal);
        Assert.Contains(Executable, failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_bad_runtime_folder_faults_the_environment_task_and_is_tried_again_next_time()
    {
        var provider = new WebViewEnvironmentProvider(
            Path.Combine(Path.GetTempPath(), "user-data"),
            Path.Combine(Path.GetTempPath(), "missing-runtime"),
            additionalBrowserArguments: null,
            fileExists: _ => false);

        var first = provider.GetAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => first);

        var second = provider.GetAsync();
        Assert.NotSame(first, second);
        await Assert.ThrowsAsync<InvalidOperationException>(() => second);
    }

    [Fact]
    public void The_runtime_log_line_carries_only_allow_listed_properties()
    {
        var logger = new CapturingLogger();
        var folder = Path.Combine(Path.GetTempPath(), "FOTO HAVN app", "runtime", "webview2");

        WebViewEnvironmentLog.RuntimeSelected(
            logger,
            new WebViewRuntimeSelection(WebViewRuntimeKind.FixedVersion, folder),
            "155.0.4283.60");

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Equal("FixedVersion", entry.Properties["Kind"]);
        Assert.Equal("155.0.4283.60", entry.Properties["Version"]);
        Assert.Equal(folder, entry.Properties["Runtime"]);
        Assert.Equal(
            ["Kind", "Runtime", "Version"],
            entry.Properties.Keys.Where(key => key != "{OriginalFormat}").Order(StringComparer.Ordinal).ToArray());
        Assert.Contains("155.0.4283.60", entry.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void The_Evergreen_log_line_names_the_system_runtime()
    {
        var logger = new CapturingLogger();

        WebViewEnvironmentLog.RuntimeSelected(
            logger,
            new WebViewRuntimeSelection(WebViewRuntimeKind.Evergreen, null),
            "155.0.4283.45");

        var entry = Assert.Single(logger.Entries);
        Assert.Equal("Evergreen", entry.Properties["Kind"]);
        Assert.Equal("system", entry.Properties["Runtime"]);
        Assert.Equal("155.0.4283.45", entry.Properties["Version"]);
    }

    private sealed class CapturingLogger : ILogger
    {
        public List<(LogLevel Level, string Message, Dictionary<string, object?> Properties)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var properties = new Dictionary<string, object?>(StringComparer.Ordinal);
            if (state is IReadOnlyList<KeyValuePair<string, object?>> list)
            {
                foreach (var pair in list)
                {
                    properties[pair.Key] = pair.Value;
                }
            }

            Entries.Add((logLevel, formatter(state, exception), properties));
        }
    }
}
