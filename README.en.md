# BetterFlyout

*[Leer en español](README.md)*

"Now playing" popup for Windows 10/11, styled after the Windows 10 media flyout but with a Windows 11 finish.
It shows on top of games and fullscreen apps, and every aspect of it is customizable.

**Stack:** C# / .NET 10 + WPF, using the WinRT `GlobalSystemMediaTransportControlsSessionManager` API (the same one used by
the Windows flyout and FluentFlyout). Works with Spotify, browsers (YouTube, etc.), Apple Music, TIDAL, VLC, foobar2000…

## Requirements and usage

- Windows 10 2004+ / Windows 11
- .NET 10 SDK to build (`dotnet --list-sdks`)

```powershell
dotnet build -c Release
.\bin\Release\net10.0-windows10.0.19041.0\BetterFlyout.exe
```

It lives in the system tray:
- **Click** the icon: show the popup
- **Double-click**: open settings
- **Right-click**: menu (show, settings, pause popups, exit)
- Launching it again opens settings in the instance that is already running.

## When it shows up (all configurable)

- **Manual** track change (media keys or the popup's buttons)
- **Automatic** track change (one song ends and the next one starts)
- On pause / on resume (each one separately, with its own duration)
- When pressing media keys or volume keys
- When the music app changes
- Global hotkey (default `Ctrl+Alt+M`)
- Optional: only when something is running fullscreen

## Customization

Everything applies live (use "Vista previa fija" / pinned preview in settings) and is saved to `%APPDATA%\BetterFlyout\settings.json`.

- **Position:** 9 anchors, X/Y margins, monitor (primary / mouse / active window / specific), respect the taskbar or not
- **Layout:** Win10 classic, horizontal, vertical, compact, artwork only, text only; width, min height, scale, padding, spacing, corners, opacity
- **Background:** color with alpha, gradient, tint from the album art color, blurred album art background, border, shadow (color, blur, opacity, offset)
- **Album art:** size, corner radius (up to a circle), shadow, vinyl-style spin, dim when paused, pause badge
- **Text:** font, size/color/weight for title, artist, album and app name, scrolling (marquee) or multi-line text, text shadow
- **Controls:** outline / filled / icon-only style, size, stroke, colors, hover, accent (album art / Windows / custom)
- **Progress:** bar, edge line or none; elapsed/total/remaining time
- **Volume:** Win10-style panel (left/right), draggable, mouse wheel, click the number to mute
- **Animation:** separate in and out animations (fade, slide in 4 directions, zoom, pop), duration, distance
- **Built-in styles**, JSON export/import

## Fullscreen / DirectX

The popup is a `WS_EX_TOPMOST | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW` window that never activates (it doesn't steal focus
from the game or minimize it) and keeps re-asserting itself on top while visible.

| Game mode | Is the popup visible? |
|---|---|
| Borderless window | Yes |
| DX11/DX12 fullscreen on Win10/11 (fullscreen optimizations, flip model) | Yes, in most games |
| Same as above, with **UIAccess mode** | Yes, in the accessibility window band, above every other "always on top" window |
| True **exclusive** fullscreen (old DX9, or with "Disable fullscreen optimizations" checked) | No. No window can draw there without injecting into the game (like Steam/RTSS), which can trigger anti-cheat bans. Use borderless window. |

### UIAccess mode (recommended for games)

In PowerShell **as administrator**:

```powershell
.\tools\Install-UIAccess.ps1
```

The script builds with `uiAccess="true"`, creates a self-signed certificate, signs the `.exe`, **deletes the private key**
(so nobody can reuse it to sign anything else) and installs the app to `C:\Program Files\BetterFlyout`. Run the app from
there, without admin rights. To remove it: `.\tools\Uninstall-UIAccess.ps1`.

## Snapshot tool

Renders the popup to a PNG without opening the app (handy for testing styles):

```powershell
BetterFlyout.exe --snapshot shot.png                          # default style
BetterFlyout.exe --snapshot shot.png --preset "Neón"          # a built-in style
BetterFlyout.exe --snapshot shot.png --saved                  # your saved settings
```

## Project layout

```
src/App.cs                     Entry point, single instance, tray and show logic
src/Services/MediaService.cs   Media sessions (GSMTC), album art and dominant color
src/Services/AudioService.cs   System volume (Core Audio)
src/Services/InputHooks.cs     Media/volume key hook and global hotkey
src/Views/FlyoutWindow.cs      The popup (UI generated from the settings)
src/Views/Controls.cs          Marquee text, icon buttons
src/Views/SettingsWindow.cs    Settings window
src/Settings/*                 Settings model, persistence and built-in styles
tools/*.ps1                    UIAccess install/uninstall
```

> Note: the settings window and the built-in style names are in Spanish.

## License

Copyright (C) 2026 [aaddrruuss](https://github.com/aaddrruuss/)

BetterFlyout is free software: you can use, redistribute and modify it under the terms of the
[GNU General Public License v3.0](LICENSE) (or, at your option, any later version).

In short:
- ✅ You may use, copy, modify and distribute it, including for commercial purposes.
- 📌 You must keep the copyright notice and credit the original author.
- 🔓 If you distribute a modified version, it must also be released under GPL-3.0 with its source code.
- ⚠️ It comes with no warranty.

See the [LICENSE](LICENSE) file for the full legal text.
