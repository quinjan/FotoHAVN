using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace FotoHavn.App.Controls;

public enum CaptureProgressPhase
{
    NotStarted,
    Active,
    Complete,
}

public sealed partial class CaptureProgress : UserControl
{
    public static readonly DependencyProperty ActiveCaptureProperty = DependencyProperty.Register(
        nameof(ActiveCapture),
        typeof(int),
        typeof(CaptureProgress),
        new PropertyMetadata(1, OnProgressChanged));

    public static readonly DependencyProperty PhaseProperty = DependencyProperty.Register(
        nameof(Phase),
        typeof(CaptureProgressPhase),
        typeof(CaptureProgress),
        new PropertyMetadata(CaptureProgressPhase.NotStarted, OnProgressChanged));

    public CaptureProgress()
    {
        InitializeComponent();
        Refresh();
    }

    public int ActiveCapture
    {
        get => (int)GetValue(ActiveCaptureProperty);
        set => SetValue(ActiveCaptureProperty, value);
    }

    public CaptureProgressPhase Phase
    {
        get => (CaptureProgressPhase)GetValue(PhaseProperty);
        set => SetValue(PhaseProperty, value);
    }

    public void SetProgress(int activeCapture, int completedCaptures)
    {
        ActiveCapture = Math.Clamp(
            completedCaptures is > 0 and < 4 && completedCaptures >= activeCapture
                ? completedCaptures + 1
                : activeCapture,
            1,
            4);
        Phase = completedCaptures >= 4 ? CaptureProgressPhase.Complete : CaptureProgressPhase.Active;
        Refresh(completedCaptures);
    }

    public void SetCompact(bool compact)
    {
        ProgressLabel.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
    }

    private static void OnProgressChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args) =>
        ((CaptureProgress)sender).Refresh();

    private void Refresh(int? completedOverride = null)
    {
        if (Step1 is null)
        {
            return;
        }

        var active = Math.Clamp(ActiveCapture, 1, 4);
        var completed = completedOverride ?? Phase switch
        {
            CaptureProgressPhase.NotStarted => 0,
            CaptureProgressPhase.Active => active - 1,
            CaptureProgressPhase.Complete => active,
            _ => 0,
        };
        var label = completed >= 4 ? "All 4 Captures saved" : $"Photo {active} of 4";
        ProgressLabel.Text = label.ToUpperInvariant();
        AutomationProperties.SetHelpText(this, label);
        AutomationProperties.SetItemStatus(this, completed >= 4 ? "complete" : "active");

        Border[] borders = [Step1, Step2, Step3, Step4];
        TextBlock[] numbers = [Number1, Number2, Number3, Number4];
        FontIcon[] checks = [Check1, Check2, Check3, Check4];
        var primary = (Brush)Application.Current.Resources["TextPrimaryBrush"];
        var hairline = (Brush)Application.Current.Resources["HairlineBrush"];
        for (var index = 0; index < borders.Length; index++)
        {
            var captureNumber = index + 1;
            var complete = captureNumber <= completed;
            var isActive = captureNumber == active && !complete;
            borders[index].Background = complete ? primary : new SolidColorBrush(Colors.White);
            borders[index].BorderBrush = complete || isActive ? primary : hairline;
            borders[index].BorderThickness = new Thickness(isActive ? 2 : 1);
            numbers[index].Visibility = complete ? Visibility.Collapsed : Visibility.Visible;
            checks[index].Visibility = complete ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}
