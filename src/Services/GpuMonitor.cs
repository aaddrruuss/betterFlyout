// BetterFlyout - Copyright (C) 2026 aaddrruuss (https://github.com/aaddrruuss/)
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;

namespace BetterFlyout.Services;

/// <summary>
/// Mide el uso del motor 3D de la GPU (el mismo dato que "GPU" en el Administrador de tareas) con los
/// contadores de rendimiento de Windows (PDH). Se usa en el modo de dibujado automático.
/// Muestrea en un hilo del sistema, sin tocar la interfaz y sin crear objetos en cada lectura.
/// </summary>
public static class GpuMonitor
{
    /// <summary>Uso actual (0-100) del motor 3D más cargado; -1 si no se puede medir.</summary>
    public static double Usage { get; private set; } = -1;
    /// <summary>La GPU se considera saturada (con histéresis para no cambiar de modo sin parar).</summary>
    public static bool Busy { get; private set; }
    /// <summary>Se dispara (en un hilo del sistema) cuando cambia <see cref="Busy"/>.</summary>
    public static event Action? BusyChanged;

    private const int IntervalMs = 2000;
    private const double Hysteresis = 10;

    private static readonly object Lock = new();
    private static Timer? _timer;
    private static Func<double> _threshold = () => 85;
    private static IntPtr _query, _counter, _buf;
    private static uint _bufSize;
    private static bool _primed, _failed;
    private static readonly Dictionary<ulong, double> Engines = new();

    public static bool Running => _timer != null;

    public static void Start(Func<double> threshold)
    {
        lock (Lock)
        {
            _threshold = threshold;
            if (_timer != null) return;
            _failed = false;
            _timer = new Timer(_ => Sample(), null, 0, IntervalMs);
        }
    }

    public static void Stop()
    {
        lock (Lock)
        {
            _timer?.Dispose();
            _timer = null;
            Close();
            Usage = -1;
            SetBusy(false);
        }
    }

    /// <summary>Lectura inmediata (para pruebas): dos muestras separadas por <paramref name="gapMs"/>.</summary>
    public static double MeasureOnce(int gapMs = 500)
    {
        lock (Lock) { Sample(); }
        Thread.Sleep(gapMs);
        lock (Lock) { Sample(); return Usage; }
    }

    private static void Sample()
    {
        if (!Monitor.TryEnter(Lock)) return;
        try
        {
            if (_failed) return;
            if (_query == IntPtr.Zero && !Open()) { Fail("no se pudo abrir el contador de GPU"); return; }

            if (PdhCollectQueryData(_query) != 0) { _primed = false; return; }
            if (!_primed) { _primed = true; return; } // El primer dato no tiene con qué compararse.

            uint size = _bufSize, count;
            uint st = PdhGetFormattedCounterArrayW(_counter, PDH_FMT_DOUBLE | PDH_FMT_NOCAP100, ref size, out count, _buf);
            if (st == PDH_MORE_DATA)
            {
                if (_buf != IntPtr.Zero) Marshal.FreeHGlobal(_buf);
                _bufSize = size + 4096;
                _buf = Marshal.AllocHGlobal((int)_bufSize);
                size = _bufSize;
                st = PdhGetFormattedCounterArrayW(_counter, PDH_FMT_DOUBLE | PDH_FMT_NOCAP100, ref size, out count, _buf);
            }
            if (st != 0) { if (st != PDH_NO_DATA && st != PDH_INVALID_DATA) Log("PDH " + st.ToString("X")); Usage = 0; Update(); return; }

            // Cada instancia es "pid_X_luid_..._phys_N_eng_M_engtype_3D": se suma por motor (todo lo que va
            // tras "luid") y el uso de la GPU es el del motor más cargado, como en el Administrador de tareas.
            Engines.Clear();
            for (int i = 0; i < count; i++)
            {
                IntPtr item = _buf + i * ItemSize;
                uint cstatus = (uint)Marshal.ReadInt32(item, StatusOffset);
                if (cstatus > 1) continue; // PDH_CSTATUS_VALID_DATA / NEW_DATA
                double v = BitConverter.Int64BitsToDouble(Marshal.ReadInt64(item, ValueOffset));
                ulong key = EngineKey(Marshal.ReadIntPtr(item));
                Engines[key] = (Engines.TryGetValue(key, out var acc) ? acc : 0) + v;
            }
            double max = 0;
            foreach (var v in Engines.Values) if (v > max) max = v;
            Usage = Math.Clamp(max, 0, 100);
            Update();
        }
        catch (Exception ex) { Fail(ex.Message); }
        finally { Monitor.Exit(Lock); }
    }

