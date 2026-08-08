using System.IO;
using System.Text.RegularExpressions;

namespace FotoHavn.UiVerificationHost;

public sealed record VerificationAnnotation(
    string Heading,
    string AutomationName,
    AutomationRole AutomationRole,
    string AutomationState,
    IReadOnlyList<string> ReadingOrder,
    InitialFocusPolicy InitialFocus,
    IReadOnlyList<AnnouncementExpectation> Announcements,
    int MinimumTargetSize,
    int ProminentTargetSize,
    IReadOnlyList<WinUiElementRole> WinUiPatterns)
{
    public static VerificationAnnotation Load(string path)
    {
        var heading = string.Empty;
        var automationName = string.Empty;
        AutomationRole? automationRole = null;
        var automationState = string.Empty;
        var readingOrder = new List<string>();
        InitialFocusPolicy? initialFocus = null;
        var announcements = new List<AnnouncementExpectation>();
        var targetSizes = new List<int>();
        var winUiPatterns = new List<WinUiElementRole>();
        var section = string.Empty;

        foreach (var rawLine in File.ReadLines(path))
        {
            var line = rawLine.Trim();
            if (rawLine.Length > 0 && !char.IsWhiteSpace(rawLine[0]))
            {
                var separator = line.IndexOf(':');
                section = separator >= 0 ? line[..separator] : string.Empty;
                if (section == "winuiPattern")
                {
                    winUiPatterns.AddRange(Unquote(line[(separator + 1)..]).Split(';',
                            StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                        .Select(ParseWinUiElementRole)
                        .Where(role => role is not null)
                        .Select(role => role!.Value));
                }

                continue;
            }

            if (section == "heading" && line.StartsWith("text:", StringComparison.Ordinal))
            {
                heading = Value(line);
            }
            else if (section == "automation")
            {
                if (line.StartsWith("name:", StringComparison.Ordinal)) automationName = Value(line);
                else if (line.StartsWith("role:", StringComparison.Ordinal)) automationRole = ParseAutomationRole(Value(line));
                else if (line.StartsWith("state:", StringComparison.Ordinal)) automationState = Value(line);
            }
            else if (section == "readingOrder" && line.StartsWith("- ", StringComparison.Ordinal))
            {
                readingOrder.Add(Unquote(line[2..]));
            }
            else if (section == "focus" && line.StartsWith("initial:", StringComparison.Ordinal))
            {
                initialFocus = ParseInitialFocus(Value(line));
            }
            else if (section == "touch" && line.StartsWith("minimumTarget:", StringComparison.Ordinal))
            {
                targetSizes.AddRange(Regex.Matches(Value(line), @"(?<size>\d+)x\k<size>")
                    .Select(match => int.Parse(match.Groups["size"].Value)));
            }
            else if (section == "announcements")
            {
                if (line.StartsWith("- text:", StringComparison.Ordinal))
                {
                    announcements.Add(new(Unquote(line[7..]), null));
                }
                else if (line.StartsWith("priority:", StringComparison.Ordinal) && announcements.Count > 0)
                {
                    announcements[^1] = announcements[^1] with { Priority = ParseAnnouncementPriority(Value(line)) };
                }
            }
        }

        if (string.IsNullOrWhiteSpace(heading) ||
            string.IsNullOrWhiteSpace(automationName) ||
            automationRole is null ||
            string.IsNullOrWhiteSpace(automationState) ||
            readingOrder.Count == 0 ||
            initialFocus is null ||
            targetSizes.Count == 0)
        {
            throw new InvalidDataException($"Verification annotation '{path}' is incomplete.");
        }

        return new(
            heading,
            automationName,
            automationRole.Value,
            automationState,
            readingOrder,
            initialFocus.Value,
            announcements,
            targetSizes.Min(),
            targetSizes.Max(),
            winUiPatterns);
    }

    private static string Value(string line) => Unquote(line[(line.IndexOf(':') + 1)..]);

    private static string Unquote(string value) => value.Trim().Trim('"');

    private static AutomationRole ParseAutomationRole(string value) => value switch
    {
        "window" => AutomationRole.Window,
        "dialog" => AutomationRole.Dialog,
        _ => throw new InvalidDataException($"Unknown automation role '{value}'."),
    };

    private static InitialFocusPolicy ParseInitialFocus(string value) => value switch
    {
        "page heading" => InitialFocusPolicy.PageHeading,
        "dialog heading" => InitialFocusPolicy.DialogHeading,
        "primary action" => InitialFocusPolicy.PrimaryAction,
        "primary guest action when present" => InitialFocusPolicy.PrimaryGuestActionWhenPresent,
        "safe action" => InitialFocusPolicy.SafeAction,
        _ => throw new InvalidDataException($"Unknown initial-focus policy '{value}'."),
    };

    private static AnnouncementPriority ParseAnnouncementPriority(string value) => value switch
    {
        "polite" => AnnouncementPriority.Polite,
        "assertive" => AnnouncementPriority.Assertive,
        _ => throw new InvalidDataException($"Unknown announcement priority '{value}'."),
    };

    private static WinUiElementRole? ParseWinUiElementRole(string value) => value switch
    {
        "Page" => WinUiElementRole.Page,
        "ContentDialog" => WinUiElementRole.ContentDialog,
        "Button" => WinUiElementRole.Button,
        "TextBlock" => WinUiElementRole.TextBlock,
        "native WinUI controls" or "UI Automation patterns per component contract" => null,
        _ => throw new InvalidDataException($"Unknown WinUI pattern contract '{value}'."),
    };
}

public enum AutomationRole { Window, Dialog }

public enum InitialFocusPolicy { PageHeading, DialogHeading, PrimaryAction, PrimaryGuestActionWhenPresent, SafeAction }

public enum AnnouncementPriority { Polite, Assertive }

public enum WinUiElementRole { Page, ContentDialog, Button, TextBlock }

public sealed record AnnouncementExpectation(string Text, AnnouncementPriority? Priority);
