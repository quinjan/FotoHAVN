using System.Text.Json;
using System.IO;

namespace FotoHavn.UiVerificationHost;

public sealed record VerificationScript(
    string Id,
    string FixtureId,
    JsonElement Request,
    IReadOnlyList<VerificationAction> Actions)
{
    public string InitialInjectionIdentity =>
        Request.ValueKind == JsonValueKind.Object &&
        Request.TryGetProperty("identity", out var identity) &&
        identity.GetString() is { Length: > 0 } value
            ? value
            : throw new InvalidDataException("A verification script request requires an injection identity.");

    public string FinalInjectionIdentity => Actions.Count == 0
        ? InitialInjectionIdentity
        : Actions[^1].ExpectedInjectionIdentity;

    internal void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Id);
        ArgumentException.ThrowIfNullOrWhiteSpace(FixtureId);
        _ = InitialInjectionIdentity;
        _ = FinalInjectionIdentity;
    }

}

public sealed record VerificationAction(string AutomationId, string ExpectedInjectionIdentity);

public sealed class ApprovedTransitionCatalog
{
    private readonly IReadOnlyDictionary<string, VerificationScript> scripts;

    private ApprovedTransitionCatalog(IReadOnlyDictionary<string, VerificationScript> scripts) =>
        this.scripts = scripts;

    public IReadOnlyCollection<string> Ids => scripts.Keys.ToArray();

    public VerificationScript Resolve(string id) => scripts.TryGetValue(id, out var script)
        ? script
        : throw new InvalidDataException($"Unknown approved transition '{id}'.");

    public static ApprovedTransitionCatalog Load(string repositoryRoot, VerificationPlan plan)
    {
        var path = Path.Combine(repositoryRoot, "tools", "FotoHavn.UiVerificationHost", "ApprovedTransitionCatalog.json");
        var entries = JsonSerializer.Deserialize<VerificationScript[]>(File.ReadAllText(path), JsonOptions)
            ?? throw new InvalidDataException("The approved transition catalog is empty.");
        var cases = plan.Cases.ToDictionary(item => item.FixtureId, StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            entry.Validate();
            if (!cases.TryGetValue(entry.FixtureId, out var verificationCase))
            {
                throw new InvalidDataException(
                    $"Approved transition '{entry.Id}' refers to unknown fixture '{entry.FixtureId}'.");
            }

            if (entry.FinalInjectionIdentity != verificationCase.InjectionIdentity)
            {
                throw new InvalidDataException(
                    $"Approved transition '{entry.Id}' ends at '{entry.FinalInjectionIdentity}', " +
                    $"not '{verificationCase.InjectionIdentity}'.");
            }
        }

        return new(entries.ToDictionary(item => item.Id, StringComparer.Ordinal));
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}
