using FotoHavn.Core;
using FotoHavn.App.Surfaces;
using FotoHavn.App.Controls;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI;
using Windows.Storage;
using Windows.Graphics.Imaging;
using Windows.Graphics;
#if UI_VERIFICATION
using FotoHavn.App.UiVerification;
#endif

namespace FotoHavn.App;

public sealed partial class MainWindow : Window
{
    private readonly IApplicationPresentationController presentationController;
    private readonly CameraBoundary? camera;
    private readonly ApplicationPresentationAdapter presentationAdapter;
    private readonly HashSet<Border> hoveredEventCards = [];
    private ApplicationCanvasPresentation? canvas;
    private bool applyingPresentation;
    private string? loadingPhotoStripPath;
    private bool photoStripVisibleSignaled;
    private bool photoStripFadeStarted;
    private bool setupWasOpen;
    private bool exitConfirmationWasOpen;
    private bool exitConfirmationBusy;
    private ApplicationSurface? lastSurface;
    private GuardedExitAction? exitEventInvoker;
    private string? lastAnnouncement;
    private ApplicationPresentation? currentPresentation;
    private ApplicationSurfaceOverride? currentSurfaceOverride;
    private ResponsiveLayoutMode responsiveMode = ResponsiveLayoutMode.Standard;
#if UI_VERIFICATION
    private readonly UiVerificationRenderSettledSignal renderSettledSignal;
    private ApplicationSurfaceOverride? mediaPendingRenderSettlement;
#endif

    public MainWindow(IApplicationPresentationController presentationController, CameraBoundary? camera = null)
    {
        this.presentationController = presentationController;
        this.camera = camera;
        InitializeComponent();
        presentationAdapter = new ApplicationPresentationAdapter(
            EventScrollViewer,
            SetupLayer,
            StartGuestLayer,
            GuestAssistanceLayer,
            GuestCaptureLayer,
            GuestAssistanceLayer,
            PhotoStripLayer,
            ExitEventConfirmationLayer,
            EventDeletionLayer,
            StartEventConfirmationLayer,
            DiscardDraftLayer,
            SaveChangesLayer);
#if UI_VERIFICATION
        renderSettledSignal = new UiVerificationRenderSettledSignal(WindowRoot);
#endif
        PreviewViewport.Width = PreviewViewport.Height * CameraPreviewRenderPolicy.CropAspectRatio;
        PreviewSurface.Width = PreviewViewport.Width;
        PreviewSurface.Height = PreviewViewport.Height;
        var mirror = CameraPreviewRenderPolicy.CreateMirror(PreviewSurface.Width);
        PreviewSurface.RenderTransform = new ScaleTransform
        {
            ScaleX = mirror.ScaleX,
            CenterX = mirror.CenterX,
        };
        var guestMirror = CameraPreviewRenderPolicy.CreateMirror(GuestPreviewViewport.Width);
        GuestPreviewImage.RenderTransform = new ScaleTransform
        {
            ScaleX = guestMirror.ScaleX,
            CenterX = guestMirror.CenterX,
        };
        presentationController.PresentationChanged += PresentationChanged;
        Activated += MainWindowActivated;
        if (camera is not null)
        {
            camera.PreviewFrameAvailable += PreviewFrameAvailable;
        }
    }

    public async Task LoadPresentationAsync(CancellationToken cancellationToken = default)
    {
        var presentation = await presentationController.ExecuteAsync(new LaunchApplication(), cancellationToken);
        HeadingText.Text = presentation.Heading;
        EventTiles.ItemsSource = presentation.EventTiles;
        canvas = presentation.Canvas;
        ApplyPresentation(presentation);
    }

    private async void EventTileClicked(object sender, RoutedEventArgs args)
    {
        if (sender is Button { DataContext: EventTilePresentation { Kind: EventTileKind.NewEvent } })
        {
            await ExecuteAsync(new OpenNewEvent());
        }
        else if (sender is Button { DataContext: EventTilePresentation { Kind: EventTileKind.SavedEvent, EventId: { } eventId } })
        {
            await ExecuteAsync(new OpenSavedEvent(eventId));
        }
    }

    private async void StartSavedEventClicked(object sender, RoutedEventArgs args)
    {
        if (sender is Button { DataContext: EventTilePresentation { EventId: { } eventId } })
        {
            await ExecuteAsync(new StartSavedEvent(eventId));
        }
    }

    private async void ConfirmStartEventClicked(object sender, RoutedEventArgs args) =>
        await ExecuteAsync(new ConfirmStartSavedEvent());

    private async void CancelStartEventClicked(object sender, RoutedEventArgs args) =>
        await ExecuteAsync(new CancelStartSavedEvent());

    private async void ExitEventHoldCompleted(object? sender, EventArgs args)
    {
        exitEventInvoker = sender as GuardedExitAction;
        await ExecuteAsync(new ExitActiveEvent());
    }

    private async void ConfirmExitEventClicked(object sender, RoutedEventArgs args)
    {
        exitConfirmationBusy = true;
        SetExitActionsEnabled(false);
        exitEventInvoker?.ShowBusy("Exiting event…");
        await ExecuteAsync(new ConfirmExitActiveEvent());
    }

    private async void CancelExitEventClicked(object sender, RoutedEventArgs args)
    {
        await CancelExitConfirmationAsync();
    }

