using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using Utilities.WebRequestRest;

public class YoutubePlayerLiveStream : MonoBehaviour
{
    [SerializeField]
    private RawImage thumbnailScreen;

    [SerializeField]
    private string liveStreamUrl;

    private bool hasThumbnail;

    private readonly Regex dataRegexOption = new Regex(@"ytInitialPlayerResponse\s*=\s*({.+?})\s*;\s*(?:var\s+meta|</script|\n)", RegexOptions.Multiline);

    private readonly Dictionary<string, string> headers = new Dictionary<string, string>
    {
        {
            "User-Agent", "Mozilla/5.0 (X11; Linux x86_64; rv:10.0) Gecko/20100101 Firefox/10.0 (Chrome)"
        }
    };

    private void Start()
    {
        GetYoutubeVideoResource(OnUrlParsed, liveStreamUrl);
    }

    private async void GetYoutubeVideoResource(Action<string> callback, string url)
    {
        try
        {
            await Task.Delay(1000);
            await GetPageData(url, callback);
        }
        catch (Exception e)
        {
            Debug.LogError(e);

            if (Application.isPlaying)
            {
                GetYoutubeVideoResource(callback, url);
            }
        }
    }

    private void OnUrlParsed(string url)
    {
        if (TryGetComponent<VLCMinimalPlayback>(out var vlcPlayer))
        {
            vlcPlayer.Uri = url;
            vlcPlayer.Volume = 80;
        }
    }

    private async Task GetPageData(string url, Action<string> callback)
    {
        var result = await Rest.GetAsync(url, headers);

        if (result.Successful)
        {
            var pageData = Encoding.UTF8.GetString(result.ResponseData);
            await GetUrlFromJson(callback, pageData);
        }
        else
        {
            Debug.LogError($"Failed to get {url}!\n[{result.ResponseCode}]{result.ResponseBody}");
        }
    }

    private async Task GetUrlFromJson(Action<string> callback, string pageSource)
    {
        var playerResponse = string.Empty;

        var dataMatch = dataRegexOption.Match(pageSource);

        if (dataMatch.Success)
        {
            var extractedJson = dataMatch.Result("$1");

            if (!extractedJson.Contains("raw_player_response:ytInitialPlayerResponse"))
            {
                playerResponse = JObject.Parse(extractedJson).ToString();
            }
            else
            {
                Debug.LogError("Failed to get initial player response!");
                return;
            }
        }
        else
        {
            await Task.Delay(250);
            await GetUrlFromJson(callback, pageSource);
        }

        var playerData = JObject.Parse(playerResponse);
        var videoDetails = playerData["videoDetails"];
        var streamingData = playerData["streamingData"];
        var isLiveStream = videoDetails?["isLiveContent"]?.Value<bool>();

        if (isLiveStream.HasValue && isLiveStream.Value)
        {
            var isUpcoming = videoDetails["isUpcoming"]?.Value<bool>();

            if (isUpcoming.HasValue)
            {
                if (!hasThumbnail)
                {
                    var thumbnails = videoDetails!["thumbnail"]!["thumbnails"];
                    var thumbnail = thumbnails!.Last();
                    var thumbnailUrl = thumbnail["url"]!.ToString();
                    var thumbnailTexture = await Rest.DownloadTextureAsync(thumbnailUrl);
                    thumbnailScreen.texture = thumbnailTexture;
                    hasThumbnail = true;
                }

                await Task.Delay(15000);
                await GetUrlFromJson(callback, pageSource);
            }

            var isLive = videoDetails["isLive"]?.Value<bool>();

            if (isLive.HasValue)
            {
                var liveUrl = streamingData!["hlsManifestUrl"]!.ToString();
                callback.Invoke(liveUrl);
            }
        }
        else
        {
            // TODO replace with adaptive formats
            // Get the highest quality format
            var videoUrl = streamingData!["formats"]!.Last()!["url"]!.ToString();
            callback.Invoke(videoUrl);
        }
    }
}
