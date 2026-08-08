using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FotoHavn.UiVerificationHost;

public sealed class VerificationRunner(HostOptions options)
{
    private static readonly TimeSpan LaunchTimeout = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan SettlementTimeout = TimeSpan.FromSeconds(15);

    public int Run()
    {
        var plan = VerificationPlan.Load(options.RepositoryRoot);
        var approvedTransitions = ApprovedTransitionCatalog.Load(options.RepositoryRoot, plan);
        if (options.ValidatePlanOnly)
        {
            Console.WriteLine(
                $"Validated {plan.Cases.Count} approved UI verification fixtures and " +
                $"{approvedTransitions.Ids.Count} transition scripts.");
            return 0;
        }

        var applicationPath = options.ApplicationPath!;
        AssertVerificationBuild(applicationPath);
        var environment = PinnedEnvironment.Verify(options.RepositoryRoot, options.AllowEnvironmentDrift);
        Directory.CreateDirectory(options.OutputPath);
        WriteJson(Path.Combine(options.OutputPath, "environment.json"), environment);

        var scripts = options.TransitionIds.Select(approvedTransitions.Resolve)
            .ToDictionary(item => item.FixtureId, StringComparer.Ordinal);
        var selected = plan.Cases.Where(item =>
            options.FixtureIds.Count == 0 && scripts.Count == 0 ||
            options.FixtureIds.Contains(item.FixtureId, StringComparer.Ordinal) ||
            scripts.ContainsKey(item.FixtureId)).ToArray();
        if (selected.Length == 0)
        {
            throw new InvalidOperationException("No approved fixture matched the requested selection.");
        }

        var results = new List<ScenarioResult>(selected.Length);
        foreach (var verificationCase in selected)
        {
            scripts.TryGetValue(verificationCase.FixtureId, out var script);
            var result = ExecuteCase(verificationCase, script, environment);
            results.Add(result);
            Console.WriteLine($"{result.Status}: {verificationCase.FixtureId}");
        }

        var run = new VerificationRun(
            1,
            DateTimeOffset.UtcNow,
            GitCommit(),
            Hash(applicationPath),
            options.CompletedThroughBatch,
            environment.IsPinned,
            results.Count,
            results.Count(item => item.Status == ScenarioStatus.Match),
            results.Count(item => item.Status == ScenarioStatus.PlannedMigrationDebt),
            results.Count(item => item.Status is ScenarioStatus.ReviewRequired or ScenarioStatus.EnvironmentDrift),
            results.Select(item => new ResultPointer(item.FixtureId, item.Status, item.ResultPath)).ToArray());
        WriteJson(Path.Combine(options.OutputPath, "run.json"), run);
        return run.BlockingResults == 0 ? 0 : 1;
    }

    private ScenarioResult ExecuteCase(
        VerificationCase verificationCase,
        VerificationScript? script,
        EnvironmentEvidence environment)
    {
        var caseRoot = Path.Combine(options.OutputPath, SafeName(verificationCase.FixtureId));
        Directory.CreateDirectory(caseRoot);
        var actualPath = Path.Combine(caseRoot, "actual.png");
        var diffPath = Path.Combine(caseRoot, "diff.png");
        var requestPath = Path.Combine(caseRoot, "request.json");
        IReadOnlyList<string> arguments;
        if (script is null)
        {
            var eventName = verificationCase.FixtureId.Contains("long-event-name", StringComparison.Ordinal)
                ? "Mika & Paolo’s Extraordinarily Long Wedding Celebration"
                : "Mika & Paolo’s Wedding";
            File.WriteAllText(
                requestPath,
                JsonSerializer.Serialize(
                    new
                    {
                        identity = verificationCase.InjectionIdentity,
                        fixtureId = verificationCase.FixtureId,
                        expectedSurfaceStatus = verificationCase.ExpectedSurfaceStatus,
                        presentation = new { eventName },
                    },
                    JsonOptions));
            arguments = ["--ui-verification-request", requestPath];
        }
        else
        {
            File.WriteAllText(requestPath, script.Request.GetRawText());
            arguments = ["--ui-verification-request", requestPath];
        }

        AutomationEvidence automation;
        using (var window = WindowSession.Launch(options.ApplicationPath!, arguments, LaunchTimeout))
        using (var inspection = new AutomationInspection(window.Handle))
        {
            window.SetEffectiveClientSize(verificationCase.Width, verificationCase.Height);
            inspection.WaitForSettlement(
                script?.InitialInjectionIdentity ?? verificationCase.InjectionIdentity,
                SettlementTimeout);
            if (script is not null)
            {
                foreach (var action in script.Actions)
                {
                    inspection.Invoke(action.AutomationId);
                    inspection.WaitForSettlement(action.ExpectedInjectionIdentity, SettlementTimeout);
                }

                if (!script.FinalInjectionIdentity.Equals(
                    verificationCase.InjectionIdentity,
                    StringComparison.Ordinal))
                {
                    throw new InvalidDataException(
                        $"Script for '{verificationCase.FixtureId}' ends at '{script.FinalInjectionIdentity}', " +
                        $"not '{verificationCase.InjectionIdentity}'.");
                }
            }

            inspection.NormalizeApplicationFocus();

            var origin = window.GetClientOrigin();
            automation = inspection.Snapshot(
                verificationCase,
                origin.X,
                origin.Y,
                window.EffectiveScale);
            window.CaptureClient(actualPath);
        }

        var image = ImageEvidence.Compare(verificationCase.TargetPath, actualPath, diffPath);
        var status = Classify(verificationCase, image, automation, environment);
        var relativeResultPath = Path.Combine(SafeName(verificationCase.FixtureId), "result.json")
            .Replace('\\', '/');
        var result = new ScenarioResult(
            1,
            verificationCase.FixtureId,
            verificationCase.InjectionIdentity,
            verificationCase.Batch,
            verificationCase.Width,
            verificationCase.Height,
            verificationCase.UiAutomationChecks,
            status,
            image,
            new(
                Path.GetRelativePath(options.RepositoryRoot, verificationCase.TargetPath).Replace('\\', '/'),
                Path.Combine(SafeName(verificationCase.FixtureId), "actual.png").Replace('\\', '/'),
                Path.Combine(SafeName(verificationCase.FixtureId), "diff.png").Replace('\\', '/')),
            automation,
            environment,
            DateTimeOffset.UtcNow,
            relativeResultPath);
        WriteJson(Path.Combine(caseRoot, "result.json"), result);
        return result;
    }

