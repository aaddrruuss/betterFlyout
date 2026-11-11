// BetterFlyout - Copyright (C) 2026 aaddrruuss (https://github.com/aaddrruuss/)
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Windows.Media.Control;
using BetterFlyout.Settings;

namespace BetterFlyout.Services;

public enum MediaChangeKind { TrackChanged, ArtUpdated, Paused, Resumed, Timeline, SessionChanged, NoSession }

/// <summary>Estado actual de la sesión multimedia (solo se toca desde el hilo de UI).</summary>
public sealed class MediaState
{
    public bool HasSession { get; set; }
    public string Title { get; set; } = "";
    public string Artist { get; set; } = "";
    public string Album { get; set; } = "";
    public string AppId { get; set; } = "";
    public string AppName { get; set; } = "";
    public BitmapSource? Art { get; set; }
    public Color? ArtColor { get; set; }
    public bool IsPlaying { get; set; }
    public bool CanPrev { get; set; } = true;
    public bool CanNext { get; set; } = true;
    public bool CanPlayPause { get; set; } = true;
    public TimeSpan Position { get; set; }
    public TimeSpan Start { get; set; }
    public TimeSpan End { get; set; }
    public DateTimeOffset LastUpdated { get; set; }

    public string TrackKey => $"{AppId}|{Title}|{Artist}";
    public TimeSpan Duration => End - Start > TimeSpan.Zero ? End - Start : TimeSpan.Zero;

    /// <summary>Posición interpolada (GSMTC solo actualiza el timeline cada pocos segundos).</summary>
    public TimeSpan EstimatedPosition
    {
        get
        {
            var pos = Position - Start;
            if (IsPlaying && LastUpdated != default)
            {
                var delta = DateTimeOffset.Now - LastUpdated;
                if (delta > TimeSpan.Zero && delta < TimeSpan.FromHours(6)) pos += delta;
            }
            if (pos < TimeSpan.Zero) pos = TimeSpan.Zero;
            if (Duration > TimeSpan.Zero && pos > Duration) pos = Duration;
            return pos;
        }
    }

    public static MediaState Sample() => new()
    {
        HasSession = true, Title = "Nombre de la canción (vista previa)", Artist = "Artista de ejemplo", Album = "Álbum de ejemplo",
        AppName = "Spotify", IsPlaying = true, Start = TimeSpan.Zero, End = TimeSpan.FromSeconds(214),
        Position = TimeSpan.FromSeconds(83), LastUpdated = DateTimeOffset.Now, ArtColor = Color.FromRgb(0x60, 0xCD, 0xFF),
    };
}

/// <summary>
/// Envuelve GlobalSystemMediaTransportControlsSessionManager (la misma API que usa el flyout de Windows / FluentFlyout).
/// </summary>
public sealed class MediaService
{
    private readonly Dispatcher _ui;
    private GlobalSystemMediaTransportControlsSessionManager? _mgr;
    private GlobalSystemMediaTransportControlsSession? _session;
    private int _propsVersion;
    private string _lastTrackKey = "";
    private string? _lastAppId;

    public MediaState State { get; private set; } = new();
    public event Action<MediaChangeKind>? Changed;

    public MediaService(Dispatcher ui) => _ui = ui;

    public async Task InitAsync()
    {
        _mgr = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
        _mgr.CurrentSessionChanged += (_, _) => _ui.BeginInvoke(() => Attach(_mgr.GetCurrentSession()));
        _mgr.SessionsChanged += (_, _) => _ui.BeginInvoke(() => Attach(_mgr.GetCurrentSession()));
        Attach(_mgr.GetCurrentSession());
    }

    private void Attach(GlobalSystemMediaTransportControlsSession? session)
    {
        if (session != null && _session != null && ReferenceEquals(session, _session)) return;

        if (_session != null)
        {
            _session.MediaPropertiesChanged -= OnPropsChanged;
            _session.PlaybackInfoChanged -= OnPlaybackChanged;
            _session.TimelinePropertiesChanged -= OnTimelineChanged;
        }

        _session = session;

        if (_session == null)
        {
            State = new MediaState();
            _lastTrackKey = "";
            Changed?.Invoke(MediaChangeKind.NoSession);
            return;
        }

        _session.MediaPropertiesChanged += OnPropsChanged;
        _session.PlaybackInfoChanged += OnPlaybackChanged;
        _session.TimelinePropertiesChanged += OnTimelineChanged;

        State.HasSession = true;
        State.AppId = SafeAppId(_session);
        State.AppName = PrettyAppName(State.AppId);
        bool sourceChanged = _lastAppId != null && _lastAppId != State.AppId;
        _lastAppId = State.AppId;

        ReadPlayback(raise: false);
        ReadTimeline(raise: false);
        _ = RefreshPropsAsync();
        if (sourceChanged) Changed?.Invoke(MediaChangeKind.SessionChanged);
    }

