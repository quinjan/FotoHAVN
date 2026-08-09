using System.Collections.Concurrent;
using System.Windows.Automation;

namespace FotoHavn.UiVerificationHost;

public sealed class AutomationInspection : IDisposable
{
    private const string RenderSettledId = "FotoHavn.Verification.RenderSettled";
    private const string HostReadyId = "FotoHavn.Verification.HostReady";
    private const string HostReadySettledStatus = "host-ready-settled";
    private readonly AutomationElement root;
    private readonly ConcurrentQueue<LiveRegionEvidence> liveRegions = new();
    private readonly AutomationEventHandler liveRegionHandler;

    public AutomationInspection(IntPtr windowHandle)
    {
        root = AutomationElement.FromHandle(windowHandle);
        liveRegionHandler = (sender, _) =>
        {
            try
            {
                var element = (AutomationElement)sender;
                liveRegions.Enqueue(new(
                    element.Current.AutomationId,
                    element.Current.Name,
                    element.Current.ItemStatus,
                    ReadLiveSetting(element),
                    DateTimeOffset.UtcNow));
            }
            catch (ElementNotAvailableException)
            {
            }
        };
        Automation.AddAutomationEventHandler(
            AutomationElementIdentifiers.LiveRegionChangedEvent,
            root,
            TreeScope.Subtree,
            liveRegionHandler);
    }

    public void PrepareEvidence(string expectedInjectionIdentity, TimeSpan timeout)
    {
        Invoke(HostReadyId);
        WaitForSettlement(expectedInjectionIdentity, timeout, HostReadySettledStatus);
    }

    public void WaitForSettlement(
        string expectedInjectionIdentity,
        TimeSpan timeout,
        string expectedStatus = "settled")
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            var signal = root.FindFirst(
                TreeScope.Descendants,
                new PropertyCondition(AutomationElement.AutomationIdProperty, RenderSettledId));
            if (signal is not null &&
                signal.Current.ItemStatus == expectedStatus &&
                signal.Current.HelpText == expectedInjectionIdentity)
            {
                Thread.Sleep(350);
                return;
            }

