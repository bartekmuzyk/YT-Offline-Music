using YT_Offline_Music.YouTube.Models;

namespace YT_Offline_Music.YouTube.ModelBuilders;

public static class PlaylistModelBuilder
{
    public static Playlist Build(Google.Apis.YouTube.v3.Data.Playlist source)
    {
        return new Playlist
        {
            Id = source.Id,
            Title = source.Snippet.Title,
            PlaylistOwner = source.Snippet.ChannelTitle,
            ThumbnailUrl = source.Snippet.Thumbnails.High.Url
        };
    }
}