    private static void Update()
    {
        double t = Math.Clamp(_threshold(), 1, 100);
        if (!Busy && Usage >= t) SetBusy(true);
        else if (Busy && Usage < t - Hysteresis) SetBusy(false);
    }

    private static void SetBusy(bool v)
    {
        if (Busy == v) return;
        Busy = v;
        try { BusyChanged?.Invoke(); } catch { }
    }

    /// <summary>Hash (FNV-1a) del nombre de instancia a partir de "luid", leído directamente de memoria nativa.</summary>
    private static ulong EngineKey(IntPtr name)
    {
        if (name == IntPtr.Zero) return 0;
        int start = -1;
        for (int i = 0; i < 512; i++)
        {
            char c = (char)Marshal.ReadInt16(name, i * 2);
            if (c == 0) break;
            if (c == 'l' && (char)Marshal.ReadInt16(name, (i + 1) * 2) == 'u'
                && (char)Marshal.ReadInt16(name, (i + 2) * 2) == 'i' && (char)Marshal.ReadInt16(name, (i + 3) * 2) == 'd')
            { start = i; break; }
        }
        if (start < 0) start = 0;
        ulong h = 14695981039346656037UL;
        for (int i = start; i < start + 512; i++)
        {
            char c = (char)Marshal.ReadInt16(name, i * 2);
            if (c == 0) break;
            h = (h ^ c) * 1099511628211UL;
        }
        return h;
    }

    private static bool Open()
    {
        if (PdhOpenQueryW(null, IntPtr.Zero, out _query) != 0) { _query = IntPtr.Zero; return false; }
        // Nombre en inglés: funciona igual en Windows en español u otros idiomas.
        if (PdhAddEnglishCounterW(_query, @"\GPU Engine(*engtype_3D)\Utilization Percentage", IntPtr.Zero, out _counter) != 0)
        {
            Close();
            return false;
        }
        _primed = false;
        return true;
    }

    private static void Close()
    {
        if (_query != IntPtr.Zero) PdhCloseQuery(_query);
        _query = _counter = IntPtr.Zero;
        if (_buf != IntPtr.Zero) Marshal.FreeHGlobal(_buf);
        _buf = IntPtr.Zero; _bufSize = 0; _primed = false;
        Engines.Clear();
    }

    private static void Fail(string why)
    {
        _failed = true;
        Usage = -1;
        SetBusy(false);
        Log("GpuMonitor desactivado: " + why);
    }

    private static void Log(string msg)
    {
        try { BetterFlyout.Settings.Log.Write(msg); } catch { }
    }

    // ── PDH ──
    // PDH_FMT_COUNTERVALUE_ITEM_W = { LPWSTR szName; { DWORD CStatus; double value } } -> 24 bytes en x86 y x64.
    private const int ItemSize = 24, StatusOffset = 8, ValueOffset = 16;
    private const uint PDH_FMT_DOUBLE = 0x00000200, PDH_FMT_NOCAP100 = 0x00008000;
    private const uint PDH_MORE_DATA = 0x800007D2, PDH_NO_DATA = 0x800007D5, PDH_INVALID_DATA = 0xC0000BC6;

    [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
    private static extern uint PdhOpenQueryW(string? dataSource, IntPtr userData, out IntPtr query);
    [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
    private static extern uint PdhAddEnglishCounterW(IntPtr query, string path, IntPtr userData, out IntPtr counter);
    [DllImport("pdh.dll")]
    private static extern uint PdhCollectQueryData(IntPtr query);
    [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
    private static extern uint PdhGetFormattedCounterArrayW(IntPtr counter, uint format, ref uint bufferSize, out uint itemCount, IntPtr buffer);
    [DllImport("pdh.dll")]
    private static extern uint PdhCloseQuery(IntPtr query);
}
