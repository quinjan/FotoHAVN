using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace FotoHavn.App.Controls;

public sealed partial class DialogSemanticIcon : UserControl
{
    public static readonly DependencyProperty GlyphProperty = DependencyProperty.Register(
        nameof(Glyph),
        typeof(string),
        typeof(DialogSemanticIcon),
        new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty IntentProperty = DependencyProperty.Register(
        nameof(Intent),
        typeof(DialogSemanticIntent),
        typeof(DialogSemanticIcon),
        new PropertyMetadata(DialogSemanticIntent.Neutral, OnIntentChanged));

    public DialogSemanticIcon()
    {
        InitializeComponent();
        Loaded += (_, _) => ApplyIntent();
    }

    public string Glyph
    {
        get => (string)GetValue(GlyphProperty);
        set => SetValue(GlyphProperty, value);
    }

    public DialogSemanticIntent Intent
    {
        get => (DialogSemanticIntent)GetValue(IntentProperty);
        set => SetValue(IntentProperty, value);
    }

    private static void OnIntentChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args) =>
        ((DialogSemanticIcon)dependencyObject).ApplyIntent();

    private void ApplyIntent()
    {
        if (!IsLoaded)
        {
            return;
        }

        var resource = Intent switch
        {
            DialogSemanticIntent.Destructive => "ColorStatusDangerForegroundBrush",
            DialogSemanticIntent.Success => "ColorStatusSuccessForegroundBrush",
            _ => "ColorTextPrimaryBrush",
        };
        var brush = (Brush)Application.Current.Resources[resource];
        IconFrame.BorderBrush = brush;
        Icon.Foreground = brush;
    }

    public void ApplyResponsiveLayout(bool stress)
    {
        var size = stress ? 42 : (double)Application.Current.Resources["DialogSemanticIconSize"];
        IconFrame.Width = size;
        IconFrame.Height = size;
        IconFrame.CornerRadius = new(size / 2);
        Icon.FontSize = stress ? 18 : (double)Application.Current.Resources["DialogSemanticIconGlyphSize"];
    }
}

public enum DialogSemanticIntent
{
    Neutral,
    Destructive,
    Success,
}
