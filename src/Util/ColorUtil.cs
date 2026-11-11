using System;
using System.Windows.Media;

namespace BetterFlyout.Util;

public static class ColorUtil
{
    public static Color Parse(string? s, Color fallback)
    {
        if (string.IsNullOrWhiteSpace(s)) return fallback;
        try { if (ColorConverter.ConvertFromString(s.Trim()) is Color c) return c; } catch { }
        return fallback;
    }

    public static string ToHex(Color c) => $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}";

    public static SolidColorBrush Brush(string? s) => Brush(Parse(s, Colors.Transparent));

    public static SolidColorBrush Brush(Color c)
    {
        var b = new SolidColorBrush(c);
        b.Freeze();
        return b;
    }

    public static Color WithAlpha(Color c, byte a) => Color.FromArgb(a, c.R, c.G, c.B);

    public static Color Mix(Color a, Color b, double t)
    {
        t = Math.Clamp(t, 0, 1);
        return Color.FromArgb(
            (byte)(a.A + (b.A - a.A) * t),
            (byte)(a.R + (b.R - a.R) * t),
            (byte)(a.G + (b.G - a.G) * t),
            (byte)(a.B + (b.B - a.B) * t));
    }

    /// <summary>Luminancia relativa simple (0..1).</summary>
    public static double Luma(Color c) => (0.2126 * c.R + 0.7152 * c.G + 0.0722 * c.B) / 255.0;
}
