// BetterFlyout - Copyright (C) 2026 aaddrruuss (https://github.com/aaddrruuss/)
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace BetterFlyout.Settings;

public enum Anchor { TopLeft, TopCenter, TopRight, MiddleLeft, Center, MiddleRight, BottomLeft, BottomCenter, BottomRight }
public enum MonitorMode { Primary, Cursor, ActiveWindow, Specific }
public enum FlyoutLayout { Classic, Horizontal, Vertical, Compact, ArtOnly, TextOnly }
public enum WeightKind { Light, Normal, SemiBold, Bold }
public enum ControlStyle { Outline, Filled, IconOnly }
public enum TextAlign { Left, Center, Right }
public enum AnimationKind { None, Fade, SlideUp, SlideDown, SlideLeft, SlideRight, Zoom, ZoomFade, Pop }
public enum ProgressStyle { Bar, Line, None }
public enum RenderBackend { Gpu, Cpu, Auto }

/// <summary>
/// Todas las opciones del flyout. Cada propiedad notifica cambios -> el popup se reconstruye en vivo.
/// </summary>
public sealed class FlyoutSettings : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (Equals(field, value)) return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        return true;
    }

    // ───────── Idioma ─────────
    private BetterFlyout.Util.AppLanguage _language = BetterFlyout.Util.AppLanguage.System; public BetterFlyout.Util.AppLanguage Language { get => _language; set => Set(ref _language, value); }

    // ───────── General / disparadores ─────────
    private bool _enabled = true; public bool Enabled { get => _enabled; set => Set(ref _enabled, value); }
    private bool _startWithWindows; public bool StartWithWindows { get => _startWithWindows; set => Set(ref _startWithWindows, value); }
    private bool _showOnManualTrackChange = true; public bool ShowOnManualTrackChange { get => _showOnManualTrackChange; set => Set(ref _showOnManualTrackChange, value); }
    private bool _showOnAutoTrackChange = true; public bool ShowOnAutoTrackChange { get => _showOnAutoTrackChange; set => Set(ref _showOnAutoTrackChange, value); }
    private bool _showOnPause = true; public bool ShowOnPause { get => _showOnPause; set => Set(ref _showOnPause, value); }
    private bool _showOnResume = true; public bool ShowOnResume { get => _showOnResume; set => Set(ref _showOnResume, value); }
    private bool _showOnMediaKeys = true; public bool ShowOnMediaKeys { get => _showOnMediaKeys; set => Set(ref _showOnMediaKeys, value); }
    private bool _showOnVolumeKeys; public bool ShowOnVolumeKeys { get => _showOnVolumeKeys; set => Set(ref _showOnVolumeKeys, value); }
    private bool _replaceVolumeFlyout; public bool ReplaceVolumeFlyout { get => _replaceVolumeFlyout; set => Set(ref _replaceVolumeFlyout, value); }
    private bool _replaceMediaFlyout; public bool ReplaceMediaFlyout { get => _replaceMediaFlyout; set => Set(ref _replaceMediaFlyout, value); }
    private int _volumeStep = 2; public int VolumeStep { get => _volumeStep; set => Set(ref _volumeStep, value); }
    private bool _showOnSourceChange; public bool ShowOnSourceChange { get => _showOnSourceChange; set => Set(ref _showOnSourceChange, value); }
    private bool _hideWhenNoSession = true; public bool HideWhenNoSession { get => _hideWhenNoSession; set => Set(ref _hideWhenNoSession, value); }
    private bool _onlyInFullscreen; public bool OnlyInFullscreen { get => _onlyInFullscreen; set => Set(ref _onlyInFullscreen, value); }
    private int _displayDurationMs = 3000; public int DisplayDurationMs { get => _displayDurationMs; set => Set(ref _displayDurationMs, value); }
    private int _pausedDisplayDurationMs = 3000; public int PausedDisplayDurationMs { get => _pausedDisplayDurationMs; set => Set(ref _pausedDisplayDurationMs, value); }
    private int _manualWindowMs = 2500; public int ManualWindowMs { get => _manualWindowMs; set => Set(ref _manualWindowMs, value); }
    private bool _keepOpenOnHover = true; public bool KeepOpenOnHover { get => _keepOpenOnHover; set => Set(ref _keepOpenOnHover, value); }
    private bool _clickThrough; public bool ClickThrough { get => _clickThrough; set => Set(ref _clickThrough, value); }
    private bool _closeOnRightClick = true; public bool CloseOnRightClick { get => _closeOnRightClick; set => Set(ref _closeOnRightClick, value); }
    private string _hotkey = "Ctrl+Alt+M"; public string Hotkey { get => _hotkey; set => Set(ref _hotkey, value); }

    // ───────── Posición ─────────
    private MonitorMode _monitorMode = MonitorMode.Primary; public MonitorMode MonitorMode { get => _monitorMode; set => Set(ref _monitorMode, value); }
    private int _monitorIndex; public int MonitorIndex { get => _monitorIndex; set => Set(ref _monitorIndex, value); }
    private Anchor _anchor = Anchor.TopLeft; public Anchor Anchor { get => _anchor; set => Set(ref _anchor, value); }
    private double _marginX = 48; public double MarginX { get => _marginX; set => Set(ref _marginX, value); }
    private double _marginY = 48; public double MarginY { get => _marginY; set => Set(ref _marginY, value); }
    private bool _useWorkArea = true; public bool UseWorkArea { get => _useWorkArea; set => Set(ref _useWorkArea, value); }

    // ───────── Tamaño / diseño ─────────
    private FlyoutLayout _layout = FlyoutLayout.Classic; public FlyoutLayout Layout { get => _layout; set => Set(ref _layout, value); }
    private double _width = 440; public double Width { get => _width; set => Set(ref _width, value); }
    private double _minHeight; public double MinHeight { get => _minHeight; set => Set(ref _minHeight, value); }
    private double _scale = 1.0; public double Scale { get => _scale; set => Set(ref _scale, value); }
    private double _padding = 16; public double Padding { get => _padding; set => Set(ref _padding, value); }
    private double _spacing = 16; public double Spacing { get => _spacing; set => Set(ref _spacing, value); }
    private double _cornerRadius = 8; public double CornerRadius { get => _cornerRadius; set => Set(ref _cornerRadius, value); }
    private double _opacity = 1.0; public double Opacity { get => _opacity; set => Set(ref _opacity, value); }
    private TextAlign _textAlignment = TextAlign.Left; public TextAlign TextAlignment { get => _textAlignment; set => Set(ref _textAlignment, value); }

    // ───────── Fondo / borde / sombra ─────────
    private string _backgroundColor = "#F51F1F1F"; public string BackgroundColor { get => _backgroundColor; set => Set(ref _backgroundColor, value); }
    private bool _backgroundGradient; public bool BackgroundGradient { get => _backgroundGradient; set => Set(ref _backgroundGradient, value); }
    private string _backgroundColor2 = "#EB2C2C34"; public string BackgroundColor2 { get => _backgroundColor2; set => Set(ref _backgroundColor2, value); }
    private double _gradientAngle = 90; public double GradientAngle { get => _gradientAngle; set => Set(ref _gradientAngle, value); }
    private bool _tintWithArtColor; public bool TintWithArtColor { get => _tintWithArtColor; set => Set(ref _tintWithArtColor, value); }
    private double _artTintStrength = 0.35; public double ArtTintStrength { get => _artTintStrength; set => Set(ref _artTintStrength, value); }
    private string _borderColor = "#1FFFFFFF"; public string BorderColor { get => _borderColor; set => Set(ref _borderColor, value); }
    private double _borderThickness = 1; public double BorderThickness { get => _borderThickness; set => Set(ref _borderThickness, value); }
    private bool _artBackground; public bool ArtBackground { get => _artBackground; set => Set(ref _artBackground, value); }
    private double _artBackgroundBlur = 40; public double ArtBackgroundBlur { get => _artBackgroundBlur; set => Set(ref _artBackgroundBlur, value); }
    private double _artBackgroundOpacity = 0.35; public double ArtBackgroundOpacity { get => _artBackgroundOpacity; set => Set(ref _artBackgroundOpacity, value); }
    private bool _shadow = true; public bool Shadow { get => _shadow; set => Set(ref _shadow, value); }
    private string _shadowColor = "#FF000000"; public string ShadowColor { get => _shadowColor; set => Set(ref _shadowColor, value); }
    private double _shadowBlur = 22; public double ShadowBlur { get => _shadowBlur; set => Set(ref _shadowBlur, value); }
    private double _shadowOpacity = 0.45; public double ShadowOpacity { get => _shadowOpacity; set => Set(ref _shadowOpacity, value); }
    private double _shadowDepth = 4; public double ShadowDepth { get => _shadowDepth; set => Set(ref _shadowDepth, value); }
    private bool _blurBehind; public bool BlurBehind { get => _blurBehind; set => Set(ref _blurBehind, value); }

    // ───────── Carátula ─────────
    private bool _showArt = true; public bool ShowArt { get => _showArt; set => Set(ref _showArt, value); }
    private double _artSize = 92; public double ArtSize { get => _artSize; set => Set(ref _artSize, value); }
    private double _artCornerRadius = 4; public double ArtCornerRadius { get => _artCornerRadius; set => Set(ref _artCornerRadius, value); }
    private bool _artShadow; public bool ArtShadow { get => _artShadow; set => Set(ref _artShadow, value); }
    private bool _artSpinWhenPlaying; public bool ArtSpinWhenPlaying { get => _artSpinWhenPlaying; set => Set(ref _artSpinWhenPlaying, value); }
    private bool _dimArtWhenPaused; public bool DimArtWhenPaused { get => _dimArtWhenPaused; set => Set(ref _dimArtWhenPaused, value); }
    private bool _showPausedBadge; public bool ShowPausedBadge { get => _showPausedBadge; set => Set(ref _showPausedBadge, value); }

    // ───────── Texto ─────────
    private string _fontFamily = "Segoe UI Variable Display, Segoe UI"; public string FontFamily { get => _fontFamily; set => Set(ref _fontFamily, value); }
    private double _titleFontSize = 24; public double TitleFontSize { get => _titleFontSize; set => Set(ref _titleFontSize, value); }
    private string _titleColor = "#FFFFFFFF"; public string TitleColor { get => _titleColor; set => Set(ref _titleColor, value); }
    private WeightKind _titleWeight = WeightKind.Light; public WeightKind TitleWeight { get => _titleWeight; set => Set(ref _titleWeight, value); }
    private WeightKind _artistWeight = WeightKind.SemiBold; public WeightKind ArtistWeight { get => _artistWeight; set => Set(ref _artistWeight, value); }
    private bool _titleWrap; public bool TitleWrap { get => _titleWrap; set => Set(ref _titleWrap, value); }
    private bool _marquee = true; public bool Marquee { get => _marquee; set => Set(ref _marquee, value); }
    private double _marqueeSpeed = 30; public double MarqueeSpeed { get => _marqueeSpeed; set => Set(ref _marqueeSpeed, value); }
    private double _artistFontSize = 12; public double ArtistFontSize { get => _artistFontSize; set => Set(ref _artistFontSize, value); }
    private string _artistColor = "#E6FFFFFF"; public string ArtistColor { get => _artistColor; set => Set(ref _artistColor, value); }
    private bool _showArtist = true; public bool ShowArtist { get => _showArtist; set => Set(ref _showArtist, value); }
    private bool _showAlbum; public bool ShowAlbum { get => _showAlbum; set => Set(ref _showAlbum, value); }
    private double _albumFontSize = 12; public double AlbumFontSize { get => _albumFontSize; set => Set(ref _albumFontSize, value); }
    private string _albumColor = "#99FFFFFF"; public string AlbumColor { get => _albumColor; set => Set(ref _albumColor, value); }
    private bool _showAppName = true; public bool ShowAppName { get => _showAppName; set => Set(ref _showAppName, value); }
    private double _appNameFontSize = 11; public double AppNameFontSize { get => _appNameFontSize; set => Set(ref _appNameFontSize, value); }
    private string _appNameColor = "#B3FFFFFF"; public string AppNameColor { get => _appNameColor; set => Set(ref _appNameColor, value); }
    private bool _appNameUppercase; public bool AppNameUppercase { get => _appNameUppercase; set => Set(ref _appNameUppercase, value); }
    private bool _textShadow; public bool TextShadow { get => _textShadow; set => Set(ref _textShadow, value); }

    // ───────── Controles ─────────
    private ControlStyle _controlStyle = ControlStyle.Outline; public ControlStyle ControlStyle { get => _controlStyle; set => Set(ref _controlStyle, value); }
    private double _controlStrokeThickness = 1.5; public double ControlStrokeThickness { get => _controlStrokeThickness; set => Set(ref _controlStrokeThickness, value); }
    private bool _showControls = true; public bool ShowControls { get => _showControls; set => Set(ref _showControls, value); }
    private double _controlSize = 40; public double ControlSize { get => _controlSize; set => Set(ref _controlSize, value); }
    private double _controlIconScale = 0.36; public double ControlIconScale { get => _controlIconScale; set => Set(ref _controlIconScale, value); }
    private string _controlIconColor = "#FFFFFFFF"; public string ControlIconColor { get => _controlIconColor; set => Set(ref _controlIconColor, value); }
    private string _controlBackground = "#00FFFFFF"; public string ControlBackground { get => _controlBackground; set => Set(ref _controlBackground, value); }
    private string _controlHoverBackground = "#1FFFFFFF"; public string ControlHoverBackground { get => _controlHoverBackground; set => Set(ref _controlHoverBackground, value); }
    private bool _playButtonAccent; public bool PlayButtonAccent { get => _playButtonAccent; set => Set(ref _playButtonAccent, value); }
    private double _controlCornerRadius = 999; public double ControlCornerRadius { get => _controlCornerRadius; set => Set(ref _controlCornerRadius, value); }
    private double _controlSpacing = 14; public double ControlSpacing { get => _controlSpacing; set => Set(ref _controlSpacing, value); }

    // ───────── Progreso / acento ─────────
    private ProgressStyle _progressStyle = ProgressStyle.Line; public ProgressStyle ProgressStyle { get => _progressStyle; set => Set(ref _progressStyle, value); }
    private double _progressHeight = 3; public double ProgressHeight { get => _progressHeight; set => Set(ref _progressHeight, value); }
    private string _accentColor = "#FF60CDFF"; public string AccentColor { get => _accentColor; set => Set(ref _accentColor, value); }
    private bool _useArtAccent; public bool UseArtAccent { get => _useArtAccent; set => Set(ref _useArtAccent, value); }
    private bool _useSystemAccent = true; public bool UseSystemAccent { get => _useSystemAccent; set => Set(ref _useSystemAccent, value); }
    private string _progressTrackColor = "#1AFFFFFF"; public string ProgressTrackColor { get => _progressTrackColor; set => Set(ref _progressTrackColor, value); }
    private bool _showTimes; public bool ShowTimes { get => _showTimes; set => Set(ref _showTimes, value); }
    private double _timeFontSize = 10; public double TimeFontSize { get => _timeFontSize; set => Set(ref _timeFontSize, value); }
    private string _timeColor = "#99FFFFFF"; public string TimeColor { get => _timeColor; set => Set(ref _timeColor, value); }
    private bool _showRemainingTime; public bool ShowRemainingTime { get => _showRemainingTime; set => Set(ref _showRemainingTime, value); }

    // ───────── Panel de volumen (estilo Windows 10) ─────────
    private bool _showVolumePanel = true; public bool ShowVolumePanel { get => _showVolumePanel; set => Set(ref _showVolumePanel, value); }
    private bool _volumePanelOnRight; public bool VolumePanelOnRight { get => _volumePanelOnRight; set => Set(ref _volumePanelOnRight, value); }
    private double _volumePanelWidth = 58; public double VolumePanelWidth { get => _volumePanelWidth; set => Set(ref _volumePanelWidth, value); }
    private double _volumeBarWidth = 4; public double VolumeBarWidth { get => _volumeBarWidth; set => Set(ref _volumeBarWidth, value); }
    private string _volumeTextColor = "#FFFFFFFF"; public string VolumeTextColor { get => _volumeTextColor; set => Set(ref _volumeTextColor, value); }
    private double _volumeFontSize = 13; public double VolumeFontSize { get => _volumeFontSize; set => Set(ref _volumeFontSize, value); }
    private double _cardGap = 4; public double CardGap { get => _cardGap; set => Set(ref _cardGap, value); }

    // ───────── Animación ─────────
    private AnimationKind _animationIn = AnimationKind.SlideDown; public AnimationKind AnimationIn { get => _animationIn; set => Set(ref _animationIn, value); }
    private AnimationKind _animationOut = AnimationKind.SlideUp; public AnimationKind AnimationOut { get => _animationOut; set => Set(ref _animationOut, value); }
    private int _animationDurationMs = 260; public int AnimationDurationMs { get => _animationDurationMs; set => Set(ref _animationDurationMs, value); }
    private double _slideDistance = 24; public double SlideDistance { get => _slideDistance; set => Set(ref _slideDistance, value); }
    private bool _crossfadeOnTrackChange = true; public bool CrossfadeOnTrackChange { get => _crossfadeOnTrackChange; set => Set(ref _crossfadeOnTrackChange, value); }

    // ───────── Pantalla completa ─────────
    private RenderBackend _renderBackend = RenderBackend.Gpu; public RenderBackend RenderBackend { get => _renderBackend; set => Set(ref _renderBackend, value); }
    /// <summary>Modo automático: % de uso de la GPU a partir del cual el popup se dibuja con la CPU.</summary>
    private int _autoGpuThreshold = 85; public int AutoGpuThreshold { get => _autoGpuThreshold; set => Set(ref _autoGpuThreshold, value); }
    private int _animationFps = 60; public int AnimationFps { get => _animationFps; set => Set(ref _animationFps, value); }
    private bool _forceTopmost = true; public bool ForceTopmost { get => _forceTopmost; set => Set(ref _forceTopmost, value); }
    private int _topmostIntervalMs = 150; public int TopmostIntervalMs { get => _topmostIntervalMs; set => Set(ref _topmostIntervalMs, value); }

    /// <summary>Propiedades de comportamiento que los presets visuales no deben tocar.</summary>
    private static readonly System.Collections.Generic.HashSet<string> BehaviorProps = new()
    {
        nameof(Language), nameof(Enabled), nameof(StartWithWindows), nameof(ShowOnManualTrackChange), nameof(ShowOnAutoTrackChange),
        nameof(ShowOnPause), nameof(ShowOnResume), nameof(ShowOnMediaKeys), nameof(ShowOnVolumeKeys), nameof(ReplaceVolumeFlyout), nameof(ReplaceMediaFlyout), nameof(VolumeStep), nameof(ShowOnSourceChange), nameof(HideWhenNoSession),
        nameof(OnlyInFullscreen), nameof(DisplayDurationMs), nameof(PausedDisplayDurationMs), nameof(ManualWindowMs),
        nameof(KeepOpenOnHover), nameof(ClickThrough), nameof(CloseOnRightClick), nameof(Hotkey), nameof(MonitorMode),
        nameof(MonitorIndex), nameof(UseWorkArea), nameof(RenderBackend), nameof(AutoGpuThreshold), nameof(AnimationFps), nameof(ForceTopmost), nameof(TopmostIntervalMs),
    };

    /// <summary>Copia propiedades desde otra instancia (dispara PropertyChanged por cada cambio).</summary>
    public void CopyFrom(FlyoutSettings other, bool visualOnly = false)
    {
        foreach (var p in typeof(FlyoutSettings).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!p.CanRead || !p.CanWrite || p.GetCustomAttribute<JsonIgnoreAttribute>() != null) continue;
            if (visualOnly && BehaviorProps.Contains(p.Name)) continue;
            p.SetValue(this, p.GetValue(other));
        }
    }

    /// <summary>Restablece solo la parte visual a valores por defecto.</summary>
    public void ResetVisual() => CopyFrom(new FlyoutSettings(), visualOnly: true);
}
