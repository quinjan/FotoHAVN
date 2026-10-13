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
