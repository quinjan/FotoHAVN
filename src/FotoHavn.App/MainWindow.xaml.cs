using FotoHavn.Core;
using FotoHavn.App.Controls;
using FotoHavn.App.Surfaces;
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
    private readonly BitmapImage verificationCameraPreview =
        new(new Uri("ms-appx:///UiVerification/camera-preview.jpg"));
    private readonly BitmapImage verificationPhotoStripPreview =
        new(new Uri("ms-appx:///UiVerification/guest-cycle-photo-strip.png"));
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
        GuestPhotoStripResult.ApplyResponsiveLayout(mode);
        GuestCaptureProgress.SetCompact(mode == ResponsiveLayoutMode.Stress);
        GuestPreviewViewport.Width = double.NaN;
        GuestPreviewViewport.HorizontalAlignment = HorizontalAlignment.Stretch;
        GuestCaptureLayer.Padding = mode switch
        {
            ResponsiveLayoutMode.Standard => new Thickness(24, 0, 24, 24),
            ResponsiveLayoutMode.Compact => new Thickness(24, 0, 24, 20),
            _ => new Thickness(10, 0, 10, 10),
        };
        CaptureHeaderRow.Height = new(mode == ResponsiveLayoutMode.Stress ? 52 : 62);
        CaptureMetaRow.Height = new(mode == ResponsiveLayoutMode.Stress ? 32 : 38);
        CountdownText.FontSize = mode == ResponsiveLayoutMode.Stress ? 112 : 190;
        PhotoStripLayer.Padding = mode switch
        {
            ResponsiveLayoutMode.Standard => new Thickness(120, 46, 120, 46),
            ResponsiveLayoutMode.Compact => new Thickness(60, 32, 60, 32),
            _ => new Thickness(16),
        };
        AssistanceHeadingText.Margin = new Thickness(0, 8, 0, 0);
        AssistanceMessageText.Margin = new Thickness(0, 10, 0, 0);
        AssistanceCaptureProgress.Margin = new Thickness(0, 16, 0, 0);
        AssistanceProgressText.Margin = new Thickness(0, 10, 0, 0);
        AssistanceRetryButton.Height = 64;
        AssistanceRetryButton.Margin = new Thickness(0, 20, 0, 0);
        switch (mode)
        {
            case ResponsiveLayoutMode.Standard:
                GuestStartContent.Width = 780;
                GuestStartContent.Margin = new Thickness(0, 29, 0, 0);
                ActiveEventHeadingText.FontSize = 60;
                AssistancePanel.Margin = new Thickness(260, 145, 260, 142);
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
                AssistancePanel.Margin = new Thickness(24, 4, 24, 4);
                AssistancePanel.Padding = new Thickness(20, 8, 20, 8);
                AssistanceContent.Width = Math.Max(300, args.NewSize.Width - 80);
                AssistanceHeadingText.FontSize = 26;
                AssistanceHeadingText.Margin = new Thickness(0, 4, 0, 0);
                AssistanceMessageText.Margin = new Thickness(0, 4, 0, 0);
                AssistanceCaptureProgress.Margin = new Thickness(0, 6, 0, 0);
                AssistanceProgressText.Margin = new Thickness(0, 4, 0, 0);
                AssistanceRetryButton.Height = 48;
                AssistanceRetryButton.Margin = new Thickness(0, 6, 0, 0);
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

    private void GuestPreviewViewportSizeChanged(object sender, SizeChangedEventArgs args)
    {
        if (args.NewSize.Width <= 0 || args.NewSize.Height <= 0)
        {
            return;
        }

        var layout = CameraPreviewRenderPolicy.CalculateLayout(
            args.NewSize.Width,
            args.NewSize.Height);
        GuestCaptureGuideLeftColumn.Width = new GridLength(layout.GuideLeft);
        GuestCaptureGuideColumn.Width = new GridLength(layout.GuideWidth);
        GuestCaptureGuideRightColumn.Width = new GridLength(
            args.NewSize.Width - layout.GuideLeft - layout.GuideWidth);
        GuestCaptureGuideTopRow.Height = new GridLength(layout.GuideTop);
        GuestCaptureGuideRow.Height = new GridLength(layout.GuideHeight);
        GuestCaptureGuideBottomRow.Height = new GridLength(
            args.NewSize.Height - layout.GuideTop - layout.GuideHeight);
        camera?.UpdateCaptureCrop(layout.SourceCrop);
#if UI_VERIFICATION
        GuestPreviewImage.RenderTransform = new CompositeTransform
        {
            ScaleX = 1.72,
            ScaleY = 1.72,
            CenterX = args.NewSize.Width / 2,
            CenterY = args.NewSize.Height / 2,
            TranslateX = -args.NewSize.Width * 0.2,
            TranslateY = -args.NewSize.Height * 0.2,
        };
#else
        var guestMirror = CameraPreviewRenderPolicy.CreateMirror(args.NewSize.Width);
        GuestPreviewImage.RenderTransform = new ScaleTransform
        {
            ScaleX = guestMirror.ScaleX,
            CenterX = guestMirror.CenterX,
        };
#endif
    }

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
            KeepEventActiveButton.IsEnabled = !isExitBusy;
            ConfirmExitEventButton.IsEnabled = !isExitBusy;
            SetButtonContent(KeepEventActiveButton, "Keep Event Active");
            SetButtonContent(ConfirmExitEventButton, isExitBusy ? "Exiting Event…" : "Exit Event", isExitBusy);
            DeletionConfirmationFrame.RefreshInitialFocus();
            StartConfirmationFrame.RefreshInitialFocus();
            ExitConfirmationFrame.RefreshInitialFocus();
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
            var showsNestedSetupConfirmation = setup?.ShowsDiscardConfirmation == true ||
                setup?.ShowsSaveConfirmation == true;
            SetupLayer.Background = showsNestedSetupConfirmation
                ? new SolidColorBrush(Colors.Transparent)
                : (Brush)Application.Current.Resources["ColorOverlayScrimBrush"];
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

    private void ApplyGuestCyclePresentation(
        ActiveEventPresentation activeEvent,
        ApplicationSurfaceOverride? surfaceOverride)
    {
        var guestCycle = activeEvent.GuestCycle;
        var isStart = guestCycle.Phase == GuestCyclePhase.Start;
        var isAssistance = guestCycle.Phase is GuestCyclePhase.StartUnavailable or GuestCyclePhase.OperatorAssistance;
        var isCapture = guestCycle.Phase is GuestCyclePhase.Countdown or GuestCyclePhase.Flash or GuestCyclePhase.CaptureSaved;
        var isStrip = guestCycle.Phase is GuestCyclePhase.PhotoStripPreparing or
            GuestCyclePhase.PhotoStripPreview or GuestCyclePhase.Fading;
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
#if UI_VERIFICATION
            GuestPreviewImage.Source = verificationCameraPreview;
#endif
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
            GuestCaptureProgress.SetProgress(guestCycle.CaptureNumber, guestCycle.CompletedCaptures);
        }

        if (isAssistance)
        {
            var beforeAdmission = guestCycle.Phase == GuestCyclePhase.StartUnavailable;
            var verificationOrigin = surfaceOverride?.Surface;
            var photoStripFailure = verificationOrigin == ApplicationSurface.PhotoStrip ||
                (surfaceOverride is null && guestCycle.CompletedCaptures == 4 &&
                 guestCycle.Failure == GuestCycleFailure.StorageUnavailable);
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
            AssistanceMessageText.Text = photoStripFailure
                ? "The Photo Strip could not be prepared."
                : failure == GuestStartFailure.CameraUnavailable
                    ? retryFailed ? "The Camera is still unavailable." : "The Camera stopped responding."
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
            AssistanceCaptureProgress.Visibility = beforeAdmission ? Visibility.Collapsed : Visibility.Visible;
            if (!beforeAdmission)
            {
                AssistanceCaptureProgress.SetProgress(
                    Math.Clamp(guestCycle.CompletedCaptures + 1, 1, 4),
                    guestCycle.CompletedCaptures);
            }
            var failureOrigin = verificationOrigin switch
            {
                ApplicationSurface.Capture => ("Camera preview", "countdown or saved status"),
                ApplicationSurface.PhotoStrip => ("Photo Strip preview", "return status and progress"),
                _ => (string.Empty, string.Empty),
            };
            AutomationProperties.SetName(FailureOriginPreviewSemantic, failureOrigin.Item1);
            AutomationProperties.SetName(FailureOriginStatusSemantic, failureOrigin.Item2);
            AutomationProperties.SetAccessibilityView(
                FailureOriginPreviewSemantic,
                failureOrigin.Item1.Length == 0 ? AccessibilityView.Raw : AccessibilityView.Content);
            AutomationProperties.SetAccessibilityView(
                FailureOriginStatusSemantic,
                failureOrigin.Item2.Length == 0 ? AccessibilityView.Raw : AccessibilityView.Content);
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
            AutomationProperties.SetName(
                AssistanceMessageText,
                beforeAdmission ? $"reason: {AssistanceMessageText.Text}" : $"cause: {AssistanceMessageText.Text}");
            AutomationProperties.SetName(AssistanceProgressText, $"preserved progress: {AssistanceProgressText.Text}");
            AutomationProperties.SetName(
                AssistanceRecoveryActionSemantic,
                beforeAdmission
                    ? "Retry or Exit Event"
                    : exitOnly ? "recovery action: Exit Event" : $"recovery action: {AssistanceRetryLabel.Text}");
        }

        if (isStrip)
        {
            var remaining = guestCycle.PreviewSecondsRemaining;
            var resultState = guestCycle.Failure != GuestCycleFailure.None
                ? PhotoStripResultState.Failed
                : guestCycle.Phase == GuestCyclePhase.PhotoStripPreparing
                    ? PhotoStripResultState.Preparing
                    : guestCycle.Phase == GuestCyclePhase.Fading
                        ? PhotoStripResultState.Returning
                        : PhotoStripResultState.Visible;
            GuestPhotoStripResult.Apply(resultState, remaining);
            if (guestCycle.PhotoStripPath is { } path &&
                (!string.Equals(loadingPhotoStripPath, path, StringComparison.Ordinal) || !photoStripVisibleSignaled))
            {
#if UI_VERIFICATION
                if (string.Equals(path, UiVerificationPresentationController.PhotoStripReferencePath, StringComparison.Ordinal))
                {
                    loadingPhotoStripPath = path;
                    GuestPhotoStripResult.ImageSource = verificationPhotoStripPreview;
                    photoStripVisibleSignaled = true;
                    CompleteMediaPendingRenderSettlement();
                }
                else
#endif
                {
                _ = LoadPhotoStripAsync(path);
                }
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
            GuestPhotoStripResult.ImageSource = null;
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
            ApplicationSurface.Capture => "Capture",
            ApplicationSurface.OperatorAssistance => "Operator Assistance",
            ApplicationSurface.PhotoStrip => "Photo Strip",
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
                : ProductionSurfaceAnnouncement(
                    activeEvent,
                    surface,
                    surfaceName,
                    state,
                    unavailable,
                    retrying,
                    exitOnly));
        if (announcement.Length == 0 ||
            (surfaceOverride is null && string.Equals(announcement, lastAnnouncement, StringComparison.Ordinal)))
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

    private string ProductionSurfaceAnnouncement(
        ActiveEventPresentation? activeEvent,
        ApplicationSurface surface,
        string surfaceName,
        string state,
        bool unavailable,
        bool retrying,
        bool exitOnly) =>
        (surface, activeEvent?.GuestCycle) switch
        {
            (ApplicationSurface.Capture,
                { Phase: GuestCyclePhase.Countdown, CountdownSeconds: 3 } cycle) =>
                $"Photo {cycle.CaptureNumber} of 4. Taking photo in three seconds.",
            (ApplicationSurface.Capture, { Phase: GuestCyclePhase.CaptureSaved } cycle) =>
                $"Photo {cycle.CompletedCaptures} saved.",
            (ApplicationSurface.PhotoStrip,
                { Phase: GuestCyclePhase.PhotoStripPreview, PreviewSecondsRemaining: 10 }) =>
                "Your photo strip is ready. Returning to start in 10 seconds.",
            (ApplicationSurface.PhotoStrip,
                { Phase: GuestCyclePhase.PhotoStripPreview, PreviewSecondsRemaining: 5 }) =>
                "Returning to start in five seconds.",
            (ApplicationSurface.PhotoStrip, { Phase: GuestCyclePhase.Fading }) =>
                "Ready for the next guest.",
            (ApplicationSurface.Capture or ApplicationSurface.PhotoStrip, _) => string.Empty,
            _ when unavailable && !retrying =>
                $"{surfaceName} needs attention. {AssistanceMessageText.Text} " +
                (exitOnly ? "Exit Event to update setup." : "Retry or Exit Event."),
            _ => $"{surfaceName} {state}",
        };

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
            GuestPhotoStripResult.ImageSource = source;
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
        PhotoStripLayer.Opacity = 0.55;
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

        var displayArea = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary);
        var windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var placement = WindowPlacement.ForWindowClientArea(
            windowHandle,
            canvas.Width,
            canvas.Height,
            displayArea.WorkArea);
        AppWindow.MoveAndResize(placement);
    }
}
