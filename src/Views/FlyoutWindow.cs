// BetterFlyout - Copyright (C) 2026 aaddrruuss (https://github.com/aaddrruuss/)
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using BetterFlyout.Interop;
using BetterFlyout.Services;
using BetterFlyout.Settings;
using BetterFlyout.Util;
using MediaState = BetterFlyout.Services.MediaState;

namespace BetterFlyout.Views;

/// <summary>
/// Popup de música. Ventana por capas, sin activación, siempre encima.
/// Toda la UI se genera por código a partir de FlyoutSettings, así cualquier ajuste se aplica en vivo.
/// </summary>
public sealed class FlyoutWindow : Window
{
    private readonly FlyoutSettings S;
    private readonly MediaService _media;
    private readonly Action _onUserAction;
    private readonly IntPtr _hwnd;

    // Contenedor persistente (animado) + escala global
    private readonly Grid _animHost = new();
    private readonly TranslateTransform _tt = new();
    private readonly ScaleTransform _st = new(1, 1);
    private readonly ScaleTransform _globalScale = new(1, 1);
    private double _pad;

    // Elementos dinámicos (se regeneran en Rebuild)
    private readonly List<Border> _bgBorders = new();
    private System.Windows.Controls.Image? _artBgImage;
    private FrameworkElement? _content;
    private Border? _artBox;
    private FrameworkElement? _artPlaceholder;
    private FrameworkElement? _pausedBadge;
    private AnimationClock? _spinClock;
    private TextBlock? _appText;
    private MarqueeText? _title, _artist, _album;
    private IconButton? _prevBtn, _playBtn, _nextBtn;
    private Grid? _progress;
    private Border? _progressFill;
    private TextBlock? _posText, _durText;
    // Volumen
    private Grid? _volBar;
    private Border? _volFill, _volThumb, _volThumbDot;
    private TextBlock? _volText;
    private float _lastVol = -1;
    private bool _lastMute;

    // Estado
    private readonly DispatcherTimer _hideTimer = new();
    private readonly DispatcherTimer _topmostTimer = new();
    private readonly DispatcherTimer _progressTimer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private bool _visible, _hiding, _hover, _preview, _rebuildQueued;
    private int _animToken;
    private string _shownTrackKey = "";
    private static Windows.UI.ViewManagement.UISettings? _uiSettings;

    public bool IsShowing => _visible && !_hiding;
    public bool IsPreview => _preview;

    public FlyoutWindow(FlyoutSettings settings, MediaService media, Action onUserAction)
    {
        S = settings;
        _media = media;
        _onUserAction = onUserAction;

        Title = "BetterFlyout";
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        ShowActivated = false;
        Topmost = true;
        Focusable = false;
        SizeToContent = SizeToContent.WidthAndHeight;
        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = -32000; Top = -32000;
        UseLayoutRounding = true;
        TextOptions.SetTextFormattingMode(this, TextFormattingMode.Ideal);
        TextOptions.SetTextRenderingMode(this, TextRenderingMode.Grayscale);
        RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.HighQuality);

        var tg = new TransformGroup();
        tg.Children.Add(_st);
        tg.Children.Add(_tt);
        _animHost.RenderTransform = tg;
        _animHost.RenderTransformOrigin = new Point(0.5, 0.5);
        _animHost.LayoutTransform = _globalScale;
        Content = _animHost;

        _hideTimer.Tick += (_, _) => { _hideTimer.Stop(); HideFlyout(); };
        _topmostTimer.Tick += (_, _) => EnforceTopmost();
        _progressTimer.Tick += (_, _) => { UpdateProgress(); UpdateVolume(); };

        MouseEnter += (_, _) => { _hover = true; if (S.KeepOpenOnHover) _hideTimer.Stop(); };
        MouseLeave += (_, _) => { _hover = false; if (_visible && !_hiding && !_preview) RestartHideTimer(); };
        MouseRightButtonUp += (_, e) => { if (S.CloseOnRightClick && !_preview) { HideFlyout(); e.Handled = true; } };
        MouseWheel += (_, e) =>
        {
            if (_volBar == null) return;
            var v = AudioService.GetVolume() ?? 0;
            AudioService.SetVolume(v + (e.Delta > 0 ? 0.02f : -0.02f));
            UpdateVolume(); KeepAlive();
        };
        SizeChanged += (_, _) => { if (_visible) PositionWindow(); };

        S.PropertyChanged += OnSettingChanged;

