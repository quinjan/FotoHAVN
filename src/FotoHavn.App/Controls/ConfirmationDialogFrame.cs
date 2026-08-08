using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace FotoHavn.App.Controls;

public sealed partial class ConfirmationDialogFrame : UserControl
{
    private readonly List<(UIElement Element, bool IsHitTestVisible, AccessibilityView AccessibilityView)> backgroundState = [];
    private readonly Dictionary<UIElement, Visibility> compactCopyVisibility = [];
    private Grid? modalLayer;
    private long visibilityCallbackToken;
    private Control? previousFocus;
    private bool isActive;
    private bool stackActions;
    private bool useStressMargins;
    private double standardMaximumWidth = 500;

    public static readonly DependencyProperty DialogContentProperty = DependencyProperty.Register(
        nameof(DialogContent), typeof(object), typeof(ConfirmationDialogFrame), new PropertyMetadata(null));

    public ConfirmationDialogFrame()
    {
        InitializeComponent();
        AutomationProperties.SetAutomationId(this, SemanticAutomationIds.ModalDialog);
        Loaded += ConfirmationDialogFrameLoaded;
        Unloaded += ConfirmationDialogFrameUnloaded;
        KeyDown += ConfirmationDialogFrameKeyDown;
        SizeChanged += (_, _) => ApplyActionLayout();
    }

    public object? DialogContent
    {
        get => GetValue(DialogContentProperty);
        set => SetValue(DialogContentProperty, value);
    }

    private void ConfirmationDialogFrameLoaded(object sender, RoutedEventArgs args)
    {
        modalLayer = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(this) as Grid;
        if (modalLayer is not null)
        {
            visibilityCallbackToken = modalLayer.RegisterPropertyChangedCallback(VisibilityProperty, ModalVisibilityChanged);
        }
        ApplyModalState();
    }

    private void ConfirmationDialogFrameUnloaded(object sender, RoutedEventArgs args)
    {
        DeactivateModal();
        if (modalLayer is not null && visibilityCallbackToken != 0)
        {
            modalLayer.UnregisterPropertyChangedCallback(VisibilityProperty, visibilityCallbackToken);
        }
        modalLayer = null;
        visibilityCallbackToken = 0;
    }

    private void ModalVisibilityChanged(DependencyObject sender, DependencyProperty property) => ApplyModalState();

    private void ApplyModalState()
    {
        if (modalLayer?.Visibility == Visibility.Visible)
        {
            ActivateModal();
        }
        else
        {
            DeactivateModal();
        }
    }

    private void ActivateModal()
    {
        if (isActive || modalLayer is null)
        {
            return;
        }

        isActive = true;
        previousFocus = FocusManager.GetFocusedElement(XamlRoot) as Control;
        if (Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(modalLayer) is Panel parent)
        {
            foreach (var sibling in parent.Children.Where(element => element != modalLayer))
            {
                backgroundState.Add((sibling, sibling.IsHitTestVisible, AutomationProperties.GetAccessibilityView(sibling)));
                sibling.IsHitTestVisible = false;
                AutomationProperties.SetAccessibilityView(sibling, AccessibilityView.Raw);
            }
        }

        QueueInitialFocus();
    }

    private void DeactivateModal()
    {
        if (!isActive)
        {
            return;
        }

        isActive = false;
        foreach (var state in backgroundState)
        {
            state.Element.IsHitTestVisible = state.IsHitTestVisible;
            AutomationProperties.SetAccessibilityView(state.Element, state.AccessibilityView);
        }
        backgroundState.Clear();
        var focusTarget = previousFocus;
        previousFocus = null;
        DispatcherQueue.TryEnqueue(() => focusTarget?.Focus(FocusState.Programmatic));
    }

