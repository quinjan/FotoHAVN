using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using FotoHavn.Core.Displays;
using FotoHavn.Host.Windows.Interop;
using FotoHavn.Host.Windows.Screens;

namespace FotoHavn.Host.Windows.Tests;

public sealed class IdentifyOverlayTests
{
    private static readonly string FontsFolder = Path.Combine(AppContext.BaseDirectory, "overlay-fonts-test");

    private static readonly IdentifyOverlaySpec Spec = new(
        new DisplayInfo("virtual-guest", "Guest display", new DisplayRect(80, 60, 640, 360), new DisplayRect(80, 60, 640, 360), 1.0, IsPrimary: false),
        2,
        "Guest display");

    [Fact]
    public void The_brand_fonts_come_from_the_embedded_files_not_the_system()
    {
        var fonts = OverlayFonts.Load(FontsFolder);

        Assert.Contains("Cormorant Garamond", fonts.Display.Family.Source, StringComparison.Ordinal);
        Assert.Contains("Manrope", fonts.Meta.Family.Source, StringComparison.Ordinal);
        Assert.StartsWith(Path.GetFullPath(FontsFolder), new Uri(fonts.Display.Family.BaseUri.AbsoluteUri).LocalPath, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(FontWeights.Medium, fonts.Display.Weight);
        Assert.Equal(FontWeights.Normal, fonts.Meta.Weight);
    }

    [Fact]
    public void Loading_twice_reuses_the_extracted_files()
    {
        var first = OverlayFonts.Load(FontsFolder);
        var folder = new Uri(first.Display.Family.BaseUri.AbsoluteUri).LocalPath;
        var files = Directory.GetFiles(folder, "*.ttf");
        var written = files.Select(file => File.GetLastWriteTimeUtc(file)).ToArray();

        var second = OverlayFonts.Load(FontsFolder);

        Assert.Equal(2, files.Length);
        Assert.Equal(first.Display.Family.BaseUri, second.Display.Family.BaseUri);
        Assert.Equal(written, Directory.GetFiles(folder, "*.ttf").Select(file => File.GetLastWriteTimeUtc(file)).ToArray());
        Assert.Empty(Directory.GetFiles(folder, "*.partial"));
        Assert.Throws<ArgumentException>(() => OverlayFonts.Load(" "));
    }

    [Fact]
    public void The_overlay_is_a_non_activating_tool_window_placed_on_its_display()
    {
        Sta(() =>
        {
            var window = new IdentifyOverlayWindow(Spec, OverlayFonts.Load(FontsFolder));
            try
            {
                window.PlaceOnly();

                Assert.Equal(Spec.Display.Bounds, window.GetPhysicalBounds());
                Assert.Equal("virtual-guest", window.DisplayId);
                var style = window.ExtendedStyle;
                Assert.Equal(NativeMethods.WsExNoActivate, style & NativeMethods.WsExNoActivate);
                Assert.Equal(NativeMethods.WsExToolWindow, style & NativeMethods.WsExToolWindow);
                Assert.Equal(WindowStyle.None, window.WindowStyle);
                Assert.False(window.ShowActivated);
                Assert.False(window.ShowInTaskbar);
                Assert.False(window.Focusable);
                Assert.True(window.Topmost);
                Assert.False(window.IsVisible);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void A_click_never_activates_the_overlay()
    {
        var handled = false;

        var answer = IdentifyOverlayWindow.WindowProcedure(IntPtr.Zero, NativeMethods.WmMouseActivate, IntPtr.Zero, IntPtr.Zero, ref handled);

        Assert.True(handled);
        Assert.Equal(NativeMethods.MaNoActivate, answer.ToInt32());

        handled = false;
        IdentifyOverlayWindow.WindowProcedure(IntPtr.Zero, 0x000F, IntPtr.Zero, IntPtr.Zero, ref handled);
        Assert.False(handled);
    }

    [Theory]
    [InlineData(1.0)]
    [InlineData(1.5)]
    public void The_panel_shows_the_number_and_name_in_brand_colors_and_fonts(double dpiScale)
    {
        Sta(() =>
        {
            var fonts = OverlayFonts.Load(FontsFolder);
            var window = new IdentifyOverlayWindow(Spec, fonts);
            try
            {
                window.Build(dpiScale);

                var panel = Assert.IsType<Border>(Assert.IsType<Grid>(window.Content).Children[0]);
                var texts = Descendants<TextBlock>(panel).ToList();
                var number = Assert.Single(texts, text => text.Text == "2");
                var name = Assert.Single(texts, text => text.Text == "Guest display");
                var metrics = IdentifyOverlayLayout.For(Spec.Display.Bounds);

                Assert.Equal(Color.FromRgb(0xFB, 0xF8, 0xF2), ((SolidColorBrush)panel.Background).Color);
                Assert.Equal(Color.FromRgb(0x1E, 0x1A, 0x17), ((SolidColorBrush)number.Foreground).Color);
                Assert.Same(fonts.Display.Family, number.FontFamily);
                Assert.Same(fonts.Meta.Family, name.FontFamily);
                Assert.Equal(metrics.NumberFontSize / dpiScale, number.FontSize, 3);
                Assert.Equal(metrics.NameFontSize / dpiScale, name.FontSize, 3);
                Assert.Equal(metrics.PanelWidth / dpiScale, panel.Width, 3);
                Assert.Equal(metrics.PanelHeight / dpiScale, panel.Height, 3);
                Assert.Equal(TextTrimming.CharacterEllipsis, name.TextTrimming);
                Assert.Equal(HorizontalAlignment.Center, panel.HorizontalAlignment);
                Assert.Equal(VerticalAlignment.Center, panel.VerticalAlignment);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void The_overlay_renders_the_number_and_the_name_inside_the_panel()
    {
        Sta(() =>
        {
            var window = new IdentifyOverlayWindow(Spec, OverlayFonts.Load(FontsFolder));
            try
            {
                window.Build(1.0);
                var root = Assert.IsType<Grid>(window.Content);
                root.Measure(new Size(640, 360));
                root.Arrange(new Rect(0, 0, 640, 360));
                root.UpdateLayout();
                var panel = Assert.IsType<Border>(root.Children[0]);
                var number = Descendants<TextBlock>(panel).Single(text => text.Text == "2");
                var name = Descendants<TextBlock>(panel).Single(text => text.Text == "Guest display");

                Assert.True(number.ActualHeight > 0 && number.ActualWidth > 0);
                Assert.True(name.ActualWidth <= name.MaxWidth);
                Assert.True(number.ActualHeight + name.ActualHeight < panel.ActualHeight);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void An_overlay_needs_its_spec_and_fonts()
    {
        Sta(() =>
        {
            Assert.Throws<ArgumentNullException>(() => new IdentifyOverlayWindow(null!, OverlayFonts.Load(FontsFolder)));
            Assert.Throws<ArgumentNullException>(() => new IdentifyOverlayWindow(Spec, null!));
        });
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject root)
        where T : DependencyObject
    {
        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
        {
            if (child is T match)
            {
                yield return match;
            }

            foreach (var nested in Descendants<T>(child))
            {
                yield return nested;
            }
        }
    }

    /// <summary>WPF windows belong to a single-threaded apartment; run the body on one and rethrow what it throws.</summary>
    private static void Sta(Action body)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                body();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }
}
