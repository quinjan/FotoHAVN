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
    private Grid? modalLayer;
    private long visibilityCallbackToken;
    private Control? previousFocus;
    private bool isActive;

    public static readonly DependencyProperty DialogContentProperty = DependencyProperty.Register(
        nameof(DialogContent), typeof(object), typeof(ConfirmationDialogFrame), new PropertyMetadata(null));

    public ConfirmationDialogFrame()
    {
        InitializeComponent();
        AutomationProperties.SetAutomationId(this, SemanticAutomationIds.ModalDialog);
        Loaded += ConfirmationDialogFrameLoaded;
        Unloaded += ConfirmationDialogFrameUnloaded;
        KeyDown += ConfirmationDialogFrameKeyDown;
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

        DispatcherQueue.TryEnqueue(() => FindActionButtons().FirstOrDefault()?.Focus(FocusState.Programmatic));
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

        var focused = FocusManager.GetFocusedElement(XamlRoot) as Button;
        var shift = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift)
            .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
        if ((!shift && focused == buttons[^1]) || (shift && focused == buttons[0]))
        {
            buttons[shift ? ^1 : 0].Focus(FocusState.Keyboard);
            args.Handled = true;
        }
    }

    private List<Button> FindActionButtons() => DialogContent is DependencyObject content && FindActions(content) is { } actions
        ? actions.Children.OfType<Button>().Where(button => button.Visibility == Visibility.Visible).ToList()
        : [];

    public void ApplyResponsiveLayout(bool stress)
    {
        if (DialogContent is not DependencyObject content || FindActions(content) is not { } actions)
        {
            return;
        }

        Margin = new(stress ? 16 : 24);
        actions.Orientation = stress ? Orientation.Vertical : Orientation.Horizontal;
        actions.HorizontalAlignment = stress ? HorizontalAlignment.Stretch : HorizontalAlignment.Right;
        foreach (var child in actions.Children.OfType<FrameworkElement>())
        {
            child.HorizontalAlignment = stress ? HorizontalAlignment.Stretch : HorizontalAlignment.Right;
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
}
