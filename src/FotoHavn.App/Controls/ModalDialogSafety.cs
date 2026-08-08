using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;

namespace FotoHavn.App.Controls;

public static class ModalDialogSafety
{
    public static readonly DependencyProperty IsBusyProperty = DependencyProperty.RegisterAttached(
        "IsBusy", typeof(bool), typeof(ModalDialogSafety), new PropertyMetadata(false, OnIsBusyChanged));

    private static readonly DependencyProperty IdleCloseButtonTextProperty = DependencyProperty.RegisterAttached(
        "IdleCloseButtonText", typeof(string), typeof(ModalDialogSafety), new PropertyMetadata(string.Empty));

    private static readonly DependencyProperty IdleActionButtonTextProperty = DependencyProperty.RegisterAttached(
        "IdleActionButtonText", typeof(string), typeof(ModalDialogSafety), new PropertyMetadata(string.Empty));

    private static readonly DependencyProperty BusyButtonProperty = DependencyProperty.RegisterAttached(
        "BusyButton", typeof(ContentDialogButton), typeof(ModalDialogSafety),
        new PropertyMetadata(ContentDialogButton.Secondary));

    private static readonly DependencyProperty StressLayoutProperty = DependencyProperty.RegisterAttached(
        "StressLayout", typeof(bool), typeof(ModalDialogSafety), new PropertyMetadata(false));

    public static bool GetIsBusy(DependencyObject target) => (bool)target.GetValue(IsBusyProperty);

    public static void SetIsBusy(DependencyObject target, bool value) => target.SetValue(IsBusyProperty, value);

    public static void ConfigureDecision(ContentDialog dialog, bool destructive)
    {
        ArgumentNullException.ThrowIfNull(dialog);
        dialog.PrimaryButtonStyle = (Style)Application.Current.Resources["FotoHavnActionButtonSecondaryStyle"];
        dialog.SecondaryButtonStyle = (Style)Application.Current.Resources[
            destructive ? "FotoHavnActionButtonDestructiveStyle" : "FotoHavnActionButtonPrimaryStyle"];
        dialog.MaxWidth = (double)Application.Current.Resources["ModalDialogMaximumWidth"];
        dialog.DefaultButton = ContentDialogButton.Primary;
        dialog.CloseButtonText = string.Empty;
        EnsureLevelOneHeading(dialog);
    }

    public static void ConfigureAcknowledgement(ContentDialog dialog)
    {
        ArgumentNullException.ThrowIfNull(dialog);
        dialog.PrimaryButtonStyle = (Style)Application.Current.Resources["FotoHavnActionButtonPrimaryStyle"];
        dialog.MaxWidth = (double)Application.Current.Resources["ModalAcknowledgementMaximumWidth"];
        dialog.DefaultButton = ContentDialogButton.Primary;
        dialog.SecondaryButtonText = string.Empty;
        dialog.CloseButtonText = string.Empty;
        EnsureLevelOneHeading(dialog);
    }

    public static void BeginBusy(
        ContentDialog dialog,
        string busyLabel,
        ContentDialogButton initiatingAction = ContentDialogButton.Secondary)
    {
        ArgumentNullException.ThrowIfNull(dialog);
        ArgumentException.ThrowIfNullOrWhiteSpace(busyLabel);
        if (!GetIsBusy(dialog))
        {
            dialog.SetValue(BusyButtonProperty, initiatingAction);
            dialog.SetValue(
                IdleActionButtonTextProperty,
                initiatingAction == ContentDialogButton.Primary ? dialog.PrimaryButtonText : dialog.SecondaryButtonText);
        }

        if (initiatingAction == ContentDialogButton.Primary)
        {
            dialog.PrimaryButtonText = busyLabel;
        }
        else
        {
            dialog.SecondaryButtonText = busyLabel;
        }
        SetIsBusy(dialog, true);
        AutomationProperties.SetLiveSetting(dialog, AutomationLiveSetting.Polite);
        var peer = FrameworkElementAutomationPeer.FromElement(dialog) ?? FrameworkElementAutomationPeer.CreatePeerForElement(dialog);
        peer?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
    }

    public static void EndBusy(ContentDialog dialog)
    {
        ArgumentNullException.ThrowIfNull(dialog);
        var busyButton = (ContentDialogButton)dialog.GetValue(BusyButtonProperty);
        SetIsBusy(dialog, false);
        if (busyButton == ContentDialogButton.Primary)
        {
            dialog.PrimaryButtonText = (string)dialog.GetValue(IdleActionButtonTextProperty);
        }
        else
        {
            dialog.SecondaryButtonText = (string)dialog.GetValue(IdleActionButtonTextProperty);
        }
        AutomationProperties.SetLiveSetting(dialog, AutomationLiveSetting.Off);
    }

    public static void PresentFailure(ContentDialog dialog, string retryLabel, string failureMessage)
    {
        ArgumentNullException.ThrowIfNull(dialog);
        ArgumentException.ThrowIfNullOrWhiteSpace(retryLabel);
        ArgumentException.ThrowIfNullOrWhiteSpace(failureMessage);
        EndBusy(dialog);
        if ((ContentDialogButton)dialog.GetValue(BusyButtonProperty) == ContentDialogButton.Primary)
        {
            dialog.PrimaryButtonText = retryLabel;
        }
        else
        {
            dialog.SecondaryButtonText = retryLabel;
        }
        AutomationProperties.SetHelpText(dialog, failureMessage);
        AutomationProperties.SetLiveSetting(dialog, AutomationLiveSetting.Assertive);
        var peer = FrameworkElementAutomationPeer.FromElement(dialog) ?? FrameworkElementAutomationPeer.CreatePeerForElement(dialog);
        peer?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
    }

