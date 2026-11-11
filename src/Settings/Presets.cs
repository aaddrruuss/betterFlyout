using System;
using System.Collections.Generic;

namespace BetterFlyout.Settings;

public static class Presets
{
    /// <summary>Diseño moderno tipo tarjeta (base de varios presets).</summary>
    private static void Modern(FlyoutSettings s)
    {
        s.ResetVisual();
        s.Layout = FlyoutLayout.Horizontal; s.Width = 380; s.ArtSize = 76; s.Padding = 14; s.Spacing = 14; s.CornerRadius = 14;
        s.BackgroundColor = "#EB1C1C1E"; s.BorderColor = "#26FFFFFF"; s.ArtBackground = true; s.ArtBackgroundOpacity = 0.35;
        s.ArtCornerRadius = 10; s.ArtShadow = true; s.DimArtWhenPaused = true; s.ShowPausedBadge = true;
        s.TitleFontSize = 15; s.TitleWeight = WeightKind.SemiBold; s.ArtistFontSize = 13; s.ArtistWeight = WeightKind.Normal;
        s.ArtistColor = "#C8FFFFFF"; s.AppNameUppercase = true; s.AppNameColor = "#80FFFFFF";
        s.ControlStyle = ControlStyle.Filled; s.ControlBackground = "#00FFFFFF"; s.ControlHoverBackground = "#26FFFFFF";
        s.ControlSize = 30; s.ControlIconScale = 0.5; s.ControlSpacing = 6; s.ControlCornerRadius = 15; s.PlayButtonAccent = true;
        s.ProgressStyle = ProgressStyle.Bar; s.ProgressHeight = 4; s.ProgressTrackColor = "#30FFFFFF"; s.ShowTimes = true;
        s.UseArtAccent = true; s.AnimationIn = AnimationKind.SlideUp; s.AnimationOut = AnimationKind.SlideDown;
        s.AnimationDurationMs = 280; s.SlideDistance = 40; s.Anchor = Anchor.BottomCenter; s.MarginX = 0; s.MarginY = 24;
        s.ShowVolumePanel = false; s.ShadowBlur = 24; s.ShadowOpacity = 0.55;
    }