    private ScenarioStatus Classify(
        VerificationCase verificationCase,
        ImageComparison image,
        AutomationEvidence automation,
        EnvironmentEvidence environment)
    {
        return RolloutClassifier.Classify(
            image.ChangedPixels > 0 || automation.Violations.Count > 0,
            verificationCase.Batch,
            options.CompletedThroughBatch,
            environment.IsPinned);
    }

    private static void AssertVerificationBuild(string applicationPath)
    {
        var catalog = Path.Combine(Path.GetDirectoryName(applicationPath)!,
            "UiVerification", "ApprovedInjectionCatalog.json");
        if (!File.Exists(catalog))
        {
            throw new InvalidOperationException(
                "The application is not a UiVerificationBuild; its approved-injection catalog is absent.");
        }
    }

    private string GitCommit()
    {
        using var process = Process.Start(new ProcessStartInfo("git", "rev-parse HEAD")
        {
            WorkingDirectory = options.RepositoryRoot,
            RedirectStandardOutput = true,
            UseShellExecute = false,
        }) ?? throw new InvalidOperationException("Could not read the repository commit.");
        var commit = process.StandardOutput.ReadToEnd().Trim();
        process.WaitForExit();
        return commit;
    }

    private static string Hash(string path) =>
        Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();

    private static string SafeName(string value) => string.Concat(value.Select(character =>
        char.IsLetterOrDigit(character) || character is '-' or '_' ? character : '_'));

    private static void WriteJson<T>(string path, T value) =>
        File.WriteAllText(path, JsonSerializer.Serialize(value, JsonOptions));

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.KebabCaseLower) },
    };
}

public sealed record ScenarioResult(
    int SchemaVersion,
    string FixtureId,
    string InjectionIdentity,
    int FixtureBatch,
    int EffectiveWidth,
    int EffectiveHeight,
    IReadOnlyList<string> RequiredUiAutomationChecks,
    ScenarioStatus Status,
    ImageComparison Image,
    EvidencePaths EvidenceFiles,
    AutomationEvidence UiAutomation,
    EnvironmentEvidence Environment,
    DateTimeOffset ExecutedAtUtc,
    string ResultPath);

public sealed record EvidencePaths(string Target, string Actual, string Diff);

public enum ScenarioStatus
{
    Match,
    PlannedMigrationDebt,
    ReviewRequired,
    EnvironmentDrift,
}

public static class RolloutClassifier
{
    public static ScenarioStatus Classify(
        bool hasDifferences,
        int fixtureBatch,
        int completedThroughBatch,
        bool pinnedEnvironmentMatched) =>
        !pinnedEnvironmentMatched
            ? ScenarioStatus.EnvironmentDrift
            : !hasDifferences
                ? ScenarioStatus.Match
                : fixtureBatch > completedThroughBatch
                    ? ScenarioStatus.PlannedMigrationDebt
                    : ScenarioStatus.ReviewRequired;
}

public sealed record VerificationRun(
    int SchemaVersion,
    DateTimeOffset ExecutedAtUtc,
    string GitCommit,
    string ApplicationSha256,
    int CompletedThroughBatch,
    bool PinnedEnvironmentMatched,
    int TotalResults,
    int Matches,
    int PlannedMigrationDebt,
    int BlockingResults,
    IReadOnlyList<ResultPointer> Results);

public sealed record ResultPointer(string FixtureId, ScenarioStatus Status, string ResultPath);
