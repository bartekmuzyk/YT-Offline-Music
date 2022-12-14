using Google.Apis.YouTube.v3.Data;
using Video = YT_Offline_Music.YouTube.Models.Video;

namespace YT_Offline_Music.YouTube.ModelBuilders;

public static class VideoModelBuilder
{
    public static Video Build(PlaylistItem source)
    {
        return new Video
        {
            VideoId = source.Snippet.ResourceId.VideoId,
            Title = source.Snippet.Title,
            ThumbnailUrl = source.Snippet.Thumbnails.Medium.Url,
            Channel = source.Snippet.VideoOwnerChannelTitle
        };
    }
}