using System.Diagnostics;

namespace FotoHavn.Storage.Tests.Support;

/// <summary>Real junctions and symbolic links for the tests that prove the store never follows them.</summary>
public static class Links
{
    /// <summary>Creates a directory junction (<c>mklink /J</c>), which needs no elevation, and returns <paramref name="link"/>.</summary>
    public static string CreateJunction(string link, string target)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(link)!);
        var start = new ProcessStartInfo("cmd.exe")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        start.ArgumentList.Add("/c");
        start.ArgumentList.Add("mklink");
        start.ArgumentList.Add("/J");
        start.ArgumentList.Add(link);
        start.ArgumentList.Add(target);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("cmd.exe didn't start.");
        var output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
        process.WaitForExit();
        Assert.True(process.ExitCode == 0 && Directory.Exists(link), $"mklink /J failed: {output}");
        Assert.True(File.GetAttributes(link).HasFlag(FileAttributes.ReparsePoint));
        return link;
    }

    /// <summary>Creates a file symbolic link when the account may (Developer Mode or elevation); false otherwise.</summary>
    public static bool TryCreateFileSymbolicLink(string link, string target)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(link)!);
            File.CreateSymbolicLink(link, target);
            return true;
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
        {
            return false;
        }
    }
}
