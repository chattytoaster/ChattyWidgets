using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Windows.Media.Control;
using Windows.Storage.Streams;

namespace ChattyWidgets.Core.Services;

public class MediaInfo
{
    public string Title { get; set; } = "Нет трека";
    public string Artist { get; set; } = "Не воспроизводится";
    public string Album { get; set; } = "";
    public string ThumbnailBase64 { get; set; } = "";
    public bool IsPlaying { get; set; } = false;
    public int Volume { get; set; } = 50;
}

public class MediaMonitorService
{
    private static MediaMonitorService? _instance;
    public static MediaMonitorService Instance => _instance ??= new MediaMonitorService();

    private const byte VK_VOLUME_MUTE = 0xAD;
    private const byte VK_VOLUME_DOWN = 0xAE;
    private const byte VK_VOLUME_UP = 0xAF;
    private const byte VK_MEDIA_NEXT_TRACK = 0xB0;
    private const byte VK_MEDIA_PREV_TRACK = 0xB1;
    private const byte VK_MEDIA_STOP = 0xB2;
    private const byte VK_MEDIA_PLAY_PAUSE = 0xB3;

    private const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
    private const uint KEYEVENTF_KEYUP = 0x0002;

    [DllImport("user32.dll")]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

    private GlobalSystemMediaTransportControlsSessionManager? _sessionManager;
    private GlobalSystemMediaTransportControlsSession? _currentSession;
    private System.Threading.Timer? _pollingTimer;
    private bool _isUpdating = false;

    public MediaInfo CurrentMedia { get; private set; } = new()
    {
        Title = "Нажмите Play для запуска",
        Artist = "Windows Media Controller",
        IsPlaying = false,
        Volume = 50
    };

    public event Action<MediaInfo>? MediaUpdated;

    private MediaMonitorService()
    {
        _ = InitializeAsync();
        _pollingTimer = new System.Threading.Timer(async _ => await RefreshMediaAsync(), null, 2000, 2000);
    }

    private async Task InitializeAsync()
    {
        try
        {
            _sessionManager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            if (_sessionManager != null)
            {
                _sessionManager.CurrentSessionChanged += async (s, e) =>
                {
                    HookCurrentSession();
                    await RefreshMediaAsync();
                };
                HookCurrentSession();
                await RefreshMediaAsync();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MediaMonitor] WinRT GSMTC init error: {ex.Message}");
        }
    }

    private void HookCurrentSession()
    {
        try
        {
            if (_sessionManager == null) return;
            _currentSession = _sessionManager.GetCurrentSession();
            if (_currentSession != null)
            {
                _currentSession.MediaPropertiesChanged += async (s, e) => await RefreshMediaAsync();
                _currentSession.PlaybackInfoChanged += async (s, e) => await RefreshMediaAsync();
            }
        }
        catch { }
    }

    public async Task RefreshMediaAsync()
    {
        if (_isUpdating) return;
        _isUpdating = true;

        try
        {
            if (_sessionManager == null)
            {
                _sessionManager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            }

            _currentSession = _sessionManager?.GetCurrentSession();

            if (_currentSession != null)
            {
                var props = await _currentSession.TryGetMediaPropertiesAsync();
                var playback = _currentSession.GetPlaybackInfo();

                bool isPlaying = playback?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
                string title = !string.IsNullOrWhiteSpace(props?.Title) ? props.Title : "Воспроизводится медиа";
                string artist = !string.IsNullOrWhiteSpace(props?.Artist) ? props.Artist : "Windows Audio";
                string album = props?.AlbumTitle ?? "";

                string thumbBase64 = CurrentMedia.ThumbnailBase64;
                if (props?.Thumbnail != null)
                {
                    try
                    {
                        using var stream = await props.Thumbnail.OpenReadAsync();
                        using var netStream = stream.AsStreamForRead();
                        using var ms = new MemoryStream();
                        await netStream.CopyToAsync(ms);
                        byte[] bytes = ms.ToArray();
                        if (bytes.Length > 0)
                        {
                            string mime = (bytes.Length > 8 && bytes[0] == 0x89 && bytes[1] == 0x50) ? "image/png" : "image/jpeg";
                            thumbBase64 = $"data:{mime};base64,{Convert.ToBase64String(bytes)}";
                        }
                    }
                    catch
                    {
                        // keep current or fallback
                    }
                }
                else if (props?.Title != CurrentMedia.Title)
                {
                    thumbBase64 = "";
                }

                CurrentMedia = new MediaInfo
                {
                    Title = title,
                    Artist = artist,
                    Album = album,
                    ThumbnailBase64 = thumbBase64,
                    IsPlaying = isPlaying,
                    Volume = CurrentMedia.Volume
                };

                MediaUpdated?.Invoke(CurrentMedia);
            }
            else
            {
                // No active session
                if (CurrentMedia.Title != "Нет активного трека")
                {
                    CurrentMedia = new MediaInfo
                    {
                        Title = "Нет активного трека",
                        Artist = "Запустите плеер (Spotify, Браузер...)",
                        Album = "",
                        ThumbnailBase64 = "",
                        IsPlaying = false,
                        Volume = CurrentMedia.Volume
                    };
                    MediaUpdated?.Invoke(CurrentMedia);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MediaMonitor] Refresh error: {ex.Message}");
        }
        finally
        {
            _isUpdating = false;
        }
    }

    public async void PlayPause()
    {
        try
        {
            if (_currentSession != null)
            {
                bool success = await _currentSession.TryTogglePlayPauseAsync();
                if (success)
                {
                    CurrentMedia.IsPlaying = !CurrentMedia.IsPlaying;
                    MediaUpdated?.Invoke(CurrentMedia);
                    return;
                }
            }
        }
        catch { }

        // Fallback to media key
        SendMediaKey(VK_MEDIA_PLAY_PAUSE);
        CurrentMedia.IsPlaying = !CurrentMedia.IsPlaying;
        MediaUpdated?.Invoke(CurrentMedia);
    }

    public async void Next()
    {
        try
        {
            if (_currentSession != null)
            {
                bool success = await _currentSession.TrySkipNextAsync();
                if (success) return;
            }
        }
        catch { }
        SendMediaKey(VK_MEDIA_NEXT_TRACK);
    }

    public async void Previous()
    {
        try
        {
            if (_currentSession != null)
            {
                bool success = await _currentSession.TrySkipPreviousAsync();
                if (success) return;
            }
        }
        catch { }
        SendMediaKey(VK_MEDIA_PREV_TRACK);
    }

    public void VolumeUp() => SendMediaKey(VK_VOLUME_UP);
    public void VolumeDown() => SendMediaKey(VK_VOLUME_DOWN);
    public void Mute() => SendMediaKey(VK_VOLUME_MUTE);

    private static void SendMediaKey(byte key)
    {
        keybd_event(key, 0, KEYEVENTF_EXTENDEDKEY, UIntPtr.Zero);
        keybd_event(key, 0, KEYEVENTF_EXTENDEDKEY | KEYEVENTF_KEYUP, UIntPtr.Zero);
    }
}
