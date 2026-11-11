// BetterFlyout - Copyright (C) 2026 aaddrruuss (https://github.com/aaddrruuss/)
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace BetterFlyout.Interop;

internal static class Native
{
    public const int GWL_EXSTYLE = -20;
    public const long WS_EX_TOPMOST = 0x00000008;
    public const long WS_EX_TRANSPARENT = 0x00000020;
    public const long WS_EX_TOOLWINDOW = 0x00000080;
    public const long WS_EX_APPWINDOW = 0x00040000;
    public const long WS_EX_NOACTIVATE = 0x08000000;

    public static readonly IntPtr HWND_TOPMOST = new(-1);
    public const uint SWP_NOSIZE = 0x0001;
    public const uint SWP_NOMOVE = 0x0002;
    public const uint SWP_NOZORDER = 0x0004;
    public const uint SWP_NOACTIVATE = 0x0010;
    public const uint SWP_SHOWWINDOW = 0x0040;
    public const uint SWP_NOOWNERZORDER = 0x0200;

    public const int WH_KEYBOARD_LL = 13;
    public const int WM_KEYDOWN = 0x0100;
    public const int WM_SYSKEYDOWN = 0x0104;
    public const int WM_HOTKEY = 0x0312;

    public const uint MONITOR_DEFAULTTOPRIMARY = 1;
    public const uint MONITOR_DEFAULTTONEAREST = 2;
    public const uint MONITORINFOF_PRIMARY = 1;

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left, Top, Right, Bottom; public int Width => Right - Left; public int Height => Bottom - Top; }

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT { public int X, Y; }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct MONITORINFOEX
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string szDevice;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct KBDLLHOOKSTRUCT { public uint vkCode, scanCode, flags, time; public IntPtr dwExtraInfo; }

    public delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);
    private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdc, ref RECT lprcMonitor, IntPtr dwData);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] public static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] public static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr value);
    [DllImport("user32.dll", SetLastError = true)] public static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll")] public static extern bool GetCursorPos(out POINT pt);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern IntPtr MonitorFromPoint(POINT pt, uint flags);
    [DllImport("user32.dll")] public static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFOEX info);
    [DllImport("user32.dll")] private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr clip, MonitorEnumProc proc, IntPtr data);
    [DllImport("shcore.dll")] private static extern int GetDpiForMonitor(IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);

    [DllImport("user32.dll", SetLastError = true)] public static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc proc, IntPtr hMod, uint threadId);
    [DllImport("user32.dll", SetLastError = true)] public static extern bool UnhookWindowsHookEx(IntPtr hhk);
    [DllImport("user32.dll")] public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr GetModuleHandle(string? name);

    [DllImport("user32.dll", SetLastError = true)] public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint mods, uint vk);
    [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    public enum QUERY_USER_NOTIFICATION_STATE { NOT_PRESENT = 1, BUSY = 2, RUNNING_D3D_FULL_SCREEN = 3, PRESENTATION_MODE = 4, ACCEPTS_NOTIFICATIONS = 5, QUIET_TIME = 6, APP = 7 }
    [DllImport("shell32.dll")] public static extern int SHQueryUserNotificationState(out QUERY_USER_NOTIFICATION_STATE state);

    // --- Blur detrás (accent policy, no documentado pero estable desde Win10) ---
    [StructLayout(LayoutKind.Sequential)] private struct AccentPolicy { public int AccentState, AccentFlags, GradientColor, AnimationId; }
    [StructLayout(LayoutKind.Sequential)] private struct WindowCompositionAttribData { public int Attribute; public IntPtr Data; public int SizeOfData; }
    [DllImport("user32.dll")] private static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttribData data);

    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    [DllImport("advapi32.dll", SetLastError = true)] private static extern bool OpenProcessToken(IntPtr process, uint access, out IntPtr token);
    [DllImport("advapi32.dll", SetLastError = true)] private static extern bool GetTokenInformation(IntPtr token, int cls, out int info, int len, out int retLen);
    [DllImport("kernel32.dll")] private static extern IntPtr GetCurrentProcess();
    [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr h);

    public static void SetBlurBehind(IntPtr hwnd, bool enable)
    {
        var accent = new AccentPolicy { AccentState = enable ? 3 /*ENABLE_BLURBEHIND*/ : 0 };
        int size = Marshal.SizeOf(accent);
        IntPtr ptr = Marshal.AllocHGlobal(size);
        try
        {
            Marshal.StructureToPtr(accent, ptr, false);
            var data = new WindowCompositionAttribData { Attribute = 19 /*WCA_ACCENT_POLICY*/, Data = ptr, SizeOfData = size };
            SetWindowCompositionAttribute(hwnd, ref data);
        }
        finally { Marshal.FreeHGlobal(ptr); }
    }

    public static void SetRoundCorners(IntPtr hwnd, bool round)
    {
        int pref = round ? 2 /*DWMWCP_ROUND*/ : 1 /*DWMWCP_DONOTROUND*/;
        try { DwmSetWindowAttribute(hwnd, 33 /*DWMWA_WINDOW_CORNER_PREFERENCE*/, ref pref, sizeof(int)); } catch { }
    }

    public static bool ProcessHasUIAccess()
    {
        if (!OpenProcessToken(GetCurrentProcess(), 0x0008 /*TOKEN_QUERY*/, out var token)) return false;
        try { return GetTokenInformation(token, 26 /*TokenUIAccess*/, out int v, sizeof(int), out _) && v != 0; }
        finally { CloseHandle(token); }
    }

    public static bool IsExclusiveFullscreenRunning()
    {
        try { return SHQueryUserNotificationState(out var s) == 0 && s == QUERY_USER_NOTIFICATION_STATE.RUNNING_D3D_FULL_SCREEN; }
        catch { return false; }
    }

    public static long GetExStyle(IntPtr hwnd) => GetWindowLongPtr(hwnd, GWL_EXSTYLE).ToInt64();
    public static void SetExStyle(IntPtr hwnd, long style) => SetWindowLongPtr(hwnd, GWL_EXSTYLE, new IntPtr(style));

    public sealed record MonitorData(IntPtr Handle, RECT Bounds, RECT WorkArea, bool IsPrimary, string Device)
    {
        public double DpiScale => GetDpiScale(Handle);
    }

    public static double GetDpiScale(IntPtr hMonitor)
    {
        try { if (GetDpiForMonitor(hMonitor, 0, out uint x, out _) == 0 && x > 0) return x / 96.0; } catch { }
        return 1.0;
    }

    public static MonitorData? GetMonitorData(IntPtr hMonitor)
    {
        var mi = new MONITORINFOEX { cbSize = Marshal.SizeOf<MONITORINFOEX>(), szDevice = string.Empty };
        if (!GetMonitorInfo(hMonitor, ref mi)) return null;
        return new MonitorData(hMonitor, mi.rcMonitor, mi.rcWork, (mi.dwFlags & MONITORINFOF_PRIMARY) != 0, mi.szDevice);
    }

    public static List<MonitorData> GetMonitors()
    {
        var list = new List<MonitorData>();
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr h, IntPtr _, ref RECT _, IntPtr _) =>
        {
            var d = GetMonitorData(h);
            if (d != null) list.Add(d);
            return true;
        }, IntPtr.Zero);
        // Principal primero, resto ordenado de izquierda a derecha.
        list.Sort((a, b) => a.IsPrimary != b.IsPrimary ? (a.IsPrimary ? -1 : 1) : a.Bounds.Left.CompareTo(b.Bounds.Left));
        return list;
    }
}