        _hwnd = new WindowInteropHelper(this).EnsureHandle();
        ApplyWindowStyles();
        Rebuild();
    }

    // ═════════════════════════ Win32 ═════════════════════════

    private void ApplyWindowStyles()
    {
        if (_hwnd == IntPtr.Zero) return;
        long ex = Native.GetExStyle(_hwnd);
        ex |= Native.WS_EX_TOOLWINDOW | Native.WS_EX_NOACTIVATE | Native.WS_EX_TOPMOST;
        ex &= ~Native.WS_EX_APPWINDOW;
        if (S.ClickThrough) ex |= Native.WS_EX_TRANSPARENT; else ex &= ~Native.WS_EX_TRANSPARENT;
        Native.SetExStyle(_hwnd, ex);
        Native.SetBlurBehind(_hwnd, S.BlurBehind);
        _topmostTimer.Interval = TimeSpan.FromMilliseconds(Math.Clamp(S.TopmostIntervalMs, 16, 5000));
    }

    private void EnforceTopmost()
    {
        if (_hwnd == IntPtr.Zero) return;
        Native.SetWindowPos(_hwnd, Native.HWND_TOPMOST, 0, 0, 0, 0,
            Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE | Native.SWP_NOOWNERZORDER);
    }

    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        if (_visible) Dispatcher.BeginInvoke(PositionWindow, DispatcherPriority.Loaded);
    }

    private Native.MonitorData PickMonitor()
    {
        var monitors = Native.GetMonitors();
        Native.MonitorData? m = null;
        switch (S.MonitorMode)
        {
            case MonitorMode.Cursor:
                if (Native.GetCursorPos(out var pt)) m = Native.GetMonitorData(Native.MonitorFromPoint(pt, Native.MONITOR_DEFAULTTONEAREST));
                break;
            case MonitorMode.ActiveWindow:
                var fg = Native.GetForegroundWindow();
                if (fg != IntPtr.Zero) m = Native.GetMonitorData(Native.MonitorFromWindow(fg, Native.MONITOR_DEFAULTTONEAREST));
                break;
            case MonitorMode.Specific:
                if (S.MonitorIndex >= 0 && S.MonitorIndex < monitors.Count) m = monitors[S.MonitorIndex];
                break;
        }
        return m ?? monitors.FirstOrDefault(x => x.IsPrimary) ?? monitors.First();
    }

    private void PositionWindow()
    {
        if (_hwnd == IntPtr.Zero) return;
        UpdateLayout();
        var mon = PickMonitor();
        var area = S.UseWorkArea ? mon.WorkArea : mon.Bounds;
        double dpi = mon.DpiScale;

        double cardW = Math.Max(1, ActualWidth - 2 * _pad) * dpi;
        double cardH = Math.Max(1, ActualHeight - 2 * _pad) * dpi;
        double mx = S.MarginX * dpi, my = S.MarginY * dpi, pad = _pad * dpi;

        double x = S.Anchor switch
        {
            Anchor.TopLeft or Anchor.MiddleLeft or Anchor.BottomLeft => area.Left + mx,
            Anchor.TopRight or Anchor.MiddleRight or Anchor.BottomRight => area.Right - cardW - mx,
            _ => area.Left + (area.Width - cardW) / 2 + mx,
        };
        double y = S.Anchor switch
        {
            Anchor.TopLeft or Anchor.TopCenter or Anchor.TopRight => area.Top + my,
            Anchor.BottomLeft or Anchor.BottomCenter or Anchor.BottomRight => area.Bottom - cardH - my,
            _ => area.Top + (area.Height - cardH) / 2 + my,
        };

        Native.SetWindowPos(_hwnd, Native.HWND_TOPMOST, (int)Math.Round(x - pad), (int)Math.Round(y - pad), 0, 0,
            Native.SWP_NOSIZE | Native.SWP_NOACTIVATE | Native.SWP_NOOWNERZORDER);
    }

    // ═════════════════════════ Mostrar / ocultar ═════════════════════════

    public void ShowFlyout(int? durationMs = null)
    {
        if (_rebuildQueued) Rebuild();
        _hideTimer.Interval = TimeSpan.FromMilliseconds(Math.Max(500, durationMs ?? S.DisplayDurationMs));

        bool trackChangedWhileVisible = IsShowing && _shownTrackKey != CurrentState.TrackKey;
        UpdateMedia();
        UpdateVolume();

        if (!_visible || _hiding)
        {
            bool wasHiding = _hiding;
            _visible = true; _hiding = false;
            if (!wasHiding)
            {
                Show();
                ApplyWindowStyles();
            }
            PositionWindow();
            EnforceTopmost();
            AnimateIn(fromCurrent: wasHiding);
            if (S.ForceTopmost) _topmostTimer.Start();
            _progressTimer.Start();
            UpdateMedia();
        }
        else
        {
            EnforceTopmost();
            if (trackChangedWhileVisible && S.CrossfadeOnTrackChange) Crossfade();
        }

        _hideTimer.Stop();
        if (!_preview && !(_hover && S.KeepOpenOnHover)) _hideTimer.Start();
    }

    public void HideFlyout()
    {
        if (!_visible || _hiding || _preview) return;
        _hideTimer.Stop();
        _hiding = true;
        AnimateOut(() =>
        {
            if (!_hiding) return;
            _hiding = false; _visible = false;
            _topmostTimer.Stop(); _progressTimer.Stop();
            _spinClock?.Controller?.Pause();
            Hide();
        });
    }

    public void SetPreview(bool on)
    {
        if (_preview == on) return;
        _preview = on;
        if (on) ShowFlyout();
        else { UpdateMedia(); HideFlyout(); }
    }

    private void RestartHideTimer() { _hideTimer.Stop(); _hideTimer.Start(); }
    private void KeepAlive() { if (_visible && !_preview && !(_hover && S.KeepOpenOnHover)) RestartHideTimer(); }

    /// <summary>Renderiza el popup (con datos de ejemplo si no hay música) a PNG. Útil para probar estilos.</summary>
    public void RenderSnapshot(string path)
    {
        _preview = true;
        Rebuild();
        _animHost.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        _animHost.Arrange(new Rect(_animHost.DesiredSize));
        _animHost.UpdateLayout();
        UpdateMedia();
        _animHost.UpdateLayout();
        var size = _animHost.DesiredSize;
        double w = size.Width, h = size.Height;
        var host = new Border { Background = new SolidColorBrush(Color.FromRgb(0x3A, 0x6E, 0xA5)), Width = w, Height = h };
        Content = null;
        host.Child = _animHost;
        host.Measure(new Size(w, h)); host.Arrange(new Rect(0, 0, w, h)); host.UpdateLayout();
        foreach (var m in new[] { _title, _artist, _album }) m?.Restart();
        host.UpdateLayout();
        var rtb = new RenderTargetBitmap((int)Math.Ceiling(w * 2), (int)Math.Ceiling(h * 2), 192, 192, PixelFormats.Pbgra32);
        rtb.Render(host);
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(rtb));
        using var fs = File.Create(path);
        enc.Save(fs);
    }

    // ═════════════════════════ Animaciones ═════════════════════════

    private static IEasingFunction EaseFor(AnimationKind k, bool inDir) => k switch
    {
        AnimationKind.Pop => inDir ? new BackEase { Amplitude = 0.45, EasingMode = EasingMode.EaseOut } : new BackEase { Amplitude = 0.3, EasingMode = EasingMode.EaseIn },
        _ => inDir ? new CubicEase { EasingMode = EasingMode.EaseOut } : new CubicEase { EasingMode = EasingMode.EaseIn },
    };

    /// <summary>Valores "fuera" de una animación: (opacidad, dx, dy, escala).</summary>
    private (double op, double dx, double dy, double sc) OffState(AnimationKind k, bool inDir)
    {
        double d = S.SlideDistance;
        return k switch
        {
            AnimationKind.None => (1, 0, 0, 1),
            AnimationKind.Fade => (0, 0, 0, 1),
            AnimationKind.SlideUp => (0, 0, inDir ? d : -d, 1),
            AnimationKind.SlideDown => (0, 0, inDir ? -d : d, 1),
            AnimationKind.SlideLeft => (0, inDir ? d : -d, 0, 1),
            AnimationKind.SlideRight => (0, inDir ? -d : d, 0, 1),
            AnimationKind.Zoom => (1, 0, 0, 0.85),
            AnimationKind.ZoomFade => (0, 0, 0, 0.88),
            AnimationKind.Pop => (0, 0, 0, 0.6),
            _ => (0, 0, 0, 1),
        };
    }

    private void AnimateIn(bool fromCurrent)
    {
        int token = ++_animToken;
        var k = S.AnimationIn;
        if (k == AnimationKind.None || S.AnimationDurationMs <= 0) { SetAnimValues(1, 0, 0, 1); return; }
        var off = OffState(k, true);
        if (!fromCurrent) SetAnimValues(off.op, off.dx, off.dy, off.sc);
        Animate(1, 0, 0, 1, TimeSpan.FromMilliseconds(S.AnimationDurationMs), EaseFor(k, true), null, token);
    }

    private void AnimateOut(Action done)
    {
        int token = ++_animToken;
        var k = S.AnimationOut;
        if (k == AnimationKind.None || S.AnimationDurationMs <= 0) { done(); return; }
        var off = OffState(k, false);
        Animate(off.op, off.dx, off.dy, off.sc, TimeSpan.FromMilliseconds(Math.Max(1, S.AnimationDurationMs * 0.8)), EaseFor(k, false), done, token);
    }

    private void SetAnimValues(double op, double dx, double dy, double sc)
    {
        _animHost.BeginAnimation(OpacityProperty, null);
        _tt.BeginAnimation(TranslateTransform.XProperty, null);
        _tt.BeginAnimation(TranslateTransform.YProperty, null);
        _st.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        _st.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        _animHost.Opacity = op; _tt.X = dx; _tt.Y = dy; _st.ScaleX = _st.ScaleY = sc;
    }

    private void Animate(double op, double dx, double dy, double sc, TimeSpan dur, IEasingFunction ease, Action? done, int token)
    {
        DoubleAnimation A(double to) => new(to, dur) { EasingFunction = ease };
        var opA = new DoubleAnimation(op, dur) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } };
        if (done != null) opA.Completed += (_, _) => { if (token == _animToken) done(); };
        _animHost.BeginAnimation(OpacityProperty, opA);
        _tt.BeginAnimation(TranslateTransform.XProperty, A(dx));
        _tt.BeginAnimation(TranslateTransform.YProperty, A(dy));
        _st.BeginAnimation(ScaleTransform.ScaleXProperty, A(sc));
        _st.BeginAnimation(ScaleTransform.ScaleYProperty, A(sc));
    }

    private void Crossfade()
    {
        _content?.BeginAnimation(OpacityProperty,
            new DoubleAnimation(0.15, 1, TimeSpan.FromMilliseconds(320)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
    }

    // ═════════════════════════ Construcción de la UI ═════════════════════════

    private void OnSettingChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(FlyoutSettings.ClickThrough) or nameof(FlyoutSettings.TopmostIntervalMs) or nameof(FlyoutSettings.BlurBehind))
            ApplyWindowStyles();
        if (e.PropertyName == nameof(FlyoutSettings.ForceTopmost))
        {
            if (S.ForceTopmost && _visible) _topmostTimer.Start(); else _topmostTimer.Stop();
        }
        if (_rebuildQueued) return;
        _rebuildQueued = true;
        Dispatcher.BeginInvoke(() => { if (_rebuildQueued) { Rebuild(); if (_visible) PositionWindow(); } }, DispatcherPriority.Background);
    }

    private MediaState CurrentState => _preview && !_media.State.HasSession ? MediaState.Sample() : _media.State;

    private static FontWeight W(WeightKind k) => k switch
    {
        WeightKind.Light => FontWeights.Light,
        WeightKind.SemiBold => FontWeights.SemiBold,
        WeightKind.Bold => FontWeights.Bold,
        _ => FontWeights.Normal,
    };
    private TextAlignment TA => S.TextAlignment switch { TextAlign.Center => TextAlignment.Center, TextAlign.Right => TextAlignment.Right, _ => TextAlignment.Left };
    private HorizontalAlignment HA => S.TextAlignment switch { TextAlign.Center => HorizontalAlignment.Center, TextAlign.Right => HorizontalAlignment.Right, _ => HorizontalAlignment.Left };

    private void Rebuild()
    {
        _rebuildQueued = false;
        var s = S;
        _spinClock?.Controller?.Stop();
        _spinClock = null;
        _bgBorders.Clear();
        _artBox = null; _artPlaceholder = null; _pausedBadge = null; _appText = null; _title = _artist = _album = null;
        _prevBtn = _playBtn = _nextBtn = null; _progress = null; _progressFill = null; _posText = _durText = null; _artBgImage = null;
        _volBar = null; _volFill = _volThumb = _volThumbDot = null; _volText = null; _lastVol = -1;

        double shadowExtent = s.Shadow ? s.ShadowBlur + Math.Abs(s.ShadowDepth) : 0;
        _pad = s.BlurBehind ? 0 : Math.Ceiling(Math.Max(shadowExtent, s.SlideDistance) + 8);
        _animHost.Margin = new Thickness(_pad);
        _globalScale.ScaleX = _globalScale.ScaleY = Math.Clamp(s.Scale, 0.3, 4);
        _animHost.SetValue(TextElement.FontFamilyProperty, new FontFamily(string.IsNullOrWhiteSpace(s.FontFamily) ? "Segoe UI" : s.FontFamily));

        // Tarjeta principal
        var content = s.Layout switch
        {
            FlyoutLayout.Horizontal => BuildHorizontal(),
            FlyoutLayout.Vertical => BuildVertical(),
            FlyoutLayout.Compact => BuildCompact(),
            FlyoutLayout.ArtOnly => BuildArtOnly(),
            FlyoutLayout.TextOnly => BuildTextOnly(),
            _ => BuildClassic(),
        };
        content.Margin = new Thickness(Math.Max(0, s.Padding));
        _content = content;
        var main = MakeCard(content, Math.Max(60, s.Width), withArtBackground: s.ArtBackground && s.Layout != FlyoutLayout.ArtOnly, withLineProgress: true);

        var row = new StackPanel { Orientation = Orientation.Horizontal, Opacity = Math.Clamp(s.Opacity, 0.05, 1) };
        if (s.ShowVolumePanel)
        {
            var vol = MakeCard(BuildVolumePanel(), Math.Max(24, s.VolumePanelWidth), withArtBackground: false, withLineProgress: false);
            if (s.VolumePanelOnRight) { vol.Margin = new Thickness(s.CardGap, 0, 0, 0); row.Children.Add(main); row.Children.Add(vol); }
            else { vol.Margin = new Thickness(0, 0, s.CardGap, 0); row.Children.Add(vol); row.Children.Add(main); }
        }
        else row.Children.Add(main);
        main.MinHeight = Math.Max(0, s.MinHeight);

        if (s.TextShadow)
        {
            foreach (var mt in new[] { _title, _artist, _album }) if (mt != null) mt.Effect = TextShadowFx();
            if (_appText != null) _appText.Effect = TextShadowFx();
        }

        _animHost.Children.Clear();
        _animHost.Children.Add(row);
        UpdateMedia();
        UpdateVolume();
    }

    /// <summary>Tarjeta: fondo+sombra, capa recortada (carátula difuminada / línea de progreso), contenido y borde.</summary>
    private Grid MakeCard(FrameworkElement content, double width, bool withArtBackground, bool withLineProgress)
    {
        var s = S;
        double radius = Math.Max(0, s.CornerRadius);
        var card = new Grid { Width = width };

        var bg = new Border { CornerRadius = new CornerRadius(radius) };
        if (s.Shadow && !s.BlurBehind)
        {
            bg.Effect = new DropShadowEffect
            {
                Color = ColorUtil.Parse(s.ShadowColor, Colors.Black), BlurRadius = s.ShadowBlur, ShadowDepth = s.ShadowDepth,
                Direction = 270, Opacity = Math.Clamp(s.ShadowOpacity, 0, 1), RenderingBias = RenderingBias.Quality,
            };
        }
        _bgBorders.Add(bg);
        card.Children.Add(bg);

        var clipLayer = new Grid { IsHitTestVisible = false };
        clipLayer.SizeChanged += (_, e) =>
        {
            var g = new RectangleGeometry(new Rect(e.NewSize), radius, radius);
            g.Freeze();
            clipLayer.Clip = g;
        };
        if (withArtBackground)
        {
            _artBgImage = new System.Windows.Controls.Image
            {
                Stretch = Stretch.UniformToFill, Opacity = Math.Clamp(s.ArtBackgroundOpacity, 0, 1),
                RenderTransformOrigin = new Point(0.5, 0.5), RenderTransform = new ScaleTransform(1.35, 1.35),
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
            };
            if (s.ArtBackgroundBlur > 0) _artBgImage.Effect = new BlurEffect { Radius = s.ArtBackgroundBlur, RenderingBias = RenderingBias.Performance };
            clipLayer.Children.Add(_artBgImage);
        }
        if (withLineProgress && s.ProgressStyle == ProgressStyle.Line)
        {
            var line = BuildProgress(Math.Max(1, s.ProgressHeight), rounded: false);
            line.VerticalAlignment = VerticalAlignment.Bottom;
            clipLayer.Children.Add(line);
        }
        card.Children.Add(clipLayer);
        card.Children.Add(content);

        if (s.BorderThickness > 0)
        {
            card.Children.Add(new Border
            {
                CornerRadius = new CornerRadius(radius), BorderThickness = new Thickness(s.BorderThickness),
                BorderBrush = ColorUtil.Brush(s.BorderColor), IsHitTestVisible = false,
            });
        }
        return card;
    }

    private static DropShadowEffect TextShadowFx() => new() { BlurRadius = 6, ShadowDepth = 1, Opacity = 0.65, Color = Colors.Black };

    // ───── Piezas ─────

    private FrameworkElement BuildArt(double size, bool overlayControls = false)
    {
        var s = S;
        double r = Math.Min(s.ArtCornerRadius, size / 2);
        var host = new Grid { Width = size, Height = size, VerticalAlignment = VerticalAlignment.Center };

        if (s.ArtShadow)
        {
            host.Children.Add(new Border
            {
                CornerRadius = new CornerRadius(r), Background = Brushes.Black, Opacity = 0.9,
                Effect = new DropShadowEffect { BlurRadius = 14, ShadowDepth = 3, Direction = 270, Opacity = 0.45 },
            });
        }

        var rot = new RotateTransform(0);
        var box = new Border { CornerRadius = new CornerRadius(r), RenderTransformOrigin = new Point(0.5, 0.5), RenderTransform = rot };
        _artBox = box;
        host.Children.Add(box);

        var ph = new Grid { IsHitTestVisible = false, RenderTransformOrigin = new Point(0.5, 0.5), RenderTransform = rot };
        ph.Children.Add(new Border { CornerRadius = new CornerRadius(r), Background = new LinearGradientBrush(Color.FromRgb(0x3A, 0x3A, 0x48), Color.FromRgb(0x1E, 0x1E, 0x26), 45) });
        ph.Children.Add(new System.Windows.Shapes.Path { Data = Icons.Note, Fill = new SolidColorBrush(Color.FromArgb(0xB0, 0xFF, 0xFF, 0xFF)), Stretch = Stretch.Uniform, Width = size * 0.4, Height = size * 0.4 });
        _artPlaceholder = ph;
        host.Children.Add(ph);

        if (s.ArtSpinWhenPlaying)
        {
            var spin = new DoubleAnimation(0, 360, TimeSpan.FromSeconds(10)) { RepeatBehavior = RepeatBehavior.Forever };
            _spinClock = spin.CreateClock();
            rot.ApplyAnimationClock(RotateTransform.AngleProperty, _spinClock);
        }

        if (s.ShowPausedBadge && size >= 28)
        {
            double b = Math.Max(18, size * 0.38);
            _pausedBadge = new Border
            {
                Width = b, Height = b, CornerRadius = new CornerRadius(b / 2), Background = new SolidColorBrush(Color.FromArgb(0xA0, 0, 0, 0)),
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, IsHitTestVisible = false,
                Child = new System.Windows.Shapes.Path { Data = Icons.Pause, Fill = Brushes.White, Stretch = Stretch.Uniform, Width = b * 0.42, Height = b * 0.42 },
            };
            host.Children.Add(_pausedBadge);
        }

        if (overlayControls && s.ShowControls)
        {
            var ov = new Border
            {
                CornerRadius = new CornerRadius(r), Background = new SolidColorBrush(Color.FromArgb(0x88, 0, 0, 0)), Opacity = 0,
                Child = BuildControls(HorizontalAlignment.Center),
            };
            host.MouseEnter += (_, _) => ov.BeginAnimation(OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(150)));
            host.MouseLeave += (_, _) => ov.BeginAnimation(OpacityProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(200)));
            host.Children.Add(ov);
        }
        return host;
    }

    private MarqueeText MakeText(double size, string color, WeightKind weight, bool wrap = false)
    {
        var t = new MarqueeText { MarqueeEnabled = S.Marquee, Speed = S.MarqueeSpeed, Align = TA, Wrap = wrap };
        t.Tb.FontSize = Math.Max(1, size);
        t.Tb.FontWeight = W(weight);
        t.Tb.Foreground = ColorUtil.Brush(color);
        return t;
    }

    private TextBlock MakeAppText(TextAlignment align)
    {
        var s = S;
        _appText = new TextBlock
        {
            FontSize = s.AppNameFontSize, Foreground = ColorUtil.Brush(s.AppNameColor), TextAlignment = align,
            FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis,
        };
        return _appText;
    }

    private StackPanel BuildTextStack(bool includeApp = true)
    {
        var s = S;
        var sp = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        if (includeApp && s.ShowAppName)
        {
            var app = MakeAppText(TA);
            app.Margin = new Thickness(0, 0, 0, 2);
            sp.Children.Add(app);
        }
        _title = MakeText(s.TitleFontSize, s.TitleColor, s.TitleWeight, s.TitleWrap);
        sp.Children.Add(_title);
        if (s.ShowArtist)
        {
            _artist = MakeText(s.ArtistFontSize, s.ArtistColor, s.ArtistWeight);
            _artist.Margin = new Thickness(0, 1, 0, 0);
            sp.Children.Add(_artist);
        }
        if (s.ShowAlbum)
        {
            _album = MakeText(s.AlbumFontSize, s.AlbumColor, WeightKind.Normal);
            _album.Margin = new Thickness(0, 1, 0, 0);
            sp.Children.Add(_album);
        }
        return sp;
    }

    private IconButton MakeButton(double size, double iconScale, Geometry icon, bool primary)
    {
        var s = S;
        var iconColor = ColorUtil.Parse(s.ControlIconColor, Colors.White);
        var icon_ = ColorUtil.Brush(iconColor);
        var bg = ColorUtil.Brush(s.ControlBackground);
        var hov = ColorUtil.Brush(s.ControlHoverBackground);
        var b = new IconButton(size, iconScale, icon, icon_, s.ControlStyle == ControlStyle.IconOnly ? Brushes.Transparent : bg, hov, s.ControlCornerRadius);
        if (s.ControlStyle == ControlStyle.Outline)
        {
            // Como Windows 10: anillo fino; play más marcado que anterior/siguiente.
            b.BorderThickness = new Thickness(Math.Max(0.5, s.ControlStrokeThickness) * (primary ? 1.25 : 1));
            b.BorderBrush = ColorUtil.Brush(primary ? iconColor : ColorUtil.WithAlpha(iconColor, (byte)(iconColor.A * 0.6)));
        }
        return b;
    }

    private StackPanel BuildControls(HorizontalAlignment align)
    {
        var s = S;
        var sp = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = align, VerticalAlignment = VerticalAlignment.Center };
        double size = Math.Max(12, s.ControlSize);
        bool outline = s.ControlStyle == ControlStyle.Outline;
        _prevBtn = MakeButton(size, s.ControlIconScale * 0.9, Icons.Prev, false);
        _playBtn = MakeButton(outline ? size : size * 1.12, s.ControlIconScale, Icons.Pause, true);
        _nextBtn = MakeButton(size, s.ControlIconScale * 0.9, Icons.Next, false);
        _playBtn.Margin = new Thickness(s.ControlSpacing, 0, s.ControlSpacing, 0);
        _prevBtn.Click += () => { _onUserAction(); _media.Previous(); KeepAlive(); };
        _playBtn.Click += () => { _onUserAction(); _media.TogglePlayPause(); KeepAlive(); };
        _nextBtn.Click += () => { _onUserAction(); _media.Next(); KeepAlive(); };
        sp.Children.Add(_prevBtn); sp.Children.Add(_playBtn); sp.Children.Add(_nextBtn);
        return sp;
    }

    private Grid BuildProgress(double height, bool rounded)
    {
        var g = new Grid { Height = height, IsHitTestVisible = false };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var cr = new CornerRadius(rounded ? height / 2 : 0);
        var track = new Border { CornerRadius = cr, Background = ColorUtil.Brush(S.ProgressTrackColor) };
        Grid.SetColumnSpan(track, 2);
        g.Children.Add(track);
        _progressFill = new Border { CornerRadius = cr };
        g.Children.Add(_progressFill);
        _progress = g;
        return g;
    }

    /// <summary>Barra de progreso + fila [tiempo] [controles] [tiempo].</summary>
    private FrameworkElement BuildTransport(bool includeControls = true)
    {
        var s = S;
        var sp = new StackPanel();
        if (s.ProgressStyle == ProgressStyle.Bar)
        {
            var bar = BuildProgress(Math.Max(1, s.ProgressHeight), rounded: true);
            bar.Margin = new Thickness(0, s.Spacing * 0.6, 0, 0);
            sp.Children.Add(bar);
        }

        bool times = s.ShowTimes && s.ProgressStyle != ProgressStyle.None;
        bool controls = includeControls && s.ShowControls;
        if (!times && !controls) return sp;

        var row = new Grid { Margin = new Thickness(0, times || s.ProgressStyle == ProgressStyle.Bar ? 4 : s.Spacing * 0.5, 0, 0) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        if (times)
        {
            var va = controls ? VerticalAlignment.Top : VerticalAlignment.Center;
            _posText = new TextBlock { FontSize = s.TimeFontSize, Foreground = ColorUtil.Brush(s.TimeColor), VerticalAlignment = va };
            _durText = new TextBlock { FontSize = s.TimeFontSize, Foreground = ColorUtil.Brush(s.TimeColor), VerticalAlignment = va };
            Grid.SetColumn(_durText, 2);
            row.Children.Add(_posText);
            row.Children.Add(_durText);
        }
        if (controls)
        {
            var ctr = BuildControls(times ? HorizontalAlignment.Center : HA);
            if (times) Grid.SetColumn(ctr, 1); else Grid.SetColumnSpan(ctr, 3);
            row.Children.Add(ctr);
        }
        sp.Children.Add(row);
        return sp;
    }

    /// <summary>Diseño del flyout de Windows 10: controles arriba, título grande, artista; carátula + app a la derecha.</summary>
    private FrameworkElement BuildClassic()
    {
        var s = S;
        var g = new Grid();
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var left = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        if (s.ShowControls)
        {
            var ctr = BuildControls(HA);
            ctr.Margin = new Thickness(0, 0, 0, s.Spacing * 0.7);
            left.Children.Add(ctr);
        }
        left.Children.Add(BuildTextStack(includeApp: false));
        left.Children.Add(BuildTransport(includeControls: false));
        g.Children.Add(left);

        if (s.ShowArt || s.ShowAppName)
        {
            var right = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(s.Spacing, 0, 0, 0) };
            if (s.ShowArt) right.Children.Add(BuildArt(s.ArtSize));
            if (s.ShowAppName)
            {
                var app = MakeAppText(TextAlignment.Center);
                app.Margin = new Thickness(0, s.ShowArt ? 6 : 0, 0, 0);
                app.MaxWidth = Math.Max(s.ArtSize, 60);
                right.Children.Add(app);
            }
            Grid.SetColumn(right, 1);
            g.Children.Add(right);
        }
        return g;
    }

    private FrameworkElement BuildHorizontal()
    {
        var s = S;
        var g = new Grid();
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        if (s.ShowArt)
        {
            var art = BuildArt(s.ArtSize);
            art.Margin = new Thickness(0, 0, s.Spacing, 0);
            g.Children.Add(art);
        }
        var info = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        info.Children.Add(BuildTextStack());
        info.Children.Add(BuildTransport());
        Grid.SetColumn(info, 1);
        g.Children.Add(info);
        return g;
    }

    private FrameworkElement BuildVertical()
    {
        var s = S;
        var sp = new StackPanel();
        if (s.ShowArt)
        {
            var art = BuildArt(Math.Min(s.ArtSize, Math.Max(20, s.Width - 2 * s.Padding)));
            art.HorizontalAlignment = HorizontalAlignment.Center;
            art.Margin = new Thickness(0, 0, 0, s.Spacing);
            sp.Children.Add(art);
        }
        sp.Children.Add(BuildTextStack());
        sp.Children.Add(BuildTransport());
        return sp;
    }

    private FrameworkElement BuildCompact()
    {
        var s = S;
        var g = new Grid();
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        if (s.ShowArt)
        {
            var art = BuildArt(s.ArtSize);
            art.Margin = new Thickness(0, 0, s.Spacing, 0);
            g.Children.Add(art);
        }
        var text = BuildTextStack(includeApp: false);
        Grid.SetColumn(text, 1);
        g.Children.Add(text);
        if (s.ShowControls)
        {
            var ctr = BuildControls(HorizontalAlignment.Right);
            ctr.Margin = new Thickness(s.Spacing * 0.5, 0, 0, 0);
            Grid.SetColumn(ctr, 2);
            g.Children.Add(ctr);
        }
        if (s.ProgressStyle == ProgressStyle.Bar)
        {
            var bar = BuildProgress(Math.Max(1, s.ProgressHeight), rounded: true);
            bar.Margin = new Thickness(0, s.Spacing * 0.6, 0, 0);
            Grid.SetRow(bar, 1); Grid.SetColumnSpan(bar, 3);
            g.Children.Add(bar);
        }
        return g;
    }

    private FrameworkElement BuildArtOnly()
    {
        var s = S;
        var sp = new StackPanel();
        var art = BuildArt(Math.Min(s.ArtSize, Math.Max(20, s.Width - 2 * s.Padding)), overlayControls: true);
        art.HorizontalAlignment = HorizontalAlignment.Center;
        sp.Children.Add(art);
        if (s.ProgressStyle == ProgressStyle.Bar)
        {
            var bar = BuildProgress(Math.Max(1, s.ProgressHeight), rounded: true);
            bar.Margin = new Thickness(0, s.Spacing * 0.6, 0, 0);
            sp.Children.Add(bar);
        }
        return sp;
    }

    private FrameworkElement BuildTextOnly()
    {
        var sp = new StackPanel();
        sp.Children.Add(BuildTextStack());
        sp.Children.Add(BuildTransport());
        return sp;
    }

    /// <summary>Panel de volumen vertical (como el de Windows 10), arrastrable y con rueda del ratón.</summary>
    private FrameworkElement BuildVolumePanel()
    {
        var s = S;
        var g = new Grid { Margin = new Thickness(0, Math.Max(10, s.Padding), 0, Math.Max(8, s.Padding * 0.6)) };
        g.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star), MinHeight = 40 });
        g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        double bw = Math.Max(2, s.VolumeBarWidth);
        double thumb = Math.Max(14, bw + 6);
        var bar = new Grid { Width = Math.Max(28, thumb + 8), Background = Brushes.Transparent, Cursor = Cursors.Hand, Margin = new Thickness(0, thumb / 2, 0, thumb / 2) };
        var track = new Border { Width = bw, CornerRadius = new CornerRadius(bw / 2), Background = ColorUtil.Brush(TrackColorForVolume()) };
        _volFill = new Border { Width = bw, CornerRadius = new CornerRadius(bw / 2), RenderTransformOrigin = new Point(0.5, 1), RenderTransform = new ScaleTransform(1, 0) };
        _volThumbDot = new Border { Width = thumb * 0.55, Height = thumb * 0.55, CornerRadius = new CornerRadius(thumb), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        _volThumb = new Border
        {
            Width = thumb, Height = thumb, CornerRadius = new CornerRadius(thumb / 2), VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 0, -thumb / 2), RenderTransform = new TranslateTransform(),
            Background = ColorUtil.Brush(ThumbRingColor()), BorderBrush = ColorUtil.Brush(Color.FromArgb(0x24, 0xFF, 0xFF, 0xFF)),
            BorderThickness = new Thickness(1), Child = _volThumbDot, IsHitTestVisible = false,
        };
        bar.Children.Add(track);
        bar.Children.Add(_volFill);
        bar.Children.Add(_volThumb);
        bar.SizeChanged += (_, _) => { _lastVol = -1; UpdateVolume(); };

        void SetFromMouse(MouseEventArgs e)
        {
            double h = bar.ActualHeight;
            if (h <= 0) return;
            double y = e.GetPosition(bar).Y;
            AudioService.SetVolume((float)Math.Clamp(1 - y / h, 0, 1));
            UpdateVolume(); KeepAlive();
        }
        bar.MouseLeftButtonDown += (_, e) => { bar.CaptureMouse(); SetFromMouse(e); e.Handled = true; };
        bar.MouseMove += (_, e) => { if (bar.IsMouseCaptured) SetFromMouse(e); };
        bar.MouseLeftButtonUp += (_, e) => { bar.ReleaseMouseCapture(); e.Handled = true; };
        _volBar = bar;
        g.Children.Add(bar);

        _volText = new TextBlock
        {
            FontSize = s.VolumeFontSize, Foreground = ColorUtil.Brush(s.VolumeTextColor), HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 6, 0, 0), Cursor = Cursors.Hand, Background = Brushes.Transparent,
        };
        _volText.MouseLeftButtonUp += (_, _) => { AudioService.ToggleMute(); UpdateVolume(); KeepAlive(); };
        Grid.SetRow(_volText, 1);
        g.Children.Add(_volText);
        return g;
    }

    private bool BackgroundIsLight() => ColorUtil.Luma(ColorUtil.Parse(S.BackgroundColor, Colors.Black)) > 0.6;
    private Color TrackColorForVolume() => BackgroundIsLight() ? Color.FromArgb(0x50, 0, 0, 0) : Color.FromArgb(0x60, 0xFF, 0xFF, 0xFF);
    private Color ThumbRingColor()
    {
        var bg = ColorUtil.Parse(S.BackgroundColor, Colors.Black);
        return BackgroundIsLight() ? Colors.White : ColorUtil.Mix(Color.FromRgb(bg.R, bg.G, bg.B), Colors.White, 0.12);
    }

    private void UpdateVolume()
    {
        if (_volBar == null || _volFill == null || _volThumb == null || _volText == null) return;
        float v = _preview && !_media.State.HasSession ? (AudioService.GetVolume() ?? 0.36f) : (AudioService.GetVolume() ?? 0);
        bool muted = AudioService.IsMuted();
        if (Math.Abs(v - _lastVol) < 0.001 && muted == _lastMute) return;
        _lastVol = v; _lastMute = muted;

        double h = _volBar.ActualHeight;
        // Solo transformaciones de render: no alteran el layout (evita bucles de tamaño).
        ((ScaleTransform)_volFill.RenderTransform).ScaleY = Math.Clamp(v, 0, 1);
        ((TranslateTransform)_volThumb.RenderTransform).Y = -Math.Clamp(h * v, 0, h);
        _volFill.Opacity = muted ? 0.35 : 1;

        if (muted)
        {
            _volText.FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets");
            _volText.Text = "";
        }
        else
        {
            _volText.ClearValue(TextBlock.FontFamilyProperty);
            _volText.Text = ((int)Math.Round(v * 100)).ToString();
        }
    }

    // ═════════════════════════ Datos dinámicos ═════════════════════════

    private Color SystemAccent()
    {
        try
        {
            _uiSettings ??= new Windows.UI.ViewManagement.UISettings();
            var type = BackgroundIsLight() ? Windows.UI.ViewManagement.UIColorType.AccentDark1 : Windows.UI.ViewManagement.UIColorType.AccentLight2;
            var c = _uiSettings.GetColorValue(type);
            return Color.FromArgb(c.A, c.R, c.G, c.B);
        }
        catch { return Color.FromRgb(0x60, 0xCD, 0xFF); }
    }

    private Color Accent(MediaState st)
    {
        if (S.UseArtAccent && st.ArtColor is { } c) return c;
        if (S.UseSystemAccent) return SystemAccent();
        return ColorUtil.Parse(S.AccentColor, Color.FromRgb(0x60, 0xCD, 0xFF));
    }

    public void UpdateMedia()
    {
        if (_content == null) return;
        var s = S;
        var st = CurrentState;
        _shownTrackKey = st.TrackKey;
        var accent = Accent(st);

        // Fondo (sólido / degradado / teñido con la carátula) para todas las tarjetas
        var c1 = ColorUtil.Parse(s.BackgroundColor, Color.FromArgb(0xF5, 0x1F, 0x1F, 0x1F));
        var c2 = ColorUtil.Parse(s.BackgroundColor2, c1);
        if (s.TintWithArtColor && st.ArtColor is { } ac)
        {
            c1 = ColorUtil.Mix(c1, Color.FromArgb(c1.A, ac.R, ac.G, ac.B), s.ArtTintStrength);
            c2 = ColorUtil.Mix(c2, Color.FromArgb(c2.A, ac.R, ac.G, ac.B), s.ArtTintStrength);
        }
        Brush bgBrush;
        if (s.BackgroundGradient) { var lg = new LinearGradientBrush(c1, c2, s.GradientAngle); lg.Freeze(); bgBrush = lg; }
        else bgBrush = ColorUtil.Brush(c1);
        foreach (var b in _bgBorders) b.Background = bgBrush;

        // Carátula
        if (_artBox != null)
        {
            if (st.Art != null)
            {
                var ib = new ImageBrush(st.Art) { Stretch = Stretch.UniformToFill };
                ib.Freeze();
                _artBox.Background = ib;
                if (_artPlaceholder != null) _artPlaceholder.Visibility = Visibility.Collapsed;
            }
            else
            {
                _artBox.Background = null;
                if (_artPlaceholder != null) _artPlaceholder.Visibility = Visibility.Visible;
            }
            _artBox.Opacity = s.DimArtWhenPaused && !st.IsPlaying ? 0.55 : 1;
            if (_artPlaceholder != null) _artPlaceholder.Opacity = _artBox.Opacity;
        }
        if (_pausedBadge != null) _pausedBadge.Visibility = st.IsPlaying ? Visibility.Collapsed : Visibility.Visible;
        if (_spinClock?.Controller != null)
        {
            if (st.IsPlaying && _visible) _spinClock.Controller.Resume(); else _spinClock.Controller.Pause();
        }
        if (_artBgImage != null) _artBgImage.Source = st.Art;

        // Texto
        if (_appText != null)
        {
            string app = string.IsNullOrEmpty(st.AppName) ? "" : st.AppName;
            if (s.Layout != FlyoutLayout.Classic && string.IsNullOrEmpty(app)) app = "Reproduciendo";
            if (!st.IsPlaying && s.ShowPausedBadge && s.Layout != FlyoutLayout.Classic) app += " · En pausa";
            _appText.Text = s.AppNameUppercase ? app.ToUpperInvariant() : app;
        }
        if (_title != null) _title.Text = string.IsNullOrEmpty(st.Title) ? "Nada en reproducción" : st.Title;
        if (_artist != null) _artist.Text = st.Artist;
        if (_album != null) _album.Text = st.Album;

        // Controles
        if (_playBtn != null)
        {
            _playBtn.Icon = st.IsPlaying ? Icons.Pause : Icons.Play;
            _playBtn.Enabled = st.CanPlayPause;
            if (s.PlayButtonAccent)
            {
                _playBtn.SetBackgrounds(ColorUtil.Brush(accent), ColorUtil.Brush(ColorUtil.Mix(accent, Colors.White, 0.2)));
                _playBtn.IconBrush = ColorUtil.Brush(ColorUtil.Luma(accent) > 0.6 ? Color.FromRgb(0x10, 0x10, 0x10) : Colors.White);
                if (s.ControlStyle == ControlStyle.Outline) _playBtn.BorderBrush = ColorUtil.Brush(accent);
            }
        }
        if (_prevBtn != null) _prevBtn.Enabled = st.CanPrev;
        if (_nextBtn != null) _nextBtn.Enabled = st.CanNext;
        var accentBrush = ColorUtil.Brush(accent);
        if (_progressFill != null) _progressFill.Background = accentBrush;
        if (_volFill != null) _volFill.Background = accentBrush;
        if (_volThumbDot != null) _volThumbDot.Background = accentBrush;

        UpdateProgress();
    }

    private void UpdateProgress()
    {
        var st = CurrentState;
        var dur = st.Duration;
        var pos = st.EstimatedPosition;
        double ratio = dur.TotalSeconds > 0 ? Math.Clamp(pos.TotalSeconds / dur.TotalSeconds, 0, 1) : 0;
        if (_progress != null)
        {
            _progress.Visibility = dur.TotalSeconds > 0 ? Visibility.Visible : Visibility.Hidden;
            _progress.ColumnDefinitions[0].Width = new GridLength(ratio, GridUnitType.Star);
            _progress.ColumnDefinitions[1].Width = new GridLength(1 - ratio, GridUnitType.Star);
        }
        if (_posText != null) _posText.Text = dur.TotalSeconds > 0 ? Fmt(pos) : "";
        if (_durText != null) _durText.Text = dur.TotalSeconds > 0 ? (S.ShowRemainingTime ? "-" + Fmt(dur - pos) : Fmt(dur)) : "";
    }

    private static string Fmt(TimeSpan t) => t.TotalHours >= 1 ? t.ToString(@"h\:mm\:ss") : $"{(int)t.TotalMinutes}:{t.Seconds:00}";
}