    private void OnPropsChanged(GlobalSystemMediaTransportControlsSession s, MediaPropertiesChangedEventArgs e)
        => _ui.BeginInvoke(async () => { if (ReferenceEquals(s, _session)) await RefreshPropsAsync(); });

    private void OnPlaybackChanged(GlobalSystemMediaTransportControlsSession s, PlaybackInfoChangedEventArgs e)
        => _ui.BeginInvoke(() => { if (ReferenceEquals(s, _session)) ReadPlayback(raise: true); });

    private void OnTimelineChanged(GlobalSystemMediaTransportControlsSession s, TimelinePropertiesChangedEventArgs e)
        => _ui.BeginInvoke(() => { if (ReferenceEquals(s, _session)) ReadTimeline(raise: true); });

    private void ReadPlayback(bool raise)
    {
        if (_session == null) return;
        try
        {
            var info = _session.GetPlaybackInfo();
            if (info == null) return;
            var status = info.PlaybackStatus;
            // "Changing" se emite entre canciones; lo ignoramos para no parpadear.
            if (status == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Changing) return;

            bool playing = status == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
            State.CanNext = info.Controls.IsNextEnabled;
            State.CanPrev = info.Controls.IsPreviousEnabled;
            State.CanPlayPause = info.Controls.IsPlayPauseToggleEnabled || info.Controls.IsPlayEnabled || info.Controls.IsPauseEnabled;

            if (playing != State.IsPlaying)
            {
                // Al pausar, congela la posición estimada para que la barra no siga avanzando.
                if (!playing) { State.Position = State.Start + State.EstimatedPosition; State.LastUpdated = DateTimeOffset.Now; }
                else State.LastUpdated = DateTimeOffset.Now;
                State.IsPlaying = playing;
                if (raise) Changed?.Invoke(playing ? MediaChangeKind.Resumed : MediaChangeKind.Paused);
            }
        }
        catch (Exception ex) { Log.Write("ReadPlayback: " + ex.Message); }
    }

    private void ReadTimeline(bool raise)
    {
        if (_session == null) return;
        try
        {
            var t = _session.GetTimelineProperties();
            if (t == null) return;
            State.Start = t.StartTime;
            State.End = t.EndTime;
            State.Position = t.Position;
            State.LastUpdated = t.LastUpdatedTime == default ? DateTimeOffset.Now : t.LastUpdatedTime;
            if (raise) Changed?.Invoke(MediaChangeKind.Timeline);
        }
        catch (Exception ex) { Log.Write("ReadTimeline: " + ex.Message); }
    }

    private async Task RefreshPropsAsync()
    {
        var session = _session;
        if (session == null) return;
        int version = ++_propsVersion;
        try
        {
            var props = await session.TryGetMediaPropertiesAsync();
            if (version != _propsVersion || props == null) return;

            string title = props.Title ?? "";
            string artist = string.IsNullOrWhiteSpace(props.Artist) ? props.AlbumArtist ?? "" : props.Artist;
            if (string.IsNullOrWhiteSpace(title)) return; // Algunas apps emiten propiedades vacías entre pistas.

            var key = $"{State.AppId}|{title}|{artist}";
            bool newTrack = key != _lastTrackKey;

            BitmapSource? art = null; Color? artColor = null;
            if (props.Thumbnail != null)
            {
                try
                {
                    using var ras = await props.Thumbnail.OpenReadAsync();
                    using var src = ras.AsStreamForRead();
                    using var ms = new MemoryStream();
                    await src.CopyToAsync(ms);
                    var bytes = ms.ToArray();
                    if (version != _propsVersion) return;
                    (art, artColor) = await Task.Run(() => DecodeArt(bytes));
                }
                catch (Exception ex) { Log.Write("Thumbnail: " + ex.Message); }
            }
            if (version != _propsVersion) return;

            State.Title = title;
            State.Artist = artist;
            State.Album = props.AlbumTitle ?? "";

            if (newTrack)
            {
                _lastTrackKey = key;
                State.Art = art;
                State.ArtColor = artColor;
                ReadTimeline(raise: false);
                Changed?.Invoke(MediaChangeKind.TrackChanged);
            }
            else if (art != null)
            {
                State.Art = art;
                State.ArtColor = artColor;
                Changed?.Invoke(MediaChangeKind.ArtUpdated);
            }
        }
        catch (Exception ex) { Log.Write("RefreshProps: " + ex.Message); }
    }

