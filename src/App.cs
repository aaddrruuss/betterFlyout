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
        int pi = Array.IndexOf(args, "--preset");
        if (pi >= 0 && pi + 1 < args.Length && Presets.All.TryGetValue(args[pi + 1], out var apply)) apply(s);
        var media = new MediaService(app.Dispatcher);
        var w = new FlyoutWindow(s, media, () => { });
        w.RenderSnapshot(path);
        return 0;
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

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        DispatcherUnhandledException += (_, ex) => { Log.Write("UI: " + ex.Exception); ex.Handled = true; };
        AppDomain.CurrentDomain.UnhandledException += (_, ex) => Log.Write("Fatal: " + ex.ExceptionObject);
        TaskSchedulerUnobserved();

        S = SettingsStore.Load();
        SettingsStore.AutoSave(S);
        S.PropertyChanged += OnSettingChanged;

        _media = new MediaService(Dispatcher);
        _flyout = new FlyoutWindow(S, _media, MarkManual);
        _media.Changed += OnMediaChanged;

        _hook = new MediaKeyHook(Dispatcher);
        _hook.KeyPressed += OnMediaKey;
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
        if (S.OnlyInFullscreen && !IsFullscreenAppActive()) return false;
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
        switch (key)
        {
            case MediaKey.VolumeUp or MediaKey.VolumeDown or MediaKey.VolumeMute:
                if (S.ShowOnVolumeKeys && CanShow(requireSession: false)) _flyout.ShowFlyout();
                break;
            default:
                MarkManual();
                if (S.ShowOnMediaKeys && CanShow()) _flyout.ShowFlyout();
                break;
        }
    }

    /// <summary>¿La ventana en primer plano ocupa todo su monitor (juego, vídeo a pantalla completa…)?</summary>
    private static bool IsFullscreenAppActive()
    {
        if (Native.IsExclusiveFullscreenRunning()) return true;
        var fg = Native.GetForegroundWindow();
        if (fg == IntPtr.Zero) return false;
        var cls = new System.Text.StringBuilder(64);
        GetClassName(fg, cls, cls.Capacity);
        var c = cls.ToString();
        if (c is "Progman" or "WorkerW" or "Shell_TrayWnd") return false;
        if (!Native.GetWindowRect(fg, out var r)) return false;
        var m = Native.GetMonitorData(Native.MonitorFromWindow(fg, Native.MONITOR_DEFAULTTONEAREST));
        return m != null && r.Left <= m.Bounds.Left && r.Top <= m.Bounds.Top && r.Right >= m.Bounds.Right && r.Bottom >= m.Bounds.Bottom;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder name, int max);

    // ═════════════ Ajustes con efectos externos ═════════════

    private void OnSettingChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(FlyoutSettings.Hotkey): _hotkey?.Register(S.Hotkey); break;
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

    private void CreateTray()
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Mostrar popup", null, (_, _) => _flyout.ShowFlyout());
        menu.Items.Add("Configuración…", null, (_, _) => OpenSettings());
        _pauseItem = new Forms.ToolStripMenuItem("Pausar popups") { CheckOnClick = true };
        _pauseItem.CheckedChanged += (_, _) => _paused = _pauseItem.Checked;
        menu.Items.Add(_pauseItem);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Salir", null, (_, _) => ExitApp());

        _tray = new Forms.NotifyIcon
        {
            Icon = MakeTrayIcon(),
            Text = Native.ProcessHasUIAccess() ? "BetterFlyout (UIAccess)" : "BetterFlyout",
            ContextMenuStrip = menu,
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
        _settingsWindow.Closed += (_, _) => { _settingsWindow = null; SettingsStore.Save(S); };
        _settingsWindow.Show();
        _settingsWindow.Activate();
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
