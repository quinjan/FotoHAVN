using FotoHavn.Core;
using FotoHavn.App.Controls;
using FotoHavn.App.Surfaces;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
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
    private ApplicationCanvasPresentation? canvas;
    private bool applyingPresentation;
    private string? loadingPhotoStripPath;
    private bool photoStripVisibleSignaled;
    private bool photoStripFadeStarted;
    private bool setupWasOpen;
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
        var guestMirror = CameraPreviewRenderPolicy.CreateMirror(GuestPreviewViewport.Width);
        GuestPreviewImage.RenderTransform = new ScaleTransform
        {
            ScaleX = guestMirror.ScaleX,
            CenterX = guestMirror.CenterX,
        };
        presentationController.PresentationChanged += PresentationChanged;
        if (camera is not null)
        {
            camera.PreviewFrameAvailable += PreviewFrameAvailable;
        }
    }

    public async Task LoadPresentationAsync(CancellationToken cancellationToken = default)
    {
        var presentation = await presentationController.ExecuteAsync(new LaunchApplication(), cancellationToken);
        HeadingText.Text = presentation.Heading;
        EventTiles.ItemsSource = presentation.EventTiles.Where(tile => tile.Kind == EventTileKind.SavedEvent);
        canvas = presentation.Canvas;
        ApplyPresentation(presentation);
    }

    private void OperatorCanvasSizeChanged(object sender, SizeChangedEventArgs args) =>
        ApplyOperatorResponsiveLayout(args.NewSize.Width, args.NewSize.Height);

    private void ApplyOperatorResponsiveLayout(double width, double height)
    {
        var mode = ResponsiveLayout.Resolve(width, height);
        var horizontalPadding = mode switch
        {
            ResponsiveLayoutMode.Standard => 48,
            ResponsiveLayoutMode.Compact => 32,
            ResponsiveLayoutMode.Stress => 16,
            _ => throw new ArgumentOutOfRangeException(),
        };
        SavedEventsContent.Padding = new(horizontalPadding, 32, horizontalPadding, 36);
        SetupDialog.Margin = new(mode == ResponsiveLayoutMode.Standard ? 40 : 16);
        SetupContentGrid.Padding = new(mode == ResponsiveLayoutMode.Stress ? 16 : 34);
        var stacksSetup = mode != ResponsiveLayoutMode.Standard;
        ReparentSetupPreview(stacksSetup);
        SetupFieldsColumn.Width = new(stacksSetup ? 1 : 410, stacksSetup ? GridUnitType.Star : GridUnitType.Pixel);
        SetupGapColumn.Width = new(stacksSetup ? 0 : 32);
        SetupPreviewColumn.Width = new(stacksSetup ? 0 : 1, stacksSetup ? GridUnitType.Pixel : GridUnitType.Star);
        Grid.SetRow(SetupPreviewPanel, stacksSetup ? 2 : 1);
        Grid.SetColumn(SetupPreviewPanel, stacksSetup ? 0 : 2);
        Grid.SetColumnSpan(SetupPreviewPanel, stacksSetup ? 3 : 1);
        SetupCameraViewport.Width = stacksSetup ? double.NaN : Math.Max(240, Math.Min(588, width - 560));
        SetupCameraViewport.MaxWidth = stacksSetup ? 378 : 588;
        SetupCameraViewport.Height = stacksSetup ? 252 : 392;
        SetupCameraViewport.HorizontalAlignment = stacksSetup ? HorizontalAlignment.Stretch : HorizontalAlignment.Left;
        SetupCommitActions.Orientation = mode == ResponsiveLayoutMode.Stress ? Orientation.Vertical : Orientation.Horizontal;
        SetupCommitActions.HorizontalAlignment = mode == ResponsiveLayoutMode.Stress ? HorizontalAlignment.Stretch : HorizontalAlignment.Right;
        foreach (var action in SetupCommitActions.Children.OfType<FrameworkElement>())
        {
            action.HorizontalAlignment = mode == ResponsiveLayoutMode.Stress ? HorizontalAlignment.Stretch : HorizontalAlignment.Right;
        }
        Grid.SetRow(SetupCommitActions, mode == ResponsiveLayoutMode.Stress ? 1 : 0);
        SetupCommitActions.Margin = mode == ResponsiveLayoutMode.Stress ? new(0, 10, 0, 0) : new(0);
        CancelSetupButton.HorizontalAlignment = mode == ResponsiveLayoutMode.Stress ? HorizontalAlignment.Stretch : HorizontalAlignment.Left;

        DispatcherQueue.TryEnqueue(() =>
        {
            if (EventTiles.ItemsPanelRoot is ItemsWrapGrid panel)
            {
                var columns = mode switch
                {
                    ResponsiveLayoutMode.Standard => 3,
                    ResponsiveLayoutMode.Compact => 2,
                    ResponsiveLayoutMode.Stress => 1,
                    _ => 1,
                };
                var availableWidth = Math.Max(280, width - (horizontalPadding * 2));
                panel.MaximumRowsOrColumns = columns;
                panel.ItemWidth = Math.Max(280, (availableWidth - ((columns - 1) * 18)) / columns);
                panel.ItemHeight = 274;
            }
        });
    }

    private void ReparentSetupPreview(bool stacksSetup)
    {
        if (stacksSetup && VisualTreeHelper.GetParent(SetupPreviewPanel) is Grid)
        {
            SetupContentGrid.Children.Remove(SetupPreviewPanel);
            SetupFieldGroups.Children.Insert(2, SetupPreviewPanel);
        }
        else if (!stacksSetup && VisualTreeHelper.GetParent(SetupPreviewPanel) is StackPanel)
        {
            SetupFieldGroups.Children.Remove(SetupPreviewPanel);
            SetupContentGrid.Children.Add(SetupPreviewPanel);
        }
    }

    private async void NewEventClicked(object sender, RoutedEventArgs args) =>
        await ExecuteAsync(new OpenNewEvent());

    private async void EventCardActionRequested(object sender, EventCardActionEventArgs args)
    {
        if (args.Action == EventCardAction.New)
        {
            await ExecuteAsync(new OpenNewEvent());
            return;
        }

        if (args.EventId is not { } eventId)
        {
            return;
        }

        await ExecuteAsync(args.Action switch
        {
            EventCardAction.Start => new StartSavedEvent(eventId),
            EventCardAction.Edit => new OpenSavedEvent(eventId),
            EventCardAction.Delete => new DeleteSavedEvent(eventId),
            EventCardAction.RetryDeletion => new RetryEventDeletion(eventId),
            _ => throw new InvalidOperationException($"Unsupported Event Card action '{args.Action}'."),
        });
    }

    private async void ConfirmStartEventClicked(object sender, RoutedEventArgs args) =>
        await ExecuteAsync(new ConfirmStartSavedEvent());

    private async void CancelStartEventClicked(object sender, RoutedEventArgs args) =>
        await ExecuteAsync(new CancelStartSavedEvent());

    private async void ExitEventClicked(object sender, RoutedEventArgs args) =>
        await ExecuteAsync(new ExitActiveEvent());

    private async void ConfirmExitEventClicked(object sender, RoutedEventArgs args) =>
        await ExecuteAsync(new ConfirmExitActiveEvent());

    private async void CancelExitEventClicked(object sender, RoutedEventArgs args) =>
        await ExecuteAsync(new CancelExitActiveEvent());

    private async void StartGuestCycleClicked(object sender, RoutedEventArgs args) =>
        await ExecuteAsync(new StartGuestCycle());

    private async void RetryGuestCycleClicked(object sender, RoutedEventArgs args) =>
        await ExecuteAsync(new RetryGuestCycle());

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
            HeadingText.Text = "Saved Events";
            EventTiles.ItemsSource = presentation.EventTiles.Where(tile => tile.Kind == EventTileKind.SavedEvent);
            var activeEvent = presentation.ActiveEvent;
            EventScrollViewer.Visibility = activeEvent is null ? Visibility.Visible : Visibility.Collapsed;
            ActiveEventLayer.Visibility = activeEvent is null ? Visibility.Collapsed : Visibility.Visible;
            ExitEventConfirmationLayer.Visibility = activeEvent?.ShowsExitConfirmation == true
                ? Visibility.Visible
                : Visibility.Collapsed;
            StartEventConfirmationLayer.Visibility = presentation.StartEventConfirmation is null
                ? Visibility.Collapsed
                : Visibility.Visible;
            StartEventConfirmationText.Text = surfaceOverride?.Surface == ApplicationSurface.Confirmation
                ? surfaceOverride.AccessibleName
                : presentation.StartEventConfirmation?.Prompt ?? string.Empty;
            StartEventNameText.Text = presentation.StartEventConfirmation?.EventName ?? string.Empty;
            StartEventIdentityText.Text = presentation.StartEventConfirmation?.EventId.Value ?? string.Empty;
            var deletion = presentation.EventDeletion;
            EventDeletionLayer.Visibility = deletion is null ? Visibility.Collapsed : Visibility.Visible;
            EventDeletionTitleText.Text = surfaceOverride?.Surface == ApplicationSurface.Confirmation
                ? surfaceOverride.AccessibleName
                : deletion?.Title ?? string.Empty;
            EventDeletionMessageText.Text = deletion?.Message ?? string.Empty;
            EventDeletionEventNameText.Text = deletion?.EventName ?? string.Empty;
            EventDeletionIdentityText.Text = deletion?.EventId.Value ?? string.Empty;
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
                ActiveEventHeadingText.Text = activeEvent.Heading;
                ActiveEventExplanationText.Text = activeEvent.Explanation;
                ApplyGuestCyclePresentation(activeEvent.GuestCycle);
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
            presentationAdapter.Apply(presentation, surfaceOverride);
            SetupDialog.IsHitTestVisible = setup is null ||
                (!setup.ShowsDiscardConfirmation && !setup.ShowsSaveConfirmation);
            if (setup is null)
            {
                SetupCameraViewport.Source = null;
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
            var setupIdentity = setup.EventId?.Value ?? "New Event";
            EventSetupIdentityText.Text = editingSavedEvent ? $"Event ID {setupIdentity}" : "New Event";
            SaveEventNameText.Text = setup.EventName;
            SaveEventIdentityText.Text = setupIdentity;
            EventNameFieldGroup.Present(
                string.IsNullOrWhiteSpace(setup.EventName)
                    ? SetupFieldState.Invalid
                    : setup.IsNameDirty ? SetupFieldState.Dirty : SetupFieldState.Ready,
                string.IsNullOrWhiteSpace(setup.EventName) ? "Enter an Event name." : string.Empty);
            var cameraFailure = setup.ActionableFailureMessage ?? "Choose an Available Camera and try again.";
            CameraFieldGroup.Present(setup.CameraState switch
            {
                CameraConnectionState.Connecting => SetupFieldState.Checking,
                CameraConnectionState.Ready => setup.IsCameraDirty ? SetupFieldState.Dirty : SetupFieldState.Ready,
                CameraConnectionState.Unavailable => SetupFieldState.Unavailable,
                _ => SetupFieldState.Invalid,
            }, setup.CameraState == CameraConnectionState.Connecting ? "Checking camera…" : cameraFailure);
            StorageFieldGroup.Present(
                setup.IsStorageReady ? SetupFieldState.Ready : SetupFieldState.Invalid,
                setup.IsStorageReady
                    ? string.Empty
                    : "Storage must be writable and have at least 1 GB free. Check C:\\Program Files\\FotoHAVN\\Events, then try again.");
            SetupCameraViewport.Status = setup.CameraState == CameraConnectionState.Ready ? "Live" : "Unavailable";
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
            SetupFailureBorder.Visibility = Visibility.Collapsed;
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
                        SetupCameraViewport.Source = source;
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

    private void ApplyGuestCyclePresentation(GuestCyclePresentation guestCycle)
    {
        var isStart = guestCycle.Phase == GuestCyclePhase.Start;
        var isAssistance = guestCycle.Phase is GuestCyclePhase.StartUnavailable or GuestCyclePhase.OperatorAssistance;
        var isCapture = guestCycle.Phase is GuestCyclePhase.Countdown or GuestCyclePhase.Flash or GuestCyclePhase.CaptureSaved;
        var isStrip = guestCycle.Phase is GuestCyclePhase.PhotoStripPreview or GuestCyclePhase.Fading;
        StartGuestLayer.Visibility = isStart ? Visibility.Visible : Visibility.Collapsed;
        GuestAssistanceLayer.Visibility = isAssistance ? Visibility.Visible : Visibility.Collapsed;
        GuestCaptureLayer.Visibility = isCapture ? Visibility.Visible : Visibility.Collapsed;
        PhotoStripLayer.Visibility = isStrip ? Visibility.Visible : Visibility.Collapsed;
        ExitEventButton.Visibility = isStart ? Visibility.Visible : Visibility.Collapsed;
        AssistanceExitEventButton.Visibility = guestCycle.Phase == GuestCyclePhase.StartUnavailable
            ? Visibility.Visible
            : Visibility.Collapsed;

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
            AssistanceEyebrowText.Text = beforeAdmission ? "THE BOOTH ISN’T READY" : "WE PAUSED YOUR PHOTOS";
            AssistanceMessageText.Text = beforeAdmission
                ? $"{guestCycle.AssistanceDetail} Please call the operator."
                : $"{guestCycle.AssistanceDetail} Your {guestCycle.CompletedCaptures} completed Captures are safe.";
            AssistanceDetailTitle.Text = guestCycle.Failure == GuestCycleFailure.CameraUnavailable
                ? "Camera unavailable"
                : "Storage unavailable";
            AssistanceProgressText.Text = beforeAdmission
                ? "No Guest Cycle has begun"
                : $"Guest Cycle paused · {guestCycle.CompletedCaptures} of 4 Captures retained";
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
