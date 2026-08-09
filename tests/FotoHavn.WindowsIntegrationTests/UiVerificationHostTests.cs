using FotoHavn.UiVerificationHost;
using System.Drawing;
using System.Drawing.Imaging;
using Xunit;

namespace FotoHavn.WindowsIntegrationTests;

public sealed class UiVerificationHostTests
{
    [Fact]
    public void Holding_fixture_requires_focus_on_the_exit_action()
    {
        var plan = VerificationPlan.Load(FindRepositoryRoot());
        var holding = Assert.Single(
            plan.Cases,
            item => item.FixtureId == "guest-start.exit-holding.standard");
        var ready = Assert.Single(
            plan.Cases,
            item => item.FixtureId == "guest-start.ready.standard");

        Assert.Equal(
            "FotoHavn.ActionButton.ExitEvent",
            AutomationInspection.RequiredFocusAutomationId(holding));
        Assert.Null(AutomationInspection.RequiredFocusAutomationId(ready));
    }

    [Fact]
    public void Plan_resolves_every_approved_fixture_to_the_production_verification_contract()
    {
        var repositoryRoot = FindRepositoryRoot();

        var plan = VerificationPlan.Load(repositoryRoot);

        Assert.Equal(103, plan.Cases.Count);
        var first = Assert.Single(plan.Cases, item => item.FixtureId == "saved-events.new-event.standard");
        Assert.Equal("injection.saved-events.new-event", first.InjectionIdentity);
        Assert.Equal(1280, first.Width);
        Assert.Equal(720, first.Height);
        Assert.Equal(3, first.Batch);
        Assert.Equal(64, first.TargetSha256.Length);
        Assert.Contains("UIA-SURFACE-STRUCTURE", first.UiAutomationChecks);
        Assert.Equal(AutomationRole.Window, first.Annotation.AutomationRole);
        Assert.Equal(InitialFocusPolicy.PageHeading, first.Annotation.InitialFocus);
        var announcement = Assert.Single(first.Annotation.Announcements);
        Assert.Equal("Saved Events ready.", announcement.Text);
        Assert.Equal(AnnouncementPriority.Polite, announcement.Priority);
        Assert.All(plan.Cases, item => Assert.True(File.Exists(item.TargetPath), item.TargetPath));
    }

