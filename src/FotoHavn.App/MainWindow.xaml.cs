using FotoHavn.Core;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
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

    private async void CameraMenuButtonClicked(object sender, RoutedEventArgs args) =>
        await ExecuteAsync(new ToggleCameraMenu());

    private async void CameraFlyoutClosed(object? sender, object args) =>
        await ExecuteAsync(new DismissCameraMenu());

    private async void CameraItemClicked(object sender, ItemClickEventArgs args)
    {
        if (args.ClickedItem is AvailableCamera selected)
        {
            CameraFlyout.Hide();
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

    private async void SetupLayerKeyDown(object sender, KeyRoutedEventArgs args)
    {
        if (orchestrator.CurrentPresentation.Setup?.ShowsDiscardConfirmation == true)
        {
            args.Handled = true;
            return;
        }

        if (args.Key == Windows.System.VirtualKey.Escape)
        {
            if (CameraFlyout.IsOpen)
            {
                CameraFlyout.Hide();
                await ExecuteAsync(new DismissCameraMenu());
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
                ActiveEventNameText.Text = activeEvent.Name;
                ActiveEventHeadingText.Text = activeEvent.Heading;
                ActiveEventExplanationText.Text = activeEvent.Explanation;
                StartGuestCycleButton.Content = activeEvent.StartActionLabel;
            }

            var setup = presentation.Setup;
            SetupLayer.Visibility = setup is null ? Visibility.Collapsed : Visibility.Visible;
            DiscardDraftLayer.Visibility = setup?.ShowsDiscardConfirmation == true
                ? Visibility.Visible
                : Visibility.Collapsed;
            SetupDialog.IsHitTestVisible = setup?.ShowsDiscardConfirmation != true;
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

            CameraList.ItemsSource = setup.AvailableCameras;
            CameraMenuButtonText.Text = setup.SelectedCamera?.DisplayName ?? "Select Camera";
            CameraStatusText.Text = setup.CameraState switch
            {
                CameraConnectionState.NotSelected => "Select a Camera",
                CameraConnectionState.Connecting => "Connecting…",
                CameraConnectionState.Ready => "Ready",
                CameraConnectionState.Unavailable => "Unavailable",
                CameraConnectionState.AccessDenied => "Access denied",
                CameraConnectionState.InUseByAnotherApp => "In use by another app",
                CameraConnectionState.Disconnected => "Disconnected",
                _ => throw new ArgumentOutOfRangeException(),
            };
            SetupFailureText.Text = setup.ActionableFailureMessage ?? string.Empty;
            SetupFailureBorder.Visibility = setup.ActionableFailureMessage is null
                ? Visibility.Collapsed
                : Visibility.Visible;
            RetryStorageButton.Visibility = setup.IsStorageReady
                ? Visibility.Collapsed
                : Visibility.Visible;
            PrinterComboBox.SelectedIndex = setup.IsNoPrinterSelected ? 0 : -1;
            SaveCloseButton.IsEnabled = setup.CanSave;
            SaveStartButton.IsEnabled = setup.CanSave;

            if (setup.CameraMenu.IsOpen && !CameraFlyout.IsOpen)
            {
                CameraFlyout.ShowAt(CameraMenuButton);
            }
            else if (!setup.CameraMenu.IsOpen && CameraFlyout.IsOpen)
            {
                CameraFlyout.Hide();
            }
        }
        finally
        {
            applyingPresentation = false;
        }
    }

    private void PreviewFrameAvailable(object? sender, SoftwareBitmap bitmap)
    {
        if (orchestrator.CurrentPresentation.Setup is null)
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
                    PreviewImage.Source = source;
                }
            }
        }))
        {
            bitmap.Dispose();
        }
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