    public static async Task<ContentDialogResult> ShowAsync(
        ContentDialog dialog,
        ContentDialogButton safeInitialFocus,
        ContentDialogButton confirmationAction = ContentDialogButton.Secondary,
        Control? invoker = null)
    {
        ArgumentNullException.ThrowIfNull(dialog);

        if (string.IsNullOrWhiteSpace(AutomationProperties.GetAutomationId(dialog)))
        {
            AutomationProperties.SetAutomationId(dialog, SemanticAutomationIds.ModalDialog);
        }

        TypedEventHandler<ContentDialog, ContentDialogClosingEventArgs> preventBusyDismissal = (_, args) =>
        {
            if (GetIsBusy(dialog))
            {
                args.Cancel = true;
            }
        };
        TypedEventHandler<ContentDialog, ContentDialogOpenedEventArgs> applyResponsiveLayout =
            (_, _) => ApplyButtonLayout(dialog);
        dialog.DefaultButton = safeInitialFocus;
        dialog.Closing += preventBusyDismissal;
        dialog.Opened += applyResponsiveLayout;
        try
        {
            var result = await dialog.ShowAsync();
            var confirmed = confirmationAction switch
            {
                ContentDialogButton.Primary => result == ContentDialogResult.Primary,
                ContentDialogButton.Secondary => result == ContentDialogResult.Secondary,
                _ => false,
            };
            if (!confirmed)
            {
                invoker?.Focus(FocusState.Programmatic);
            }

            return result;
        }
        catch
        {
            invoker?.Focus(FocusState.Programmatic);
            throw;
        }
        finally
        {
            dialog.Closing -= preventBusyDismissal;
            dialog.Opened -= applyResponsiveLayout;
        }
    }

    private static void OnIsBusyChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is not ContentDialog dialog)
        {
            throw new ArgumentException("Busy dismissal safety applies only to ContentDialog.");
        }

        var isBusy = (bool)args.NewValue;
        dialog.IsPrimaryButtonEnabled = !isBusy;
        dialog.IsSecondaryButtonEnabled = !isBusy;
        if (isBusy)
        {
            dialog.SetValue(IdleCloseButtonTextProperty, dialog.CloseButtonText);
            dialog.CloseButtonText = string.Empty;
        }
        else if (dialog.GetValue(IdleCloseButtonTextProperty) is string closeButtonText)
        {
            dialog.CloseButtonText = closeButtonText;
        }
        AutomationProperties.SetHelpText(dialog, isBusy ? "An action is in progress. This dialog cannot be dismissed." : string.Empty);
    }

    public static void ApplyResponsiveLayout(ContentDialog dialog, double availableWidth, double availableHeight)
    {
        ArgumentNullException.ThrowIfNull(dialog);
        var resources = Application.Current.Resources;
        var stress = ResponsiveLayout.Resolve(availableWidth, availableHeight) == ResponsiveLayoutMode.Stress;
        dialog.SetValue(StressLayoutProperty, stress);
        dialog.MaxWidth = stress
            ? Math.Max(0, availableWidth - (2 * (double)resources["SpaceStackDefault"]))
            : (double)resources["ModalDialogMaximumWidth"];
        if (dialog.IsLoaded)
        {
            ApplyButtonLayout(dialog);
        }
    }

    private static void EnsureLevelOneHeading(ContentDialog dialog)
    {
        if (dialog.Title is TextBlock heading)
        {
            AutomationProperties.SetHeadingLevel(heading, AutomationHeadingLevel.Level1);
            return;
        }

        if (dialog.Title is string title)
        {
            heading = new TextBlock
            {
                Text = title,
                Style = (Style)Application.Current.Resources["TypeHeadingDialogStyle"],
                TextWrapping = TextWrapping.Wrap,
            };
            AutomationProperties.SetHeadingLevel(heading, AutomationHeadingLevel.Level1);
            dialog.Title = heading;
        }
    }

    private static void ApplyButtonLayout(ContentDialog dialog)
    {
        if (FindNamedDescendant<Grid>(dialog, "CommandSpace") is not { } commandSpace ||
            FindNamedDescendant<Button>(dialog, "PrimaryButton") is not { } primary ||
            FindNamedDescendant<Button>(dialog, "SecondaryButton") is not { } secondary)
        {
            return;
        }

        var stress = (bool)dialog.GetValue(StressLayoutProperty);
        commandSpace.RowDefinitions.Clear();
        Grid.SetRow(primary, 0);
        Grid.SetRow(secondary, stress ? 1 : 0);
        Grid.SetColumn(primary, 0);
        Grid.SetColumnSpan(primary, stress ? 5 : 1);
        Grid.SetColumn(secondary, stress ? 0 : 4);
        Grid.SetColumnSpan(secondary, stress ? 5 : 1);
        primary.HorizontalAlignment = HorizontalAlignment.Stretch;
        secondary.HorizontalAlignment = HorizontalAlignment.Stretch;
        if (stress)
        {
            commandSpace.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            commandSpace.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            primary.Margin = new Thickness(0, 0, 0, (double)Application.Current.Resources["TargetSeparation"]);
        }
        else
        {
            primary.Margin = new Thickness(0);
        }
    }

    private static T? FindNamedDescendant<T>(DependencyObject root, string name) where T : FrameworkElement
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is T match && match.Name == name)
            {
                return match;
            }

            if (FindNamedDescendant<T>(child, name) is { } descendant)
            {
                return descendant;
            }
        }

        return null;
    }
}
