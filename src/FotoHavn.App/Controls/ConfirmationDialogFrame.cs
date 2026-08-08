using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;

namespace FotoHavn.App.Controls;

public sealed class ConfirmationDialogFrame : ContentControl
{
    public ConfirmationDialogFrame()
    {
        DefaultStyleKey = typeof(ConfirmationDialogFrame);
        AutomationProperties.SetAutomationId(this, SemanticAutomationIds.ModalDialog);
        SizeChanged += (_, args) => ApplyActionLayout(args.NewSize.Width);
        Loaded += (_, _) => ApplyActionLayout(ActualWidth);
    }

    private void ApplyActionLayout(double width)
    {
        if (Content is not DependencyObject content || FindActions(content) is not { } actions)
        {
            return;
        }

        var viewport = XamlRoot?.Size;
        var stress = viewport is { } size
            ? ResponsiveLayout.Resolve(size.Width, size.Height) == ResponsiveLayoutMode.Stress
            : width < 440;
        Margin = new(stress ? 16 : 24);
        actions.Orientation = stress ? Orientation.Vertical : Orientation.Horizontal;
        actions.HorizontalAlignment = stress ? HorizontalAlignment.Stretch : HorizontalAlignment.Right;
        foreach (var child in actions.Children.OfType<FrameworkElement>())
        {
            child.HorizontalAlignment = stress ? HorizontalAlignment.Stretch : HorizontalAlignment.Right;
        }
    }

    private static StackPanel? FindActions(DependencyObject root)
    {
        if (root is StackPanel panel && AutomationProperties.GetName(panel) == "Dialog actions")
        {
            return panel;
        }

        for (var index = 0; index < Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(root); index++)
        {
            if (FindActions(Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(root, index)) is { } match)
            {
                return match;
            }
        }

        return null;
    }
}
