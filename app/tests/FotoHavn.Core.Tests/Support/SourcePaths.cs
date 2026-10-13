namespace FotoHavn.Core.Tests.Support;

/// <summary>Paths into the source tree, found from the test output folder by walking up to <c>FotoHAVN.slnx</c>.</summary>
internal static class SourcePaths
{
    /// <summary>Set <c>FOTOHAVN_UPDATE_FIXTURES=1</c> to rewrite the brain-generated presentation fixtures instead of comparing them.</summary>
    public const string UpdateFixturesVariable = "FOTOHAVN_UPDATE_FIXTURES";

    public static bool UpdatingFixtures => Environment.GetEnvironmentVariable(UpdateFixturesVariable) == "1";

    public static string AppRoot { get; } = FindAppRoot();

    /// <summary>The canonical command envelopes, one per command, from the protocol snapshots.</summary>
    public static string CommandSnapshotFolder => Path.Combine(AppRoot, "tests", "FotoHavn.Protocol.Tests", "Snapshots", "commands");

    public static string PresentationFixtureFolder => Path.Combine(AppRoot, "ui", "src", "fixtures", "presentations");

    /// <summary>Reads a committed text file with <c>\n</c> line endings, whatever Git's checkout setting.</summary>
    public static string ReadNormalized(string path) => File.ReadAllText(path).Replace("\r\n", "\n", StringComparison.Ordinal);

    private static string FindAppRoot()
    {
        for (var folder = new DirectoryInfo(AppContext.BaseDirectory); folder is not null; folder = folder.Parent)
        {
            if (File.Exists(Path.Combine(folder.FullName, "FotoHAVN.slnx")))
            {
                return folder.FullName;
            }
        }

        throw new InvalidOperationException("FotoHAVN.slnx not found above " + AppContext.BaseDirectory);
    }
}
