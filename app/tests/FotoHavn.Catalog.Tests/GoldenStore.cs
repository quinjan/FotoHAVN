using System.Text.Json;
using FotoHavn.Core.Catalog;

namespace FotoHavn.Catalog.Tests;

/// <summary>
/// The golden compositions: every Template with every Look, plus the four blank previews, composed from the fixture photographs
/// and compared as SHA-256 hashes of the decoded pixels (never the PNG bytes), stored in <c>Golden/composition-hashes.json</c>.
/// </summary>
/// <remarks>
/// Updating after an intended change to the look of a composition:
/// <list type="number">
/// <item>set <c>FOTOHAVN_UPDATE_GOLDEN=1</c> and run the Catalog tests (<c>dotnet test --project tests/FotoHavn.Catalog.Tests</c>);</item>
/// <item>that rewrites <c>tests/FotoHavn.Catalog.Tests/Golden/composition-hashes.json</c> and writes all 32 PNGs to
/// <c>app/output/golden/</c> for visual review;</item>
/// <item>open the PNGs, confirm the change is the intended one, unset the variable, and commit the JSON.</item>
/// </list>
/// </remarks>
internal static class GoldenStore
{
    private const string UpdateVariable = "FOTOHAVN_UPDATE_GOLDEN";

    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };
    private static readonly Lazy<IReadOnlyDictionary<string, string>> Hashes = new(Load);

    public static string AppRoot { get; } = FindAppRoot();

    public static string HashesPath { get; } = Path.Combine(AppRoot, "tests", "FotoHavn.Catalog.Tests", "Golden", "composition-hashes.json");

    public static string OutputDirectory { get; } = Path.Combine(AppRoot, "output", "golden");

    public static IReadOnlyList<TemplateDefinition> Templates { get; } = new CatalogData().Templates;

    public static IReadOnlyList<LookDefinition> Looks { get; } = new CatalogData().Looks;

    public static string FullKey(string templateId, string lookId) => $"{templateId}|{lookId}";

    public static string BlankKey(string templateId) => $"{templateId}|blank";

    public static string Expected(string key) =>
        Hashes.Value.TryGetValue(key, out var hash) ? hash : "(no golden hash; run with FOTOHAVN_UPDATE_GOLDEN=1)";

    public static byte[] ComposeFull(string templateId, string lookId)
    {
        var template = Templates.Single(t => t.Id == templateId);
        var photos = Enumerable.Range(0, template.WindowCount).Select(FixturePhotos.ForWindow).ToArray<CompositionPhoto?>();
        return Compose(templateId, lookId, photos);
    }

    public static byte[] ComposeBlank(string templateId)
    {
        var template = Templates.Single(t => t.Id == templateId);
        return Compose(templateId, CatalogData.NaturaleId, new CompositionPhoto?[template.WindowCount]);
    }

    public static byte[] Compose(string templateId, string lookId, CompositionPhoto?[] photos) =>
        new SkiaCompositor().ComposeAsync(new CompositionRequest(templateId, lookId, photos), CancellationToken.None).GetAwaiter().GetResult().Png.ToArray();

    private static IReadOnlyDictionary<string, string> Load()
    {
        if (Environment.GetEnvironmentVariable(UpdateVariable) == "1")
        {
            return Regenerate();
        }

        return File.Exists(HashesPath)
            ? JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(HashesPath)) ?? []
            : new Dictionary<string, string>();
    }

    private static SortedDictionary<string, string> Regenerate()
    {
        Directory.CreateDirectory(OutputDirectory);
        var hashes = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var template in Templates)
        {
            Record(hashes, BlankKey(template.Id), $"{template.Id}.blank", ComposeBlank(template.Id));
            foreach (var look in Looks)
            {
                Record(hashes, FullKey(template.Id, look.Id), $"{template.Id}.{look.Id}", ComposeFull(template.Id, look.Id));
            }
        }

        Directory.CreateDirectory(Path.GetDirectoryName(HashesPath)!);
        File.WriteAllText(HashesPath, JsonSerializer.Serialize(hashes, Indented) + "\n");
        return hashes;
    }

    private static void Record(SortedDictionary<string, string> hashes, string key, string fileName, byte[] png)
    {
        hashes[key] = PixelImage.Decode(png).Hash();
        File.WriteAllBytes(Path.Combine(OutputDirectory, fileName + ".png"), png);
    }

    private static string FindAppRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "FotoHAVN.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("FotoHAVN.slnx was not found above the test output directory.");
    }
}
