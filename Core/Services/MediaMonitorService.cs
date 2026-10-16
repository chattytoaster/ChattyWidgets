using System.Runtime.InteropServices;

namespace ChattyWidgets.Core.Services;

public class MediaInfo
{
    public string Title { get; set; } = "Нет трека";
    public string Artist { get; set; } = "Не воспроизводится";
    public string Album { get; set; } = "";
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
    }

    public void PlayPause()
    {
        SendMediaKey(VK_MEDIA_PLAY_PAUSE);
        CurrentMedia.IsPlaying = !CurrentMedia.IsPlaying;
        MediaUpdated?.Invoke(CurrentMedia);
    }

    public void Next()
    {
        SendMediaKey(VK_MEDIA_NEXT_TRACK);
    }

    public void Previous()
    {
        SendMediaKey(VK_MEDIA_PREV_TRACK);
    }

    public void VolumeUp()
    {
        SendMediaKey(VK_VOLUME_UP);
    }

    public void VolumeDown()
    {
        SendMediaKey(VK_VOLUME_DOWN);
    }

    public void Mute()
    {
        SendMediaKey(VK_VOLUME_MUTE);
    }

    private static void SendMediaKey(byte key)
    {
        keybd_event(key, 0, KEYEVENTF_EXTENDEDKEY, UIntPtr.Zero);
        keybd_event(key, 0, KEYEVENTF_EXTENDEDKEY | KEYEVENTF_KEYUP, UIntPtr.Zero);
    }
}
