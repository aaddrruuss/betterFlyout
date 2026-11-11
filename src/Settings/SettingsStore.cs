using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Threading;

namespace BetterFlyout.Settings;

public static class SettingsStore
{
    public static readonly string Folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BetterFlyout");
    public static readonly string FilePath = Path.Combine(Folder, "settings.json");

    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    private static DispatcherTimer? _saveTimer;

    public static FlyoutSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return Deserialize(File.ReadAllText(FilePath)) ?? new FlyoutSettings();
        }
        catch (Exception ex) { Log.Write("Error leyendo settings: " + ex.Message); }
        return new FlyoutSettings();
    }

    public static FlyoutSettings? Deserialize(string json) => JsonSerializer.Deserialize<FlyoutSettings>(json, JsonOptions);
    public static string Serialize(FlyoutSettings s) => JsonSerializer.Serialize(s, JsonOptions);

    public static void Save(FlyoutSettings s)
    {
        try
        {
            Directory.CreateDirectory(Folder);
            var tmp = FilePath + ".tmp";
            File.WriteAllText(tmp, Serialize(s));
            File.Move(tmp, FilePath, true);
        }
        catch (Exception ex) { Log.Write("Error guardando settings: " + ex.Message); }
    }

    /// <summary>Guarda automáticamente (con debounce) cada vez que cambia algo.</summary>
    public static void AutoSave(FlyoutSettings s)
    {
        _saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
        _saveTimer.Tick += (_, _) => { _saveTimer.Stop(); Save(s); };
        s.PropertyChanged += (_, _) => { _saveTimer.Stop(); _saveTimer.Start(); };
    }
}

public static class Log
{
    private static readonly object Gate = new();
    public static void Write(string msg)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(SettingsStore.Folder);
                File.AppendAllText(Path.Combine(SettingsStore.Folder, "log.txt"), $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {msg}{Environment.NewLine}");
            }
        }
        catch { }
    }
}