    private void ConfirmationDialogFrameKeyDown(object sender, KeyRoutedEventArgs args)
    {
        var buttons = FindActionButtons();
        if (buttons.Count == 0)
        {
            return;
        }

        if (args.Key == VirtualKey.Escape && buttons[0].IsEnabled)
        {
            if ((FrameworkElementAutomationPeer.CreatePeerForElement(buttons[0])?.GetPattern(PatternInterface.Invoke)) is IInvokeProvider invokeProvider)
            {
                invokeProvider.Invoke();
                args.Handled = true;
            }
            return;
        }

        if (args.Key != VirtualKey.Tab)
        {
            return;
        }

        var enabledButtons = buttons.Where(button => button.IsEnabled).ToList();
        if (enabledButtons.Count == 0)
        {
            FindDialogHeading()?.Focus(FocusState.Keyboard);
            args.Handled = true;
            return;
        }

        var focused = FocusManager.GetFocusedElement(XamlRoot) as Button;
        var shift = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift)
            .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
        if ((!shift && focused == enabledButtons[^1]) || (shift && focused == enabledButtons[0]))
        {
            enabledButtons[shift ? ^1 : 0].Focus(FocusState.Keyboard);
            args.Handled = true;
        }
    }

    public void RefreshInitialFocus()
    {
        if (isActive)
        {
            QueueInitialFocus();
        }
    }

    private void QueueInitialFocus() => DispatcherQueue.TryEnqueue(() =>
    {
        var enabledAction = FindActionButtons().FirstOrDefault(button => button.IsEnabled);
        if (enabledAction is not null)
        {
            enabledAction.Focus(FocusState.Programmatic);
            return;
        }

        FindDialogHeading()?.Focus(FocusState.Programmatic);
    });

    private List<Button> FindActionButtons() => DialogContent is DependencyObject content && FindActions(content) is { } actions
        ? actions.Children.OfType<Button>().Where(button => button.Visibility == Visibility.Visible).ToList()
        : [];

    private Control? FindDialogHeading() => DialogContent is DependencyObject content
        ? FindDialogHeading(content)
        : null;

    private static Control? FindDialogHeading(DependencyObject root)
    {
        if (root is Control control && AutomationProperties.GetHeadingLevel(control) != AutomationHeadingLevel.None)
        {
            return control;
        }

        for (var index = 0; index < Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(root); index++)
        {
            if (FindDialogHeading(Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(root, index)) is { } match)
            {
                return match;
            }
        }

        return null;
    }

    public void SetStandardMaximumWidth(double width)
    {
        standardMaximumWidth = width;
        MaxWidth = useStressMargins ? double.PositiveInfinity : standardMaximumWidth;
    }

    public void ApplyResponsiveLayout(bool stressMargins, bool shouldStackActions)
    {
        if (DialogContent is not DependencyObject content || FindActions(content) is not { } actions)
        {
            return;
        }

        useStressMargins = stressMargins;
        stackActions = shouldStackActions;
        Margin = new(stressMargins ? 16 : 24);
        MaxWidth = stressMargins ? double.PositiveInfinity : standardMaximumWidth;
        DialogBorder.Padding = stressMargins
            ? new Thickness(18, 14, 18, 16)
            : (Thickness)Application.Current.Resources["ModalDialogPadding"];
        if (FindDialogHeading() is { } heading)
        {
            heading.FontSize = stressMargins ? 21 : 32;
        }
        if (DialogContent is StackPanel rootPanel)
        {
            rootPanel.Spacing = stressMargins
                ? 6
                : (double)Application.Current.Resources["DialogContentSpacing"];
        }
        ApplySupportingCopyVisibility(DialogContent as DependencyObject, shouldStackActions);
        ApplyResponsiveContent(DialogContent as DependencyObject, stressMargins);
        ApplyActionLayout();
    }

    private void ApplyActionLayout()
    {
        if (DialogContent is not DependencyObject content || FindActions(content) is not { } actions)
        {
            return;
        }

        var buttons = actions.Children.OfType<Button>()
            .Where(button => button.Visibility == Visibility.Visible)
            .ToArray();
        actions.Orientation = stackActions ? Orientation.Vertical : Orientation.Horizontal;
        actions.HorizontalAlignment = HorizontalAlignment.Stretch;
        var innerWidth = Math.Max(0, ActualWidth - 64);
        var standardButtonWidth = buttons.Length == 0
            ? double.NaN
            : Math.Max(0, (innerWidth - (actions.Spacing * (buttons.Length - 1))) / buttons.Length);
        foreach (var button in buttons)
        {
            button.HorizontalAlignment = HorizontalAlignment.Stretch;
            button.Width = stackActions ? double.NaN : standardButtonWidth;
        }
    }

    private static StackPanel? FindActions(DependencyObject root)
    {
        if (root is StackPanel panel && AutomationProperties.GetName(panel) == "Dialog actions")
        {
            return panel;
        }

        for (var index = 0; index < Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(root); index++)
        {
            if (FindActions(Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(root, index)) is { } match)
            {
                return match;
            }
        }

        return null;
    }

    private static void ApplyResponsiveContent(DependencyObject? root, bool stress)
    {
        if (root is null)
        {
            return;
        }

        if (root is DialogSemanticIcon icon)
        {
            icon.ApplyResponsiveLayout(stress);
        }
        if (root is DialogEventIdentity identity)
        {
            identity.ApplyResponsiveLayout(stress);
        }

        for (var index = 0; index < Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(root); index++)
        {
            ApplyResponsiveContent(Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(root, index), stress);
        }
    }

    private void ApplySupportingCopyVisibility(DependencyObject? root, bool hideSupportingCopy)
    {
        if (root is null)
        {
            return;
        }

        if (root is TextBlock text &&
            AutomationProperties.GetName(text) == "Consequence when present")
        {
            if (hideSupportingCopy)
            {
                compactCopyVisibility.TryAdd(text, text.Visibility);
                text.Visibility = Visibility.Collapsed;
            }
            else if (compactCopyVisibility.Remove(text, out var visibility))
            {
                text.Visibility = visibility;
            }
        }

        for (var index = 0; index < Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(root); index++)
        {
            ApplySupportingCopyVisibility(
                Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(root, index),
                hideSupportingCopy);
        }
    }
}