    private void MainWindowActivated(object sender, WindowActivatedEventArgs args)
    {
        if (args.WindowActivationState == WindowActivationState.Deactivated)
        {
            return;
        }

        if (ExitEventConfirmationLayer.Visibility == Visibility.Visible)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                KeepEventActiveButton.Focus(FocusState.Programmatic);
                if (currentPresentation is not null && lastSurface is { } modalSurface)
                {
                    ApplySurfaceAnnouncement(currentPresentation, modalSurface, currentSurfaceOverride);
                }
            });
            _ = FocusExitConfirmationAsync();
        }
        else if (lastSurface is { } surface)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                FocusInitialGuestAction(surface);
                if (currentPresentation is not null)
                {
                    ApplySurfaceAnnouncement(currentPresentation, surface, currentSurfaceOverride);
                }
            });
            _ = FocusInitialGuestActionAsync(surface);
        }
    }

    private async void ExitEventConfirmationKeyDown(object sender, KeyRoutedEventArgs args)
    {
        if (args.Key == Windows.System.VirtualKey.Escape)
        {
            args.Handled = true;
            if (!exitConfirmationBusy)
            {
                await CancelExitConfirmationAsync();
            }
            return;
        }

        if (args.Key != Windows.System.VirtualKey.Tab)
        {
            return;
        }

        var shiftPressed = Microsoft.UI.Input.InputKeyboardSource
            .GetKeyStateForCurrentThread(Windows.System.VirtualKey.Shift)
            .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
        var focused = FocusManager.GetFocusedElement(WindowRoot.XamlRoot);
        if ((shiftPressed && ReferenceEquals(focused, KeepEventActiveButton)) ||
            (!shiftPressed && ReferenceEquals(focused, ConfirmExitEventButton)))
        {
            args.Handled = true;
            (shiftPressed ? ConfirmExitEventButton : KeepEventActiveButton).Focus(FocusState.Keyboard);
        }
    }

    private async Task CancelExitConfirmationAsync()
    {
        await ExecuteAsync(new CancelExitActiveEvent());
        exitConfirmationBusy = false;
        ExitEventAction.ShowIdle();
        AssistanceExitEventAction.ShowIdle();
        AssistanceExitOnlyAction.ShowIdle();
        SetExitActionsEnabled(true);
        var invoker = exitEventInvoker;
        exitEventInvoker = null;
        invoker?.FocusAction();
    }

    private async Task FocusExitConfirmationAsync()
    {
        var dispatcherQueue = DispatcherQueue;
        await Task.Delay(150);
        dispatcherQueue.TryEnqueue(() =>
        {
            if (ExitEventConfirmationLayer.Visibility == Visibility.Visible)
            {
                KeepEventActiveButton.Focus(FocusState.Programmatic);
            }
        });
    }

    private async Task FocusInitialGuestActionAsync(ApplicationSurface surface)
    {
        var dispatcherQueue = DispatcherQueue;
        foreach (var delay in new[] { 100, 150, 250 })
        {
            await Task.Delay(delay);
            dispatcherQueue.TryEnqueue(() =>
            {
                if (ExitEventConfirmationLayer.Visibility != Visibility.Visible)
                {
                    FocusInitialGuestAction(surface);
                }
            });
        }
    }

    private void SetExitActionsEnabled(bool enabled)
    {
        ExitEventAction.IsEnabled = enabled;
        AssistanceExitEventAction.IsEnabled = enabled;
        AssistanceExitOnlyAction.IsEnabled = enabled;
        KeepEventActiveButton.IsEnabled = enabled;
        ConfirmExitEventButton.IsEnabled = enabled;
    }

    private void ActiveEventLayerSizeChanged(object sender, SizeChangedEventArgs args)
    {
        var mode = ResponsiveLayout.Resolve(args.NewSize.Width, args.NewSize.Height);
        responsiveMode = mode;
        switch (mode)
        {
            case ResponsiveLayoutMode.Standard:
                GuestStartContent.Width = 780;
                GuestStartContent.Margin = new Thickness(0, 29, 0, 0);
                ActiveEventHeadingText.FontSize = 60;
                AssistancePanel.Margin = new Thickness(260, 193, 260, 191);
                AssistancePanel.Padding = new Thickness(40, 30, 40, 30);
                AssistanceContent.Width = 700;
                AssistanceHeadingText.FontSize = 38;
                GuestRetentionText.Visibility = Visibility.Visible;
                break;
            case ResponsiveLayoutMode.Compact:
                GuestStartContent.Width = Math.Min(720, args.NewSize.Width - 96);
                GuestStartContent.Margin = new Thickness(0, 12, 0, 0);
                ActiveEventHeadingText.FontSize = 52;
                AssistancePanel.Margin = new Thickness(80, 80, 80, 50);
                AssistancePanel.Padding = new Thickness(32, 24, 32, 24);
                AssistanceContent.Width = Math.Min(650, args.NewSize.Width - 200);
                AssistanceHeadingText.FontSize = 36;
                GuestRetentionText.Visibility = Visibility.Visible;
                break;
            default:
                GuestStartContent.Width = Math.Max(320, args.NewSize.Width - 50);
                GuestStartContent.Margin = new Thickness(0, 40, 0, 0);
                ActiveEventHeadingText.FontSize = 32;
                AssistancePanel.Margin = new Thickness(24, 33, 24, 43);
                AssistancePanel.Padding = new Thickness(20, 16, 20, 16);
                AssistanceContent.Width = Math.Max(300, args.NewSize.Width - 80);
                AssistanceHeadingText.FontSize = 30;
                GuestRetentionText.Visibility = Visibility.Collapsed;
                AssistanceEyebrowText.Visibility = Visibility.Collapsed;
                AssistanceProgressText.Visibility = currentPresentation?.ActiveEvent?.GuestCycle.Phase ==
                    GuestCyclePhase.OperatorAssistance
                        ? Visibility.Visible
                        : Visibility.Collapsed;
                break;
        }
        if (mode is not ResponsiveLayoutMode.Stress)
        {
            AssistanceEyebrowText.Visibility = Visibility.Visible;
            AssistanceProgressText.Visibility = Visibility.Visible;
        }
    }

    private async void StartGuestCycleClicked(object sender, RoutedEventArgs args) =>
        await ExecuteAsync(new StartGuestCycle());

    private async void RetryGuestCycleClicked(object sender, RoutedEventArgs args) =>
        await ExecuteAsync(new RetryGuestCycle());

    private async void EditSavedEventClicked(object sender, RoutedEventArgs args)
    {
        if (sender is Button { DataContext: EventTilePresentation { EventId: { } eventId } })
        {
            await ExecuteAsync(new OpenSavedEvent(eventId));
        }
    }

    private async void DeleteSavedEventClicked(object sender, RoutedEventArgs args)
    {
        if (sender is Button { DataContext: EventTilePresentation { EventId: { } eventId } })
        {
            await ExecuteAsync(new DeleteSavedEvent(eventId));
        }
    }

    private async void RetryEventDeletionClicked(object sender, RoutedEventArgs args)
    {
        if (sender is Button { DataContext: EventTilePresentation { EventId: { } eventId } })
        {
            await ExecuteAsync(new RetryEventDeletion(eventId));
        }
    }

    private async void ConfirmEventDeletionClicked(object sender, RoutedEventArgs args)
    {
        if (presentationController.CurrentPresentation.EventDeletion?.Stage == EventDeletionStage.Confirmation)
        {
            await ExecuteAsync(new ConfirmDeleteSavedEvent());
        }
        else
        {
            await ExecuteAsync(new DismissEventDeletionResult());
        }
    }

    private async void CancelEventDeletionClicked(object sender, RoutedEventArgs args) =>
        await ExecuteAsync(new CancelDeleteSavedEvent());

    private void SavedEventCardPointerEntered(object sender, PointerRoutedEventArgs args)
    {
        if (sender is Border card)
        {
            hoveredEventCards.Add(card);
            ApplySavedEventCardState(card, isActive: true);
        }
    }

    private void SavedEventCardPointerExited(object sender, PointerRoutedEventArgs args)
    {
        if (sender is Border card)
        {
            hoveredEventCards.Remove(card);
            if (FindNamedDescendant<Button>(card, "StartEventButton") is not { FocusState: not FocusState.Unfocused })
            {
                ApplySavedEventCardState(card, isActive: false);
            }
        }
    }

    private void StartEventButtonGotFocus(object sender, RoutedEventArgs args)
    {
        if (sender is Button button && FindAncestor<Border>(button, "SavedEventCard") is { } card)
        {
            ApplySavedEventCardState(card, isActive: true);
        }
    }

    private void StartEventButtonLostFocus(object sender, RoutedEventArgs args)
    {
        if (sender is Button button &&
            FindAncestor<Border>(button, "SavedEventCard") is { } card &&
            !hoveredEventCards.Contains(card))
        {
            ApplySavedEventCardState(card, isActive: false);
        }
    }

    private static void ApplySavedEventCardState(Border card, bool isActive)
    {
        if (card.DataContext is EventTilePresentation { DeletionIncomplete: true })
        {
            card.Background = (Brush)Application.Current.Resources["SurfaceBrush"];
            return;
        }

        if (FindNamedDescendant<Button>(card, "StartEventButton") is { } startButton)
        {
            startButton.Opacity = isActive ? 1 : 0;
        }

        if (FindNamedDescendant<StackPanel>(card, "SavedEventMetadata") is { } metadata)
        {
            metadata.Opacity = isActive ? 0.16 : 1;
        }

        card.Background = (Brush)Application.Current.Resources[
            isActive ? "EventHoverBrush" : "SurfaceBrush"];
    }

    private static T? FindNamedDescendant<T>(DependencyObject root, string name)
        where T : FrameworkElement
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is T { Name: var childName } match && childName == name)
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

    private static T? FindAncestor<T>(DependencyObject child, string name)
        where T : FrameworkElement
    {
        for (var current = VisualTreeHelper.GetParent(child); current is not null; current = VisualTreeHelper.GetParent(current))
        {
            if (current is T { Name: var currentName } match && currentName == name)
            {
                return match;
            }
        }

        return null;
    }

    private async void EventNameTextChanged(object sender, TextChangedEventArgs args)
    {
        if (!applyingPresentation)
        {
            await ExecuteAsync(new ChangeEventName(EventNameTextBox.Text));
        }
    }

    private async void CameraSelectionChanged(object sender, SelectionChangedEventArgs args)
    {
        if (!applyingPresentation && CameraComboBox.SelectedItem is AvailableCamera selected)
        {
            await ExecuteAsync(new SelectCamera(selected.DeviceId));
        }
    }

    private async void RetryStorageClicked(object sender, RoutedEventArgs args) =>
        await ExecuteAsync(new RetryEventStorage());

    private async void CancelSetupClicked(object sender, RoutedEventArgs args) =>
        await ExecuteAsync(new CancelEventSetup());

    private async void KeepEditingClicked(object sender, RoutedEventArgs args) =>
        await ExecuteAsync(new KeepEditingEventSetup());

    private async void DiscardDraftClicked(object sender, RoutedEventArgs args) =>
        await ExecuteAsync(new DiscardEventSetupDraft());

    private async void SaveCloseClicked(object sender, RoutedEventArgs args) =>
        await ExecuteAsync(new SaveAndCloseEventSetup());

    private async void SaveStartClicked(object sender, RoutedEventArgs args) =>
        await ExecuteAsync(new SaveAndStartEvent());

    private async void CancelSaveClicked(object sender, RoutedEventArgs args) =>
        await ExecuteAsync(new CancelEventSetupSave());

    private async void ConfirmSaveClicked(object sender, RoutedEventArgs args) =>
        await ExecuteAsync(new ConfirmEventSetupSave());

    private async void SetupLayerKeyDown(object sender, KeyRoutedEventArgs args)
    {
        if (presentationController.CurrentPresentation.Setup is { } setup &&
            (setup.ShowsDiscardConfirmation || setup.ShowsSaveConfirmation))
        {
            args.Handled = true;
            return;
        }

        if (args.Key == Windows.System.VirtualKey.Escape)
        {
            if (CameraComboBox.IsDropDownOpen)
            {
                CameraComboBox.IsDropDownOpen = false;
            }
            else
            {
                await ExecuteAsync(new CancelEventSetup());
            }

            args.Handled = true;
        }
    }

    private void PresentationChanged(object? sender, ApplicationPresentation presentation) =>
        DispatcherQueue.TryEnqueue(() => ApplyPresentation(presentation));

    private void ApplyPresentation(ApplicationPresentation presentation)
    {
        var shouldFocusEventName = false;
        var surfaceOverride = (presentationController as IApplicationSurfaceOverrideSource)?.CurrentSurfaceOverride;
        currentPresentation = presentation;
        currentSurfaceOverride = surfaceOverride;
#if UI_VERIFICATION
        if (surfaceOverride is not null)
        {
            mediaPendingRenderSettlement = null;
            renderSettledSignal.Begin(surfaceOverride);
        }
#endif
        applyingPresentation = true;
        try
        {
            HeadingText.Text = presentation.Heading;
            EventTiles.ItemsSource = presentation.EventTiles;
            var activeEvent = presentation.ActiveEvent;
            EventScrollViewer.Visibility = activeEvent is null ? Visibility.Visible : Visibility.Collapsed;
            ActiveEventLayer.Visibility = activeEvent is null ? Visibility.Collapsed : Visibility.Visible;
            ExitEventConfirmationLayer.Visibility = activeEvent?.ShowsExitConfirmation == true
                ? Visibility.Visible
                : Visibility.Collapsed;
            var exitConfirmationOpen = activeEvent?.ShowsExitConfirmation == true;
            if (!exitConfirmationOpen)
            {
                exitConfirmationBusy = false;
            }
            StartGuestLayer.IsHitTestVisible = !exitConfirmationOpen;
            GuestAssistanceLayer.IsHitTestVisible = !exitConfirmationOpen;
            GuestCaptureLayer.IsHitTestVisible = !exitConfirmationOpen;
            PhotoStripLayer.IsHitTestVisible = !exitConfirmationOpen;
            var shouldFocusExitConfirmation = activeEvent?.ShowsExitConfirmation == true && !exitConfirmationWasOpen;
            exitConfirmationWasOpen = activeEvent?.ShowsExitConfirmation == true;
            StartEventConfirmationLayer.Visibility = presentation.StartEventConfirmation is null
                ? Visibility.Collapsed
                : Visibility.Visible;
            StartEventConfirmationText.Text = presentation.StartEventConfirmation?.Prompt ?? string.Empty;
            var deletion = presentation.EventDeletion;
            EventDeletionLayer.Visibility = deletion is null ? Visibility.Collapsed : Visibility.Visible;
            EventDeletionTitleText.Text = deletion?.Title ?? string.Empty;
            EventDeletionMessageText.Text = deletion?.Message ?? string.Empty;
            EventDeletionProgressRing.Visibility = deletion?.IsBusy == true
                ? Visibility.Visible
                : Visibility.Collapsed;
            EventDeletionProgressRing.IsActive = deletion?.IsBusy == true;
            EventDeletionActions.Visibility = deletion?.IsBusy == true
                ? Visibility.Collapsed
                : Visibility.Visible;
            CancelEventDeletionButton.Visibility = deletion?.CancelActionLabel is null
                ? Visibility.Collapsed
                : Visibility.Visible;
            CancelEventDeletionButton.Content = deletion?.CancelActionLabel ?? string.Empty;
            ConfirmEventDeletionButton.Content = deletion?.PrimaryActionLabel ?? string.Empty;
            ConfirmEventDeletionButton.Background = deletion?.Stage == EventDeletionStage.Confirmation
                ? (Brush)Application.Current.Resources["DangerBrush"]
                : (Brush)Application.Current.Resources["ButtonBackground"];
            ConfirmEventDeletionButton.Foreground = deletion?.Stage == EventDeletionStage.Confirmation
                ? new SolidColorBrush(Microsoft.UI.Colors.White)
                : (Brush)Application.Current.Resources["TextPrimaryBrush"];
            if (activeEvent is not null)
            {
                ActiveEventNameText.Text = activeEvent.Name.ToUpperInvariant();
                AutomationProperties.SetName(ActiveEventNameText, $"Event name: {activeEvent.Name}");
                ActiveEventHeadingText.Text = activeEvent.Heading;
                ActiveEventExplanationText.Text = activeEvent.Explanation;
                AutomationProperties.SetName(ActiveEventExplanationText, $"Instructions: {activeEvent.Explanation}");
                ApplyGuestCyclePresentation(activeEvent, surfaceOverride);
            }

            var setup = presentation.Setup;
            shouldFocusEventName = setup is not null && !setupWasOpen;
            setupWasOpen = setup is not null;
            SetupLayer.Visibility = setup is null ? Visibility.Collapsed : Visibility.Visible;
            DiscardDraftLayer.Visibility = setup?.ShowsDiscardConfirmation == true
                ? Visibility.Visible
                : Visibility.Collapsed;
            SaveChangesLayer.Visibility = setup?.ShowsSaveConfirmation == true
                ? Visibility.Visible
                : Visibility.Collapsed;
            var activeSurface = presentationAdapter.Apply(presentation, surfaceOverride);
            ApplySurfaceAnnouncement(presentation, activeSurface, surfaceOverride);
            var surfaceChanged = activeSurface != lastSurface;
            lastSurface = activeSurface;
            if (shouldFocusExitConfirmation)
            {
                _ = FocusExitConfirmationAsync();
            }
            else if (surfaceChanged)
            {
                FocusInitialGuestAction(activeSurface);
                _ = FocusInitialGuestActionAsync(activeSurface);
            }
            SetupDialog.IsHitTestVisible = setup is null ||
                (!setup.ShowsDiscardConfirmation && !setup.ShowsSaveConfirmation);
            if (setup is null)
            {
                PreviewImage.Source = null;
#if UI_VERIFICATION
                if (surfaceOverride is not null)
                {
                    CompleteVerificationRenderAfterLayout(presentation, surfaceOverride);
                }
#endif
                return;
            }

            if (EventNameTextBox.Text != setup.EventName)
            {
                EventNameTextBox.Text = setup.EventName;
            }

            SetupTitleText.Text = setup.Title;
            var editingSavedEvent = setup.EventId is not null;
            ApplyDirtyFieldState(EventNameTextBox, NameDirtyIndicator, setup.IsNameDirty, editingSavedEvent);
            ApplyDirtyFieldState(CameraComboBox, CameraDirtyIndicator, setup.IsCameraDirty, editingSavedEvent);
            StorageStatusText.Visibility = setup.IsStorageReady ? Visibility.Collapsed : Visibility.Visible;
            DiscardTitleText.Text = editingSavedEvent ? "Discard changes?" : "Discard this draft?";
            DiscardActionButton.Content = editingSavedEvent ? "Discard Changes" : "Discard Draft";
            ConfirmSaveButton.Content = setup.SaveConfirmationStartsEvent
                ? "Save & Start Event"
                : "Save & Close";

            CameraComboBox.ItemsSource = setup.AvailableCameras;
            CameraComboBox.SelectedItem = setup.SelectedCamera is null
                ? null
                : setup.AvailableCameras.FirstOrDefault(camera =>
                    camera.DeviceId == setup.SelectedCamera.DeviceId);
            SetupFailureText.Text = setup.ActionableFailureMessage ?? string.Empty;
            SetupFailureBorder.Visibility = setup.ActionableFailureMessage is null
                ? Visibility.Collapsed
                : Visibility.Visible;
            RetryStorageButton.Visibility = setup.IsStorageReady
                ? Visibility.Collapsed
                : Visibility.Visible;
            SaveCloseButton.IsEnabled = setup.CanSave;
            SaveStartButton.IsEnabled = setup.CanStart;

        }
        finally
        {
            applyingPresentation = false;
        }

        if (shouldFocusEventName)
        {
            CameraComboBox.IsDropDownOpen = false;
            DispatcherQueue.TryEnqueue(() =>
            {
                if (presentationController.CurrentPresentation.Setup is not null)
                {
                    CameraComboBox.IsDropDownOpen = false;
                    EventNameTextBox.Focus(FocusState.Programmatic);
                }
            });
        }
#if UI_VERIFICATION
        if (surfaceOverride is not null)
        {
            CompleteVerificationRenderAfterLayout(presentation, surfaceOverride);
        }
#endif
    }

    private static void ApplyDirtyFieldState(
        Control control,
        FrameworkElement indicator,
        bool isDirty,
        bool editingSavedEvent)
    {
        control.ClearValue(Control.BorderBrushProperty);
        control.ClearValue(Control.BorderThicknessProperty);
        indicator.Visibility = isDirty ? Visibility.Visible : Visibility.Collapsed;
        AutomationProperties.SetHelpText(
            control,
            isDirty
                ? editingSavedEvent
                    ? "Changed from the saved Event."
                    : "Changed from the initial value."
                : string.Empty);
    }

    private void PreviewFrameAvailable(object? sender, SoftwareBitmap bitmap)
    {
        var presentation = presentationController.CurrentPresentation;
        var showsSetupPreview = presentation.Setup is not null;
        var showsGuestPreview = presentation.ActiveEvent?.GuestCycle.Phase is
            GuestCyclePhase.Countdown or GuestCyclePhase.Flash or GuestCyclePhase.CaptureSaved;
        if (!showsSetupPreview && !showsGuestPreview)
        {
            bitmap.Dispose();
            return;
        }

        if (!DispatcherQueue.TryEnqueue(async () =>
        {
            using (bitmap)
            {
                var displayBitmap = bitmap.BitmapPixelFormat == BitmapPixelFormat.Bgra8 &&
                    bitmap.BitmapAlphaMode == BitmapAlphaMode.Premultiplied
                    ? SoftwareBitmap.Copy(bitmap)
                    : SoftwareBitmap.Convert(bitmap, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
                using (displayBitmap)
                {
                    var source = new SoftwareBitmapSource();
                    await source.SetBitmapAsync(displayBitmap);
                    if (presentationController.CurrentPresentation.Setup is not null)
                    {
                        PreviewImage.Source = source;
                    }
                    else
                    {
                        GuestPreviewImage.Source = source;
                    }
                }
            }
        }))
        {
            bitmap.Dispose();
        }
    }

    private void ApplyGuestCyclePresentation(
        ActiveEventPresentation activeEvent,
        ApplicationSurfaceOverride? surfaceOverride)
    {
        var guestCycle = activeEvent.GuestCycle;
        var isStart = guestCycle.Phase == GuestCyclePhase.Start;
        var isAssistance = guestCycle.Phase is GuestCyclePhase.StartUnavailable or GuestCyclePhase.OperatorAssistance;
        var isCapture = guestCycle.Phase is GuestCyclePhase.Countdown or GuestCyclePhase.Flash or GuestCyclePhase.CaptureSaved;
        var isStrip = guestCycle.Phase is GuestCyclePhase.PhotoStripPreview or GuestCyclePhase.Fading;
        StartGuestLayer.Visibility = isStart ? Visibility.Visible : Visibility.Collapsed;
        GuestAssistanceLayer.Visibility = isAssistance ? Visibility.Visible : Visibility.Collapsed;
        GuestCaptureLayer.Visibility = isCapture ? Visibility.Visible : Visibility.Collapsed;
        PhotoStripLayer.Visibility = isStrip ? Visibility.Visible : Visibility.Collapsed;
        ExitEventAction.Visibility = isStart ? Visibility.Visible : Visibility.Collapsed;
        AssistanceExitEventAction.Visibility = isAssistance ? Visibility.Visible : Visibility.Collapsed;
        StartGuestCycleButton.IsEnabled = activeEvent.GuestStart.IsStartEnabled;

        if (activeEvent.ExitHoldState == ExitHoldState.Holding)
        {
            ExitEventAction.ShowHolding();
        }
        else
        {
            ExitEventAction.ShowIdle();
        }

        if (isCapture)
        {
            CountdownOverlay.Visibility = guestCycle.Phase == GuestCyclePhase.Countdown
                ? Visibility.Visible
                : Visibility.Collapsed;
            FlashOverlay.Visibility = guestCycle.Phase == GuestCyclePhase.Flash
                ? Visibility.Visible
                : Visibility.Collapsed;
            CaptureSavedOverlay.Visibility = guestCycle.Phase == GuestCyclePhase.CaptureSaved
                ? Visibility.Visible
                : Visibility.Collapsed;
            CountdownText.Text = guestCycle.CountdownSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture);
            CaptureProgressText.Text = guestCycle.ProgressText.ToUpperInvariant();
            UpdateCaptureProgress(guestCycle.CaptureNumber, guestCycle.CompletedCaptures);
        }

        if (isAssistance)
        {
            var beforeAdmission = guestCycle.Phase == GuestCyclePhase.StartUnavailable;
            var failure = beforeAdmission
                ? activeEvent.GuestStart.Failure
                : guestCycle.Failure == GuestCycleFailure.StorageUnavailable
                    ? GuestStartFailure.StorageUnavailable
                    : GuestStartFailure.CameraUnavailable;
            var retrying = beforeAdmission
                ? activeEvent.GuestStart.IsRetrying
                : guestCycle.IsRetrying;
            var retryFailed = beforeAdmission
                ? activeEvent.GuestStart.ActionState == GuestStartActionState.RetryFailed
                : guestCycle.ActionState == GuestCycleActionState.RetryFailed;
            var exitOnly = beforeAdmission
                ? activeEvent.GuestStart.RequiresEventSetupCorrection
                : guestCycle.Recovery == GuestCycleRecovery.ExitOnly;

            AssistanceEyebrowText.Text = "OPERATOR ASSISTANCE";
            AssistanceMessageText.Text = failure == GuestStartFailure.CameraUnavailable
                ? retryFailed ? "The Camera is still unavailable." : "The Camera is not ready."
                : "Photos cannot be saved right now.";
            AutomationProperties.SetName(AssistanceMessageText, $"Reason: {AssistanceMessageText.Text}");
            AssistanceProgressText.Text = beforeAdmission
                ? exitOnly
                    ? "The operator must exit the Event and update setup."
                    : retryFailed
                        ? "Check the Camera connection before trying again."
                        : "The operator can check the setup and Retry."
                : exitOnly
                    ? $"{guestCycle.CompletedCaptures} of 4 Captures are safe. Exit the Event to update setup."
                    : $"{guestCycle.CompletedCaptures} of 4 Captures are safe. Retry to continue.";
            if (responsiveMode == ResponsiveLayoutMode.Stress)
            {
                AssistanceProgressText.Visibility = beforeAdmission ? Visibility.Collapsed : Visibility.Visible;
                if (!beforeAdmission)
                {
                    AssistanceProgressText.Text = exitOnly
                        ? $"{guestCycle.CompletedCaptures} of 4 Captures safe. Exit Event to update setup."
                        : $"{guestCycle.CompletedCaptures} of 4 Captures safe. Retry to continue.";
                }
            }

            AssistanceRetryButton.Visibility = exitOnly ? Visibility.Collapsed : Visibility.Visible;
            AssistanceExitOnlyAction.Visibility = exitOnly ? Visibility.Visible : Visibility.Collapsed;
            AssistanceRetryButton.IsEnabled = !retrying;
            AssistanceRetryProgress.IsActive = retrying;
            AssistanceRetryProgress.Visibility = retrying ? Visibility.Visible : Visibility.Collapsed;
            AssistanceRetryLabel.Text = retrying
                ? failure == GuestStartFailure.CameraUnavailable ? "Checking Camera…" : "Checking storage…"
                : "Retry";
            AssistanceRetryButton.SetValue(AutomationProperties.NameProperty, AssistanceRetryLabel.Text);
        }

        if (isStrip)
        {
            var remaining = guestCycle.PreviewSecondsRemaining;
            PhotoStripReturnText.Text = $"Looking good! The booth will be ready for the next guests in {remaining}.";
            ReturnProgressFill.Width = 310 * (remaining / 10d);
            if (guestCycle.PhotoStripPath is { } path &&
                (!string.Equals(loadingPhotoStripPath, path, StringComparison.Ordinal) || !photoStripVisibleSignaled))
            {
                _ = LoadPhotoStripAsync(path);
            }

            if (guestCycle.Phase == GuestCyclePhase.Fading)
            {
                StartPhotoStripFade();
            }
            else
            {
                PhotoStripLayer.Opacity = 1;
                photoStripFadeStarted = false;
            }
        }
        else
        {
            PhotoStripImage.Source = null;
            loadingPhotoStripPath = null;
            photoStripVisibleSignaled = false;
            photoStripFadeStarted = false;
            PhotoStripLayer.Opacity = 1;
        }
    }

    private void FocusInitialGuestAction(ApplicationSurface surface)
    {
        switch (surface)
        {
            case ApplicationSurface.GuestStart:
                StartGuestCycleButton.Focus(FocusState.Programmatic);
                break;
            case ApplicationSurface.GuestStartUnavailable when AssistanceRetryButton.Visibility == Visibility.Visible:
            case ApplicationSurface.OperatorAssistance when AssistanceRetryButton.Visibility == Visibility.Visible:
                AssistanceRetryButton.Focus(FocusState.Programmatic);
                break;
            case ApplicationSurface.GuestStartUnavailable when AssistanceExitOnlyAction.Visibility == Visibility.Visible:
            case ApplicationSurface.OperatorAssistance when AssistanceExitOnlyAction.Visibility == Visibility.Visible:
                AssistanceExitOnlyAction.FocusAction();
                break;
        }
    }

    private void ApplySurfaceAnnouncement(
        ApplicationPresentation presentation,
        ApplicationSurface surface,
        ApplicationSurfaceOverride? surfaceOverride)
    {
        var activeEvent = presentation.ActiveEvent;
        var retrying = activeEvent is not null &&
            (activeEvent.GuestStart.IsRetrying || activeEvent.GuestCycle.IsRetrying);
        var unavailable = surface is ApplicationSurface.GuestStartUnavailable or ApplicationSurface.OperatorAssistance;
        var surfaceName = surface switch
        {
            ApplicationSurface.GuestStart => "Guest Start",
            ApplicationSurface.GuestStartUnavailable => "Guest Start unavailable",
            ApplicationSurface.OperatorAssistance => "Operator Assistance",
            _ => string.Empty,
        };
        if (surfaceName.Length == 0)
        {
            return;
        }

        var itemStatus = surfaceOverride?.ItemStatus ??
            (retrying ? "busy" : unavailable ? "unavailable" : "ready");
        var priority = surfaceOverride?.AnnouncementPriority ??
            (itemStatus == "unavailable" ||
             (surfaceOverride is null && surface == ApplicationSurface.OperatorAssistance)
                ? AnnouncementPriority.Assertive
                : AnnouncementPriority.Polite);
        var state = itemStatus switch
        {
            "busy" => "in progress.",
            "unavailable" => "needs attention.",
            _ => "ready.",
        };
        var exitOnly = activeEvent is not null &&
            (activeEvent.GuestStart.RequiresEventSetupCorrection ||
             activeEvent.GuestCycle.Recovery == GuestCycleRecovery.ExitOnly);
        var announcement = surfaceOverride?.Announcement ??
            (surfaceOverride is not null
                ? $"{surfaceName} {state}"
                : unavailable && !retrying
                ? $"{surfaceName} needs attention. {AssistanceMessageText.Text} " +
                  (exitOnly ? "Exit Event to update setup." : "Retry or Exit Event.")
                : $"{surfaceName} {state}");
        if (surfaceOverride is null && string.Equals(announcement, lastAnnouncement, StringComparison.Ordinal))
        {
            return;
        }

        lastAnnouncement = announcement;
        SurfaceAnnouncement.Text = announcement;
        AutomationProperties.SetName(SurfaceAnnouncement, SurfaceAnnouncement.Text);
        AutomationProperties.SetLiveSetting(
            SurfaceAnnouncement,
            priority == AnnouncementPriority.Assertive ? AutomationLiveSetting.Assertive : AutomationLiveSetting.Polite);
        AutomationProperties.SetItemStatus(SurfaceAnnouncement, priority.ToString());
        _ = RaiseSurfaceAnnouncementAsync();
    }

    private async Task RaiseSurfaceAnnouncementAsync()
    {
        var dispatcherQueue = DispatcherQueue;
        await Task.Delay(250);
        dispatcherQueue.TryEnqueue(() =>
        {
            var peer = FrameworkElementAutomationPeer.FromElement(SurfaceAnnouncement) ??
                FrameworkElementAutomationPeer.CreatePeerForElement(SurfaceAnnouncement);
            peer?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
        });
    }

    private void UpdateCaptureProgress(int activeCapture, int completedCaptures)
    {
        Border[] borders = [CaptureStep1Border, CaptureStep2Border, CaptureStep3Border, CaptureStep4Border];
        TextBlock[] numbers = [CaptureStep1Number, CaptureStep2Number, CaptureStep3Number, CaptureStep4Number];
        FontIcon[] checks = [CaptureStep1Check, CaptureStep2Check, CaptureStep3Check, CaptureStep4Check];
        var primary = (Brush)Application.Current.Resources["TextPrimaryBrush"];
        var hairline = (Brush)Application.Current.Resources["HairlineBrush"];
        for (var index = 0; index < borders.Length; index++)
        {
            var captureNumber = index + 1;
            var complete = captureNumber <= completedCaptures;
            var active = captureNumber == activeCapture && !complete;
            borders[index].Background = complete ? primary : new SolidColorBrush(Colors.White);
            borders[index].BorderBrush = complete || active ? primary : hairline;
            borders[index].BorderThickness = new Thickness(active ? 2 : 1);
            numbers[index].Visibility = complete ? Visibility.Collapsed : Visibility.Visible;
            checks[index].Visibility = complete ? Visibility.Visible : Visibility.Collapsed;
            checks[index].Foreground = new SolidColorBrush(Colors.White);
        }
    }

    private async Task LoadPhotoStripAsync(string path)
    {
        if (string.Equals(loadingPhotoStripPath, path, StringComparison.Ordinal) && photoStripVisibleSignaled)
        {
            return;
        }

        loadingPhotoStripPath = path;
        try
        {
            var file = await StorageFile.GetFileFromPathAsync(path);
            using var stream = await file.OpenReadAsync();
            var source = new BitmapImage();
            await source.SetSourceAsync(stream);
            PhotoStripImage.Source = source;
            photoStripVisibleSignaled = true;
#if UI_VERIFICATION
            CompleteMediaPendingRenderSettlement();
#endif
            await ExecuteAsync(new ConfirmPhotoStripVisible());
        }
        catch (Exception exception) when (
            exception is FileNotFoundException or
            IOException or
            System.Runtime.InteropServices.COMException)
        {
            photoStripVisibleSignaled = false;
#if UI_VERIFICATION
            CompleteMediaPendingRenderSettlement();
#endif
            await ExecuteAsync(new ReportPhotoStripDecodeFailure());
        }
    }

