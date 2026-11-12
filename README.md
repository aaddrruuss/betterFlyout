# BetterFlyout

*[Read in English](README.en.md)*

Popup de "ahora suena" para Windows 10/11, al estilo del flyout multimedia de Windows 10 pero con acabado Windows 11.
Se muestra encima de juegos y apps a pantalla completa, y se puede personalizar todo.

**Stack:** C# / .NET 10 + WPF, con WinRT `GlobalSystemMediaTransportControlsSessionManager` (la misma API que usan el flyout
de Windows y FluentFlyout). Funciona con Spotify, navegadores (YouTube, etc.), Apple Music, TIDAL, VLC, foobar2000…

## Requisitos y uso

- Windows 10 2004+ / Windows 11
- .NET 10 SDK para compilar (`dotnet --list-sdks`)

```powershell
dotnet build -c Release
.\bin\Release\net10.0-windows10.0.19041.0\BetterFlyout.exe
```

Se queda en la bandeja del sistema:
- **Clic** en el icono: muestra el popup
- **Doble clic**: abre la configuración
- **Clic derecho**: menú (mostrar, configuración, pausar popups, salir)
- Ejecutarlo otra vez abre la configuración en la instancia que ya está en marcha.

## Cuándo aparece (todo configurable)

- Cambio de canción **manual** (teclas multimedia o botones del popup)
- Cambio de canción **automático** (acaba una canción y empieza la siguiente)
- Al pausar / al reanudar (cada uno por separado, con su propia duración)
- Al pulsar teclas multimedia o de volumen
- Al cambiar de app de música
- Atajo global (por defecto `Ctrl+Alt+M`)
- Opcional: solo cuando hay algo a pantalla completa

## Personalización

Todo se aplica en vivo (usa "Vista previa fija" en la configuración) y se guarda en `%APPDATA%\BetterFlyout\settings.json`.

- **Idioma:** español o inglés (Configuración → General → Idioma). "Automático" usa el idioma de Windows.
  Para añadir otro idioma: crea un diccionario nuevo en `src/Util/Loc.cs` y comprueba que no falta nada con
  `BetterFlyout.exe --i18n-check faltan.txt`.

- **Posición:** 9 anclajes, márgenes X/Y, monitor (principal / ratón / ventana activa / concreto), respetar o no la barra de tareas
- **Diseño:** Clásico Win10, horizontal, vertical, compacto, solo carátula, solo texto; ancho, alto mínimo, escala, relleno, separación, esquinas, opacidad
- **Fondo:** color con alfa, degradado, tinte con el color de la carátula, carátula difuminada de fondo, borde, sombra (color, difuminado, opacidad, desplazamiento)
- **Carátula:** tamaño, esquinas (hasta círculo), sombra, giro tipo vinilo, atenuar al pausar, icono de pausa
- **Texto:** fuente, tamaño/color/grosor de título, artista, álbum y nombre de la app, texto en desplazamiento (marquee) o en varias líneas, sombra
- **Controles:** estilo contorno / relleno / solo icono, tamaño, grosor, colores, hover, acento (carátula / Windows / personalizado)
- **Progreso:** barra, línea en el borde o ninguno; tiempos transcurrido/total/restante
- **Volumen:** panel estilo Win10 (izquierda/derecha), arrastrable, rueda del ratón, clic en el número para silenciar
- **Animación:** entrada y salida por separado (fundido, deslizar en 4 direcciones, zoom, pop), duración, distancia
- **Estilos predefinidos**, exportar/importar JSON

## Pantalla completa / DirectX

El popup es una ventana `WS_EX_TOPMOST | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW`, sin activación (no le quita el foco al juego
ni lo minimiza), y vuelve a ponerse encima de forma continua mientras se muestra.

| Modo del juego | ¿Se ve el popup? |
|---|---|
| Ventana sin bordes | Sí |
| Pantalla completa DX11/DX12 en Win10/11 (optimizaciones de pantalla completa, flip model) | Sí, en la mayoría de juegos |
| Igual que arriba, con **modo UIAccess** | Sí, en la banda de ventanas de accesibilidad, por encima de las demás ventanas "siempre encima" |
| Pantalla completa **exclusiva** real (DX9 antiguo, o con "Deshabilitar optimizaciones de pantalla completa") | No. Ninguna ventana puede dibujarse ahí sin inyectarse en el juego (como Steam/RTSS), y eso puede provocar baneos anti-cheat. Usa ventana sin bordes. |

### Modo UIAccess (recomendado para juegos)

En PowerShell **como administrador**:

```powershell
.\tools\Install-UIAccess.ps1
```

El script compila con `uiAccess="true"`, crea un certificado autofirmado, firma el `.exe`, **borra la clave privada** (nadie
puede reutilizarla para firmar otra cosa) e instala la app en `C:\Program Files\BetterFlyout`. Ejecuta la app desde ahí, sin
permisos de administrador. Para quitarlo: `.\tools\Uninstall-UIAccess.ps1`.

## Herramienta de capturas

Genera un PNG del popup sin abrir la app (útil para probar estilos):

```powershell
BetterFlyout.exe --snapshot captura.png                      # estilo por defecto
BetterFlyout.exe --snapshot captura.png --preset "Neón"      # un estilo predefinido
BetterFlyout.exe --snapshot captura.png --saved              # tu configuración guardada
```

## Estructura

```
src/App.cs                     Entrada, instancia única, bandeja y lógica de cuándo mostrar
src/Services/MediaService.cs   Sesiones multimedia (GSMTC), carátula y color dominante
src/Services/AudioService.cs   Volumen del sistema (Core Audio)
src/Services/InputHooks.cs     Hook de teclas multimedia/volumen y atajo global
src/Views/FlyoutWindow.cs      El popup (UI generada a partir de la configuración)
src/Views/Controls.cs          Texto con marquee, botones con icono
src/Views/SettingsWindow.cs    Ventana de configuración
src/Settings/*                 Modelo de opciones, guardado y estilos predefinidos
tools/*.ps1                    Instalación con UIAccess
```

## Licencia

Copyright (C) 2026 [aaddrruuss](https://github.com/aaddrruuss/)

BetterFlyout es software libre: puedes usarlo, redistribuirlo y modificarlo bajo los términos de la
[GNU General Public License v3.0](LICENSE) (o, a tu elección, cualquier versión posterior).

En resumen:
- ✅ Puedes usarlo, copiarlo, modificarlo y distribuirlo, incluso con fines comerciales.
- 📌 Debes mantener el aviso de copyright y los créditos al autor original.
- 🔓 Si distribuyes una versión modificada, debe publicarse también bajo GPL-3.0 con su código fuente.
- ⚠️ Se ofrece sin ninguna garantía.

Consulta el archivo [LICENSE](LICENSE) para el texto legal completo.
