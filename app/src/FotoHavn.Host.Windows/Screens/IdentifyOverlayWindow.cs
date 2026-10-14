using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Interop;
using System.Windows.Media;
using FotoHavn.Core;
using FotoHavn.Core.Displays;
using FotoHavn.Host.Windows.Interop;

namespace FotoHavn.Host.Windows.Screens;

/// <summary>
/// The number one display shows for Identify displays: a borderless window over the display's bounds that is topmost, off the
/// taskbar, and never takes focus (<c>WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW</c>, <c>ShowActivated=false</c>, not focusable, and
/// <c>WM_MOUSEACTIVATE</c> answered with <c>MA_NOACTIVATE</c>). Its only content is a centred Off-white panel with the display
/// number in the Display role and the display's name in the Meta role, in the brand colors of <c>brand/tokens/tokens.json</c>.
/// </summary>
internal sealed class IdentifyOverlayWindow : Window
{
    private static readonly IntPtr TopmostInsertAfter = new(-1);

    private static readonly Brush OffWhite = Frozen(0xFB, 0xF8, 0xF2);
    private static readonly Brush Ebony = Frozen(0x1E, 0x1A, 0x17);
    private static readonly Brush Hairline = Frozen(0xD1, 0xC4, 0xB4);
    private static readonly Brush MutedBrass = Frozen(0x9A, 0x7A, 0x4F);
    private static readonly Brush SoftBrown = Frozen(0x75, 0x64, 0x57);

    private readonly IdentifyOverlaySpec _spec;
    private readonly OverlayFonts _fonts;

    public IdentifyOverlayWindow(IdentifyOverlaySpec spec, OverlayFonts fonts)
    {
        ArgumentNullException.ThrowIfNull(spec);
        ArgumentNullException.ThrowIfNull(fonts);
        _spec = spec;
        _fonts = fonts;
        Title = $"FOTOHAVN display {spec.Number}";
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        ShowActivated = false;
        Topmost = true;
        Focusable = false;
        UseLayoutRounding = true;
        WindowStartupLocation = WindowStartupLocation.Manual;
        DpiChanged += OnDpiChanged;
    }

    /// <summary>The display this overlay covers.</summary>
    public string DisplayId => _spec.Display.Id;

    public IntPtr Handle => new WindowInteropHelper(this).Handle;

    /// <summary>The window's outer bounds in physical pixels.</summary>
    public DisplayRect GetPhysicalBounds() =>
        Handle != IntPtr.Zero && NativeMethods.GetWindowRect(Handle, out var rect)
            ? new DisplayRect(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top)
            : default;

    /// <summary>The window's extended style bits (zero before the window exists).</summary>
    public long ExtendedStyle => Handle == IntPtr.Zero ? 0 : NativeMethods.GetWindowLongPtr(Handle, NativeMethods.GwlExStyle).ToInt64();

    /// <summary>Creates the window on the display's bounds (physical pixels), draws the panel at that display's DPI, and shows it without activating.</summary>
    public void Present()
    {
        new WindowInteropHelper(this).EnsureHandle();
        Place();
        Build(VisualTreeHelper.GetDpi(this).DpiScaleX);
        Show();
        Place();
    }

    /// <summary>Creates the window and places it, without drawing or showing it (the DPI is read by <see cref="Present"/>).</summary>
    public void PlaceOnly()
    {
        new WindowInteropHelper(this).EnsureHandle();
        Place();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var handle = Handle;
        var style = NativeMethods.GetWindowLongPtr(handle, NativeMethods.GwlExStyle).ToInt64();
        NativeMethods.SetWindowLongPtr(handle, NativeMethods.GwlExStyle, new IntPtr(style | NativeMethods.WsExToolWindow | NativeMethods.WsExNoActivate));
        HwndSource.FromHwnd(handle)?.AddHook(WindowProcedure);
    }

    internal static IntPtr WindowProcedure(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message != NativeMethods.WmMouseActivate)
        {
            return IntPtr.Zero;
        }

        handled = true;
        return new IntPtr(NativeMethods.MaNoActivate);
    }

    private void Place()
    {
        var bounds = _spec.Display.Bounds;
        NativeMethods.SetWindowPos(Handle, TopmostInsertAfter, bounds.X, bounds.Y, bounds.Width, bounds.Height, NativeMethods.SwpNoActivate);
    }

    private void OnDpiChanged(object sender, DpiChangedEventArgs e)
    {
        Build(e.NewDpi.DpiScaleX);
        // WPF applies the suggested rectangle for the new DPI; put the window back on the display's exact bounds.
        Dispatcher.InvokeAsync(Place, System.Windows.Threading.DispatcherPriority.Background);
    }

    /// <summary>Draws the panel. The metrics are physical pixels; WPF lays out in device-independent pixels, so divide by the window's scale.</summary>
    internal void Build(double dpiScale)
    {
        var metrics = IdentifyOverlayLayout.For(_spec.Display.Bounds);
        double Dip(double pixels) => pixels / dpiScale;

        var number = new TextBlock
        {
            Text = _spec.Number.ToString(CultureInfo.InvariantCulture),
            FontFamily = _fonts.Display.Family,
            FontWeight = _fonts.Display.Weight,
            FontStyle = _fonts.Display.Style,
            FontStretch = _fonts.Display.Stretch,
            FontSize = Dip(metrics.NumberFontSize),
            LineHeight = Dip(metrics.NumberFontSize),
            LineStackingStrategy = LineStackingStrategy.BlockLineHeight,
            Foreground = Ebony,
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center,
        };
        Typography.SetNumeralStyle(number, FontNumeralStyle.Lining);

        var rule = new Border
        {
            Width = Dip(metrics.PanelWidth * 0.12),
            Height = Math.Max(1, Dip(2)),
            Background = MutedBrass,
            Margin = new Thickness(0, Dip(metrics.Padding * 0.5), 0, Dip(metrics.Padding * 0.5)),
            HorizontalAlignment = HorizontalAlignment.Center,
        };

        var name = new TextBlock
        {
            Text = _spec.Name,
            FontFamily = _fonts.Meta.Family,
            FontWeight = _fonts.Meta.Weight,
            FontStyle = _fonts.Meta.Style,
            FontStretch = _fonts.Meta.Stretch,
            FontSize = Dip(metrics.NameFontSize),
            Foreground = SoftBrown,
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth = Dip(metrics.PanelWidth - (2 * metrics.Padding)),
        };

        var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
        stack.Children.Add(number);
        stack.Children.Add(rule);
        stack.Children.Add(name);

        var panel = new Border
        {
            Width = Dip(metrics.PanelWidth),
            Height = Dip(metrics.PanelHeight),
            Background = OffWhite,
            BorderBrush = Hairline,
            BorderThickness = new Thickness(Math.Max(1, Dip(1))),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Child = stack,
        };
        AutomationProperties.SetName(panel, $"Display {_spec.Number}: {_spec.Name}");

        Content = new Grid { Children = { panel } };
    }

    private static SolidColorBrush Frozen(byte red, byte green, byte blue)
    {
        var brush = new SolidColorBrush(Color.FromRgb(red, green, blue));
        brush.Freeze();
        return brush;
    }
}
