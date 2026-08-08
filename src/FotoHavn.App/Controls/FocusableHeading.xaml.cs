using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;

namespace FotoHavn.App.Controls;

public sealed partial class FocusableHeading : UserControl
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text),
        typeof(string),
        typeof(FocusableHeading),
        new PropertyMetadata(string.Empty, OnTextChanged));

    public static readonly DependencyProperty TextStyleProperty = DependencyProperty.Register(
        nameof(TextStyle),
        typeof(Style),
        typeof(FocusableHeading),
        new PropertyMetadata(null));

    public static readonly DependencyProperty TextAlignmentProperty = DependencyProperty.Register(
        nameof(TextAlignment),
        typeof(TextAlignment),
        typeof(FocusableHeading),
        new PropertyMetadata(TextAlignment.Left));

    public FocusableHeading()
    {
        InitializeComponent();
    }

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public Style? TextStyle
    {
        get => (Style?)GetValue(TextStyleProperty);
        set => SetValue(TextStyleProperty, value);
    }

    public TextAlignment TextAlignment
    {
        get => (TextAlignment)GetValue(TextAlignmentProperty);
        set => SetValue(TextAlignmentProperty, value);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new FocusableHeadingAutomationPeer(this);

    private static void OnTextChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is FocusableHeading heading)
        {
            AutomationProperties.SetName(heading, (string)args.NewValue);
        }
    }

    private sealed class FocusableHeadingAutomationPeer(FocusableHeading owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override string GetNameCore() => owner.Text;

        protected override string GetClassNameCore() => nameof(FocusableHeading);

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Text;
    }
}