    private static (BitmapSource?, Color?) DecodeArt(byte[] bytes)
    {
        try
        {
            var img = new BitmapImage();
            img.BeginInit();
            img.CacheOption = BitmapCacheOption.OnLoad;
            img.StreamSource = new MemoryStream(bytes);
            img.DecodePixelWidth = 400;
            img.EndInit();
            img.Freeze();

            var cropped = CropLetterbox(img);
            return (cropped, DominantColor(cropped));
        }
        catch { return (null, null); }
    }

    /// <summary>Spotify/YouTube a veces entregan miniaturas 16:9 con la carátula centrada: recortamos al cuadrado central.</summary>
    private static BitmapSource CropLetterbox(BitmapSource img)
    {
        int w = img.PixelWidth, h = img.PixelHeight;
        if (w <= 0 || h <= 0 || Math.Abs(w - h) < 4) return img;
        int side = Math.Min(w, h);
        var crop = new CroppedBitmap(img, new System.Windows.Int32Rect((w - side) / 2, (h - side) / 2, side, side));
        crop.Freeze();
        return crop;
    }

    /// <summary>Color medio ponderado por saturación (para acentos y tintes).</summary>
    private static Color? DominantColor(BitmapSource src)
    {
        try
        {
            var scaled = new TransformedBitmap(src, new ScaleTransform(24.0 / src.PixelWidth, 24.0 / src.PixelHeight));
            var conv = new FormatConvertedBitmap(scaled, PixelFormats.Bgra32, null, 0);
            int w = conv.PixelWidth, h = conv.PixelHeight, stride = w * 4;
            var px = new byte[stride * h];
            conv.CopyPixels(px, stride, 0);
            double r = 0, g = 0, b = 0, wsum = 0;
            for (int i = 0; i < px.Length; i += 4)
            {
                double bb = px[i], gg = px[i + 1], rr = px[i + 2];
                double max = Math.Max(rr, Math.Max(gg, bb)), min = Math.Min(rr, Math.Min(gg, bb));
                double sat = max <= 0 ? 0 : (max - min) / max;
                double lum = max / 255.0;
                double weight = 0.05 + sat * sat * (lum > 0.15 && lum < 0.97 ? 1 : 0.1);
                r += rr * weight; g += gg * weight; b += bb * weight; wsum += weight;
            }
            if (wsum <= 0) return null;
            var c = Color.FromRgb((byte)(r / wsum), (byte)(g / wsum), (byte)(b / wsum));
            // Asegura que el acento sea visible: aclara colores muy oscuros.
            double l = Util.ColorUtil.Luma(c);
            if (l < 0.35) c = Util.ColorUtil.Mix(c, Colors.White, 0.35 - l + 0.15);
            return c;
        }
        catch { return null; }
    }

    // ───────── Controles ─────────
    public async void TogglePlayPause() { try { if (_session != null) await _session.TryTogglePlayPauseAsync(); } catch { } }
    public async void Next() { try { if (_session != null) await _session.TrySkipNextAsync(); } catch { } }
    public async void Previous() { try { if (_session != null) await _session.TrySkipPreviousAsync(); } catch { } }

    private static string SafeAppId(GlobalSystemMediaTransportControlsSession s)
    {
        try { return s.SourceAppUserModelId ?? ""; } catch { return ""; }
    }

    public static string PrettyAppName(string aumid)
    {
        if (string.IsNullOrWhiteSpace(aumid)) return "";
        string n = aumid;
        int bang = n.LastIndexOf('!');
        if (bang >= 0 && bang < n.Length - 1) n = n[(bang + 1)..];
        if (n.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) n = n[..^4];
        string lower = n.ToLowerInvariant();
        if (lower.Contains("spotify")) return "Spotify";
        if (lower == "308046b0af4a39cb" || lower.Contains("firefox")) return "Firefox";
        if (lower.Contains("msedge")) return "Edge";
        if (lower.Contains("chrome")) return "Chrome";
        if (lower.Contains("opera")) return "Opera";
        if (lower.Contains("brave")) return "Brave";
        if (lower.Contains("zunemusic") || lower == "microsoft.media.player") return "Reproductor multimedia";
        if (lower.Contains("applemusic") || lower.Contains("itunes")) return "Apple Music";
        if (lower.Contains("tidal")) return "TIDAL";
        if (lower.Contains("deezer")) return "Deezer";
        if (lower.Contains("vlc")) return "VLC";
        if (lower.Contains("foobar")) return "foobar2000";
        if (lower.Contains("musicbee")) return "MusicBee";
        if (lower.Contains("discord")) return "Discord";
        return n.Length > 24 ? n[..24] : n;
    }
}
