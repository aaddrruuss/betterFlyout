using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace BetterFlyout.Views;

public static class Icons
{
    private static Geometry G(string s) { var g = Geometry.Parse(s); g.Freeze(); return g; }
    public static readonly Geometry Play = G("M8,5.14 L8,18.86 C8,19.64 8.85,20.12 9.52,19.71 L20.4,12.85 C21.02,12.46 21.02,11.54 20.4,11.15 L9.52,4.29 C8.85,3.88 8,4.36 8,5.14 Z");
    public static readonly Geometry Pause = G("M6.5,4 L9.5,4 C10.05,4 10.5,4.45 10.5,5 L10.5,19 C10.5,19.55 10.05,20 9.5,20 L6.5,20 C5.95,20 5.5,19.55 5.5,19 L5.5,5 C5.5,4.45 5.95,4 6.5,4 Z M14.5,4 L17.5,4 C18.05,4 18.5,4.45 18.5,5 L18.5,19 C18.5,19.55 18.05,20 17.5,20 L14.5,20 C13.95,20 13.5,19.55 13.5,19 L13.5,5 C13.5,4.45 13.95,4 14.5,4 Z");
    public static readonly Geometry Next = G("M4,5.5 L4,18.5 C4,19.3 4.9,19.77 5.56,19.32 L14.6,13.06 C15.17,12.66 15.17,11.84 14.6,11.44 L5.56,4.68 C4.9,4.23 4,4.7 4,5.5 Z M17.5,4 L19,4 C19.55,4 20,4.45 20,5 L20,19 C20,19.55 19.55,20 19,20 L17.5,20 C16.95,20 16.5,19.55 16.5,19 L16.5,5 C16.5,4.45 16.95,4 17.5,4 Z");
    public static readonly Geometry Prev = G("M20,5.5 L20,18.5 C20,19.3 19.1,19.77 18.44,19.32 L9.4,13.06 C8.83,12.66 8.83,11.84 9.4,11.44 L18.44,4.68 C19.1,4.23 20,4.7 20,5.5 Z M6.5,4 L5,4 C4.45,4 4,4.45 4,5 L4,19 C4,19.55 4.45,20 5,20 L6.5,20 C7.05,20 7.5,19.55 7.5,19 L7.5,5 C7.5,4.45 7.05,4 6.5,4 Z");
    public static readonly Geometry Note = G("M12,3 L12,13.55 C11.41,13.21 10.73,13 10,13 C7.79,13 6,14.79 6,17 C6,19.21 7.79,21 10,21 C12.21,21 14,19.21 14,17 L14,7 L18,7 L18,3 Z");
}

/// <summary>Texto de una línea con desplazamiento (marquee) automático cuando no cabe, o recorte con elipsis.</summary>
public sealed class MarqueeText : Border
{
    public readonly TextBlock Tb = new();
    private readonly Canvas _canvas = new();
    private readonly TranslateTransform _tt = new();
    private static readonly Brush FadeMask = MakeMask();

    public bool MarqueeEnabled { get; set; } = true;
    public bool Wrap { get; set; }
    public double Speed { get; set; } = 30;
    public TextAlignment Align { get; set; } = TextAlignment.Left;

    public MarqueeText()
    {
        ClipToBounds = true;
        Tb.RenderTransform = _tt;
        _canvas.Children.Add(Tb);
        Child = _canvas;
        SizeChanged += (_, _) => Restart();
    }

    public string Text
    {
        get => Tb.Text;
        set
        {
            value ??= "";
            if (Tb.Text == value) return;
            Tb.Text = value;
            InvalidateMeasure();
            Dispatcher.BeginInvoke(Restart, DispatcherPriority.Loaded);
        }
    }

