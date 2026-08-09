using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace FotoHavn.App.Controls;

public enum PhotoStripResultState
{
    Preparing,
    Visible,
    Returning,
    Failed,
}

public sealed partial class PhotoStripResult : UserControl
{
    public static readonly DependencyProperty ImageSourceProperty = DependencyProperty.Register(
        nameof(ImageSource), typeof(ImageSource), typeof(PhotoStripResult), new PropertyMetadata(null));
    public static readonly DependencyProperty RemainingSecondsProperty = DependencyProperty.Register(
        nameof(RemainingSeconds), typeof(int), typeof(PhotoStripResult), new PropertyMetadata(10, OnPresentationChanged));
    public static readonly DependencyProperty ReturnProgressProperty = DependencyProperty.Register(
        nameof(ReturnProgress), typeof(double), typeof(PhotoStripResult), new PropertyMetadata(0d));
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title), typeof(string), typeof(PhotoStripResult), new PropertyMetadata("Here’s your Photo Strip"));
    public static readonly DependencyProperty MessageProperty = DependencyProperty.Register(
        nameof(Message), typeof(string), typeof(PhotoStripResult), new PropertyMetadata(string.Empty));

    public PhotoStripResult()
    {
        InitializeComponent();
        Refresh();
    }

    public ImageSource? ImageSource
    {
        get => (ImageSource?)GetValue(ImageSourceProperty);
        set => SetValue(ImageSourceProperty, value);
    }

    public int RemainingSeconds
    {
        get => (int)GetValue(RemainingSecondsProperty);
        set => SetValue(RemainingSecondsProperty, value);
    }

    public double ReturnProgress
    {
        get => (double)GetValue(ReturnProgressProperty);
        set => SetValue(ReturnProgressProperty, value);
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Message
    {
        get => (string)GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    public PhotoStripResultState State { get; private set; }

    public void Apply(PhotoStripResultState state, int remainingSeconds)
    {
        State = state;
        RemainingSeconds = Math.Clamp(remainingSeconds, 0, 10);
        ReturnProgress = state == PhotoStripResultState.Preparing
            ? 0.38
            : Math.Clamp((11 - RemainingSeconds) / 10d, 0, 1);
        Message = state switch
        {
            PhotoStripResultState.Preparing => "Your saved photos are being arranged.",
            PhotoStripResultState.Returning => "Returning to start now.",
            PhotoStripResultState.Failed => "Your Captures are safe. Please call the operator.",
            _ => $"Returning to start in {RemainingSeconds} seconds.",
        };
        Title = state switch
        {
            PhotoStripResultState.Preparing => "Preparing your Photo Strip…",
            PhotoStripResultState.Returning => "Ready for the next guest",
            _ => "Here’s your Photo Strip",
        };
        EyebrowText.Text = state == PhotoStripResultState.Preparing
            ? "ALL FOUR PHOTOS SAVED"
            : "PHOTO STRIP READY";
        Refresh();
    }

    internal void ApplyResponsiveLayout(ResponsiveLayoutMode mode)
    {
        var stress = mode == ResponsiveLayoutMode.Stress;
        var compact = mode != ResponsiveLayoutMode.Standard;
        Grid.SetColumn(MessagePanel, stress ? 1 : 0);
        Grid.SetColumn(PreviewFrame, stress ? 0 : 1);
        PreviewColumn.Width = new GridLength(stress ? 1 : 0, stress ? GridUnitType.Star : GridUnitType.Auto);
        MessageColumn.Width = new GridLength(1, GridUnitType.Star);
        EyebrowText.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
        TitleText.FontSize = stress ? 27 : compact ? 36 : 42;
        var frameHeight = stress ? 284d : compact ? 440d : 520d;
        var frameWidth = stress ? 135d : compact ? 190d : 226d;
        PreviewFrame.Height = frameHeight;
        PreviewFrame.Width = frameWidth;

        PreviewImage.Height = frameHeight;
        PreviewImage.Width = frameWidth;
    }

    private static void OnPresentationChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args) =>
        ((PhotoStripResult)sender).Refresh();

    private void Refresh()
    {
        if (PreparingProgress is null)
        {
            return;
        }

        PreparingProgress.Visibility = State == PhotoStripResultState.Preparing ? Visibility.Visible : Visibility.Collapsed;
        PreparingProgress.IsActive = State == PhotoStripResultState.Preparing;
        PreviewImage.Opacity = State == PhotoStripResultState.Preparing
            ? 0.16
            : State == PhotoStripResultState.Returning ? 0.55 : 1;
        FailureText.Visibility = State == PhotoStripResultState.Failed ? Visibility.Visible : Visibility.Collapsed;
        ReturnProgressBar.Visibility = State == PhotoStripResultState.Failed ? Visibility.Collapsed : Visibility.Visible;
        AutomationProperties.SetItemStatus(this, State.ToString().ToLowerInvariant());
        AutomationProperties.SetHelpText(this, Message);
    }
}
