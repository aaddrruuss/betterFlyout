// BetterFlyout - Copyright (C) 2026 aaddrruuss (https://github.com/aaddrruuss/)
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using BetterFlyout.Interop;
using BetterFlyout.Services;
using BetterFlyout.Settings;
using BetterFlyout.Util;
using BetterFlyout.Views;
using Microsoft.Win32;
using Forms = System.Windows.Forms;

namespace BetterFlyout;

public static class Program
{
    private const string MutexName = "BetterFlyout.SingleInstance.v1";
    private const string ShowSettingsEvent = "BetterFlyout.ShowSettings.v1";

    [STAThread]
    public static int Main(string[] args)
    {
        // Modo herramienta: BetterFlyout.exe --snapshot salida.png [--preset "Nombre"] [--saved]
        int snap = Array.IndexOf(args, "--snapshot");
        if (snap >= 0 && snap + 1 < args.Length) return Snapshot(args, args[snap + 1]);
        // Modo desarrollo: BetterFlyout.exe --i18n-check faltan.txt  -> lista textos sin traducir al inglés
        int i18n = Array.IndexOf(args, "--i18n-check");
        if (i18n >= 0 && i18n + 1 < args.Length) return I18nCheck(args[i18n + 1]);

        using var mutex = new Mutex(true, MutexName, out bool first);
        if (!first)
        {
            // Ya hay una instancia: pedirle que abra la configuración.
            try { using var ev = EventWaitHandle.OpenExisting(ShowSettingsEvent); ev.Set(); } catch { }
            return 0;
        }

        var app = new App();
        using var ev2 = new EventWaitHandle(false, EventResetMode.AutoReset, ShowSettingsEvent);
        var listener = new Thread(() =>
        {
            while (true)
            {
                ev2.WaitOne();
                try { app.Dispatcher.BeginInvoke(app.OpenSettings); } catch { return; }
            }
        }) { IsBackground = true };
        listener.Start();
        return app.Run();
    }

    private static int Snapshot(string[] args, string path)
    {
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var s = args.Contains("--saved") ? SettingsStore.Load() : new FlyoutSettings();
        int li = Array.IndexOf(args, "--lang");
        if (li >= 0 && li + 1 < args.Length) s.Language = args[li + 1] == "en" ? AppLanguage.English : AppLanguage.Spanish;
        Loc.Set(s.Language);
        int pi = Array.IndexOf(args, "--preset");
        if (pi >= 0 && pi + 1 < args.Length && Presets.All.TryGetValue(args[pi + 1], out var apply)) apply(s);
        var media = new MediaService(app.Dispatcher);
        var w = new FlyoutWindow(s, media, () => { });
        w.RenderSnapshot(path);
        return 0;
    }

    private static int I18nCheck(string outPath)
    {
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        Loc.Set(AppLanguage.English);
        var s = new FlyoutSettings();
        var media = new MediaService(app.Dispatcher);
        var fw = new FlyoutWindow(s, media, () => { });
        fw.RenderSnapshot(System.IO.Path.GetTempFileName());
        var sw = new SettingsWindow(s, fw);
        foreach (var k in Presets.All.Keys) Loc.T(k);
        foreach (var k in App.TrayTexts) Loc.T(k);
        System.IO.File.WriteAllLines(outPath, Loc.Missing);
        sw.Close();
        return Loc.Missing.Count == 0 ? 0 : 1;
    }
}

public sealed class App : Application
{
    private FlyoutSettings S = null!;
    private MediaService _media = null!;
    private FlyoutWindow _flyout = null!;
    private MediaKeyHook? _hook;
    private GlobalHotkey? _hotkey;
    private Forms.NotifyIcon? _tray;
    private Forms.ToolStripMenuItem? _pauseItem;
    private SettingsWindow? _settingsWindow;
    private DateTime _lastManualAction = DateTime.MinValue;
    private DateTime _ignoreUntil = DateTime.MaxValue;
    private bool _paused;
    private DispatcherTimer? _trimTimer;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        DispatcherUnhandledException += (_, ex) => { Log.Write("UI: " + ex.Exception); ex.Handled = true; };
        AppDomain.CurrentDomain.UnhandledException += (_, ex) => Log.Write("Fatal: " + ex.ExceptionObject);
        TaskSchedulerUnobserved();

        S = SettingsStore.Load();
        Loc.Set(S.Language);
        SettingsStore.AutoSave(S);
        S.PropertyChanged += OnSettingChanged;

        _media = new MediaService(Dispatcher);
        _flyout = new FlyoutWindow(S, _media, MarkManual);
        _media.Changed += OnMediaChanged;
        _flyout.Hidden += ScheduleTrim;