#if UI_VERIFICATION
    private void CompleteVerificationRenderAfterLayout(
        ApplicationPresentation presentation,
        ApplicationSurfaceOverride surfaceOverride)
    {
        if (surfaceOverride.Surface == ApplicationSurface.PhotoStrip &&
            presentation.ActiveEvent?.GuestCycle.PhotoStripPath is not null &&
            !photoStripVisibleSignaled)
        {
            mediaPendingRenderSettlement = surfaceOverride;
            return;
        }

        renderSettledSignal.CompleteAfterLayout(surfaceOverride);
    }

    private void CompleteMediaPendingRenderSettlement()
    {
        if (mediaPendingRenderSettlement is not { } surfaceOverride)
        {
            return;
        }

        mediaPendingRenderSettlement = null;
        renderSettledSignal.CompleteAfterLayout(surfaceOverride);
    }
#endif

    private void StartPhotoStripFade()
    {
        if (photoStripFadeStarted)
        {
            return;
        }

        photoStripFadeStarted = true;
        var storyboard = new Storyboard();
        var animation = new DoubleAnimation
        {
            From = 1,
            To = 0,
            Duration = (Duration)Application.Current.Resources["FotoHavnPreviewFadeDuration"],
        };
        Storyboard.SetTarget(animation, PhotoStripLayer);
        Storyboard.SetTargetProperty(animation, "Opacity");
        storyboard.Children.Add(animation);
        storyboard.Begin();
    }

    private async Task ExecuteAsync(ApplicationCommand command)
    {
        try
        {
            await presentationController.ExecuteAsync(command);
        }
        catch (OperationCanceledException)
        {
        }
    }

    public void ShowCentered()
    {
        Activate();
        if (lastSurface is { } surface)
        {
            FocusInitialGuestAction(surface);
        }
        ConfigureWindow(canvas ?? throw new InvalidOperationException("Presentation must load before the window is shown."));
    }

    private void ConfigureWindow(ApplicationCanvasPresentation canvas)
    {
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(null);

        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.SetBorderAndTitleBar(false, false);
            presenter.IsResizable = false;
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = true;
        }

        var rasterizationScale = FixedCanvas.XamlRoot?.RasterizationScale ?? 1;
        var physicalWidth = checked((int)Math.Round(canvas.Width * rasterizationScale));
        var physicalHeight = checked((int)Math.Round(canvas.Height * rasterizationScale));
        var workArea = DisplayArea.Primary.WorkArea;
        var x = workArea.X + ((workArea.Width - physicalWidth) / 2);
        var y = workArea.Y + ((workArea.Height - physicalHeight) / 2);
        AppWindow.MoveAndResize(new RectInt32(x, y, physicalWidth, physicalHeight));
    }
}
