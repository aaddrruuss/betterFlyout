// BetterFlyout - Copyright (C) 2026 aaddrruuss (https://github.com/aaddrruuss/)
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Collections.Generic;
using System.Globalization;

namespace BetterFlyout.Util;

public enum AppLanguage { System, Spanish, English }

/// <summary>
/// Traducciones estilo gettext: el texto en español es la clave.
/// Para añadir un idioma: crear otro diccionario (español → idioma) y añadirlo en <see cref="Set"/>.
/// Si falta una traducción se muestra el texto en español.
/// </summary>
public static class Loc
{
    private static Dictionary<string, string>? _map;

    /// <summary>Código ISO del idioma activo ("es", "en").</summary>
    public static string Code { get; private set; } = "es";

    /// <summary>Claves pedidas sin traducción (para --i18n-check).</summary>
    public static readonly SortedSet<string> Missing = new();

    public static void Set(AppLanguage lang)
    {
        Code = lang switch
        {
            AppLanguage.Spanish => "es",
            AppLanguage.English => "en",
            _ => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "es" ? "es" : "en",
        };
        _map = Code switch { "en" => En, _ => null };
    }

    public static string T(string es)
    {
        if (_map == null || string.IsNullOrEmpty(es)) return es;
        if (_map.TryGetValue(es, out var t)) return t;
        Missing.Add(es);
        return es;
    }

