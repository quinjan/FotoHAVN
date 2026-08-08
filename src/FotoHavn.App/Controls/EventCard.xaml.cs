using FotoHavn.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;

namespace FotoHavn.App.Controls;

public sealed partial class EventCard : UserControl
{
    public event EventHandler<EventCardActionEventArgs>? ActionRequested;

    public EventCard()
    {
        InitializeComponent();
        AutomationProperties.SetAutomationId(this, SemanticAutomationIds.EventCard);
    }

    public string CompactEventId => Presentation?.EventId is { } eventId ? FormatCompactEventId(eventId.Value) : string.Empty;

    public string AccessibleEventId => Presentation?.EventId is not null
        ? $"Event ID ending in {FormatAccessibleEventId(CompactEventId)}"
        : string.Empty;

    private EventTilePresentation? Presentation => DataContext as EventTilePresentation;

    private void EventCardDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args) => ApplyPresentation();

    private void ApplyPresentation()
    {
        if (Presentation is not { } item)
        {
            return;
        }

        NewEventButton.Visibility = item.ShowsCreate ? Visibility.Visible : Visibility.Collapsed;
        SavedEventCardRoot.Visibility = item.ShowsSavedEventCard ? Visibility.Visible : Visibility.Collapsed;
        if (!item.ShowsSavedEventCard)
        {
            return;
        }

        var semanticScope = item.EventId?.Value ?? "unknown";
        AutomationProperties.SetAutomationId(this, SemanticAutomationIds.Scoped(SemanticAutomationIds.EventCard, semanticScope));
        AutomationProperties.SetName(this, $"{item.Label}, {AccessibleEventId}");
        AutomationProperties.SetItemStatus(this, item.DeletionIncomplete ? "deletion-incomplete" : "ready");
        EventNameText.Text = item.Label;
        ToolTipService.SetToolTip(EventNameText, item.Label);
        CompactEventIdText.Text = CompactEventId;
        AutomationProperties.SetName(CompactEventIdText, AccessibleEventId);
        SavedMetadataText.Text = item.SupportingText;
        StartButton.Visibility = item.ShowsStart ? Visibility.Visible : Visibility.Collapsed;
        EditButton.Visibility = item.ShowsEdit ? Visibility.Visible : Visibility.Collapsed;
        DeleteButton.Visibility = item.ShowsDelete ? Visibility.Visible : Visibility.Collapsed;
        RetryButton.Visibility = item.ShowsRetryDeletion ? Visibility.Visible : Visibility.Collapsed;
        SetActionSemantics(StartButton, "Start", item);
        SetActionSemantics(EditButton, "Edit", item);
        SetActionSemantics(DeleteButton, "Delete", item);
        SetActionSemantics(RetryButton, "Retry deletion for", item);
    }

    private static string FormatCompactEventId(string value)
    {
        var hexadecimal = new string(value.Where(Uri.IsHexDigit).ToArray()).ToUpperInvariant();
        var suffix = hexadecimal.Length > 8 ? hexadecimal[^8..] : hexadecimal.PadLeft(8, '0');
        return $"{suffix[..4]} · {suffix[4..]}";
    }

    private static string FormatAccessibleEventId(string compactEventId) => string.Join(
        ", ",
        compactEventId.Split(" · ").Select(group => string.Join(' ', group)));

    private static void SetActionSemantics(Button button, string verb, EventTilePresentation item)
    {
        var scope = item.EventId?.Value ?? "unknown";
        AutomationProperties.SetAutomationId(button, SemanticAutomationIds.Scoped(SemanticAutomationIds.EventCard, $"{scope}.{verb.Replace(" ", string.Empty)}"));
        var compact = FormatCompactEventId(scope);
        AutomationProperties.SetName(button, $"{verb} {item.Label}, Event ID ending in {FormatAccessibleEventId(compact)}");
        ToolTipService.SetToolTip(button, $"{verb} {item.Label}, {compact}");
    }

    private void NewEventClicked(object sender, RoutedEventArgs args) => Raise(EventCardAction.New);
    private void StartClicked(object sender, RoutedEventArgs args) => Raise(EventCardAction.Start);
    private void EditClicked(object sender, RoutedEventArgs args) => Raise(EventCardAction.Edit);
    private void DeleteClicked(object sender, RoutedEventArgs args) => Raise(EventCardAction.Delete);
    private void RetryClicked(object sender, RoutedEventArgs args) => Raise(EventCardAction.RetryDeletion);

    private void Raise(EventCardAction action) => ActionRequested?.Invoke(this, new(action, Presentation?.EventId));
}

public enum EventCardAction
{
    New,
    Start,
    Edit,
    Delete,
    RetryDeletion,
}

public sealed record EventCardActionEventArgs(EventCardAction Action, EventId? EventId);
