using UnityEngine;
using System;
using LibVLCSharp;
using UnityEngine.UI;

/// <summary>
/// this class serves as an example on how to configure playback in Unity with VLC for Unity using LibVLCSharp.
/// for libvlcsharp usage documentation, please visit https://code.videolan.org/videolan/LibVLCSharp/-/blob/master/docs/home.md
/// </summary>
public class VLCMinimalPlayback : MonoBehaviour
{
    private const int SEEK_TIME_DELTA = 5000;

    private LibVLC libVlc;
    private MediaPlayer mediaPlayer;

    [SerializeField]
    private RawImage screenCanvas;

    private RenderTexture texture;

    private bool hasTexture;
    private Texture2D vlcTexture = null;

    private bool isPlaying;

    [SerializeField]
    private bool playOnAwake = true;

    [SerializeField]
    private bool flipTextureX = true;

    [SerializeField]
    private bool flipTextureY = true;

    [SerializeField]
    private string uri = "http://commondatastorage.googleapis.com/gtv-videos-bucket/sample/BigBuckBunny.mp4";

    public string Uri
    {
        get => uri;
        set
        {
            Stop();
            uri = value;
            Play();
        }
    }

    public int Volume
    {
        get => mediaPlayer?.Volume ?? 0;
        set => mediaPlayer?.SetVolume(Mathf.Clamp(value, 0, 100));
    }

    private void Awake()
    {
        Core.Initialize(Application.dataPath);

        libVlc = new LibVLC(enableDebugLogs: true, "--no-osd");

        //Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
        //libVlc.Log += (s, e) => Debug.Log(e.FormattedLog); // enable this for logs in the editor

        if (!Application.isEditor &&
            Application.platform == RuntimePlatform.Android)
        {
            flipTextureY = !flipTextureY;
            flipTextureX = !flipTextureX;
        }

        if (playOnAwake)
        {
            Play();
        }
    }

    public void SeekForward()
    {
        Debug.Log("[VLC] Seeking forward");
        mediaPlayer.SetTime(mediaPlayer.Time + SEEK_TIME_DELTA);
    }

    public void SeekBackward()
    {
        Debug.Log("[VLC] Seeking backward");
        mediaPlayer.SetTime(mediaPlayer.Time - SEEK_TIME_DELTA);
    }

    private void OnDestroy()
    {
        mediaPlayer?.Stop();
        mediaPlayer?.Media?.Dispose();
        mediaPlayer?.Dispose();
        mediaPlayer = null;
        libVlc?.Dispose();
        libVlc = null;
    }

    public void Play()
    {
        if (isPlaying) { return; }
        Debug.Log("[VLC] Play");
        isPlaying = true;

        mediaPlayer = new MediaPlayer(libVlc);
        mediaPlayer.Media = new Media(libVlc, new Uri(uri));
        mediaPlayer.Play();
    }

    public void Stop()
    {
        if (!isPlaying) { return; }
        Debug.Log("[VLC] Stop");
        isPlaying = false;

        mediaPlayer?.Stop();
        mediaPlayer?.Media?.Dispose();
        mediaPlayer?.Dispose();
        mediaPlayer = null;
        vlcTexture = null;
    }

    private void Update()
    {
        if (!isPlaying) { return; }

        uint width = 0;
        uint height = 0;
        mediaPlayer.Size(0, ref width, ref height);

        //Automatically resize output textures if size changes
        if (!hasTexture || vlcTexture.width != width || vlcTexture.height != height)
        {
            ResizeOutputTextures(width, height);
        }

        if (hasTexture)
        {
            var intPtr = mediaPlayer.GetTexture(width, height, out var updated);

            if (updated && intPtr != IntPtr.Zero)
            {
                vlcTexture.UpdateExternalTexture(intPtr);

                var flip = new Vector2(flipTextureX ? -1 : 1, flipTextureY ? -1 : 1);
                //If you wanted to do post processing outside of VLC you could use a shader here.
                Graphics.Blit(vlcTexture, texture, flip, Vector2.zero);
            }
        }
    }

    private void ResizeOutputTextures(uint px, uint py)
    {
        var intPtr = mediaPlayer.GetTexture(px, py, out var updated);

        if (px != 0 && py != 0 && updated && intPtr != IntPtr.Zero)
        {
            //If the currently playing video uses the Bottom Right orientation, we have to do this to avoid stretching it.
            if (GetVideoOrientation() == VideoOrientation.BottomRight)
            {
                (px, py) = (py, px);
            }

            vlcTexture = Texture2D.CreateExternalTexture((int)px, (int)py, TextureFormat.RGBA32, false, true, intPtr);
            texture = new RenderTexture(vlcTexture.width, vlcTexture.height, 0, RenderTextureFormat.ARGB32);
            hasTexture = true;

            if (screenCanvas != null)
            {
                if (!screenCanvas.gameObject.TryGetComponent<AspectRatioFitter>(out var screenAspectRatioFitter))
                {
                    screenAspectRatioFitter = screenCanvas.gameObject.AddComponent<AspectRatioFitter>();
                }

                screenAspectRatioFitter.aspectRatio = vlcTexture.width / (float)vlcTexture.height;

                screenCanvas.texture = texture;
            }
        }
    }

    private VideoOrientation? GetVideoOrientation()
    {
        var tracks = mediaPlayer?.Tracks(TrackType.Video);

        if (tracks == null || tracks.Count == 0)
        {
            return null;
        }

        //At the moment we're assuming the track we're playing is the first track
        var orientation = tracks[0]?.Data.Video.Orientation;

        return orientation;
    }
}