            Thread.Sleep(50);
        }

        throw new TimeoutException(
            $"FotoHAVN did not settle injection '{expectedInjectionIdentity}' before the timeout.");
    }

    public void Invoke(string automationId)
    {
        var element = root.FindFirst(
            TreeScope.Descendants,
            new PropertyCondition(AutomationElement.AutomationIdProperty, automationId))
            ?? throw new InvalidOperationException($"UI Automation element '{automationId}' was not found.");
        if (!element.TryGetCurrentPattern(InvokePattern.Pattern, out var pattern))
        {
            throw new InvalidOperationException($"UI Automation element '{automationId}' does not support Invoke.");
        }

        ((InvokePattern)pattern).Invoke();
    }

    public void NormalizeApplicationFocus()
    {
        try
        {
            var focused = AutomationElement.FocusedElement;
            if (focused is not null && IsDescendantOf(root, focused) &&
                focused.Current.IsEnabled && !focused.Current.IsOffscreen &&
                focused.Current.IsKeyboardFocusable)
            {
                return;
            }
        }
        catch (ElementNotAvailableException)
        {
        }

        string[] preferredIds =
        [
            "FotoHavn.Confirmation.SafeAction",
            "FotoHavn.ActionButton.Primary.GuestStart",
            "FotoHavn.ActionButton.AssistanceRetry",
            "FotoHavn.ActionButton.AssistanceExitOnly",
            "FotoHavn.ActionButton.ExitEvent",
        ];
        foreach (var automationId in preferredIds)
        {
            var element = root.FindFirst(
                TreeScope.Descendants,
                new PropertyCondition(AutomationElement.AutomationIdProperty, automationId));
            if (element is null || element.Current.IsOffscreen || !element.Current.IsEnabled ||
                !element.Current.IsKeyboardFocusable)
            {
                continue;
            }

            element.SetFocus();
            Thread.Sleep(100);
            return;
        }
    }

    public AutomationEvidence Snapshot(
        VerificationCase verificationCase,
        int clientOriginX,
        int clientOriginY,
        double effectiveScale)
    {
        var elements = root.FindAll(TreeScope.Descendants, Condition.TrueCondition)
            .Cast<AutomationElement>()
            .Select(element => ReadElement(element, clientOriginX, clientOriginY, effectiveScale))
            .Where(item => item.IsMeaningful)
            .ToArray();
        var structureFindings = new List<string>();
        var semanticFindings = new List<string>();
        var targetFindings = new List<string>();
        var geometryFindings = new List<string>();
        if (root.Current.ControlType != ControlType.Window)
        {
            semanticFindings.Add(
                $"Expected the native application root to expose Window, found " +
                $"'{root.Current.ControlType.ProgrammaticName.Replace("ControlType.", string.Empty, StringComparison.Ordinal)}'.");
        }

        var surfaceRoots = elements.Where(item =>
            item.AutomationId?.StartsWith("FotoHavn.Surface.", StringComparison.Ordinal) == true).ToArray();
        if (surfaceRoots.Length != 1)
        {
            structureFindings.Add($"Expected one semantic surface root; found {surfaceRoots.Length}.");
        }
        else
        {
            var surfaceRoot = surfaceRoots[0];
            if (surfaceRoot.AutomationId != verificationCase.ExpectedSurfaceAutomationId)
            {
                structureFindings.Add(
                    $"Expected surface root '{verificationCase.ExpectedSurfaceAutomationId}', " +
                    $"found '{surfaceRoot.AutomationId}'.");
            }

            if (surfaceRoot.Name != verificationCase.ExpectedSurfaceName)
            {
                structureFindings.Add(
                    $"Expected surface name '{verificationCase.ExpectedSurfaceName}', found '{surfaceRoot.Name}'.");
            }

            if (surfaceRoot.ItemStatus != verificationCase.ExpectedSurfaceStatus)
            {
                structureFindings.Add(
                    $"Expected surface state '{verificationCase.ExpectedSurfaceStatus}', " +
                    $"found '{surfaceRoot.ItemStatus}'.");
            }

            var expectedSurfaceRoles = ExpectedSemanticSurfaceTypes(verificationCase.Annotation.AutomationRole);
            if (!expectedSurfaceRoles.Contains(surfaceRoot.ControlType, StringComparer.Ordinal))
            {
                semanticFindings.Add(
                    $"Expected semantic surface role '{string.Join(" or ", expectedSurfaceRoles)}', " +
                    $"found '{surfaceRoot.ControlType}'.");
            }
        }

        foreach (var element in elements.Where(item => item.IsActionable && !item.IsOffscreen))
        {
            if (string.IsNullOrWhiteSpace(element.AutomationId))
            {
                semanticFindings.Add($"Actionable {element.ControlType} '{element.Name}' has no stable Automation ID.");
            }

            if (string.IsNullOrWhiteSpace(element.Name))
            {
                semanticFindings.Add($"Actionable {element.ControlType} has no accessible name.");
            }

            var requiredPattern = RequiredPattern(element.ControlType);
            if (requiredPattern is not null && !element.Patterns.Contains(requiredPattern, StringComparer.Ordinal))
            {
                semanticFindings.Add(
                    $"{element.ControlType} '{element.AutomationId ?? element.Name}' does not expose {requiredPattern}.");
            }

            if (IsFrameworkChromeOrVerificationElement(element.AutomationId))
            {
                continue;
            }

            var requiredTargetSize = element.AutomationId?.Contains(
                    "Primary",
                    StringComparison.OrdinalIgnoreCase) == true
                ? verificationCase.Annotation.ProminentTargetSize
                : verificationCase.Annotation.MinimumTargetSize;
            const double viewportRoundingEpsilon = 1;
            var isClippedAtViewportBoundary = element.Bounds.Left <= viewportRoundingEpsilon ||
                element.Bounds.Top <= viewportRoundingEpsilon ||
                element.Bounds.Right >= verificationCase.Width - viewportRoundingEpsilon ||
                element.Bounds.Bottom >= verificationCase.Height - viewportRoundingEpsilon;
            if (!isClippedAtViewportBoundary &&
                (element.Bounds.Width < requiredTargetSize || element.Bounds.Height < requiredTargetSize))
            {
                targetFindings.Add(
                    $"'{element.AutomationId ?? element.Name}' is {element.Bounds.Width:0}x{element.Bounds.Height:0}; " +
                    $"the required target is {requiredTargetSize}x{requiredTargetSize}.");
            }

            if (element.Bounds.Left < 0 || element.Bounds.Top < 0 ||
                element.Bounds.Right > verificationCase.Width || element.Bounds.Bottom > verificationCase.Height)
            {
                geometryFindings.Add($"'{element.AutomationId ?? element.Name}' extends outside the effective viewport.");
            }
        }

        foreach (var expectedControlType in verificationCase.Annotation.WinUiPatterns
            .Select(ExpectedWinUiControlType)
            .Distinct(StringComparer.Ordinal))
        {
            if (!elements.Any(item => item.ControlType == expectedControlType))
            {
                semanticFindings.Add(
                    $"The annotation requires {expectedControlType}, but that role is absent from the automation tree.");
            }
        }

        AutomationElement? focused;
        try { focused = AutomationElement.FocusedElement; }
        catch (ElementNotAvailableException) { focused = null; }
        var readingFindings = CheckReadingOrder(
            verificationCase.Annotation,
            verificationCase.Width < 800 || verificationCase.Height < 500,
            elements.Where(item => !item.IsOffscreen &&
                !IsFrameworkChromeOrVerificationElement(item.AutomationId))
                .ToArray());
        var focusedEvidence = focused is null
            ? null
            : ReadElement(focused, clientOriginX, clientOriginY, effectiveScale);
        var primaryGuestActionAbsent = verificationCase.Annotation.InitialFocus ==
                InitialFocusPolicy.PrimaryGuestActionWhenPresent &&
            !elements.Any(item => !item.IsOffscreen &&
                (item.AutomationId?.Contains("Primary", StringComparison.OrdinalIgnoreCase) == true ||
                 item.Name?.Contains("Touch to start", StringComparison.OrdinalIgnoreCase) == true));
        var focusMatches = primaryGuestActionAbsent ||
            (focusedEvidence is not null && MatchesExpectedFocus(
                focusedEvidence,
                verificationCase.Annotation,
                elements,
                verificationCase.FixtureId));
        var focusValid = primaryGuestActionAbsent ||
            (focused is not null && focusedEvidence is { HasKeyboardFocus: true } &&
             IsDescendantOf(root, focused) && focusMatches);
        var focusFindings = focusValid
                ? Array.Empty<string>()
                : [$"Focus does not match '{verificationCase.Annotation.InitialFocus}' inside FotoHAVN."];
        var liveRegionEvidence = liveRegions.ToArray();
        var liveFindings = CheckAnnouncements(verificationCase.Annotation, liveRegionEvidence);
        var checks = new[]
        {
            Check("surface-structure", structureFindings),
            Check("identifiers-names-roles-states-patterns", semanticFindings),
            Check("reading-order", readingFindings),
            Check("focus", focusFindings),
            Check("live-region-events", liveFindings),
            Check("target-sizes", targetFindings),
            Check("responsive-geometry", geometryFindings),
        };
        return new(
            elements,
            elements.Select(item => item.AutomationId ?? item.Name)
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Select(item => item!)
                .ToArray(),
            focusedEvidence,
            liveRegionEvidence,
            checks,
            checks.SelectMany(item => item.Findings).ToArray());
    }

    public void Dispose() => Automation.RemoveAutomationEventHandler(
        AutomationElementIdentifiers.LiveRegionChangedEvent,
        root,
        liveRegionHandler);

    private static AutomationElementEvidence ReadElement(
        AutomationElement element,
        int clientOriginX,
        int clientOriginY,
        double effectiveScale)
    {
        try
        {
            var current = element.Current;
            var bounds = current.BoundingRectangle;
            var patterns = SupportedPatterns
                .Where(item => element.TryGetCurrentPattern(item.Pattern, out _))
                .Select(item => item.Name)
                .ToArray();
            var controlType = current.ControlType.ProgrammaticName.Replace("ControlType.", string.Empty, StringComparison.Ordinal);
            var automationId = NullIfEmpty(current.AutomationId);
            var name = NullIfEmpty(current.Name);
            var actionable = IsActionable(controlType);
            var liveSetting = ReadLiveSetting(element);
            return new(
                automationId,
                name,
                controlType,
                NullIfEmpty(current.ItemStatus),
                NullIfEmpty(current.HelpText),
                current.IsEnabled,
                current.IsOffscreen,
                current.IsKeyboardFocusable,
                current.HasKeyboardFocus,
                new(
                    Finite((bounds.Left - clientOriginX) / effectiveScale),
                    Finite((bounds.Top - clientOriginY) / effectiveScale),
                    Finite(bounds.Width / effectiveScale),
                    Finite(bounds.Height / effectiveScale)),
                patterns,
                liveSetting,
                actionable,
                automationId is not null || name is not null || actionable);
        }
        catch (ElementNotAvailableException)
        {
            return new(null, null, "Unavailable", null, null, false, true, false, false,
                new(0, 0, 0, 0), [], null, false, false);
        }
    }

    private static bool IsActionable(string controlType) => controlType is
        "Button" or "Edit" or "ComboBox" or "ListItem" or "CheckBox" or "RadioButton" or "Hyperlink";

    private static string? NullIfEmpty(string value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private static double Finite(double value) => double.IsFinite(value) ? value : 0;

    private static string? ReadLiveSetting(AutomationElement element)
    {
        var value = element.GetCurrentPropertyValue(
            AutomationElementIdentifiers.LiveSettingProperty,
            ignoreDefaultValue: true);
        return ReferenceEquals(value, AutomationElement.NotSupported) || value is Exception
            ? null
            : value?.ToString();
    }

    private static IReadOnlyList<string> ExpectedSemanticSurfaceTypes(AutomationRole role) => role switch
    {
        AutomationRole.Window => ["Group", "Pane"],
        AutomationRole.Dialog => ["Window", "Group"],
        _ => throw new ArgumentOutOfRangeException(nameof(role)),
    };

    public static bool IsFrameworkChromeOrVerificationElement(string? automationId)
    {
        if (automationId?.StartsWith("FotoHavn.Verification.", StringComparison.Ordinal) == true)
        {
            return true;
        }

        return automationId is
            "VerticalSmallDecrease" or "VerticalSmallIncrease" or
            "VerticalLargeDecrease" or "VerticalLargeIncrease" or
            "HorizontalSmallDecrease" or "HorizontalSmallIncrease" or
            "HorizontalLargeDecrease" or "HorizontalLargeIncrease";
    }

    private static string? RequiredPattern(string controlType) => controlType switch
    {
        "Button" or "Hyperlink" => "Invoke",
        "Edit" => "Value",
        "ComboBox" => "ExpandCollapse",
        "ListItem" or "RadioButton" => "SelectionItem",
        "CheckBox" => "Toggle",
        _ => null,
    };

    private static string ExpectedWinUiControlType(WinUiElementRole pattern) => pattern switch
    {
        WinUiElementRole.Page or WinUiElementRole.ContentDialog => "Group",
        WinUiElementRole.Button => "Button",
        WinUiElementRole.TextBlock => "Text",
        _ => throw new ArgumentOutOfRangeException(nameof(pattern)),
    };

    public static IReadOnlyList<string> CheckReadingOrder(
        VerificationAnnotation annotation,
        IReadOnlyList<AutomationElementEvidence> actualOrder) =>
        CheckReadingOrder(annotation, false, actualOrder);

    public static IReadOnlyList<string> CheckReadingOrder(
        VerificationAnnotation annotation,
        bool isStressViewport,
        IReadOnlyList<AutomationElementEvidence> actualOrder)
    {
        var findings = new List<string>();
        var cursor = 0;
        foreach (var expected in annotation.ReadingOrder)
        {
            if (isStressViewport && expected.Contains("Operator Assistance label", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var expectedLabel = expected.Contains("heading", StringComparison.OrdinalIgnoreCase)
                ? annotation.Heading
                : expected;
            var match = -1;
            for (var index = cursor; index < actualOrder.Count; index++)
            {
                if (SemanticLandmarkMatches(expectedLabel, actualOrder[index], annotation))
                {
                    match = index;
                    break;
                }
            }

            if (match < 0)
            {
                var isOptional = expected.Contains("when present", StringComparison.OrdinalIgnoreCase) ||
                    expected.Contains("when relevant", StringComparison.OrdinalIgnoreCase) ||
                    expected.Equals("Event identity", StringComparison.OrdinalIgnoreCase) &&
                    annotation.Heading.Equals("New Event", StringComparison.OrdinalIgnoreCase);
                if (!isOptional)
                {
                    findings.Add($"Reading-order contract item '{expected}' is missing or out of order.");
                }
            }
            else
            {
                cursor = match + 1;
            }
        }

        return findings;
    }

    private static bool SemanticLandmarkMatches(
        string expected,
        AutomationElementEvidence actual,
        VerificationAnnotation annotation)
    {
        var automationId = actual.AutomationId ?? string.Empty;
        var name = actual.Name ?? string.Empty;
        if (expected.Equals(annotation.Heading, StringComparison.OrdinalIgnoreCase))
        {
            return name.Equals(annotation.Heading, StringComparison.OrdinalIgnoreCase) ||
                automationId.Contains("Heading", StringComparison.OrdinalIgnoreCase) ||
                automationId.Contains("TitleText", StringComparison.OrdinalIgnoreCase) ||
                automationId.Contains("ConfirmationText", StringComparison.OrdinalIgnoreCase);
        }

        if (LandmarkAutomationFragments.TryGetValue(expected, out var fragments) &&
            fragments.Any(fragment =>
                automationId.Contains(fragment, StringComparison.OrdinalIgnoreCase) ||
                name.Contains(fragment, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        var stopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "and", "or", "the", "when", "present", "relevant", "semantic", "brand", "header",
        };
        var words = System.Text.RegularExpressions.Regex.Matches(expected, "[A-Za-z0-9]+")
            .Select(match => match.Value)
            .Where(word => !stopWords.Contains(word))
            .ToArray();
        return words.Length > 0 && words.All(word =>
            automationId.Contains(word, StringComparison.OrdinalIgnoreCase) ||
            name.Contains(word, StringComparison.OrdinalIgnoreCase));
    }

    private static readonly IReadOnlyDictionary<string, string[]> LandmarkAutomationFragments =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["heading"] = ["Heading", "TitleText"],
            ["New Event"] = ["NewEventButton"],
            ["Event cards"] = ["EventTiles"],
            ["Start, Edit, and Delete actions"] = ["StartEditDeleteActions"],
            ["Event identity"] = ["EventSetupIdentity"],
            ["Event name"] = ["SetupFieldGroup.Eventname", "EventNameTextBox"],
            ["Camera and status"] = ["SetupFieldGroup.Camera"],
            ["Camera preview"] = ["CameraViewport", "Camera preview", "Live preview"],
            ["Printer"] = ["SetupFieldGroup.Printer"],
            ["Storage"] = ["SetupFieldGroup.Storage"],
            ["footer actions"] = ["SetupFooter"],
            ["semantic icon"] = ["SemanticIcon", "Semantic icon"],
            ["dialog heading"] = ["TitleText", "ConfirmationText"],
            ["consequence when present"] = ["MessageText", "Consequence"],
            ["Event identity when relevant"] = ["IdentityText", "Event identity"],
            ["status when present"] = ["InlineStatus", "StatusCallout", "ProgressRing"],
            ["safe action"] = ["SafeAction", "Cancel", "Keep"],
            ["confirming action"] = ["ConfirmingAction"],
            ["success status"] = ["SemanticIcon", "Success"],
            ["message"] = ["MessageText"],
            ["primary action"] = ["Primary", "ConfirmingAction"],
        };

    private static bool MatchesExpectedFocus(
        AutomationElementEvidence focused,
        VerificationAnnotation annotation,
        IReadOnlyList<AutomationElementEvidence> elements,
        string fixtureId)
    {
        if (elements.Any(item => item.AutomationId == "ExitEventConfirmationLayer"))
        {
            return focused.AutomationId == "FotoHavn.Confirmation.SafeAction";
        }

        if (fixtureId.StartsWith("guest-start.exit-hold", StringComparison.Ordinal) ||
            elements.Any(item =>
                item.AutomationId == "FotoHavn.ActionButton.ExitEvent" && item.ItemStatus == "Holding"))
        {
            return focused.AutomationId == "FotoHavn.ActionButton.ExitEvent";
        }

        if (annotation.InitialFocus == InitialFocusPolicy.PageHeading)
        {
            return focused.Name == annotation.Heading;
        }

        if (annotation.InitialFocus == InitialFocusPolicy.DialogHeading)
        {
            return focused.Name == annotation.Heading &&
                focused.ControlType == "Text";
        }

        if (annotation.InitialFocus == InitialFocusPolicy.SafeAction)
        {
            return focused.AutomationId?.Contains("Safe", StringComparison.OrdinalIgnoreCase) == true ||
                focused.Name?.Contains("Cancel", StringComparison.OrdinalIgnoreCase) == true ||
                focused.Name?.Contains("Keep", StringComparison.OrdinalIgnoreCase) == true;
        }

        if (annotation.InitialFocus == InitialFocusPolicy.PrimaryAction)
        {
            return focused.AutomationId?.Contains("Primary", StringComparison.OrdinalIgnoreCase) == true ||
                focused.AutomationId?.Contains("ConfirmingAction", StringComparison.OrdinalIgnoreCase) == true;
        }

        if (annotation.InitialFocus == InitialFocusPolicy.PrimaryGuestActionWhenPresent)
        {
            var primaryIsPresent = elements.Any(item =>
                !item.IsOffscreen &&
                (item.AutomationId?.Contains("Primary", StringComparison.OrdinalIgnoreCase) == true ||
                 item.Name?.Contains("Touch to start", StringComparison.OrdinalIgnoreCase) == true));
            if (!primaryIsPresent)
            {
                return true;
            }

            return focused.AutomationId?.Contains("Primary", StringComparison.OrdinalIgnoreCase) == true;
        }

        throw new ArgumentOutOfRangeException(nameof(annotation.InitialFocus));
    }

    private static IReadOnlyList<string> CheckAnnouncements(
        VerificationAnnotation annotation,
        IReadOnlyList<LiveRegionEvidence> events)
    {
        var productionEvents = events.Where(item => item.AutomationId != RenderSettledId).ToArray();
        var findings = new List<string>();
        foreach (var expected in annotation.Announcements)
        {
            var priority = expected.Priority?.ToString();
            if (!productionEvents.Any(item =>
                item.Name == expected.Text &&
                (priority is null ||
                 item.LiveSetting == priority ||
                 string.Equals(item.ItemStatus, priority, StringComparison.OrdinalIgnoreCase))))
            {
                findings.Add(
                    $"Expected {expected.Priority} announcement '{expected.Text}' was not observed.");
            }
        }

        return findings;
    }

    private static bool IsDescendantOf(AutomationElement ancestor, AutomationElement element)
    {
        for (var current = element; current is not null; current = TreeWalker.RawViewWalker.GetParent(current))
        {
            if (Automation.Compare(ancestor, current))
            {
                return true;
            }
        }

        return false;
    }

    private static AutomationCheckResult Check(string id, IReadOnlyList<string> findings) =>
        new(id, findings.Count == 0, findings);

    private static readonly (string Name, AutomationPattern Pattern)[] SupportedPatterns =
    [
        ("Invoke", InvokePattern.Pattern),
        ("Value", ValuePattern.Pattern),
        ("Selection", SelectionPattern.Pattern),
        ("SelectionItem", SelectionItemPattern.Pattern),
        ("Toggle", TogglePattern.Pattern),
        ("ExpandCollapse", ExpandCollapsePattern.Pattern),
        ("Scroll", ScrollPattern.Pattern),
        ("RangeValue", RangeValuePattern.Pattern),
        ("Text", TextPattern.Pattern),
        ("Window", WindowPattern.Pattern),
    ];
}

public sealed record AutomationEvidence(
    IReadOnlyList<AutomationElementEvidence> Elements,
    IReadOnlyList<string> ReadingOrder,
    AutomationElementEvidence? FocusedElement,
    IReadOnlyList<LiveRegionEvidence> LiveRegionEvents,
    IReadOnlyList<AutomationCheckResult> Checks,
    IReadOnlyList<string> Violations);

public sealed record AutomationCheckResult(
    string Id,
    bool Passed,
    IReadOnlyList<string> Findings);

public sealed record AutomationElementEvidence(
    string? AutomationId,
    string? Name,
    string ControlType,
    string? ItemStatus,
    string? HelpText,
    bool IsEnabled,
    bool IsOffscreen,
    bool IsKeyboardFocusable,
    bool HasKeyboardFocus,
    ElementBounds Bounds,
    IReadOnlyList<string> Patterns,
    string? LiveSetting,
    bool IsActionable,
    bool IsMeaningful);

public sealed record ElementBounds(double Left, double Top, double Width, double Height)
{
    public double Right => Left + Width;
    public double Bottom => Top + Height;
}

public sealed record LiveRegionEvidence(
    string AutomationId,
    string Name,
    string ItemStatus,
    string? LiveSetting,
    DateTimeOffset ObservedAtUtc);