    [Fact]
    public void Annotation_rejects_unknown_announcement_priorities()
    {
        var repositoryRoot = FindRepositoryRoot();
        var source = Path.Combine(
            repositoryRoot,
            "docs",
            "design-system",
            "reference-states",
            "targets",
            "saved-events",
            "new-event--1280x720.yaml");
        var temporary = Path.Combine(Path.GetTempPath(), $"fotohavn-annotation-{Guid.NewGuid():N}.yaml");
        try
        {
            File.WriteAllText(
                temporary,
                File.ReadAllText(source).Replace(
                    "priority: \"polite\"",
                    "priority: \"urgent\"",
                    StringComparison.Ordinal));

            var exception = Assert.Throws<InvalidDataException>(() => VerificationAnnotation.Load(temporary));

            Assert.Contains("Unknown announcement priority 'urgent'", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(temporary);
        }
    }

    [Fact]
    public void Evidence_comparison_reports_exact_pixels_hashes_and_rollout_debt()
    {
        var evidenceRoot = Path.Combine(Path.GetTempPath(), $"fotohavn-evidence-{Guid.NewGuid():N}");
        Directory.CreateDirectory(evidenceRoot);
        try
        {
            var targetPath = Path.Combine(evidenceRoot, "target.png");
            var actualPath = Path.Combine(evidenceRoot, "actual.png");
            var diffPath = Path.Combine(evidenceRoot, "diff.png");
            SavePixels(targetPath, Color.Red, Color.Blue);
            SavePixels(actualPath, Color.Red, Color.Green);

            var comparison = ImageEvidence.Compare(targetPath, actualPath, diffPath);

            Assert.Equal(1, comparison.ChangedPixels);
            Assert.Equal(2, comparison.TotalPixels);
            Assert.Equal(64, comparison.TargetSha256.Length);
            Assert.Equal(64, comparison.ActualSha256.Length);
            Assert.Equal(64, comparison.DiffSha256.Length);
            Assert.True(File.Exists(diffPath));
            Assert.Equal(ScenarioStatus.PlannedMigrationDebt,
                RolloutClassifier.Classify(hasDifferences: true, fixtureBatch: 3, completedThroughBatch: 1, pinnedEnvironmentMatched: true));
            Assert.Equal(ScenarioStatus.ReviewRequired,
                RolloutClassifier.Classify(hasDifferences: true, fixtureBatch: 1, completedThroughBatch: 1, pinnedEnvironmentMatched: true));

            var exact = ImageEvidence.Compare(targetPath, targetPath, Path.Combine(evidenceRoot, "exact-diff.png"));
            Assert.Equal(ScenarioStatus.Match,
                RolloutClassifier.Classify(exact.ChangedPixels > 0, fixtureBatch: 1, completedThroughBatch: 1, pinnedEnvironmentMatched: true));
        }
        finally
        {
            Directory.Delete(evidenceRoot, recursive: true);
        }
    }

    private static void SavePixels(string path, Color first, Color second)
    {
        using var bitmap = new Bitmap(2, 1, PixelFormat.Format32bppArgb);
        bitmap.SetPixel(0, 0, first);
        bitmap.SetPixel(1, 0, second);
        bitmap.Save(path, ImageFormat.Png);
    }

    [Fact]
    public void Transition_script_identifies_its_initial_and_final_production_states()
    {
        var repositoryRoot = FindRepositoryRoot();
        var plan = VerificationPlan.Load(repositoryRoot);
        var catalog = ApprovedTransitionCatalog.Load(repositoryRoot, plan);

        var script = catalog.Resolve("transition.guest-start.capture-countdown");

        Assert.Equal("injection.guest-start.ready", script.InitialInjectionIdentity);
        Assert.Equal("injection.capture.countdown-3", script.FinalInjectionIdentity);
    }

    [Fact]
    public void Application_fingerprint_changes_when_compiled_application_content_changes()
    {
        var applicationRoot = Path.Combine(Path.GetTempPath(), $"fotohavn-app-hash-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(applicationRoot, "UiVerification"));
        try
        {
            var applicationPath = Path.Combine(applicationRoot, "FotoHAVN.exe");
            File.WriteAllText(applicationPath, "generic launcher");
            File.WriteAllText(Path.ChangeExtension(applicationPath, ".dll"), "application code v1");
            File.WriteAllText(Path.ChangeExtension(applicationPath, ".pri"), "resources");
            File.WriteAllText(Path.Combine(applicationRoot, "App.xbf"), "app xaml");
            var mainWindowPath = Path.Combine(applicationRoot, "MainWindow.xbf");
            File.WriteAllText(mainWindowPath, "window xaml v1");
            File.WriteAllText(Path.Combine(applicationRoot, "UiVerification", "ApprovedInjectionCatalog.json"), "[]");
            File.WriteAllText(Path.Combine(applicationRoot, "UiVerification", "camera-preview.jpg"), "camera frame");
            File.WriteAllText(Path.Combine(applicationRoot, "UiVerification", "canonical-presentation.json"), "{}");

            var first = VerificationRunner.HashApplication(applicationPath);
            File.WriteAllText(mainWindowPath, "window xaml v2");
            var second = VerificationRunner.HashApplication(applicationPath);

            Assert.Equal(64, first.Length);
            Assert.Equal(64, second.Length);
            Assert.NotEqual(first, second);
        }
        finally
        {
            Directory.Delete(applicationRoot, recursive: true);
        }
    }

    [Fact]
    public void Reading_order_uses_stable_semantic_landmarks_instead_of_visible_copy()
    {
        var annotation = new VerificationAnnotation(
            "Start this Event?",
            "Start this Event?",
            AutomationRole.Dialog,
            "ready",
            ["semantic icon", "dialog heading", "Event identity when relevant", "safe action", "confirming action"],
            InitialFocusPolicy.SafeAction,
            [],
            48,
            48,
            [WinUiElementRole.ContentDialog, WinUiElementRole.Button, WinUiElementRole.TextBlock]);
        AutomationElementEvidence[] elements =
        [
            Evidence(null, "Semantic icon", "Group"),
            Evidence("StartEventConfirmationText", "Start this Event?", "Text"),
            Evidence("StartEventIdentityText", "0198a7d2-5bc1-7f45-8e90-3f7a2f91c4e8", "Text"),
            Evidence("FotoHavn.Confirmation.SafeAction", "Cancel", "Button"),
            Evidence("FotoHavn.Confirmation.ConfirmingAction", "Start Event", "Button"),
        ];

        var findings = AutomationInspection.CheckReadingOrder(annotation, elements);

        Assert.Empty(findings);
    }

    [Fact]
    public void Reading_order_omits_identity_when_the_dialog_action_has_no_relevant_event_identity()
    {
        var annotation = new VerificationAnnotation(
            "Exit this Event?",
            "Exit this Event?",
            AutomationRole.Dialog,
            "ready",
            ["semantic icon", "dialog heading", "Event identity when relevant", "safe action", "confirming action"],
            InitialFocusPolicy.SafeAction,
            [],
            48,
            48,
            [WinUiElementRole.ContentDialog, WinUiElementRole.Button, WinUiElementRole.TextBlock]);
        AutomationElementEvidence[] elements =
        [
            Evidence(null, "Semantic icon", "Group"),
            Evidence("ExitEventTitleText", "Exit this Event?", "Text"),
            Evidence("FotoHavn.Confirmation.SafeAction", "Keep Event Active", "Button"),
            Evidence("FotoHavn.Confirmation.ConfirmingAction", "Exit Event", "Button"),
        ];

        var findings = AutomationInspection.CheckReadingOrder(annotation, elements);

        Assert.Empty(findings);
    }

    [Theory]
    [InlineData("VerticalSmallDecrease")]
    [InlineData("HorizontalLargeIncrease")]
    [InlineData("FotoHavn.Verification.HostReady")]
    public void Framework_chrome_and_verification_controls_are_not_product_touch_targets(string automationId)
    {
        Assert.True(AutomationInspection.IsFrameworkChromeOrVerificationElement(automationId));
    }

    [Fact]
    public void Product_controls_remain_subject_to_touch_target_checks()
    {
        Assert.False(AutomationInspection.IsFrameworkChromeOrVerificationElement("ExitEventButton"));
    }

    private static AutomationElementEvidence Evidence(string? id, string? name, string controlType) => new(
        id,
        name,
        controlType,
        null,
        null,
        true,
        false,
        controlType == "Button",
        false,
        new(0, 0, 48, 48),
        [],
        null,
        controlType == "Button",
        true);

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "FotoHAVN.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Could not locate the FotoHAVN repository root.");
    }
}
