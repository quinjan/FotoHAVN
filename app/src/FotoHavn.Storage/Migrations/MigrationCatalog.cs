using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;

namespace FotoHavn.Storage.Migrations;

/// <summary>One schema migration: <see cref="Version"/> is the <c>PRAGMA user_version</c> it leads to.</summary>
/// <param name="Version">The <c>NNNN</c> of the file name, as a number (1 for <c>0001_initial.sql</c>).</param>
/// <param name="Name">The name part (<c>initial</c>).</param>
/// <param name="Sql">The script: statements only, no <c>PRAGMA</c> and no <c>BEGIN</c>/<c>COMMIT</c>. The runner wraps it in a transaction.</param>
public sealed record MigrationScript(int Version, string Name, string Sql);

/// <summary>
/// The ordered migrations embedded in this assembly. The naming rule: a file in <c>Migrations/</c> named
/// <c>NNNN_name.sql</c> (four digits, then lowercase letters, digits, or underscores), embedded as the resource
/// <c>FotoHavn.Storage.Migrations.NNNN_name.sql</c>. Versions start at 1 and run without gaps or repeats; the runner
/// (wave B1) applies every script above the database's <c>user_version</c> in version order, each in its own transaction,
/// and sets <c>user_version</c> to the script's version in the same transaction. A shipped script is never edited.
/// </summary>
public static partial class MigrationCatalog
{
    private const string ResourcePrefix = "FotoHavn.Storage.Migrations.";

    /// <summary>The migrations embedded in this assembly, in version order.</summary>
    public static IReadOnlyList<MigrationScript> Embedded { get; } = LoadEmbedded(typeof(MigrationCatalog).Assembly);

    /// <summary>The newest version: what a fresh database ends at.</summary>
    public static int LatestVersion => Embedded[^1].Version;

    /// <summary>Reads the migrations embedded in <paramref name="assembly"/>.</summary>
    public static IReadOnlyList<MigrationScript> LoadEmbedded(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        var scripts = new List<MigrationScript>();
        foreach (var resource in assembly.GetManifestResourceNames().Where(name => name.StartsWith(ResourcePrefix, StringComparison.Ordinal)))
        {
            using var stream = assembly.GetManifestResourceStream(resource)!;
            using var reader = new StreamReader(stream);
            scripts.Add(Parse(resource[ResourcePrefix.Length..], reader.ReadToEnd()));
        }

        return Order(scripts);
    }

    /// <summary>Reads one script from its file name (<c>0001_initial.sql</c>). Throws <see cref="FormatException"/> when the name breaks the rule.</summary>
    public static MigrationScript Parse(string fileName, string sql)
    {
        ArgumentNullException.ThrowIfNull(fileName);
        ArgumentNullException.ThrowIfNull(sql);
        var match = FileNamePattern().Match(fileName);
        if (!match.Success)
        {
            throw new FormatException($"'{fileName}' isn't a migration file name (NNNN_name.sql).");
        }

        return new MigrationScript(
            int.Parse(match.Groups["version"].Value, NumberStyles.None, CultureInfo.InvariantCulture),
            match.Groups["name"].Value,
            sql);
    }

    /// <summary>Sorts <paramref name="scripts"/> by version and checks they start at 1 with no gap or repeat. Throws <see cref="InvalidOperationException"/> otherwise.</summary>
    public static IReadOnlyList<MigrationScript> Order(IEnumerable<MigrationScript> scripts)
    {
        ArgumentNullException.ThrowIfNull(scripts);
        var ordered = scripts.OrderBy(script => script.Version).ToList();
        if (ordered.Count == 0)
        {
            throw new InvalidOperationException("There are no migrations.");
        }

        for (var index = 0; index < ordered.Count; index++)
        {
            if (ordered[index].Version != index + 1)
            {
                throw new InvalidOperationException($"Migration versions must run 1, 2, 3 without gaps or repeats; found {ordered[index].Version} at position {index + 1}.");
            }
        }

        return ordered;
    }

    [GeneratedRegex(@"^(?<version>[0-9]{4})_(?<name>[a-z0-9_]+)\.sql$")]
    private static partial Regex FileNamePattern();
}
