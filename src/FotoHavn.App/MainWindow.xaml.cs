using FotoHavn.Core;
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

namespace FotoHavn.App;

public sealed partial class MainWindow : Window
{
    private readonly EventGuestCycleOrchestrator orchestrator;
    private readonly CameraBoundary camera;
    private readonly HashSet<Border> hoveredEventCards = [];
    private ApplicationCanvasPresentation? canvas;
    private bool applyingPresentation;
    private string? loadingPhotoStripPath;
    private bool photoStripVisibleSignaled;
    private bool photoStripFadeStarted;

    public MainWindow(EventGuestCycleOrchestrator orchestrator, CameraBoundary camera)
    {
        this.orchestrator = orchestrator;
        this.camera = camera;
        InitializeComponent();
        PreviewViewport.Width = PreviewViewport.Height * CameraPreviewRenderPolicy.CropAspectRatio;
        PreviewSurface.Width = PreviewViewport.Width;
        PreviewSurface.Height = PreviewViewport.Height;
        var mirror = CameraPreviewRenderPolicy.CreateMirror(PreviewSurface.Width);
        PreviewSurface.RenderTransform = new ScaleTransform
        {
            ScaleX = mirror.ScaleX,
            CenterX = mirror.CenterX,
        };
        orchestrator.PresentationChanged += PresentationChanged;
        camera.PreviewFrameAvailable += PreviewFrameAvailable;
    }

    public async Task LoadPresentationAsync(CancellationToken cancellationToken = default)
    {
        var presentation = await orchestrator.ExecuteAsync(new LaunchApplication(), cancellationToken);
        HeadingText.Text = presentation.Heading;
        EventTiles.ItemsSource = presentation.EventTiles;
        FixedCanvas.Width = presentation.Canvas.Width;
        FixedCanvas.Height = presentation.Canvas.Height;
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
        if (orchestrator.CurrentPresentation.Setup is { } setup &&
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
            StartEventConfirmationLayer.Visibility = presentation.StartEventConfirmation is null
                ? Visibility.Collapsed
                : Visibility.Visible;
            StartEventConfirmationText.Text = presentation.StartEventConfirmation?.Prompt ?? string.Empty;
            if (activeEvent is not null)
            {
                ActiveEventNameText.Text = activeEvent.Name.ToUpperInvariant();
                ActiveEventHeadingText.Text = activeEvent.Heading;
                ActiveEventExplanationText.Text = activeEvent.Explanation;
                ApplyGuestCyclePresentation(activeEvent.GuestCycle);
            }

            var setup = presentation.Setup;
            SetupLayer.Visibility = setup is null ? Visibility.Collapsed : Visibility.Visible;
            DiscardDraftLayer.Visibility = setup?.ShowsDiscardConfirmation == true
                ? Visibility.Visible
                : Visibility.Collapsed;
            SaveChangesLayer.Visibility = setup?.ShowsSaveConfirmation == true
                ? Visibility.Visible
                : Visibility.Collapsed;
            SetupDialog.IsHitTestVisible = setup is null ||
                (!setup.ShowsDiscardConfirmation && !setup.ShowsSaveConfirmation);
            if (setup is null)
            {
                PreviewImage.Source = null;
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
            PrinterComboBox.SelectedIndex = setup.IsNoPrinterSelected ? 0 : -1;
            SaveCloseButton.IsEnabled = setup.CanSave;
            SaveStartButton.IsEnabled = setup.CanStart;

        }
        finally
        {
            applyingPresentation = false;
        }
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
        var presentation = orchestrator.CurrentPresentation;
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
                    if (orchestrator.CurrentPresentation.Setup is not null)
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
            await ExecuteAsync(new ConfirmPhotoStripVisible());
        }
        catch (Exception exception) when (
            exception is FileNotFoundException or
            IOException or
            System.Runtime.InteropServices.COMException)
        {
            photoStripVisibleSignaled = false;
            await ExecuteAsync(new ReportPhotoStripDecodeFailure());
        }
    }

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
            await orchestrator.ExecuteAsync(command);
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
