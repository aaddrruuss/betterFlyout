using System;
using System.Runtime.InteropServices;
using BetterFlyout.Settings;

namespace BetterFlyout.Services;

/// <summary>Volumen maestro del dispositivo de salida por defecto (Core Audio / IAudioEndpointVolume).</summary>
public static class AudioService
{
    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    private class MMDeviceEnumeratorCom { }

    [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        [PreserveSig] int EnumAudioEndpoints(int dataFlow, int stateMask, out IntPtr devices);
        [PreserveSig] int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice device);
    }

    [Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        [PreserveSig] int Activate(ref Guid iid, int clsCtx, IntPtr activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object iface);
    }

    [Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioEndpointVolume
    {
        [PreserveSig] int RegisterControlChangeNotify(IntPtr notify);
        [PreserveSig] int UnregisterControlChangeNotify(IntPtr notify);
        [PreserveSig] int GetChannelCount(out uint count);
        [PreserveSig] int SetMasterVolumeLevel(float levelDb, ref Guid ctx);
        [PreserveSig] int SetMasterVolumeLevelScalar(float level, ref Guid ctx);
        [PreserveSig] int GetMasterVolumeLevel(out float levelDb);
        [PreserveSig] int GetMasterVolumeLevelScalar(out float level);
        [PreserveSig] int SetChannelVolumeLevel(uint ch, float levelDb, ref Guid ctx);
        [PreserveSig] int SetChannelVolumeLevelScalar(uint ch, float level, ref Guid ctx);
        [PreserveSig] int GetChannelVolumeLevel(uint ch, out float levelDb);
        [PreserveSig] int GetChannelVolumeLevelScalar(uint ch, out float level);
        [PreserveSig] int SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, ref Guid ctx);
        [PreserveSig] int GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);
    }

    private static IAudioEndpointVolume? _ep;
    private static DateTime _epTime;

    /// <summary>Obtiene (y cachea brevemente) el endpoint por defecto; se refresca para seguir cambios de dispositivo.</summary>
    private static IAudioEndpointVolume? Endpoint()
    {
        if (_ep != null && (DateTime.UtcNow - _epTime).TotalSeconds < 3) return _ep;
        try
        {
            var en = (IMMDeviceEnumerator)new MMDeviceEnumeratorCom();
            if (en.GetDefaultAudioEndpoint(0 /*eRender*/, 1 /*eMultimedia*/, out var dev) != 0 || dev == null) return _ep = null;
            var iid = typeof(IAudioEndpointVolume).GUID;
            if (dev.Activate(ref iid, 0x17 /*CLSCTX_ALL*/, IntPtr.Zero, out var o) != 0) return _ep = null;
            _ep = (IAudioEndpointVolume)o;
            _epTime = DateTime.UtcNow;
            return _ep;
        }
        catch (Exception ex) { Log.Write("Audio: " + ex.Message); return _ep = null; }
    }

    /// <summary>Volumen 0..1, o null si no hay dispositivo.</summary>
    public static float? GetVolume()
    {
        try { var ep = Endpoint(); if (ep != null && ep.GetMasterVolumeLevelScalar(out float v) == 0) return v; } catch { _ep = null; }
        return null;
    }

    public static bool IsMuted()
    {
        try { var ep = Endpoint(); if (ep != null && ep.GetMute(out bool m) == 0) return m; } catch { _ep = null; }
        return false;
    }

    public static void SetVolume(float v)
    {
        try
        {
            var ep = Endpoint();
            if (ep == null) return;
            var ctx = Guid.Empty;
            ep.SetMasterVolumeLevelScalar(Math.Clamp(v, 0f, 1f), ref ctx);
            if (v > 0 && IsMuted()) ep.SetMute(false, ref ctx);
        }
        catch { _ep = null; }
    }

    public static void ToggleMute()
    {
        try { var ep = Endpoint(); if (ep == null) return; var ctx = Guid.Empty; ep.SetMute(!IsMuted(), ref ctx); } catch { _ep = null; }
    }
}
