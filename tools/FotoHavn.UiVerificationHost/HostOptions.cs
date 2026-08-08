using System.IO;

namespace FotoHavn.UiVerificationHost;

public sealed record HostOptions(
    string RepositoryRoot,
    string? ApplicationPath,
    string OutputPath,
    IReadOnlyList<string> FixtureIds,
    IReadOnlyList<string> TransitionIds,
    int CompletedThroughBatch,
    bool ValidatePlanOnly,
    bool AllowEnvironmentDrift)
{
    public static HostOptions Parse(string[] args)
    {
        var repositoryRoot = FindRepositoryRoot(Environment.CurrentDirectory);
        string? applicationPath = null;
        string? outputPath = null;
        var fixtureIds = new List<string>();
        var transitionIds = new List<string>();
        var completedThroughBatch = 1;
        var validatePlanOnly = false;
        var allowEnvironmentDrift = false;

        for (var index = 0; index < args.Length; index++)
        {
            var value = args[index];
            string Next() => index + 1 < args.Length
                ? args[++index]
                : throw new ArgumentException($"{value} requires a value.");

            switch (value)
            {
                case "--repository-root": repositoryRoot = Path.GetFullPath(Next()); break;
                case "--app": applicationPath = Path.GetFullPath(Next()); break;
                case "--output": outputPath = Path.GetFullPath(Next()); break;
                case "--fixture": fixtureIds.Add(Next()); break;
                case "--transition": transitionIds.Add(Next()); break;
                case "--completed-through-batch": completedThroughBatch = int.Parse(Next()); break;
                case "--validate-plan": validatePlanOnly = true; break;
                case "--allow-environment-drift": allowEnvironmentDrift = true; break;
                case "--help": throw new ArgumentException(Usage);
                default: throw new ArgumentException($"Unknown option '{value}'.{Environment.NewLine}{Usage}");
            }
        }

        outputPath ??= Path.Combine(repositoryRoot, "artifacts", "ui-verification",
            DateTimeOffset.UtcNow.ToString("yyyyMMddTHHmmssZ"));
        if (!validatePlanOnly && applicationPath is null)
        {
            throw new ArgumentException($"--app is required unless --validate-plan is used.{Environment.NewLine}{Usage}");
        }

        return new(
            repositoryRoot,
            applicationPath,
            outputPath,
            fixtureIds,
            transitionIds,
            completedThroughBatch,
            validatePlanOnly,
            allowEnvironmentDrift);
    }

    public const string Usage = """
        FotoHavn.UiVerificationHost
          --validate-plan [--repository-root PATH]
          --app PATH [--output PATH] [--fixture ID ...] [--transition ID ...]
          [--completed-through-batch N] [--allow-environment-drift]
        """;

    private static string FindRepositoryRoot(string start)
    {
        var directory = new DirectoryInfo(Path.GetFullPath(start));
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "FotoHAVN.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new DirectoryNotFoundException("Could not locate FotoHAVN.slnx; pass --repository-root.");
    }
}
