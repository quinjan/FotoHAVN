namespace FotoHavn.Protocol.Tests;

/// <summary>Paths into the source tree, found from the test output folder by walking up to <c>FotoHAVN.slnx</c>.</summary>
internal static class SourcePaths
{
    /// <summary>
    /// Set <c>FOTOHAVN_UPDATE_PROTOCOL=1</c> to rewrite the committed schema and snapshot files instead of comparing them.
    /// <c>scripts/generate-protocol.ps1</c> sets it for the schema; set it yourself to accept snapshot changes.
    /// </summary>
    public const string UpdateVariable = "FOTOHAVN_UPDATE_PROTOCOL";

    public static bool Updating => Environment.GetEnvironmentVariable(UpdateVariable) == "1";

    public static string AppRoot { get; } = FindAppRoot();

    public static string SchemaFile => Path.Combine(AppRoot, "src", "FotoHavn.Protocol", "schema", ProtocolSchema.FileName);

    public static string SnapshotFolder => Path.Combine(AppRoot, "tests", "FotoHavn.Protocol.Tests", "Snapshots");

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
