using FotoHavn.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace FotoHavn.App.Controls;

public sealed partial class EventCard : UserControl
{
    private XamlRoot? observedXamlRoot;

    public event EventHandler<EventCardActionEventArgs>? ActionRequested;

    public EventCard()
    {
        InitializeComponent();
        AutomationProperties.SetAutomationId(this, SemanticAutomationIds.EventCard);
        Loaded += EventCardLoaded;
        Unloaded += EventCardUnloaded;
    }

    public string CompactEventId => Presentation?.EventId is { } eventId ? FormatCompactEventId(eventId.Value) : string.Empty;

    public string AccessibleEventId => Presentation?.EventId is not null
        ? $"Event ID ending in {FormatAccessibleEventId(CompactEventId)}"
        : string.Empty;

    private EventTilePresentation? Presentation => DataContext as EventTilePresentation;

    private void EventCardLoaded(object sender, RoutedEventArgs args)
    {
        if (XamlRoot is { } root && root != observedXamlRoot)
        {
            observedXamlRoot = root;
            root.Changed += XamlRootChanged;
        }

        ApplyResponsiveLayout();
    }

    private void EventCardUnloaded(object sender, RoutedEventArgs args)
    {
        if (observedXamlRoot is { } root)
        {
            root.Changed -= XamlRootChanged;
            observedXamlRoot = null;
        }
    }

    private void XamlRootChanged(XamlRoot sender, XamlRootChangedEventArgs args) => ApplyResponsiveLayout();

    private void ApplyResponsiveLayout()
    {
        if (XamlRoot is not { } root)
        {
            return;
        }

        var stress = ResponsiveLayout.Resolve(root.Size.Width, root.Size.Height) == ResponsiveLayoutMode.Stress;
        Height = stress ? 142 : 256;
        CardActionsColumn.Width = stress ? GridLength.Auto : new GridLength(0);
        Grid.SetRow(CardActions, stress ? 0 : 4);
        Grid.SetRowSpan(CardActions, stress ? 5 : 1);
        Grid.SetColumn(CardActions, stress ? 1 : 0);
        CardActions.Margin = stress ? new Thickness(24, 0, 0, 0) : new Thickness(0);
        CardActions.VerticalAlignment = stress ? VerticalAlignment.Center : VerticalAlignment.Bottom;
    }

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
        var itemStatus = item.DeletionIncomplete
            ? "deletion-incomplete"
            : item.State.ToString().ToLowerInvariant();
        AutomationProperties.SetItemStatus(this, itemStatus);
        EventNameText.Text = item.Label;
        ToolTipService.SetToolTip(EventNameText, item.Label);
        CompactEventIdText.Text = CompactEventId;
        AutomationProperties.SetName(CompactEventIdText, AccessibleEventId);
        SavedMetadataText.Text = item.SupportingText;
        StartButton.Visibility = item.ShowsStart ? Visibility.Visible : Visibility.Collapsed;
        StartButton.IsEnabled = item.State is not EventCardState.Unavailable and not EventCardState.Busy;
        EditButton.Visibility = item.ShowsEdit ? Visibility.Visible : Visibility.Collapsed;
        DeleteButton.Visibility = item.ShowsDelete ? Visibility.Visible : Visibility.Collapsed;
        EditButton.IsEnabled = item.State != EventCardState.Busy;
        DeleteButton.IsEnabled = item.State != EventCardState.Busy;
        RetryButton.Visibility = item.ShowsRetryDeletion ? Visibility.Visible : Visibility.Collapsed;
        ApplyVisualState(item);
        SetActionSemantics(StartButton, "Start", item);
        SetActionSemantics(EditButton, "Edit", item);
        SetActionSemantics(DeleteButton, "Delete", item);
        SetActionSemantics(RetryButton, "Retry deletion for", item);
    }

    private void ApplyVisualState(EventTilePresentation item)
    {
        var resources = Application.Current.Resources;
        SavedEventCardRoot.Background = (Brush)resources[
            item.State == EventCardState.Hover
                ? "ColorSurfaceHoverBrush"
                : item.State is EventCardState.Unavailable or EventCardState.Busy || item.DeletionIncomplete
                    ? "ColorBorderDisabledBrush"
                    : "ColorSurfacePanelBrush"];
        SavedEventCardRoot.BorderBrush = (Brush)resources[item.State switch
        {
            EventCardState.Focus => "ColorFocusRingBrush",
            EventCardState.Hover => "ColorBorderStrongBrush",
            EventCardState.Unavailable => "ColorStatusWarningBorderBrush",
            _ => "ColorBorderDefaultBrush",
        }];
        SavedEventCardRoot.BorderThickness = item.State == EventCardState.Focus
            ? new Thickness(2)
            : new Thickness(1);

        var status = item.DeletionIncomplete
            ? "Deletion did not finish"
            : item.State switch
            {
                EventCardState.Busy => "Opening Event…",
                EventCardState.Unavailable => "Camera unavailable",
                _ => string.Empty,
            };
        CardStatus.Visibility = string.IsNullOrEmpty(status) ? Visibility.Collapsed : Visibility.Visible;
        CardStatusText.Text = status;
        CardStatusText.Foreground = (Brush)resources[
            item.DeletionIncomplete
                ? "ColorStatusDangerForegroundBrush"
                : item.State == EventCardState.Unavailable
                    ? "ColorStatusWarningForegroundBrush"
                    : "ColorTextMutedBrush"];
        CardProgressRing.Visibility = item.State == EventCardState.Busy ? Visibility.Visible : Visibility.Collapsed;
        CardProgressRing.IsActive = item.State == EventCardState.Busy;
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
