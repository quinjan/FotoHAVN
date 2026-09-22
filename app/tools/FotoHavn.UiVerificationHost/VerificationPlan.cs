using System.Security.Cryptography;
using System.Text.Json;
using System.IO;

namespace FotoHavn.UiVerificationHost;

public sealed class VerificationPlan
{
    private VerificationPlan(IReadOnlyList<VerificationCase> cases) => Cases = cases;

    public IReadOnlyList<VerificationCase> Cases { get; }

    public static VerificationPlan Load(string repositoryRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRoot);
        repositoryRoot = Path.GetFullPath(repositoryRoot);
        var traceabilityRoot = Path.Combine(repositoryRoot, "docs", "design-system", "traceability");
        var referenceRoot = Path.Combine(repositoryRoot, "docs", "design-system", "reference-states");

        var scenarios = Directory.GetFiles(Path.Combine(traceabilityRoot, "scenarios"), "*.json")
            .SelectMany(ReadScenarios)
            .ToArray();
        var fixtureOwners = scenarios
            .SelectMany(scenario => scenario.VisualFixtures.Concat(scenario.ResponsiveViewportCases)
                .Select(fixture => KeyValuePair.Create(fixture, scenario)))
            .ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal);

        var hashes = ReadTargetHashes(Path.Combine(traceabilityRoot, "manifest.json"));
        var injections = JsonSerializer.Deserialize<ApprovedInjectionEntry[]>(File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "FotoHavn.App",
            "UiVerification",
            "ApprovedInjectionCatalog.json")), JsonOptions)
            ?.ToDictionary(item => item.Identity, StringComparer.Ordinal)
            ?? throw new InvalidDataException("The approved injection catalog is empty.");
        var capturePlan = JsonSerializer.Deserialize<CapturePlanEntry[]>(
            File.ReadAllText(Path.Combine(referenceRoot, "capture-plan.json")),
            JsonOptions) ?? throw new InvalidDataException("The capture plan is empty.");

        var cases = new List<VerificationCase>(capturePlan.Length);
        foreach (var entry in capturePlan)
        {
            if (!fixtureOwners.TryGetValue(entry.Id, out var scenario))
            {
                throw new InvalidDataException($"Fixture '{entry.Id}' is not owned by a traceability scenario.");
            }

            if (!hashes.TryGetValue(entry.Id, out var hash))
            {
                throw new InvalidDataException($"Fixture '{entry.Id}' has no pinned target hash.");
            }

            if (!injections.TryGetValue(scenario.DeterministicInjectionIdentity, out var injection))
            {
                throw new InvalidDataException(
                    $"Fixture '{entry.Id}' refers to unknown injection '{scenario.DeterministicInjectionIdentity}'.");
            }

            var dimensions = entry.Viewport.Split('x', 2);
            if (dimensions.Length != 2 ||
                !int.TryParse(dimensions[0], out var width) ||
                !int.TryParse(dimensions[1], out var height))
            {
                throw new InvalidDataException($"Fixture '{entry.Id}' has invalid viewport '{entry.Viewport}'.");
            }

            var targetPath = Path.GetFullPath(Path.Combine(referenceRoot, entry.Target));
            if (!File.Exists(targetPath))
            {
                throw new FileNotFoundException($"Fixture '{entry.Id}' target is missing.", targetPath);
            }

            var actualHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(targetPath))).ToLowerInvariant();
            if (!actualHash.Equals(hash.Sha256, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"Fixture '{entry.Id}' target hash changed: expected {hash.Sha256}, found {actualHash}.");
            }

            var annotation = VerificationAnnotation.Load(Path.ChangeExtension(targetPath, ".yaml"));

            cases.Add(new(
                entry.Id,
                scenario.DeterministicInjectionIdentity,
                width,
                height,
                scenario.Batch,
                targetPath,
                hash.Sha256,
                scenario.UiAutomationChecks.Concat(scenario.SharedPatternChecks).Distinct(StringComparer.Ordinal).ToArray(),
                $"FotoHavn.Surface.{PascalCase(injection.Surface)}",
                annotation.AutomationName,
                annotation.AutomationState,
                annotation));
        }

        return new(cases);
    }

    private static IEnumerable<ScenarioEntry> ReadScenarios(string path)
    {
        var catalog = JsonSerializer.Deserialize<ScenarioCatalog>(File.ReadAllText(path), JsonOptions)
            ?? throw new InvalidDataException($"Scenario catalog '{path}' is empty.");
        return catalog.Scenarios;
    }

    private static Dictionary<string, TargetHashEntry> ReadTargetHashes(string path)
    {
        var manifest = JsonSerializer.Deserialize<TraceabilityManifest>(File.ReadAllText(path), JsonOptions)
            ?? throw new InvalidDataException("The traceability manifest is empty.");
        return manifest.TargetHashes.ToDictionary(item => item.Id, StringComparer.Ordinal);
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static string PascalCase(string value) => string.Concat(value.Split('-')
        .Select(part => char.ToUpperInvariant(part[0]) + part[1..]));

    private sealed record CapturePlanEntry(string Id, string Viewport, string Target);
    private sealed record ScenarioCatalog(IReadOnlyList<ScenarioEntry> Scenarios);
    private sealed record ScenarioEntry(
        string Id,
        int Batch,
        string DeterministicInjectionIdentity,
        IReadOnlyList<string> VisualFixtures,
        IReadOnlyList<string> ResponsiveViewportCases,
        IReadOnlyList<string> UiAutomationChecks,
        IReadOnlyList<string> SharedPatternChecks);
    private sealed record TraceabilityManifest(IReadOnlyList<TargetHashEntry> TargetHashes);
    private sealed record TargetHashEntry(string Id, string Path, string Sha256);
    private sealed record ApprovedInjectionEntry(
        string Identity,
        string Surface,
        string ExpectedName,
        string ExpectedStatus);
}

public sealed record VerificationCase(
    string FixtureId,
    string InjectionIdentity,
    int Width,
    int Height,
    int Batch,
    string TargetPath,
    string TargetSha256,
    IReadOnlyList<string> UiAutomationChecks,
    string ExpectedSurfaceAutomationId,
    string ExpectedSurfaceName,
    string ExpectedSurfaceStatus,
    VerificationAnnotation Annotation);
