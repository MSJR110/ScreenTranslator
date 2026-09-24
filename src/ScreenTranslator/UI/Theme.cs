using System.Windows;
using System.Windows.Media;
using ScreenTranslator.Native;

namespace ScreenTranslator.UI;

/// <summary>
/// Application-wide brushes/fonts, swapped at runtime between dark and light. Windows bind via DynamicResource.
/// The look is "warm glass": acrylic surfaces tinted toward plum/cream, a coral→amber signature gradient for anything
/// that should feel alive (primary buttons, toggles, icon tiles) and a soft ambient bloom behind every window.
/// </summary>
public static class Theme
{
    public static readonly FontFamily AppFont = new(new Uri("pack://application:,,,/"), "./Assets/Fonts/#Vazirmatn, Segoe UI, Tahoma");

    public static bool IsDark { get; private set; } = true;

    public static event Action? Changed;

    /// <summary>Signature gradient endpoints (same in both themes so the brand reads identically).</summary>
    public static Color Coral => IsDark ? Color.FromRgb(0xFF, 0x7A, 0x4D) : Color.FromRgb(0xEE, 0x5A, 0x2E);
    public static Color Amber => IsDark ? Color.FromRgb(0xFF, 0xB5, 0x47) : Color.FromRgb(0xF2, 0x9B, 0x1C);
    public static Color Rose  => IsDark ? Color.FromRgb(0xFF, 0x6F, 0x9E) : Color.FromRgb(0xE8, 0x4E, 0x84);

    public static void Apply(string mode)
    {
        bool dark = mode switch
        {
            "light" => false,
            "dark" => true,
            _ => !WindowEffects.IsSystemLightTheme(),
        };
        IsDark = dark;

        var res = Application.Current.Resources;
        var coral = Coral;
        var amber = Amber;

        res["AppFont"] = AppFont;
        res["AccentColor"] = coral;
        res["Accent2Color"] = amber;
        res["Accent"] = Freeze(new SolidColorBrush(coral));
        res["Accent2"] = Freeze(new SolidColorBrush(amber));
        res["AccentSoft"] = Freeze(new SolidColorBrush(Color.FromArgb(dark ? (byte)0x30 : (byte)0x24, coral.R, coral.G, coral.B)));
        res["AccentGradient"] = Freeze(new LinearGradientBrush(coral, amber, new Point(0, 0), new Point(1, 1)));
        res["AccentGradientSoft"] = Freeze(new LinearGradientBrush(
            Color.FromArgb(dark ? (byte)0x3C : (byte)0x3A, coral.R, coral.G, coral.B),
            Color.FromArgb(dark ? (byte)0x3C : (byte)0x3A, amber.R, amber.G, amber.B), new Point(0, 0), new Point(1, 1)));
        // glass rim: a highlight along the top edge that fades out toward the bottom
        res["Rim"] = Freeze(new LinearGradientBrush(
            Color.FromArgb(dark ? (byte)0x3A : (byte)0x90, 0xFF, 0xFF, 0xFF),
            Color.FromArgb(dark ? (byte)0x10 : (byte)0x30, 0xFF, 0xFF, 0xFF), new Point(0, 0), new Point(0, 1)));
        res["Ambient"] = Freeze(AmbientBrush(dark));

        if (dark)
        {
            Set("Surface", 0xF4, 0x20, 0x1A, 0x1E);      // painted fallback when acrylic is unavailable
            Set("SurfaceTint", 0x74, 0x1A, 0x13, 0x17);  // tint layered over acrylic
            Set("Text", 0xFF, 0xFA, 0xF4, 0xF0);
            Set("Muted", 0xFF, 0xB3, 0xA6, 0x9F);
            Set("Divider", 0x22, 0xFF, 0xEA, 0xDE);
            Set("ButtonBg", 0x1A, 0xFF, 0xEC, 0xE0);
            Set("ButtonHover", 0x2C, 0xFF, 0xEC, 0xE0);
            Set("ButtonPressed", 0x40, 0xFF, 0xEC, 0xE0);
            Set("Error", 0xFF, 0xFF, 0x6E, 0x6E);
            Set("Success", 0xFF, 0x5C, 0xE0, 0x9A);
            Set("Border", 0x30, 0xFF, 0xEA, 0xDE);
            Set("InputBg", 0x14, 0xFF, 0xEC, 0xE0);
        }
        else
        {
            Set("Surface", 0xF6, 0xFD, 0xF8, 0xF4);
            Set("SurfaceTint", 0xB0, 0xFF, 0xFA, 0xF5);
            Set("Text", 0xFF, 0x2B, 0x1F, 0x1A);
            Set("Muted", 0xFF, 0x6B, 0x5E, 0x57);
            Set("Divider", 0x16, 0x4A, 0x2E, 0x1E);
            Set("ButtonBg", 0x10, 0x5A, 0x3A, 0x28);
            Set("ButtonHover", 0x1C, 0x5A, 0x3A, 0x28);
            Set("ButtonPressed", 0x2C, 0x5A, 0x3A, 0x28);
            Set("Error", 0xFF, 0xC8, 0x2F, 0x2F);
            Set("Success", 0xFF, 0x1E, 0x9E, 0x5A);
            Set("Border", 0x22, 0x5A, 0x3A, 0x28);
            Set("InputBg", 0x0C, 0x5A, 0x3A, 0x28);
        }

        Changed?.Invoke();

        static void Set(string key, byte a, byte r, byte g, byte b)
            => Application.Current.Resources[key] = Freeze(new SolidColorBrush(Color.FromArgb(a, r, g, b)));
    }

    /// <summary>Two soft blooms (coral top-right, amber bottom-left) that stretch with the window and sit under its content.</summary>
    private static Brush AmbientBrush(bool dark)
    {
        var coral = Coral; var amber = Amber; var rose = Rose;
        byte a1 = dark ? (byte)0x46 : (byte)0x30, a2 = dark ? (byte)0x30 : (byte)0x22;

        var group = new DrawingGroup();
        group.Children.Add(new GeometryDrawing(Brushes.Transparent, null, new RectangleGeometry(new Rect(0, 0, 100, 100))));
        group.Children.Add(new GeometryDrawing(Radial(rose, coral, a1), null, new EllipseGeometry(new Point(96, 2), 62, 54)));
        group.Children.Add(new GeometryDrawing(Radial(amber, amber, a2), null, new EllipseGeometry(new Point(4, 100), 58, 48)));
        group.ClipGeometry = new RectangleGeometry(new Rect(0, 0, 100, 100));

        return new DrawingBrush(group) { Stretch = Stretch.Fill, Viewbox = new Rect(0, 0, 100, 100), ViewboxUnits = BrushMappingMode.Absolute };

        static RadialGradientBrush Radial(Color c1, Color c2, byte alpha) => new()
        {
            GradientStops =
            {
                new GradientStop(Color.FromArgb(alpha, c1.R, c1.G, c1.B), 0),
                new GradientStop(Color.FromArgb((byte)(alpha / 2), c2.R, c2.G, c2.B), 0.45),
                new GradientStop(Color.FromArgb(0, c2.R, c2.G, c2.B), 1),
            },
        };
    }

    private static Brush Freeze(Brush b) { b.Freeze(); return b; }

    public static Brush Brush(string key) => (Brush)Application.Current.Resources[key];

    public static Color AccentColor => (Color)Application.Current.Resources["AccentColor"];
}