    private static readonly Dictionary<string, string> En = new()
    {
        // ── Ventana / general ──
        ["BetterFlyout · Configuración"] = "BetterFlyout · Settings",
        ["Vista previa fija (el popup se queda visible)"] = "Pinned preview (the popup stays visible)",
        ["Mostrar popup ahora"] = "Show popup now",
        [" · Software libre bajo licencia GPL-3.0-or-later, SIN NINGUNA GARANTÍA. Puedes redistribuirlo y modificarlo bajo sus términos (ver LICENSE)."] =
            " · Free software under the GPL-3.0-or-later license, WITHOUT ANY WARRANTY. You can redistribute and modify it under its terms (see LICENSE).",
        ["Idioma / Language"] = "Language / Idioma",
        ["Automático (idioma de Windows)"] = "Automatic (Windows language)",
        ["Español"] = "Español", // los nombres de idioma se muestran siempre en su propio idioma
        ["English"] = "English",

        // ── Pestañas ──
        ["General"] = "General",
        ["Posición"] = "Position",
        ["Diseño"] = "Layout",
        ["Fondo"] = "Background",
        ["Carátula"] = "Album art",
        ["Texto"] = "Text",
        ["Controles"] = "Controls",
        ["Volumen"] = "Volume",
        ["Animación"] = "Animation",
        ["Pantalla completa"] = "Fullscreen",
        ["Estilos / perfiles"] = "Styles / profiles",

        // ── General ──
        ["Cuándo aparece el popup"] = "When the popup shows up",
        ["Popups activados"] = "Popups enabled",
        ["Al cambiar de canción manualmente (teclas multimedia / botones)"] = "On manual track change (media keys / buttons)",
        ["Al cambiar de canción automáticamente (termina una y empieza otra)"] = "On automatic track change (one song ends and the next starts)",
        ["Al pausar"] = "On pause",
        ["Al reanudar"] = "On resume",
        ["Al pulsar cualquier tecla multimedia"] = "When any media key is pressed",
        ["Al pulsar teclas de volumen"] = "When volume keys are pressed",
        ["Al cambiar de aplicación de música"] = "When the music app changes",
        ["No mostrar si no hay nada reproduciéndose"] = "Don't show if nothing is playing",
        ["Solo cuando hay una app a pantalla completa"] = "Only when an app is fullscreen",
        ["Ventana para considerar un cambio 'manual' (ms)"] = "Time window to count a change as 'manual' (ms)",
        ["Duración e interacción"] = "Duration and interaction",
        ["Tiempo visible (ms)"] = "Visible time (ms)",
        ["Tiempo visible al pausar (ms)"] = "Visible time on pause (ms)",
        ["Mantener abierto mientras el ratón está encima"] = "Keep open while the mouse is over it",
        ["Clic derecho cierra el popup"] = "Right-click closes the popup",
        ["Clic a través (el popup ignora el ratón)"] = "Click-through (the popup ignores the mouse)",
        ["Atajo global para mostrar (ej. Ctrl+Alt+M, vacío = ninguno)"] = "Global hotkey to show (e.g. Ctrl+Alt+M, empty = none)",
        ["Iniciar con Windows"] = "Start with Windows",

        // ── Posición ──
        ["Monitor"] = "Monitor",
        ["Principal"] = "Primary",
        ["Donde está el ratón"] = "Where the mouse is",
        ["Donde está la ventana activa"] = "Where the active window is",
        ["Monitor concreto"] = "Specific monitor",
        ["(principal)"] = "(primary)",
        ["Anclaje"] = "Anchor",
        ["Arriba izquierda"] = "Top left",
        ["Arriba centro"] = "Top center",
        ["Arriba derecha"] = "Top right",
        ["Centro izquierda"] = "Middle left",
        ["Centro"] = "Center",
        ["Centro derecha"] = "Middle right",
        ["Abajo izquierda"] = "Bottom left",
        ["Abajo centro"] = "Bottom center",
        ["Abajo derecha"] = "Bottom right",
        ["Margen / desplazamiento X (px)"] = "Margin / offset X (px)",
        ["Margen / desplazamiento Y (px)"] = "Margin / offset Y (px)",
        ["Respetar la barra de tareas (área de trabajo)"] = "Respect the taskbar (work area)",

        // ── Diseño ──
        ["Clásico Windows 10"] = "Windows 10 classic",
        ["Horizontal moderno"] = "Modern horizontal",
        ["Vertical (tarjeta)"] = "Vertical (card)",
        ["Compacto (una línea)"] = "Compact (single line)",
        ["Solo carátula"] = "Album art only",
        ["Solo texto"] = "Text only",
        ["Ancho (px)"] = "Width (px)",
        ["Alto mínimo (px)"] = "Minimum height (px)",
        ["Escala global"] = "Global scale",
        ["Relleno interior (px)"] = "Inner padding (px)",
        ["Separación entre elementos (px)"] = "Spacing between elements (px)",
        ["Radio de esquinas (px)"] = "Corner radius (px)",
        ["Opacidad general"] = "Overall opacity",
        ["Alineación del texto"] = "Text alignment",
        ["Izquierda"] = "Left",
        ["Derecha"] = "Right",

        // ── Fondo ──
        ["Color de fondo"] = "Background color",
        ["Degradado"] = "Gradient",
        ["Segundo color del degradado"] = "Second gradient color",
        ["Ángulo del degradado"] = "Gradient angle",
        ["Teñir el fondo con el color de la carátula"] = "Tint the background with the album art color",
        ["Intensidad del tinte"] = "Tint strength",
        ["Carátula difuminada de fondo"] = "Blurred album art background",
        ["Desenfoque de la carátula de fondo"] = "Background album art blur",
        ["Opacidad de la carátula de fondo"] = "Background album art opacity",
        ["Desenfocar lo de detrás de la ventana (experimental, sin sombra)"] = "Blur what's behind the window (experimental, no shadow)",
        ["Borde"] = "Border",
        ["Color del borde"] = "Border color",
        ["Grosor del borde"] = "Border thickness",
        ["Sombra"] = "Shadow",
        ["Color de la sombra"] = "Shadow color",
        ["Difuminado de la sombra"] = "Shadow blur",
        ["Opacidad de la sombra"] = "Shadow opacity",
        ["Desplazamiento de la sombra"] = "Shadow offset",

        // ── Carátula ──
        ["Mostrar carátula"] = "Show album art",
        ["Tamaño (px)"] = "Size (px)",
        ["Radio de esquinas (999 = círculo)"] = "Corner radius (999 = circle)",
        ["Sombra de la carátula"] = "Album art shadow",
        ["Girar mientras suena (efecto vinilo)"] = "Spin while playing (vinyl effect)",
        ["Atenuar al pausar"] = "Dim when paused",
        ["Icono de pausa sobre la carátula"] = "Pause icon over the album art",

        // ── Texto ──
        ["Fuente (admite lista: 'A, B')"] = "Font (accepts a list: 'A, B')",
        ["Desplazar texto largo (marquee)"] = "Scroll long text (marquee)",
        ["Velocidad del desplazamiento (px/s)"] = "Scroll speed (px/s)",
        ["Sombra en el texto"] = "Text shadow",
        ["Fina"] = "Light",
        ["Normal"] = "Normal",
        ["Seminegrita"] = "Semibold",
        ["Negrita"] = "Bold",
        ["Título"] = "Title",
        ["Tamaño"] = "Size",
        ["Color"] = "Color",
        ["Grosor"] = "Weight",
        ["Varias líneas (en vez de desplazar/recortar)"] = "Multiple lines (instead of scrolling/trimming)",
        ["Artista"] = "Artist",
        ["Mostrar artista"] = "Show artist",
        ["Álbum"] = "Album",
        ["Mostrar álbum"] = "Show album",
        ["Nombre de la app"] = "App name",
        ["Mostrar nombre de la app (Spotify, Chrome…)"] = "Show app name (Spotify, Chrome…)",
        ["En mayúsculas"] = "Uppercase",

        // ── Controles ──
        ["Botones"] = "Buttons",
        ["Mostrar botones (anterior / play / siguiente)"] = "Show buttons (previous / play / next)",
        ["Estilo"] = "Style",
        ["Contorno (Windows 10)"] = "Outline (Windows 10)",
        ["Relleno"] = "Filled",
        ["Solo icono"] = "Icon only",
        ["Tamaño del icono (proporción)"] = "Icon size (ratio)",
        ["Grosor del contorno"] = "Outline thickness",
        ["Separación"] = "Spacing",
        ["Color del icono"] = "Icon color",
        ["Fondo al pasar el ratón"] = "Hover background",
        ["Botón play con color de acento"] = "Accent-colored play button",
        ["Color de acento"] = "Accent color",
        ["Usar el color de la carátula"] = "Use the album art color",
        ["Usar el color de acento de Windows"] = "Use the Windows accent color",
        ["Color de acento personalizado"] = "Custom accent color",
        ["Progreso"] = "Progress",
        ["Barra de progreso"] = "Progress bar",
        ["Barra"] = "Bar",
        ["Línea en el borde inferior"] = "Line on the bottom edge",
        ["Ninguna"] = "None",
        ["Color del fondo de la barra"] = "Bar track color",
        ["Mostrar tiempos"] = "Show times",
        ["Mostrar tiempo restante en vez de duración"] = "Show remaining time instead of duration",
        ["Tamaño de los tiempos"] = "Time text size",
        ["Color de los tiempos"] = "Time text color",

        // ── Volumen ──
        ["Sustituir los flyouts de Windows"] = "Replace the Windows flyouts",
        ["Ocultar el flyout de volumen de Windows (BetterFlyout gestiona las teclas de volumen)"] = "Hide the Windows volume flyout (BetterFlyout handles the volume keys)",
        ["Paso de volumen por pulsación (%)"] = "Volume step per key press (%)",
        ["Ocultar el flyout multimedia de Windows (BetterFlyout gestiona play/pausa/siguiente/anterior)"] = "Hide the Windows media flyout (BetterFlyout handles play/pause/next/previous)",
        ["Funciona interceptando las teclas antes de que lleguen a Windows. Si cierras BetterFlyout, todo vuelve a ser como antes. Las teclas multimedia solo se interceptan si hay música en una app compatible; si no, pasan a Windows."] =
            "It works by intercepting the keys before they reach Windows. If you close BetterFlyout, everything goes back to normal. Media keys are only intercepted when music is playing in a supported app; otherwise they go to Windows.",
        ["Panel"] = "Panel",
        ["Mostrar panel de volumen (como Windows 10)"] = "Show volume panel (like Windows 10)",
        ["Panel a la derecha"] = "Panel on the right",
        ["Ancho del panel"] = "Panel width",
        ["Grosor de la barra"] = "Bar thickness",
        ["Separación entre paneles"] = "Gap between panels",
        ["Tamaño del número"] = "Number size",
        ["Color del número"] = "Number color",
        ["Arrastra la barra o usa la rueda del ratón sobre el popup para cambiar el volumen. Clic en el número = silenciar."] =
            "Drag the bar or use the mouse wheel over the popup to change the volume. Click the number = mute.",

        // ── Animación ──
        ["Fundido"] = "Fade",
        ["Deslizar hacia arriba"] = "Slide up",
        ["Deslizar hacia abajo"] = "Slide down",
        ["Deslizar a la izquierda"] = "Slide left",
        ["Deslizar a la derecha"] = "Slide right",
        ["Zoom"] = "Zoom",
        ["Zoom + fundido"] = "Zoom + fade",
        ["Rebote (pop)"] = "Bounce (pop)",
        ["Entrada"] = "In",
        ["Salida"] = "Out",
        ["Duración (ms)"] = "Duration (ms)",
        ["Distancia del deslizamiento (px)"] = "Slide distance (px)",
        ["Fundido del contenido al cambiar de canción"] = "Crossfade content on track change",

        // ── Pantalla completa ──
        ["✔ Modo UIAccess ACTIVO: el popup está en la banda de ventanas de accesibilidad, por encima de juegos y apps a pantalla completa."] =
            "✔ UIAccess mode ACTIVE: the popup lives in the accessibility window band, above games and fullscreen apps.",
        ["Modo normal (sin UIAccess). El popup se muestra sobre juegos en 'ventana sin bordes' y sobre la mayoría de juegos DirectX 11/12 a pantalla completa (Windows usa optimizaciones de pantalla completa / flip model). Para máxima compatibilidad instala con tools\\Install-UIAccess.ps1 (ver README)."] =
            "Normal mode (no UIAccess). The popup shows over games in 'borderless window' mode and over most DirectX 11/12 fullscreen games (Windows uses fullscreen optimizations / flip model). For maximum compatibility install with tools\\Install-UIAccess.ps1 (see README).",
        ["Juegos en pantalla completa EXCLUSIVA real (DirectX 9 antiguos, o con 'Deshabilitar optimizaciones de pantalla completa' marcado) no permiten que ninguna ventana se dibuje encima sin inyectarse en el juego (como hace Steam/RTSS), lo que puede provocar baneos anti-cheat. Solución: usar modo 'ventana sin bordes' en esos juegos."] =
            "Games in true EXCLUSIVE fullscreen (old DirectX 9, or with 'Disable fullscreen optimizations' checked) don't allow any window to draw on top without injecting into the game (like Steam/RTSS do), which can trigger anti-cheat bans. Solution: use 'borderless window' mode in those games.",
        ["Forzar 'siempre encima' continuamente mientras se muestra"] = "Keep forcing 'always on top' while visible",
        ["Rendimiento"] = "Performance",
        ["Dibujar el popup con"] = "Draw the popup with",
        ["Tarjeta gráfica (GPU)"] = "Graphics card (GPU)",
        ["Procesador (CPU)"] = "Processor (CPU)",
        ["Automático (CPU si la GPU está saturada)"] = "Automatic (CPU when the GPU is saturated)",
        ["Automático: mide el uso de la GPU (el mismo dato que el Administrador de tareas) cada 2 segundos y, al aparecer el popup, elige CPU si la GPU está por encima del umbral y GPU si no. Vuelve a GPU cuando el uso baja 10 puntos por debajo del umbral, para no estar cambiando sin parar. La medición gasta muy poco (unos milisegundos cada 2 segundos) y solo está activa en este modo."] =
            "Automatic: measures GPU usage (the same figure as Task Manager) every 2 seconds and, when the popup appears, picks CPU if the GPU is above the threshold and GPU otherwise. It goes back to GPU once usage drops 10 points below the threshold, so it doesn't keep switching back and forth. Measuring costs very little (a few milliseconds every 2 seconds) and only runs in this mode.",
        ["Automático: pasar a CPU con la GPU al (%)"] = "Automatic: switch to CPU when the GPU is at (%)",
        ["Uso de la GPU: midiendo…"] = "GPU usage: measuring…",
        ["Uso de la GPU: {0:0} % → el popup se dibujará con {1}"] = "GPU usage: {0:0}% → the popup will be drawn with {1}",
        ["GPU (por defecto): animaciones más suaves y efectos (sombra, desenfoque) casi gratis, con muy poco uso del procesador. Inconveniente: comparte la tarjeta gráfica con el juego; si el juego la usa al 100 %, el popup y el juego pueden ir a tirones."] =
            "GPU (default): smoother animations and nearly free effects (shadow, blur), with very little CPU usage. Downside: it shares the graphics card with the game; if the game uses it at 100%, both the popup and the game may stutter.",
        ["CPU: el popup no usa la tarjeta gráfica para dibujarse, así que no le quita recursos a juegos que la tienen al 100 %. Inconveniente: gasta algo más de procesador mientras se ve (más con sombra, carátula difuminada, texto en movimiento o FPS altos) y las animaciones pueden ir algo menos fluidas. Windows sigue usando la gráfica, de forma mínima, para mostrarlo en pantalla."] =
            "CPU: the popup doesn't use the graphics card to draw itself, so it doesn't take resources from games running it at 100%. Downside: it uses a bit more CPU while visible (more with shadow, blurred album art, scrolling text or high FPS) and animations may be slightly less smooth. Windows still uses the graphics card, minimally, to put it on screen.",
        ["FPS máximos de las animaciones"] = "Maximum animation FPS",
        ["Siempre encima"] = "Always on top",
        ["Intervalo de refuerzo (ms)"] = "Enforcement interval (ms)",
        ["Probar: mostrar popup dentro de 5 segundos (cambia al juego)"] = "Test: show popup in 5 seconds (switch to the game)",

        // ── Estilos / perfiles ──
        ["Estilos predefinidos (no cambian el comportamiento)"] = "Built-in styles (don't change behavior)",
        ["Aplicar estilo seleccionado"] = "Apply selected style",
        ["Importar / exportar"] = "Import / export",
        ["Exportar a JSON…"] = "Export to JSON…",
        ["Importar estilo (solo visual)…"] = "Import style (visual only)…",
        ["Importar todo…"] = "Import everything…",
        ["Restablecer todo"] = "Reset everything",
        ["¿Restablecer TODA la configuración a los valores por defecto?"] = "Reset ALL settings to their default values?",
        ["Abrir carpeta de configuración"] = "Open settings folder",
        ["El archivo settings.json se puede editar a mano; los cambios se recargan al reiniciar la app."] = "The settings.json file can be edited by hand; changes are reloaded when the app restarts.",
        ["No se pudo importar: "] = "Could not import: ",
        ["Opacidad del color"] = "Color opacity",

        // ── Nombres de estilos predefinidos ──
        ["Windows 10 clásico, estilo 11 (por defecto)"] = "Windows 10 classic, 11 style (default)",
        ["Windows 10 original (esquinas rectas)"] = "Windows 10 original (square corners)",
        ["Windows 10 clásico claro"] = "Windows 10 classic light",
        ["Moderno (tarjeta)"] = "Modern (card)",
        ["Moderno claro"] = "Modern light",
        ["Cristal (carátula de fondo)"] = "Glass (album art background)",
        ["Tarjeta vertical"] = "Vertical card",
        ["Compacto (esquina)"] = "Compact (corner)",
        ["Minimal (solo texto)"] = "Minimal (text only)",
        ["Neón"] = "Neon",
        ["Vinilo"] = "Vinyl",

        // ── Bandeja ──
        ["Mostrar popup"] = "Show popup",
        ["Configuración…"] = "Settings…",
        ["Pausar popups"] = "Pause popups",
        ["Salir"] = "Exit",

        // ── Popup ──
        ["Nada en reproducción"] = "Nothing playing",
        ["Reproduciendo"] = "Now playing",
        ["En pausa"] = "Paused",
        ["Nombre de la canción (vista previa)"] = "Song title (preview)",
        ["Artista de ejemplo"] = "Sample artist",
        ["Álbum de ejemplo"] = "Sample album",
        ["Reproductor multimedia"] = "Media Player",
    };
}
