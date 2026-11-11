// BetterFlyout - Copyright (C) 2026 aaddrruuss (https://github.com/aaddrruuss/)
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using BetterFlyout.Interop;
using BetterFlyout.Settings;
using BetterFlyout.Util;

namespace BetterFlyout.Views;

/// <summary>
/// Ventana de configuración generada por código. Sencilla a propósito: lo importante es el popup.
/// Todos los cambios se aplican en vivo y se guardan solos.
/// </summary>
public sealed class SettingsWindow : Window
{
    private readonly FlyoutSettings S;
    private readonly FlyoutWindow _flyout;
    private readonly TabControl _tabs = new();

    public SettingsWindow(FlyoutSettings settings, FlyoutWindow flyout)
    {
        S = settings;
        _flyout = flyout;
        Title = "BetterFlyout · Configuración";
        Width = 760; Height = 720; MinWidth = 560; MinHeight = 420;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        try { ThemeMode = ThemeMode.System; } catch { }

        var root = new DockPanel();
        var bottom = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(12), HorizontalAlignment = HorizontalAlignment.Right };
        var preview = new CheckBox { Content = "Vista previa fija (el popup se queda visible)", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 16, 0) };
        preview.Checked += (_, _) => _flyout.SetPreview(true);
        preview.Unchecked += (_, _) => _flyout.SetPreview(false);
        var showNow = new Button { Content = "Mostrar popup ahora", Padding = new Thickness(12, 4, 12, 4) };
        showNow.Click += (_, _) => _flyout.ShowFlyout();
        bottom.Children.Add(preview);
        bottom.Children.Add(showNow);
        DockPanel.SetDock(bottom, Dock.Bottom);
        root.Children.Add(bottom);

        // Créditos y aviso legal (GPL-3.0 §5d)
        var credits = new TextBlock { Margin = new Thickness(16, 0, 16, 8), Opacity = 0.7, TextWrapping = TextWrapping.Wrap, FontSize = 12 };
        credits.Inlines.Add("BetterFlyout © 2026 ");
        var link = new System.Windows.Documents.Hyperlink(new System.Windows.Documents.Run("aaddrruuss")) { NavigateUri = new Uri("https://github.com/aaddrruuss/") };
        link.RequestNavigate += (_, e) => { Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true }); e.Handled = true; };
        credits.Inlines.Add(link);
        credits.Inlines.Add(" · Software libre bajo licencia GPL-3.0-or-later, SIN NINGUNA GARANTÍA. Puedes redistribuirlo y modificarlo bajo sus términos (ver LICENSE).");
        DockPanel.SetDock(credits, Dock.Bottom);
        root.Children.Add(credits);
        root.Children.Add(_tabs);
        Content = root;

        BuildGeneral();
        BuildPosition();
        BuildLayout();
        BuildBackground();
        BuildArt();
        BuildText();
        BuildControls();
        BuildVolume();
        BuildAnimation();
        BuildFullscreen();
        BuildProfiles();

        Closed += (_, _) => _flyout.SetPreview(false);
    }

    // ═════════════════ Secciones ═════════════════

    private void BuildGeneral()
    {
        var p = Tab("General");
        AddHeader(p, "Cuándo aparece el popup");
        AddCheck(p, "Popups activados", nameof(S.Enabled));
        AddCheck(p, "Al cambiar de canción manualmente (teclas multimedia / botones)", nameof(S.ShowOnManualTrackChange));
        AddCheck(p, "Al cambiar de canción automáticamente (termina una y empieza otra)", nameof(S.ShowOnAutoTrackChange));
        AddCheck(p, "Al pausar", nameof(S.ShowOnPause));
        AddCheck(p, "Al reanudar", nameof(S.ShowOnResume));
        AddCheck(p, "Al pulsar cualquier tecla multimedia", nameof(S.ShowOnMediaKeys));
        AddCheck(p, "Al pulsar teclas de volumen", nameof(S.ShowOnVolumeKeys));
        AddCheck(p, "Al cambiar de aplicación de música", nameof(S.ShowOnSourceChange));
        AddCheck(p, "No mostrar si no hay nada reproduciéndose", nameof(S.HideWhenNoSession));
        AddCheck(p, "Solo cuando hay una app a pantalla completa", nameof(S.OnlyInFullscreen));
        AddSlider(p, "Ventana para considerar un cambio 'manual' (ms)", nameof(S.ManualWindowMs), 500, 10000, 100, "0");

        AddHeader(p, "Duración e interacción");
        AddSlider(p, "Tiempo visible (ms)", nameof(S.DisplayDurationMs), 500, 20000, 100, "0");
        AddSlider(p, "Tiempo visible al pausar (ms)", nameof(S.PausedDisplayDurationMs), 500, 20000, 100, "0");
        AddCheck(p, "Mantener abierto mientras el ratón está encima", nameof(S.KeepOpenOnHover));
        AddCheck(p, "Clic derecho cierra el popup", nameof(S.CloseOnRightClick));
        AddCheck(p, "Clic a través (el popup ignora el ratón)", nameof(S.ClickThrough));
        AddText(p, "Atajo global para mostrar (ej. Ctrl+Alt+M, vacío = ninguno)", nameof(S.Hotkey));
        AddCheck(p, "Iniciar con Windows", nameof(S.StartWithWindows));
    }

    private void BuildPosition()
    {
        var p = Tab("Posición");
        AddEnum<MonitorMode>(p, "Monitor", nameof(S.MonitorMode), new()
        {
            [MonitorMode.Primary] = "Principal", [MonitorMode.Cursor] = "Donde está el ratón",
            [MonitorMode.ActiveWindow] = "Donde está la ventana activa", [MonitorMode.Specific] = "Monitor concreto",
        });
        var monitors = Native.GetMonitors();
        var monCombo = new ComboBox();
        for (int i = 0; i < monitors.Count; i++)
        {
            var m = monitors[i];
            monCombo.Items.Add($"{i + 1}: {m.Device.TrimStart('\\', '.')} {m.Bounds.Width}x{m.Bounds.Height}{(m.IsPrimary ? " (principal)" : "")}");
        }
        monCombo.SetBinding(System.Windows.Controls.Primitives.Selector.SelectedIndexProperty, Bind(nameof(S.MonitorIndex)));
        AddRow(p, "Monitor concreto", monCombo);

        AddEnum<Anchor>(p, "Anclaje", nameof(S.Anchor), new()
        {
            [Anchor.TopLeft] = "Arriba izquierda", [Anchor.TopCenter] = "Arriba centro", [Anchor.TopRight] = "Arriba derecha",
            [Anchor.MiddleLeft] = "Centro izquierda", [Anchor.Center] = "Centro", [Anchor.MiddleRight] = "Centro derecha",
            [Anchor.BottomLeft] = "Abajo izquierda", [Anchor.BottomCenter] = "Abajo centro", [Anchor.BottomRight] = "Abajo derecha",
        });
        AddSlider(p, "Margen / desplazamiento X (px)", nameof(S.MarginX), -2000, 2000, 1, "0");
        AddSlider(p, "Margen / desplazamiento Y (px)", nameof(S.MarginY), -2000, 2000, 1, "0");
        AddCheck(p, "Respetar la barra de tareas (área de trabajo)", nameof(S.UseWorkArea));
    }

    private void BuildLayout()
    {
        var p = Tab("Diseño");
        AddEnum<FlyoutLayout>(p, "Diseño", nameof(S.Layout), new()
        {
            [FlyoutLayout.Classic] = "Clásico Windows 10", [FlyoutLayout.Horizontal] = "Horizontal moderno",
            [FlyoutLayout.Vertical] = "Vertical (tarjeta)", [FlyoutLayout.Compact] = "Compacto (una línea)",
            [FlyoutLayout.ArtOnly] = "Solo carátula", [FlyoutLayout.TextOnly] = "Solo texto",
        });
        AddSlider(p, "Ancho (px)", nameof(S.Width), 120, 1200, 1, "0");
        AddSlider(p, "Alto mínimo (px)", nameof(S.MinHeight), 0, 800, 1, "0");
        AddSlider(p, "Escala global", nameof(S.Scale), 0.3, 4, 0.05, "0.00");
        AddSlider(p, "Relleno interior (px)", nameof(S.Padding), 0, 80, 1, "0");
        AddSlider(p, "Separación entre elementos (px)", nameof(S.Spacing), 0, 80, 1, "0");
        AddSlider(p, "Radio de esquinas (px)", nameof(S.CornerRadius), 0, 80, 1, "0");
        AddSlider(p, "Opacidad general", nameof(S.Opacity), 0.05, 1, 0.01, "0.00");
        AddEnum<TextAlign>(p, "Alineación del texto", nameof(S.TextAlignment), new() { [TextAlign.Left] = "Izquierda", [TextAlign.Center] = "Centro", [TextAlign.Right] = "Derecha" });
    }

    private void BuildBackground()
    {
        var p = Tab("Fondo");
        AddHeader(p, "Fondo");
        AddColor(p, "Color de fondo", nameof(S.BackgroundColor));
        AddCheck(p, "Degradado", nameof(S.BackgroundGradient));
        AddColor(p, "Segundo color del degradado", nameof(S.BackgroundColor2));
        AddSlider(p, "Ángulo del degradado", nameof(S.GradientAngle), 0, 360, 1, "0");
        AddCheck(p, "Teñir el fondo con el color de la carátula", nameof(S.TintWithArtColor));
        AddSlider(p, "Intensidad del tinte", nameof(S.ArtTintStrength), 0, 1, 0.01, "0.00");
        AddCheck(p, "Carátula difuminada de fondo", nameof(S.ArtBackground));
        AddSlider(p, "Desenfoque de la carátula de fondo", nameof(S.ArtBackgroundBlur), 0, 150, 1, "0");
        AddSlider(p, "Opacidad de la carátula de fondo", nameof(S.ArtBackgroundOpacity), 0, 1, 0.01, "0.00");
        AddCheck(p, "Desenfocar lo de detrás de la ventana (experimental, sin sombra)", nameof(S.BlurBehind));

        AddHeader(p, "Borde");
        AddColor(p, "Color del borde", nameof(S.BorderColor));
        AddSlider(p, "Grosor del borde", nameof(S.BorderThickness), 0, 10, 0.5, "0.0");

        AddHeader(p, "Sombra");
        AddCheck(p, "Sombra", nameof(S.Shadow));
        AddColor(p, "Color de la sombra", nameof(S.ShadowColor));
        AddSlider(p, "Difuminado de la sombra", nameof(S.ShadowBlur), 0, 100, 1, "0");
        AddSlider(p, "Opacidad de la sombra", nameof(S.ShadowOpacity), 0, 1, 0.01, "0.00");
        AddSlider(p, "Desplazamiento de la sombra", nameof(S.ShadowDepth), 0, 40, 1, "0");
    }

    private void BuildArt()
    {
        var p = Tab("Carátula");
        AddCheck(p, "Mostrar carátula", nameof(S.ShowArt));
        AddSlider(p, "Tamaño (px)", nameof(S.ArtSize), 16, 600, 1, "0");
        AddSlider(p, "Radio de esquinas (999 = círculo)", nameof(S.ArtCornerRadius), 0, 999, 1, "0");
        AddCheck(p, "Sombra de la carátula", nameof(S.ArtShadow));
        AddCheck(p, "Girar mientras suena (efecto vinilo)", nameof(S.ArtSpinWhenPlaying));
        AddCheck(p, "Atenuar al pausar", nameof(S.DimArtWhenPaused));
        AddCheck(p, "Icono de pausa sobre la carátula", nameof(S.ShowPausedBadge));
    }

    private void BuildText()
    {
        var p = Tab("Texto");
        AddFont(p, "Fuente (admite lista: 'A, B')", nameof(S.FontFamily));
        AddCheck(p, "Desplazar texto largo (marquee)", nameof(S.Marquee));
        AddSlider(p, "Velocidad del desplazamiento (px/s)", nameof(S.MarqueeSpeed), 5, 200, 1, "0");
        AddCheck(p, "Sombra en el texto", nameof(S.TextShadow));
        var weights = new Dictionary<WeightKind, string> { [WeightKind.Light] = "Fina", [WeightKind.Normal] = "Normal", [WeightKind.SemiBold] = "Seminegrita", [WeightKind.Bold] = "Negrita" };

        AddHeader(p, "Título");
        AddSlider(p, "Tamaño", nameof(S.TitleFontSize), 6, 72, 0.5, "0.0");
        AddColor(p, "Color", nameof(S.TitleColor));
        AddEnum(p, "Grosor", nameof(S.TitleWeight), weights);
        AddCheck(p, "Varias líneas (en vez de desplazar/recortar)", nameof(S.TitleWrap));

        AddHeader(p, "Artista");
        AddCheck(p, "Mostrar artista", nameof(S.ShowArtist));
        AddSlider(p, "Tamaño", nameof(S.ArtistFontSize), 6, 48, 0.5, "0.0");
        AddColor(p, "Color", nameof(S.ArtistColor));
        AddEnum(p, "Grosor", nameof(S.ArtistWeight), weights);

        AddHeader(p, "Álbum");
        AddCheck(p, "Mostrar álbum", nameof(S.ShowAlbum));
        AddSlider(p, "Tamaño", nameof(S.AlbumFontSize), 6, 48, 0.5, "0.0");
        AddColor(p, "Color", nameof(S.AlbumColor));

        AddHeader(p, "Nombre de la app");
        AddCheck(p, "Mostrar nombre de la app (Spotify, Chrome…)", nameof(S.ShowAppName));
        AddSlider(p, "Tamaño", nameof(S.AppNameFontSize), 6, 36, 0.5, "0.0");
        AddColor(p, "Color", nameof(S.AppNameColor));
        AddCheck(p, "En mayúsculas", nameof(S.AppNameUppercase));
    }

    private void BuildControls()
    {
        var p = Tab("Controles");
        AddHeader(p, "Botones");
        AddCheck(p, "Mostrar botones (anterior / play / siguiente)", nameof(S.ShowControls));
        AddEnum<ControlStyle>(p, "Estilo", nameof(S.ControlStyle), new() { [ControlStyle.Outline] = "Contorno (Windows 10)", [ControlStyle.Filled] = "Relleno", [ControlStyle.IconOnly] = "Solo icono" });
        AddSlider(p, "Tamaño", nameof(S.ControlSize), 12, 120, 1, "0");
        AddSlider(p, "Tamaño del icono (proporción)", nameof(S.ControlIconScale), 0.15, 0.9, 0.01, "0.00");
        AddSlider(p, "Grosor del contorno", nameof(S.ControlStrokeThickness), 0.5, 6, 0.25, "0.00");
        AddSlider(p, "Separación", nameof(S.ControlSpacing), 0, 60, 1, "0");
        AddSlider(p, "Radio de esquinas (999 = círculo)", nameof(S.ControlCornerRadius), 0, 999, 1, "0");
        AddColor(p, "Color del icono", nameof(S.ControlIconColor));
        AddColor(p, "Fondo", nameof(S.ControlBackground));
        AddColor(p, "Fondo al pasar el ratón", nameof(S.ControlHoverBackground));
        AddCheck(p, "Botón play con color de acento", nameof(S.PlayButtonAccent));

        AddHeader(p, "Color de acento");
        AddCheck(p, "Usar el color de la carátula", nameof(S.UseArtAccent));
        AddCheck(p, "Usar el color de acento de Windows", nameof(S.UseSystemAccent));
        AddColor(p, "Color de acento personalizado", nameof(S.AccentColor));

        AddHeader(p, "Progreso");
        AddEnum<ProgressStyle>(p, "Barra de progreso", nameof(S.ProgressStyle), new() { [ProgressStyle.Bar] = "Barra", [ProgressStyle.Line] = "Línea en el borde inferior", [ProgressStyle.None] = "Ninguna" });
        AddSlider(p, "Grosor", nameof(S.ProgressHeight), 1, 20, 0.5, "0.0");
        AddColor(p, "Color del fondo de la barra", nameof(S.ProgressTrackColor));
        AddCheck(p, "Mostrar tiempos", nameof(S.ShowTimes));
        AddCheck(p, "Mostrar tiempo restante en vez de duración", nameof(S.ShowRemainingTime));
        AddSlider(p, "Tamaño de los tiempos", nameof(S.TimeFontSize), 6, 30, 0.5, "0.0");
        AddColor(p, "Color de los tiempos", nameof(S.TimeColor));
    }

    private void BuildVolume()
    {
        var p = Tab("Volumen");
        AddHeader(p, "Sustituir los flyouts de Windows");
        AddCheck(p, "Ocultar el flyout de volumen de Windows (BetterFlyout gestiona las teclas de volumen)", nameof(S.ReplaceVolumeFlyout));
        AddSlider(p, "Paso de volumen por pulsación (%)", nameof(S.VolumeStep), 1, 20, 1, "0");
        AddCheck(p, "Ocultar el flyout multimedia de Windows (BetterFlyout gestiona play/pausa/siguiente/anterior)", nameof(S.ReplaceMediaFlyout));
        AddNote(p, "Funciona interceptando las teclas antes de que lleguen a Windows. Si cierras BetterFlyout, todo vuelve a ser como antes. " +
                   "Las teclas multimedia solo se interceptan si hay música en una app compatible; si no, pasan a Windows.");

        AddHeader(p, "Panel");
        AddCheck(p, "Mostrar panel de volumen (como Windows 10)", nameof(S.ShowVolumePanel));
        AddCheck(p, "Panel a la derecha", nameof(S.VolumePanelOnRight));
        AddSlider(p, "Ancho del panel", nameof(S.VolumePanelWidth), 24, 200, 1, "0");
        AddSlider(p, "Grosor de la barra", nameof(S.VolumeBarWidth), 2, 40, 1, "0");
        AddSlider(p, "Separación entre paneles", nameof(S.CardGap), 0, 60, 1, "0");
        AddSlider(p, "Tamaño del número", nameof(S.VolumeFontSize), 6, 36, 0.5, "0.0");
        AddColor(p, "Color del número", nameof(S.VolumeTextColor));
        AddNote(p, "Arrastra la barra o usa la rueda del ratón sobre el popup para cambiar el volumen. Clic en el número = silenciar.");
    }

    private void BuildAnimation()
    {
        var p = Tab("Animación");
        var kinds = new Dictionary<AnimationKind, string>
        {
            [AnimationKind.None] = "Ninguna", [AnimationKind.Fade] = "Fundido", [AnimationKind.SlideUp] = "Deslizar hacia arriba",
            [AnimationKind.SlideDown] = "Deslizar hacia abajo", [AnimationKind.SlideLeft] = "Deslizar a la izquierda",
            [AnimationKind.SlideRight] = "Deslizar a la derecha", [AnimationKind.Zoom] = "Zoom", [AnimationKind.ZoomFade] = "Zoom + fundido",
            [AnimationKind.Pop] = "Rebote (pop)",
        };
        AddEnum(p, "Entrada", nameof(S.AnimationIn), kinds);
        AddEnum(p, "Salida", nameof(S.AnimationOut), kinds);
        AddSlider(p, "Duración (ms)", nameof(S.AnimationDurationMs), 0, 2000, 10, "0");
        AddSlider(p, "Distancia del deslizamiento (px)", nameof(S.SlideDistance), 0, 300, 1, "0");
        AddCheck(p, "Fundido del contenido al cambiar de canción", nameof(S.CrossfadeOnTrackChange));
    }

    private void BuildFullscreen()
    {
        var p = Tab("Pantalla completa");
        bool ui = Native.ProcessHasUIAccess();
        AddNote(p, ui
            ? "✔ Modo UIAccess ACTIVO: el popup está en la banda de ventanas de accesibilidad, por encima de juegos y apps a pantalla completa."
            : "Modo normal (sin UIAccess). El popup se muestra sobre juegos en 'ventana sin bordes' y sobre la mayoría de juegos DirectX 11/12 " +
              "a pantalla completa (Windows usa optimizaciones de pantalla completa / flip model). Para máxima compatibilidad instala con " +
              "tools\\Install-UIAccess.ps1 (ver README).");
        AddNote(p, "Juegos en pantalla completa EXCLUSIVA real (DirectX 9 antiguos, o con 'Deshabilitar optimizaciones de pantalla completa' marcado) " +
                "no permiten que ninguna ventana se dibuje encima sin inyectarse en el juego (como hace Steam/RTSS), lo que puede provocar baneos anti-cheat. " +
                "Solución: usar modo 'ventana sin bordes' en esos juegos.");
        AddCheck(p, "Forzar 'siempre encima' continuamente mientras se muestra", nameof(S.ForceTopmost));
        AddSlider(p, "Intervalo de refuerzo (ms)", nameof(S.TopmostIntervalMs), 16, 2000, 1, "0");
        var test = new Button { Content = "Probar: mostrar popup dentro de 5 segundos (cambia al juego)", HorizontalAlignment = HorizontalAlignment.Left, Padding = new Thickness(12, 4, 12, 4), Margin = new Thickness(0, 8, 0, 0) };
        test.Click += (_, _) =>
        {
            var t = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            t.Tick += (_, _) => { t.Stop(); _flyout.ShowFlyout(6000); };
            t.Start();
        };
        p.Children.Add(test);
    }

    private void BuildProfiles()
    {
        var p = Tab("Estilos / perfiles");
        AddHeader(p, "Estilos predefinidos (no cambian el comportamiento)");
        var list = new ListBox { Height = 260, Margin = new Thickness(0, 4, 0, 8) };
        foreach (var name in Presets.All.Keys) list.Items.Add(name);
        list.MouseDoubleClick += (_, _) => ApplyPreset(list.SelectedItem as string);
        p.Children.Add(list);
        var apply = new Button { Content = "Aplicar estilo seleccionado", HorizontalAlignment = HorizontalAlignment.Left, Padding = new Thickness(12, 4, 12, 4) };
        apply.Click += (_, _) => ApplyPreset(list.SelectedItem as string);
        p.Children.Add(apply);

        AddHeader(p, "Importar / exportar");
        var bar = new WrapPanel();
        AddBtn(bar, "Exportar a JSON…", () =>
        {
            var dlg = new Microsoft.Win32.SaveFileDialog { Filter = "JSON|*.json", FileName = "betterflyout-estilo.json" };
            if (dlg.ShowDialog(this) == true) File.WriteAllText(dlg.FileName, SettingsStore.Serialize(S));
        });
        AddBtn(bar, "Importar estilo (solo visual)…", () => Import(visualOnly: true));
        AddBtn(bar, "Importar todo…", () => Import(visualOnly: false));
        AddBtn(bar, "Restablecer todo", () =>
        {
            if (MessageBox.Show(this, "¿Restablecer TODA la configuración a los valores por defecto?", "BetterFlyout", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                S.CopyFrom(new FlyoutSettings());
        });
        AddBtn(bar, "Abrir carpeta de configuración", () => Process.Start(new ProcessStartInfo("explorer.exe", SettingsStore.Folder) { UseShellExecute = true }));
        p.Children.Add(bar);
        AddNote(p, "El archivo settings.json se puede editar a mano; los cambios se recargan al reiniciar la app.");
    }

    private void ApplyPreset(string? name)
    {
        if (name != null && Presets.All.TryGetValue(name, out var apply)) { apply(S); _flyout.ShowFlyout(); }
    }

    private void Import(bool visualOnly)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "JSON|*.json" };
        if (dlg.ShowDialog(this) != true) return;
        try
        {
            var other = SettingsStore.Deserialize(File.ReadAllText(dlg.FileName));
            if (other != null) { S.CopyFrom(other, visualOnly); _flyout.ShowFlyout(); }
        }
        catch (Exception ex) { MessageBox.Show(this, "No se pudo importar: " + ex.Message, "BetterFlyout"); }
    }

    // ═════════════════ Helpers de UI ═════════════════

    private StackPanel Tab(string header)
    {
        var sp = new StackPanel { Margin = new Thickness(16, 8, 16, 16) };
        _tabs.Items.Add(new TabItem { Header = header, Content = new ScrollViewer { Content = sp, VerticalScrollBarVisibility = ScrollBarVisibility.Auto } });
        return sp;
    }

    private Binding Bind(string prop, IValueConverter? conv = null) =>
        new(prop) { Source = S, Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged, Converter = conv };

    private static void AddHeader(Panel p, string text) =>
        p.Children.Add(new TextBlock { Text = text, FontSize = 16, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 14, 0, 6) });

    private static void AddNote(Panel p, string text) =>
        p.Children.Add(new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, Opacity = 0.75, Margin = new Thickness(0, 6, 0, 6) });

    private static void AddBtn(Panel p, string text, Action a)
    {
        var b = new Button { Content = text, Margin = new Thickness(0, 4, 8, 4), Padding = new Thickness(12, 4, 12, 4) };
        b.Click += (_, _) => a();
        p.Children.Add(b);
    }

    private static void AddRow(Panel p, string label, FrameworkElement control, FrameworkElement? extra = null)
    {
        var g = new Grid { Margin = new Thickness(0, 3, 0, 3) };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(260) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        g.Children.Add(new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 12, 0) });
        control.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(control, 1);
        g.Children.Add(control);
        if (extra != null) { Grid.SetColumn(extra, 2); extra.Margin = new Thickness(8, 0, 0, 0); g.Children.Add(extra); }
        p.Children.Add(g);
    }

    private void AddCheck(Panel p, string label, string prop)
    {
        var cb = new CheckBox { Content = label, Margin = new Thickness(0, 3, 0, 3) };
        cb.SetBinding(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty, Bind(prop));
        p.Children.Add(cb);
    }

    private void AddSlider(Panel p, string label, string prop, double min, double max, double step, string fmt)
    {
        var sl = new Slider { Minimum = min, Maximum = max, SmallChange = step, LargeChange = step * 10, TickFrequency = step, IsSnapToTickEnabled = true };
        sl.SetBinding(RangeBase.ValueProperty, Bind(prop));
        var box = new TextBox { Width = 70 };
        box.SetBinding(TextBox.TextProperty, new Binding(prop) { Source = S, Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.LostFocus, StringFormat = fmt, ConverterCulture = CultureInfo.CurrentCulture });
        box.KeyDown += (_, e) => { if (e.Key == System.Windows.Input.Key.Enter) box.GetBindingExpression(TextBox.TextProperty)?.UpdateSource(); };
        AddRow(p, label, sl, box);
    }

    private void AddText(Panel p, string label, string prop)
    {
        var tb = new TextBox();
        tb.SetBinding(TextBox.TextProperty, new Binding(prop) { Source = S, Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.LostFocus });
        tb.KeyDown += (_, e) => { if (e.Key == System.Windows.Input.Key.Enter) tb.GetBindingExpression(TextBox.TextProperty)?.UpdateSource(); };
        AddRow(p, label, tb);
    }

    private void AddFont(Panel p, string label, string prop)
    {
        var cb = new ComboBox { IsEditable = true, IsTextSearchEnabled = true };
        foreach (var f in Fonts.SystemFontFamilies.Select(f => f.Source).OrderBy(x => x)) cb.Items.Add(f);
        cb.SetBinding(ComboBox.TextProperty, new Binding(prop) { Source = S, Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.LostFocus });
        cb.SelectionChanged += (_, _) => { if (cb.SelectedItem is string s) S.FontFamily = s; };
        AddRow(p, label, cb);
    }

    private void AddEnum<T>(Panel p, string label, string prop, Dictionary<T, string> names) where T : struct, System.Enum
    {
        var cb = new ComboBox { DisplayMemberPath = "Value", SelectedValuePath = "Key", ItemsSource = names.ToList() };
        cb.SetBinding(System.Windows.Controls.Primitives.Selector.SelectedValueProperty, Bind(prop));
        AddRow(p, label, cb);
    }

    /// <summary>Editor de color: hex #AARRGGBB + muestra + selector + opacidad.</summary>
    private void AddColor(Panel p, string label, string prop)
    {
        var pi = typeof(FlyoutSettings).GetProperty(prop)!;
        string Get() => (string)(pi.GetValue(S) ?? "#FF000000");
        void SetVal(string v) => pi.SetValue(S, v);

        var panel = new DockPanel();
        var swatch = new Border { Width = 28, Height = 22, BorderThickness = new Thickness(1), BorderBrush = Brushes.Gray, CornerRadius = new CornerRadius(4), Margin = new Thickness(0, 0, 8, 0), Cursor = System.Windows.Input.Cursors.Hand };
        var hex = new TextBox { Width = 100, Margin = new Thickness(0, 0, 8, 0) };
        var alpha = new Slider { Minimum = 0, Maximum = 255, SmallChange = 1, LargeChange = 16, IsSnapToTickEnabled = true, TickFrequency = 1, ToolTip = "Opacidad del color" };
        DockPanel.SetDock(swatch, Dock.Left); DockPanel.SetDock(hex, Dock.Left);
        panel.Children.Add(swatch); panel.Children.Add(hex); panel.Children.Add(alpha);

        bool sync = false;
        void Refresh()
        {
            sync = true;
            var c = ColorUtil.Parse(Get(), Colors.Black);
            swatch.Background = new SolidColorBrush(c);
            if (!hex.IsKeyboardFocusWithin) hex.Text = Get();
            alpha.Value = c.A;
            sync = false;
        }
        Refresh();
        ((INotifyPropertyChanged)S).PropertyChanged += (_, e) => { if (e.PropertyName == prop) Refresh(); };

        void Commit()
        {
            var txt = hex.Text.Trim();
            if (!txt.StartsWith('#')) txt = "#" + txt;
            try { if (ColorConverter.ConvertFromString(txt) is System.Windows.Media.Color) SetVal(txt.ToUpperInvariant()); } catch { }
            Refresh();
        }
        hex.LostFocus += (_, _) => Commit();
        hex.KeyDown += (_, e) => { if (e.Key == System.Windows.Input.Key.Enter) Commit(); };
        alpha.ValueChanged += (_, _) =>
        {
            if (sync) return;
            var c = ColorUtil.Parse(Get(), Colors.Black);
            SetVal(ColorUtil.ToHex(ColorUtil.WithAlpha(c, (byte)alpha.Value)));
        };
        swatch.MouseLeftButtonUp += (_, _) =>
        {
            var c = ColorUtil.Parse(Get(), Colors.Black);
            using var dlg = new System.Windows.Forms.ColorDialog { FullOpen = true, AnyColor = true, Color = System.Drawing.Color.FromArgb(c.R, c.G, c.B) };
            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                SetVal(ColorUtil.ToHex(System.Windows.Media.Color.FromArgb(c.A, dlg.Color.R, dlg.Color.G, dlg.Color.B)));
        };
        AddRow(p, label, panel);
    }
}