        _hook = new MediaKeyHook(Dispatcher);
        _hook.KeyPressed += OnMediaKey;
        // Decide en el propio hook si Windows recibe la tecla (lecturas simples, sin bloquear).
        _hook.ShouldSwallow = k => k.IsVolume() ? S.ReplaceVolumeFlyout : S.ReplaceMediaFlyout && _media.State.HasSession;
        _hook.Start();

        _hotkey = new GlobalHotkey();
        _hotkey.Pressed += () => _flyout.ShowFlyout();
        _hotkey.Register(S.Hotkey);

        ApplyStartup();
        CreateTray();

        try { await _media.InitAsync(); }
        catch (Exception ex) { Log.Write("No se pudo iniciar GSMTC: " + ex); }
        // Ignora la ráfaga inicial de eventos (no mostrar el popup al arrancar).
        _ignoreUntil = DateTime.Now.AddSeconds(1.5);
        ScheduleTrim();

        if (e.Args.Contains("--settings")) OpenSettings();
    }

    private static void TaskSchedulerUnobserved() =>
        System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (_, ex) => { Log.Write("Task: " + ex.Exception); ex.SetObserved(); };

    // ═════════════ Lógica de disparo ═════════════

    private void MarkManual() => _lastManualAction = DateTime.Now;

    private bool CanShow(bool requireSession = true)
    {
        if (!S.Enabled || _paused || DateTime.Now < _ignoreUntil) return false;
        if (requireSession && S.HideWhenNoSession && !_media.State.HasSession) return false;
        if (S.OnlyInFullscreen && !Native.IsFullscreenAppActive()) return false;
        return true;
    }

    private void OnMediaChanged(MediaChangeKind kind)
    {
        _flyout.UpdateMedia();
        switch (kind)
        {
            case MediaChangeKind.TrackChanged:
                bool manual = (DateTime.Now - _lastManualAction).TotalMilliseconds <= S.ManualWindowMs;
                if ((manual ? S.ShowOnManualTrackChange : S.ShowOnAutoTrackChange) && CanShow()) _flyout.ShowFlyout();
                break;
            case MediaChangeKind.Paused:
                if (S.ShowOnPause && CanShow()) _flyout.ShowFlyout(S.PausedDisplayDurationMs);
                break;
            case MediaChangeKind.Resumed:
                if (S.ShowOnResume && CanShow()) _flyout.ShowFlyout();
                break;
            case MediaChangeKind.SessionChanged:
                if (S.ShowOnSourceChange && CanShow()) _flyout.ShowFlyout();
                break;
            case MediaChangeKind.NoSession:
                if (S.HideWhenNoSession && !_flyout.IsPreview) _flyout.HideFlyout();
                break;
        }
    }

    private void OnMediaKey(MediaKey key)
    {
        if (key.IsVolume())
        {
            if (S.ReplaceVolumeFlyout)
            {
                // Windows no ha recibido la tecla: aplicamos el cambio nosotros.
                float step = Math.Clamp(S.VolumeStep, 1, 50) / 100f;
                float v = AudioService.GetVolume() ?? 0;
                switch (key)
                {
                    case MediaKey.VolumeUp: AudioService.SetVolume((float)Math.Round(v + step, 2)); break;
                    case MediaKey.VolumeDown: AudioService.SetVolume((float)Math.Round(Math.Max(0, v - step), 2)); break;
                    default: AudioService.ToggleMute(); break;
                }
            }
            if ((S.ShowOnVolumeKeys || S.ReplaceVolumeFlyout) && CanShow(requireSession: false)) _flyout.ShowFlyout();
            return;
        }

        MarkManual();
        if (S.ReplaceMediaFlyout && _media.State.HasSession)
        {
            switch (key)
            {
                case MediaKey.PlayPause: _media.TogglePlayPause(); break;
                case MediaKey.Next: _media.Next(); break;
                case MediaKey.Previous: _media.Previous(); break;
                case MediaKey.Stop: _media.Stop(); break;
            }
        }
        if ((S.ShowOnMediaKeys || S.ReplaceMediaFlyout) && CanShow()) _flyout.ShowFlyout();
    }

    // ═════════════ Ajustes con efectos externos ═════════════

    private void OnSettingChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(FlyoutSettings.Hotkey): _hotkey?.Register(S.Hotkey); break;
            case nameof(FlyoutSettings.Language):
                // El popup y la ventana de ajustes se regeneran solos al cambiar un ajuste.
                Loc.Set(S.Language);
                if (_tray != null) _tray.ContextMenuStrip = BuildTrayMenu();
                break;
            case nameof(FlyoutSettings.StartWithWindows): ApplyStartup(); break;
            case nameof(FlyoutSettings.Enabled): if (_pauseItem != null) _pauseItem.Checked = !S.Enabled || _paused; break;
        }
    }

    private void ApplyStartup()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", writable: true);
            if (key == null) return;
            if (S.StartWithWindows && Environment.ProcessPath is { } exe) key.SetValue("BetterFlyout", $"\"{exe}\"");
            else if (key.GetValue("BetterFlyout") != null) key.DeleteValue("BetterFlyout", false);
        }
        catch (Exception ex) { Log.Write("Startup: " + ex.Message); }
    }

    // ═════════════ Bandeja ═════════════

    internal static readonly string[] TrayTexts = { "Mostrar popup", "Configuración…", "Pausar popups", "Salir" };

    private Forms.ContextMenuStrip BuildTrayMenu()
    {
        var old = _tray?.ContextMenuStrip;
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add(Loc.T("Mostrar popup"), null, (_, _) => _flyout.ShowFlyout());
        menu.Items.Add(Loc.T("Configuración…"), null, (_, _) => OpenSettings());
        _pauseItem = new Forms.ToolStripMenuItem(Loc.T("Pausar popups")) { CheckOnClick = true, Checked = _paused };
        var item = _pauseItem;
        item.CheckedChanged += (_, _) => _paused = item.Checked;
        menu.Items.Add(item);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(Loc.T("Salir"), null, (_, _) => ExitApp());
        old?.Dispose();
        return menu;
    }

    private void CreateTray()
    {
        _tray = new Forms.NotifyIcon
        {
            Icon = MakeTrayIcon(),
            Text = Native.ProcessHasUIAccess() ? "BetterFlyout (UIAccess)" : "BetterFlyout",
            ContextMenuStrip = BuildTrayMenu(),
            Visible = true,
        };
        _tray.MouseClick += (_, e) => { if (e.Button == Forms.MouseButtons.Left) _flyout.ShowFlyout(); };
        _tray.MouseDoubleClick += (_, e) => { if (e.Button == Forms.MouseButtons.Left) OpenSettings(); };
    }

    private static Icon MakeTrayIcon()
    {
        using var bmp = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(System.Drawing.Color.Transparent);
            using var bg = new SolidBrush(System.Drawing.Color.FromArgb(255, 0x1F, 0x1F, 0x1F));
            g.FillEllipse(bg, 1, 1, 30, 30);
            using var pen = new Pen(System.Drawing.Color.FromArgb(255, 0x60, 0xCD, 0xFF), 2.2f);
            g.DrawEllipse(pen, 2, 2, 28, 28);
            using var white = new SolidBrush(System.Drawing.Color.White);
            g.FillEllipse(white, 8, 18, 8, 7);
            g.FillRectangle(white, 14, 7, 2.4f, 15);
            g.FillPolygon(white, new[] { new PointF(14, 7), new PointF(23, 9), new PointF(23, 13), new PointF(14, 11) });
        }
        return Icon.FromHandle(bmp.GetHicon());
    }

    public void OpenSettings()
    {
        if (_settingsWindow is { IsLoaded: true })
        {
            if (_settingsWindow.WindowState == WindowState.Minimized) _settingsWindow.WindowState = WindowState.Normal;
            _settingsWindow.Activate();
            return;
        }
        _settingsWindow = new SettingsWindow(S, _flyout);
        _settingsWindow.Closed += (_, _) => { _settingsWindow = null; SettingsStore.Save(S); ScheduleTrim(); };
        _settingsWindow.Show();
        _settingsWindow.Activate();
    }

    /// <summary>
    /// Unos segundos después de quedar en reposo (popup oculto, ajustes cerrados) libera memoria:
    /// GC compactando y devolviendo a Windows las páginas que no se usan. No afecta al rendimiento al volver a mostrar.
    /// </summary>
    private void ScheduleTrim()
    {
        _trimTimer ??= new DispatcherTimer(DispatcherPriority.ApplicationIdle) { Interval = TimeSpan.FromSeconds(4) };
        _trimTimer.Stop();
        _trimTimer.Tick -= TrimTick;
        _trimTimer.Tick += TrimTick;
        _trimTimer.Start();
    }

    private void TrimTick(object? sender, EventArgs e)
    {
        _trimTimer?.Stop();
        if (_flyout.IsShowing || _settingsWindow != null) return;
        System.Runtime.GCSettings.LargeObjectHeapCompactionMode = System.Runtime.GCLargeObjectHeapCompactionMode.CompactOnce;
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
        GC.WaitForPendingFinalizers();
        Native.TrimWorkingSet();
    }

    private void ExitApp()
    {
        SettingsStore.Save(S);
        _hook?.Dispose();
        _hotkey?.Dispose();
        if (_tray != null) { _tray.Visible = false; _tray.Dispose(); }
        Shutdown();
    }
}
