// BetterFlyout - Copyright (C) 2026 aaddrruuss (https://github.com/aaddrruuss/)
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using BetterFlyout.Interop;
using BetterFlyout.Settings;

namespace BetterFlyout.Services;

public enum MediaKey { PlayPause, Next, Previous, Stop, VolumeUp, VolumeDown, VolumeMute }

public static class MediaKeyExt
{
    public static bool IsVolume(this MediaKey k) => k is MediaKey.VolumeUp or MediaKey.VolumeDown or MediaKey.VolumeMute;
}

/// <summary>
/// Hook de teclado de bajo nivel: detecta teclas multimedia sin consumirlas (la app de música sigue recibiéndolas).
/// </summary>
public sealed class MediaKeyHook : IDisposable
{
    private const int VK_VOLUME_MUTE = 0xAD, VK_VOLUME_DOWN = 0xAE, VK_VOLUME_UP = 0xAF;
    private const int VK_MEDIA_NEXT_TRACK = 0xB0, VK_MEDIA_PREV_TRACK = 0xB1, VK_MEDIA_STOP = 0xB2, VK_MEDIA_PLAY_PAUSE = 0xB3;
    private readonly Native.LowLevelKeyboardProc _proc;
    private readonly Dispatcher _ui;
    private IntPtr _hook;

    public event Action<MediaKey>? KeyPressed;

    /// <summary>
    /// Si devuelve true para una tecla, se "traga" (Windows no la recibe y no muestra su propio flyout).
    /// Se llama dentro del hook: debe ser rapidísimo y no bloquear.
    /// </summary>
    public Func<MediaKey, bool>? ShouldSwallow { get; set; }
    private readonly System.Collections.Generic.HashSet<int> _swallowedDown = new();

    public MediaKeyHook(Dispatcher ui) { _ui = ui; _proc = Callback; }

    public bool IsActive => _hook != IntPtr.Zero;

    public void Start()
    {
        if (_hook != IntPtr.Zero) return;
        _hook = Native.SetWindowsHookEx(Native.WH_KEYBOARD_LL, _proc, Native.GetModuleHandle(null), 0);
        if (_hook == IntPtr.Zero) Log.Write("No se pudo instalar el hook de teclado: " + Marshal.GetLastWin32Error());
    }

    public void Stop()
    {
        if (_hook == IntPtr.Zero) return;
        Native.UnhookWindowsHookEx(_hook);
        _hook = IntPtr.Zero;
    }

    private IntPtr Callback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && (wParam == 0x0101 /*WM_KEYUP*/ || wParam == 0x0105 /*WM_SYSKEYUP*/))
        {
            // Si nos tragamos el "down", también el "up" para no dejar a Windows con una pulsación a medias.
            if (_swallowedDown.Remove(Marshal.ReadInt32(lParam))) return new IntPtr(1);
        }
        if (nCode >= 0 && (wParam == Native.WM_KEYDOWN || wParam == Native.WM_SYSKEYDOWN))
        {
            int vk = Marshal.ReadInt32(lParam);
            MediaKey? key = vk switch
            {
                VK_MEDIA_PLAY_PAUSE => MediaKey.PlayPause,
                VK_MEDIA_NEXT_TRACK => MediaKey.Next,
                VK_MEDIA_PREV_TRACK => MediaKey.Previous,
                VK_MEDIA_STOP => MediaKey.Stop,
                VK_VOLUME_UP => MediaKey.VolumeUp,
                VK_VOLUME_DOWN => MediaKey.VolumeDown,
                VK_VOLUME_MUTE => MediaKey.VolumeMute,
                _ => null,
            };
            // Nunca bloquear dentro del hook (Windows lo desinstala si tarda > ~300 ms).
            if (key is { } k)
            {
                bool swallow = false;
                try { swallow = ShouldSwallow?.Invoke(k) == true; } catch { }
                _ui.BeginInvoke(() => KeyPressed?.Invoke(k));
                if (swallow) { _swallowedDown.Add(vk); return new IntPtr(1); }
            }
        }
        return Native.CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    public void Dispose() => Stop();
}

/// <summary>Atajo global (RegisterHotKey) para mostrar el flyout manualmente.</summary>
public sealed class GlobalHotkey : IDisposable
{
    private const int Id = 0xBF01;
    private readonly HwndSource _src;
    private bool _registered;
    public event Action? Pressed;

    public GlobalHotkey()
    {
        var p = new HwndSourceParameters("BetterFlyoutHotkey") { Width = 0, Height = 0, WindowStyle = 0, ParentWindow = new IntPtr(-3) /*HWND_MESSAGE*/ };
        _src = new HwndSource(p);
        _src.AddHook(WndProc);
    }

    /// <summary>Registra un atajo tipo "Ctrl+Alt+M". Devuelve false si el formato es inválido o ya está en uso.</summary>
    public bool Register(string? combo)
    {
        Unregister();
        if (string.IsNullOrWhiteSpace(combo)) return true;
        uint mods = 0x4000; // MOD_NOREPEAT
        Key key = Key.None;
        foreach (var raw in combo.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            switch (raw.ToLowerInvariant())
            {
                case "ctrl": case "control": mods |= 0x2; break;
                case "alt": mods |= 0x1; break;
                case "shift": mods |= 0x4; break;
                case "win": case "windows": mods |= 0x8; break;
                default:
                    if (raw.Length == 1 && char.IsDigit(raw[0])) key = Key.D0 + (raw[0] - '0');
                    else if (!Enum.TryParse(raw, true, out key)) return false;
                    break;
            }
        }
        if (key == Key.None) return false;
        _registered = Native.RegisterHotKey(_src.Handle, Id, mods, (uint)KeyInterop.VirtualKeyFromKey(key));
        if (!_registered) Log.Write($"No se pudo registrar el atajo {combo} (¿en uso por otra app?)");
        return _registered;
    }

    public void Unregister()
    {
        if (_registered) Native.UnregisterHotKey(_src.Handle, Id);
        _registered = false;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == Native.WM_HOTKEY && wParam.ToInt32() == Id) { Pressed?.Invoke(); handled = true; }
        return IntPtr.Zero;
    }

    public void Dispose() { Unregister(); _src.Dispose(); }
}
