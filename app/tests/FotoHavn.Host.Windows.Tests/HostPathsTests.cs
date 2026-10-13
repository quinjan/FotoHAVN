namespace FotoHavn.Host.Windows.Tests;

public sealed class HostPathsTests
{
    [Fact]
    public void Data_folder_defaults_to_Data_beside_the_executable()
    {
        var paths = HostPaths.Create(@"C:\FOTOHAVN", null);

        Assert.Equal(@"C:\FOTOHAVN\Data", paths.DataFolder);
        Assert.Equal(@"C:\FOTOHAVN\Data\logs\fotohavn.jsonl", paths.LogFile);
        Assert.Equal(@"C:\FOTOHAVN\Data\webview2-user-data", paths.WebViewUserDataFolder);
        Assert.Equal(@"C:\FOTOHAVN\wwwroot\index.html", paths.UiIndexFile);
    }

    [Fact]
    public void WebView2_runtime_is_Evergreen_unless_one_is_beside_the_executable()
    {
        Assert.Null(HostPaths.Create(@"C:\FOTOHAVN", null, null, _ => false).WebView2RuntimeFolder);

        var bundled = HostPaths.Create(@"C:\FOTOHAVN", null, null, path => path == @"C:\FOTOHAVN\runtime\webview2\msedgewebview2.exe");

        Assert.Equal(@"C:\FOTOHAVN\runtime\webview2", bundled.WebView2RuntimeFolder);
    }

    [Theory]
    [InlineData(@"D:\wv2", @"D:\wv2")]
    [InlineData(@"runtime\fixed", @"C:\FOTOHAVN\runtime\fixed")]
    public void An_explicit_WebView2_runtime_wins(string option, string expected)
    {
        var paths = HostPaths.Create(@"C:\FOTOHAVN", null, option, _ => true);

        Assert.Equal(expected, paths.WebView2RuntimeFolder);
    }

    [Fact]
    public void Creates_the_given_data_folder_with_host_owned_folders()
    {
        var root = Path.Combine(Path.GetTempPath(), "fotohavn-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var paths = HostPaths.Create(@"C:\FOTOHAVN", Path.Combine(root, "Data"));

            paths.EnsureCreated();

            Assert.True(Directory.Exists(paths.LogsFolder));
            Assert.True(Directory.Exists(paths.WebViewUserDataFolder));
            Assert.Equal(Path.Combine(root, "Data"), paths.DataFolder);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
