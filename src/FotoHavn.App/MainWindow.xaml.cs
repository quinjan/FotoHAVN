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
    private readonly BitmapImage verificationCameraPreview =
        new(new Uri("ms-appx:///UiVerification/camera-preview.jpg"));
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
        var edgeToEdgeSetup = width < 900 || mode == ResponsiveLayoutMode.Stress;
        SetupDialog.Margin = new(edgeToEdgeSetup ? 0 : mode == ResponsiveLayoutMode.Standard ? 40 : 24);
        SetupDialog.CornerRadius = new(edgeToEdgeSetup ? 0 : 14);
        SetupDialog.MaxWidth = edgeToEdgeSetup ? double.PositiveInfinity : 1100;
        SetupDialog.MaxHeight = edgeToEdgeSetup ? double.PositiveInfinity : 640;
        SetupContentGrid.Padding = mode switch
        {
            ResponsiveLayoutMode.Standard => new Thickness(34),
            ResponsiveLayoutMode.Compact when edgeToEdgeSetup => new Thickness(16, 12, 16, 8),
            ResponsiveLayoutMode.Compact => new Thickness(32, 24, 32, 20),
            ResponsiveLayoutMode.Stress => new Thickness(16, 12, 16, 8),
            _ => new Thickness(34),
        };
        SetupIntroductionText.Visibility = edgeToEdgeSetup ? Visibility.Collapsed : Visibility.Visible;
        SetupBodyGrid.Margin = new(0, 4, 0, 0);
        var stacksSetup = mode != ResponsiveLayoutMode.Standard;
        var stress = mode == ResponsiveLayoutMode.Stress;
        SetupTitleText.FontSize = stress ? 25 : 32;
        SetupFooterRow.Height = new(stress ? 72 : 80);
        var stacksConfirmationActions = width < 800;
        foreach (var confirmation in new[]
        {
            ExitConfirmationFrame,
            DeletionConfirmationFrame,
            StartConfirmationFrame,
            DiscardConfirmationFrame,
            SaveConfirmationFrame,
        })
        {
            confirmation.ApplyResponsiveLayout(stress, stacksConfirmationActions);
        }
        SetupFieldsColumn.Width = new(1, GridUnitType.Star);
        SetupGapColumn.Width = new(stacksSetup ? 0 : 34);
        SetupPreviewColumn.Width = new(stacksSetup ? 0 : 1, stacksSetup ? GridUnitType.Pixel : GridUnitType.Star);
        Grid.SetRow(SetupPreviewPanel, stacksSetup ? 3 : 0);
        Grid.SetRowSpan(SetupPreviewPanel, stacksSetup ? 1 : 7);
        Grid.SetColumn(SetupPreviewPanel, stacksSetup ? 0 : 2);
        Grid.SetColumnSpan(SetupPreviewPanel, stacksSetup ? 3 : 1);
        SetupPreviewPanel.Margin = new(0, 0, 0, stacksSetup ? 18 : 0);
        Grid.SetRow(PrinterFieldGroup, stacksSetup ? 4 : 3);
        Grid.SetRow(StorageFieldGroup, stacksSetup ? 5 : 4);
        Grid.SetRow(SetupFailureBorder, stacksSetup ? 6 : 5);
        SetupCameraViewport.Width = double.NaN;
        SetupCameraViewport.MaxWidth = double.PositiveInfinity;
        SetupCameraViewport.HorizontalAlignment = HorizontalAlignment.Stretch;
        SetupCommitActions.Orientation = Orientation.Horizontal;
        SetupCommitActions.HorizontalAlignment = HorizontalAlignment.Right;
        foreach (var action in SetupCommitActions.Children.OfType<FrameworkElement>())
        {
            action.HorizontalAlignment = HorizontalAlignment.Right;
            action.Width = double.NaN;
        }
        Grid.SetRow(SetupCommitActions, 0);
        SetupCommitActions.Margin = new(0);
        CancelSetupButton.HorizontalAlignment = HorizontalAlignment.Left;
        CancelSetupButton.Width = double.NaN;
        if (stress)
        {
            var actionWidth = Math.Max(96, (width - 48) / 3);
            CancelSetupButton.Width = actionWidth;
            foreach (var action in SetupCommitActions.Children.OfType<FrameworkElement>())
            {
                action.Width = actionWidth;
            }
        }

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
                EventTiles.Width = availableWidth + 18;
                panel.MaximumRowsOrColumns = columns;
                var cardWidth = Math.Max(280, (availableWidth - ((columns - 1) * 18)) / columns);
                panel.ItemWidth = cardWidth + 18;
                panel.ItemHeight = mode == ResponsiveLayoutMode.Stress ? 160 : 274;
            }
        });
        HeadingText.TextStyle = stress
            ? null
            : (Style)Application.Current.Resources["TypeHeadingPageStyle"];
        HeadingText.FontSize = stress ? 25 : 32;
        HeadingText.FontWeight = Microsoft.UI.Text.FontWeights.Bold;
    }

    private void SetupCameraViewportSizeChanged(object sender, SizeChangedEventArgs args)
    {
        if (args.NewSize.Width <= 0)
        {
            return;
        }

        var targetHeight = args.NewSize.Width * 9d / 16d;
        if (Math.Abs(SetupCameraViewport.Height - targetHeight) > 0.5)
        {
            SetupCameraViewport.Height = targetHeight;
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

    private async void PrinterSelectionChanged(object sender, SelectionChangedEventArgs args)
    {
        if (!applyingPresentation && PrinterComboBox.SelectedIndex == 0)
        {
            await ExecuteAsync(new SelectNoPrinter());
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
            var hasIncompleteDeletion = presentation.EventTiles.Any(tile => tile.DeletionIncomplete);
            SavedEventsDeletionStatus.Visibility = hasIncompleteDeletion ? Visibility.Visible : Visibility.Collapsed;
            EventTiles.Margin = new(0, hasIncompleteDeletion ? 18 : 20, 0, 0);
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
            StartEventIdentity.EventName = presentation.StartEventConfirmation?.EventName ?? string.Empty;
            StartEventIdentity.EventId = presentation.StartEventConfirmation?.EventId.Value ?? string.Empty;
            var deletion = presentation.EventDeletion;
            EventDeletionLayer.Visibility = deletion is null ? Visibility.Collapsed : Visibility.Visible;
            EventDeletionTitleText.Text = surfaceOverride?.Surface == ApplicationSurface.Confirmation
                ? surfaceOverride.AccessibleName
                : deletion?.Title ?? string.Empty;
            var verificationIdentity = surfaceOverride?.InjectionIdentity ?? string.Empty;
            var isConfirmationBusy = surfaceOverride?.Surface == ApplicationSurface.Confirmation &&
                surfaceOverride.ItemStatus == "busy";
            var isVerificationSuccess = verificationIdentity.EndsWith("success-destination", StringComparison.Ordinal);
            var isDeletionSuccess = isVerificationSuccess || deletion?.Stage == EventDeletionStage.Deleted;
            var isDeletionFailure = verificationIdentity.EndsWith("delete-failed", StringComparison.Ordinal) ||
                verificationIdentity.EndsWith("confirmation.retry", StringComparison.Ordinal) ||
                deletion?.Stage is EventDeletionStage.CouldNotStart or EventDeletionStage.Incomplete;
            var isDeletionBusy = deletion?.IsBusy == true || isConfirmationBusy && deletion is not null;
            EventDeletionMessageText.Text = isVerificationSuccess
                ? "Your changes have been saved."
                : verificationIdentity.EndsWith("confirmation.retry", StringComparison.Ordinal)
                    ? string.Empty
                    : deletion?.Message ?? string.Empty;
            EventDeletionMessageText.Visibility = string.IsNullOrWhiteSpace(EventDeletionMessageText.Text)
                ? Visibility.Collapsed
                : Visibility.Visible;
            EventDeletionIdentity.EventName = deletion?.EventName ?? string.Empty;
            EventDeletionIdentity.EventId = deletion?.EventId.Value ?? string.Empty;
            EventDeletionIdentity.Visibility = isDeletionSuccess ? Visibility.Collapsed : Visibility.Visible;
            EventDeletionFailureStatus.Visibility = isDeletionFailure ? Visibility.Visible : Visibility.Collapsed;
            EventDeletionActions.Visibility = Visibility.Visible;
            CancelEventDeletionButton.Visibility = isDeletionSuccess ? Visibility.Collapsed : Visibility.Visible;
            CancelEventDeletionButton.IsEnabled = !isDeletionBusy;
            ConfirmEventDeletionButton.IsEnabled = !isDeletionBusy;
            SetButtonContent(CancelEventDeletionButton, "Cancel");
            SetButtonContent(
                ConfirmEventDeletionButton,
                isDeletionSuccess ? "Continue" : isDeletionFailure ? "Retry" : isDeletionBusy ? "Deleting Event…" : "Delete Event",
                isDeletionBusy);
            EventDeletionSemanticIcon.Intent = isDeletionSuccess
                ? DialogSemanticIntent.Success
                : verificationIdentity.EndsWith("confirmation.retry", StringComparison.Ordinal)
                    ? DialogSemanticIntent.Neutral
                    : DialogSemanticIntent.Destructive;
            EventDeletionSemanticIcon.Glyph = isDeletionSuccess
                ? "\uE73E"
                : verificationIdentity.EndsWith("confirmation.retry", StringComparison.Ordinal)
                    ? "\uE72A"
                    : "\uE74D";
            DeletionConfirmationFrame.SetStandardMaximumWidth(isDeletionSuccess ? 440 : 500);
            ConfirmEventDeletionButton.Style = (Style)Application.Current.Resources[
                isDeletionFailure && !verificationIdentity.EndsWith("delete-failed", StringComparison.Ordinal)
                    ? "FotoHavnActionButtonPrimaryStyle"
                    : isDeletionSuccess
                        ? "FotoHavnActionButtonPrimaryStyle"
                        : "FotoHavnActionButtonDestructiveStyle"];

            var isStartBusy = presentation.StartEventConfirmation?.IsBusy == true ||
                isConfirmationBusy && presentation.StartEventConfirmation is not null;
            var isStartFailure = verificationIdentity.EndsWith("start-failed", StringComparison.Ordinal);
            StartEventFailureStatus.Visibility = isStartFailure ? Visibility.Visible : Visibility.Collapsed;
            CancelStartEventButton.IsEnabled = !isStartBusy;
            ConfirmStartEventButton.IsEnabled = !isStartBusy;
            SetButtonContent(CancelStartEventButton, "Cancel");
            SetButtonContent(
                ConfirmStartEventButton,
                isStartFailure ? "Retry" : isStartBusy ? "Starting Event…" : "Start Event",
                isStartBusy);

            var isExitBusy = activeEvent?.IsExitBusy == true ||
                isConfirmationBusy && activeEvent?.ShowsExitConfirmation == true;
            CancelExitEventButton.IsEnabled = !isExitBusy;
            ConfirmExitEventButton.IsEnabled = !isExitBusy;
            SetButtonContent(CancelExitEventButton, "Keep Event Active");
            SetButtonContent(ConfirmExitEventButton, isExitBusy ? "Exiting Event…" : "Exit Event", isExitBusy);
            DeletionConfirmationFrame.RefreshInitialFocus();
            StartConfirmationFrame.RefreshInitialFocus();
            ExitConfirmationFrame.RefreshInitialFocus();
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
            var showsNestedSetupConfirmation = setup?.ShowsDiscardConfirmation == true ||
                setup?.ShowsSaveConfirmation == true;
            SetupLayer.Background = showsNestedSetupConfirmation
                ? new SolidColorBrush(Colors.Transparent)
                : (Brush)Application.Current.Resources["ColorOverlayScrimBrush"];
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
            SetupIntroductionText.Text = editingSavedEvent
                ? "Review this Event and update its Camera or Printer."
                : "Name the Event and choose its Camera and Printer.";
            var setupIdentity = setup.EventId?.Value ?? "New Event";
            EventSetupIdentityText.Text = string.Empty;
            EventSetupIdentityText.Visibility = Visibility.Collapsed;
            EventIdentityFieldGroup.Visibility = editingSavedEvent ? Visibility.Visible : Visibility.Collapsed;
            EventIdentityValueText.Text = editingSavedEvent ? setupIdentity : string.Empty;
            SaveEventIdentity.EventName = setup.EventName;
            SaveEventIdentity.EventId = setupIdentity;
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
            SetupCameraViewport.Status = setup.CameraState switch
            {
                CameraConnectionState.Ready => "Live",
                CameraConnectionState.Connecting => "Checking Camera…",
                CameraConnectionState.Unavailable when setup.SelectedCamera is null =>
                    "Select a Camera to start the preview.",
                _ => "Camera preview unavailable.",
            };
#if UI_VERIFICATION
            SetupCameraViewport.Source = setup.CameraState == CameraConnectionState.Ready
                ? verificationCameraPreview
                : null;
            SetupCameraViewport.IsMirrored = false;
            SetupCameraViewport.PreviewScale = 1.32;
#endif
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
            PrinterComboBox.SelectedIndex = setup.IsNoPrinterSelected ? 0 : -1;
            var isSetupSaving = setup.IsBusy ||
                verificationIdentity.EndsWith("event-setup.saving", StringComparison.Ordinal);
            var isSetupSaveSuccess = verificationIdentity.EndsWith("event-setup.save-success", StringComparison.Ordinal);
            var isSetupSaveError = verificationIdentity.EndsWith("event-setup.save-error", StringComparison.Ordinal);
            SetupFailureText.Text = isSetupSaveError
                ? "The Event could not be saved here. Check storage access and try again."
                : setup.ActionableFailureMessage ?? string.Empty;
            SetupFailureBorder.Visibility = isSetupSaveError ? Visibility.Visible : Visibility.Collapsed;
            RetryStorageButton.Visibility = setup.IsStorageReady
                ? Visibility.Collapsed
                : Visibility.Visible;
            SaveCloseButton.IsEnabled = setup.CanSave && !isSetupSaving && !isSetupSaveSuccess && !isSetupSaveError;
            SaveStartButton.IsEnabled = setup.CanStart && !isSetupSaving && !isSetupSaveSuccess && !isSetupSaveError;
            var isSetupFooterSaving = isSetupSaving && !setup.ShowsSaveConfirmation;
            var isSaveCloseBusy = isSetupFooterSaving && !setup.IsSavingAndStarting;
            var isSaveStartBusy = isSetupFooterSaving && setup.IsSavingAndStarting;
            SetButtonContent(
                SaveCloseButton,
                isSaveCloseBusy ? "Saving Event…" : isSetupSaveSuccess ? "Saved" : "Save & Close",
                isSaveCloseBusy);
            SetButtonContent(
                SaveStartButton,
                isSaveStartBusy ? "Saving & starting…" : "Save & Start Event",
                isSaveStartBusy);
            CancelSaveButton.IsEnabled = !isSetupSaving;
            ConfirmSaveButton.IsEnabled = !isSetupSaving;
            SetButtonContent(
                ConfirmSaveButton,
                isSetupSaving ? "Saving Event…" : setup.SaveConfirmationStartsEvent ? "Save & Start Event" : "Save Changes",
                isSetupSaving);
            SaveConfirmationFrame.RefreshInitialFocus();

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

    private static void SetButtonContent(Button button, string label, bool busy = false)
    {
        AutomationProperties.SetName(button, label);
        if (!busy)
        {
            button.Content = label;
            return;
        }

        var content = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        content.Children.Add(new ProgressRing
        {
            Width = 18,
            Height = 18,
            IsActive = true,
        });
        content.Children.Add(new TextBlock
        {
            Text = label,
            VerticalAlignment = VerticalAlignment.Center,
        });
        button.Content = content;
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
