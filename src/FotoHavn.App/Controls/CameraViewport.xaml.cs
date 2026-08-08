using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace FotoHavn.App.Controls;

public enum CameraViewportMode
{
    SetupPreview,
    GuestCapture,
}

public sealed partial class CameraViewport : UserControl
{
    public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(
        nameof(Source), typeof(ImageSource), typeof(CameraViewport), new PropertyMetadata(null));
    public static readonly DependencyProperty StatusProperty = DependencyProperty.Register(
        nameof(Status), typeof(string), typeof(CameraViewport), new PropertyMetadata("Unavailable", OnStatusChanged));
    public static readonly DependencyProperty OverlayContentProperty = DependencyProperty.Register(
        nameof(OverlayContent), typeof(object), typeof(CameraViewport), new PropertyMetadata(null));
    public static readonly DependencyProperty ShowCaptureGuideProperty = DependencyProperty.Register(
        nameof(ShowCaptureGuide), typeof(bool), typeof(CameraViewport), new PropertyMetadata(true, OnGuideChanged));
    public static readonly DependencyProperty IsMirroredProperty = DependencyProperty.Register(
        nameof(IsMirrored), typeof(bool), typeof(CameraViewport), new PropertyMetadata(true, OnMirrorChanged));
    public static readonly DependencyProperty ModeProperty = DependencyProperty.Register(
        nameof(Mode), typeof(CameraViewportMode), typeof(CameraViewport), new PropertyMetadata(CameraViewportMode.SetupPreview, OnModeChanged));

    public CameraViewport()
    {
        InitializeComponent();
        AutomationProperties.SetAutomationId(this, SemanticAutomationIds.CameraViewport);
        ApplyMirror();
    }

    public ImageSource? Source
    {
        get => (ImageSource?)GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    public string Status
    {
        get => (string)GetValue(StatusProperty);
        set => SetValue(StatusProperty, value);
    }

    public object? OverlayContent
    {
        get => GetValue(OverlayContentProperty);
        set => SetValue(OverlayContentProperty, value);
    }

    public bool ShowCaptureGuide
    {
        get => (bool)GetValue(ShowCaptureGuideProperty);
        set => SetValue(ShowCaptureGuideProperty, value);
    }

    public bool IsMirrored
    {
        get => (bool)GetValue(IsMirroredProperty);
        set => SetValue(IsMirroredProperty, value);
    }

    public CameraViewportMode Mode
    {
        get => (CameraViewportMode)GetValue(ModeProperty);
        set => SetValue(ModeProperty, value);
    }

    private static void OnModeChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is CameraViewport viewport)
        {
            viewport.ShowCaptureGuide = viewport.Mode == CameraViewportMode.SetupPreview;
        }
    }

    private static void OnStatusChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is CameraViewport viewport)
        {
            AutomationProperties.SetItemStatus(viewport, viewport.Status);
        }
    }

    private static void OnGuideChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is CameraViewport viewport && viewport.CaptureGuide is not null)
        {
            viewport.CaptureGuide.Visibility = viewport.ShowCaptureGuide ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private static void OnMirrorChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is CameraViewport viewport)
        {
            viewport.ApplyMirror();
        }
    }

    private void ApplyMirror()
    {
        if (PreviewImage is not null)
        {
            PreviewImage.RenderTransformOrigin = new(0.5, 0.5);
            PreviewImage.RenderTransform = new ScaleTransform { ScaleX = IsMirrored ? -1 : 1 };
        }
    }
}