    public static readonly IReadOnlyDictionary<string, Action<FlyoutSettings>> All = new Dictionary<string, Action<FlyoutSettings>>
    {
        ["Windows 10 clásico, estilo 11 (por defecto)"] = s => s.ResetVisual(),

        ["Windows 10 original (esquinas rectas)"] = s =>
        {
            s.ResetVisual();
            s.CornerRadius = 0; s.ArtCornerRadius = 0; s.BorderThickness = 0; s.Shadow = false; s.BackgroundColor = "#FF1F1F1F";
            s.FontFamily = "Segoe UI"; s.ProgressStyle = ProgressStyle.None; s.Marquee = false; s.ControlStrokeThickness = 2;
            s.VolumeBarWidth = 12; s.AnimationIn = AnimationKind.Fade; s.AnimationOut = AnimationKind.Fade; s.AnimationDurationMs = 150;
        },

        ["Windows 10 clásico claro"] = s =>
        {
            s.ResetVisual();
            s.BackgroundColor = "#F5F3F3F3"; s.BorderColor = "#1A000000"; s.TitleColor = "#FF1B1B1B"; s.ArtistColor = "#E61B1B1B";
            s.AlbumColor = "#991B1B1B"; s.AppNameColor = "#B31B1B1B"; s.ControlIconColor = "#FF1B1B1B"; s.ControlHoverBackground = "#14000000";
            s.ProgressTrackColor = "#1A000000"; s.TimeColor = "#991B1B1B"; s.VolumeTextColor = "#FF1B1B1B"; s.ShadowOpacity = 0.2;
        },

        ["Moderno (tarjeta)"] = Modern,

        ["Moderno claro"] = s =>
        {
            Modern(s);
            s.BackgroundColor = "#F2F7F7F9"; s.BorderColor = "#1A000000";
            s.TitleColor = "#FF1B1B1B"; s.ArtistColor = "#CC1B1B1B"; s.AlbumColor = "#991B1B1B"; s.AppNameColor = "#801B1B1B";
            s.ControlIconColor = "#FF1B1B1B"; s.ControlHoverBackground = "#1A000000"; s.ProgressTrackColor = "#26000000";
            s.TimeColor = "#991B1B1B"; s.ShadowOpacity = 0.25; s.ArtBackgroundOpacity = 0.18; s.AccentColor = "#FF005FB8";
        },

        ["Cristal (carátula de fondo)"] = s =>
        {
            Modern(s);
            s.BackgroundColor = "#99101010"; s.ArtBackground = true; s.ArtBackgroundOpacity = 0.85; s.ArtBackgroundBlur = 60;
            s.TintWithArtColor = true; s.ArtTintStrength = 0.25; s.TextShadow = true; s.BorderColor = "#40FFFFFF";
            s.CornerRadius = 20; s.ArtCornerRadius = 14; s.Width = 400; s.ArtSize = 84;
        },

        ["Tarjeta vertical"] = s =>
        {
            Modern(s);
            s.Layout = FlyoutLayout.Vertical; s.Width = 260; s.ArtSize = 200; s.TextAlignment = TextAlign.Center;
            s.CornerRadius = 22; s.ArtCornerRadius = 14; s.Anchor = Anchor.MiddleRight; s.MarginX = 24; s.MarginY = 0;
            s.AnimationIn = AnimationKind.SlideLeft; s.AnimationOut = AnimationKind.SlideRight; s.ArtBackgroundOpacity = 0.6;
            s.ControlSize = 38;
        },

        ["Compacto (esquina)"] = s =>
        {
            Modern(s);
            s.Layout = FlyoutLayout.Compact; s.Width = 340; s.ArtSize = 40; s.Padding = 8; s.Spacing = 10; s.CornerRadius = 10;
            s.ArtCornerRadius = 6; s.TitleFontSize = 13; s.ArtistFontSize = 11.5; s.ShowAppName = false; s.ShowTimes = false;
            s.ProgressStyle = ProgressStyle.Line; s.ProgressHeight = 2; s.Anchor = Anchor.TopRight; s.MarginX = 16; s.MarginY = 16;
            s.AnimationIn = AnimationKind.SlideLeft; s.AnimationOut = AnimationKind.Fade; s.ControlSize = 26;
        },

        ["Minimal (solo texto)"] = s =>
        {
            Modern(s);
            s.Layout = FlyoutLayout.TextOnly; s.Width = 300; s.ShowControls = false; s.ShowAppName = false;
            s.ProgressStyle = ProgressStyle.Line; s.ProgressHeight = 2; s.ShowTimes = false; s.ArtBackground = false;
            s.BackgroundColor = "#CC000000"; s.BorderThickness = 0; s.CornerRadius = 8; s.Anchor = Anchor.TopCenter;
            s.AnimationIn = AnimationKind.SlideDown; s.AnimationOut = AnimationKind.SlideUp; s.TextAlignment = TextAlign.Center;
        },

        ["Neón"] = s =>
        {
            Modern(s);
            s.BackgroundGradient = true; s.BackgroundColor = "#F20D0221"; s.BackgroundColor2 = "#F2240046"; s.GradientAngle = 45;
            s.BorderColor = "#FFFF2BD6"; s.BorderThickness = 1.5; s.ShadowColor = "#FFFF2BD6"; s.ShadowOpacity = 0.7; s.ShadowBlur = 30; s.ShadowDepth = 0;
            s.TitleColor = "#FF2DE2E6"; s.ArtistColor = "#FFFF6EC7"; s.AccentColor = "#FF2DE2E6"; s.UseArtAccent = false; s.UseSystemAccent = false;
            s.ControlIconColor = "#FF2DE2E6"; s.ArtBackground = false; s.FontFamily = "Consolas"; s.AnimationIn = AnimationKind.Pop;
        },

        ["Vinilo"] = s =>
        {
            Modern(s);
            s.ArtCornerRadius = 999; s.ArtSpinWhenPlaying = true; s.ArtSize = 72; s.CornerRadius = 40; s.Padding = 12;
            s.ControlCornerRadius = 999; s.AnimationIn = AnimationKind.ZoomFade; s.AnimationOut = AnimationKind.ZoomFade;
        },
    };
}