    private static Brush MakeMask()
    {
        var b = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 0) };
        b.GradientStops.Add(new GradientStop(Colors.Transparent, 0));
        b.GradientStops.Add(new GradientStop(Colors.Black, 0.012));
        b.GradientStops.Add(new GradientStop(Colors.Black, 0.92));
        b.GradientStops.Add(new GradientStop(Colors.Transparent, 1));
        b.Freeze();
        return b;
    }

    private FormattedText MakeFt() => new(Tb.Text ?? "", CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
        new Typeface(Tb.FontFamily, Tb.FontStyle, Tb.FontWeight, Tb.FontStretch), Math.Max(1, Tb.FontSize), Brushes.Black,
        VisualTreeHelper.GetDpi(this).PixelsPerDip);

    protected override Size MeasureOverride(Size c)
    {
        var ft = MakeFt();
        double natural = ft.WidthIncludingTrailingWhitespace + 1;
        double w = double.IsInfinity(c.Width) ? natural : c.Width;
        if (Wrap && w > 0) ft.MaxTextWidth = w;
        _canvas.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        double h = ft.Height;
        if (string.IsNullOrEmpty(Tb.Text)) h = Tb.FontSize * 1.33;
        return new Size(w, h);
    }

    public void Restart()
    {
        _tt.BeginAnimation(TranslateTransform.XProperty, null);
        _tt.X = 0;
        OpacityMask = null;
        double avail = ActualWidth;
        if (avail <= 0) return;
        double natural = MakeFt().WidthIncludingTrailingWhitespace;

        if (Wrap)
        {
            Tb.TextWrapping = TextWrapping.Wrap; Tb.TextTrimming = TextTrimming.None; Tb.Width = avail; Tb.TextAlignment = Align;
            return;
        }
        Tb.TextWrapping = TextWrapping.NoWrap;
        if (!MarqueeEnabled || natural <= avail + 0.5)
        {
            Tb.Width = avail; Tb.TextTrimming = TextTrimming.CharacterEllipsis; Tb.TextAlignment = Align;
            return;
        }

        Tb.Width = natural + 2; Tb.TextTrimming = TextTrimming.None; Tb.TextAlignment = TextAlignment.Left;
        OpacityMask = FadeMask;
        double overflow = natural - avail + 12;
        double move = overflow / Math.Max(5, Speed);
        const double hold = 1.6;
        var a = new DoubleAnimationUsingKeyFrames { RepeatBehavior = RepeatBehavior.Forever };
        a.KeyFrames.Add(new DiscreteDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        a.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(hold))));
        a.KeyFrames.Add(new EasingDoubleKeyFrame(-overflow, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(hold + move)), new SineEase { EasingMode = EasingMode.EaseInOut }));
        a.KeyFrames.Add(new LinearDoubleKeyFrame(-overflow, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(hold * 2 + move))));
        a.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(hold * 2 + move * 1.35)), new SineEase { EasingMode = EasingMode.EaseInOut }));
        _tt.BeginAnimation(TranslateTransform.XProperty, a);
    }
}

/// <summary>Botón redondo con icono vectorial, hover y efecto de pulsación.</summary>
public sealed class IconButton : Border
{
    private readonly Path _path = new() { Stretch = Stretch.Uniform, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    private readonly ScaleTransform _st = new(1, 1);
    private Brush _normal, _hover;
    public event Action? Click;

    public IconButton(double size, double iconScale, Geometry icon, Brush iconBrush, Brush normal, Brush hover, double radius)
    {
        Width = Height = size;
        CornerRadius = new CornerRadius(Math.Min(radius, size / 2));
        _normal = normal; _hover = hover;
        Background = normal;
        Cursor = Cursors.Hand;
        RenderTransformOrigin = new Point(0.5, 0.5);
        RenderTransform = _st;
        _path.Data = icon;
        _path.Fill = iconBrush;
        _path.Width = _path.Height = Math.Max(4, size * iconScale);
        Child = _path;

        MouseEnter += (_, _) => Background = _hover;
        MouseLeave += (_, _) => { Background = _normal; AnimateScale(1); };
        MouseLeftButtonDown += (_, e) => { AnimateScale(0.88); CaptureMouse(); e.Handled = true; };
        MouseLeftButtonUp += (_, e) =>
        {
            AnimateScale(1);
            bool inside = IsMouseOver;
            ReleaseMouseCapture();
            if (inside) Click?.Invoke();
            e.Handled = true;
        };
    }

    public Geometry Icon { set => _path.Data = value; }
    public Brush IconBrush { set => _path.Fill = value; }
    public void SetBackgrounds(Brush normal, Brush hover) { _normal = normal; _hover = hover; Background = IsMouseOver ? hover : normal; }

    public bool Enabled
    {
        set { IsHitTestVisible = value; Opacity = value ? 1 : 0.35; }
    }

    private void AnimateScale(double to)
    {
        var a = new DoubleAnimation(to, TimeSpan.FromMilliseconds(90)) { EasingFunction = new QuadraticEase() };
        _st.BeginAnimation(ScaleTransform.ScaleXProperty, a);
        _st.BeginAnimation(ScaleTransform.ScaleYProperty, a);
    }
}
