using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Google.Apis.Services;
using Google.Apis.YouTube.v3;
using YT_Offline_Music.Persistence;
using YT_Offline_Music.YouTube.ModelBuilders;
using YT_Offline_Music.YouTube.Models;

namespace YT_Offline_Music.YouTube;

// ReSharper disable once InconsistentNaming
public class YouTubeAPI
{
    public enum FetchMode
    {
        Api,
        Store,
        StoreIfAvailable
    }

    public static YouTubeAPI Instance;
    
    private readonly YouTubeService youTubeService;
    
    public YouTubeAPI(string apiKey)
    {
        youTubeService = new YouTubeService(new BaseClientService.Initializer
        {
            ApiKey = apiKey,
            ApplicationName = "YT Offline Music"
        });
        Instance = this;
    }

    public async Task<Playlist?> GetPlaylist(string playlistId, FetchMode fetchMode)
    {
        var playlistDataStore = PersistenceManager.PersistedData.PlaylistDataStore;
        
        switch (fetchMode)
        {
            case FetchMode.Store:
            case FetchMode.StoreIfAvailable:
                if (playlistDataStore.ContainsKey(playlistId))
                {
                    return playlistDataStore[playlistId];
                }

                return fetchMode == FetchMode.Store ? null : await GetPlaylist(playlistId, FetchMode.Api);
            case FetchMode.Api:
                break;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(fetchMode),
                    fetchMode,
                    "Invalid fetch mode: " + fetchMode
                );
        }
        
        var playlistRequest = youTubeService.Playlists.List("snippet");
        playlistRequest.Id = playlistId;

        var response = await playlistRequest.ExecuteAsync();
        if (response.Items.Count < 1) return null;

        var rawPlaylist = response.Items[0]; 
        var playlistModel = PlaylistModelBuilder.Build(rawPlaylist);

        playlistDataStore[playlistId] = playlistModel;
        await PersistenceManager.SavePersistedData();

        return playlistModel;
    }

    public async Task<Video[]?> GetVideosInPlaylist(string playlistId, FetchMode fetchMode)
    {
        var playlistSongsStore = PersistenceManager.PersistedData.PlaylistSongsStore;

        switch (fetchMode)
        {
            case FetchMode.Store:
            case FetchMode.StoreIfAvailable:
                if (playlistSongsStore.ContainsKey(playlistId))
                {
                    return playlistSongsStore[playlistId];
                }

                return fetchMode == FetchMode.Store ? null : await GetVideosInPlaylist(playlistId, FetchMode.Api);
            case FetchMode.Api:
                break;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(fetchMode),
                    fetchMode,
                    "Invalid fetch mode: " + fetchMode
                );
        }

        var result = new List<Video>();

        var nextPageToken = "";
        while (nextPageToken != null)
        {
            var playlistItemsRequest = youTubeService.PlaylistItems.List("snippet");
            playlistItemsRequest.PlaylistId = playlistId;
            playlistItemsRequest.MaxResults = 50;
            playlistItemsRequest.PageToken = nextPageToken;

            var response = await playlistItemsRequest.ExecuteAsync();

            foreach (var playlistItem in response.Items)
            {
                var video = VideoModelBuilder.Build(playlistItem);
                result.Add(video);

                PersistenceManager.PersistedData.VideoDataStore[video.VideoId] = video;
            }

            nextPageToken = response.NextPageToken;
        }

        var resultArray = result.ToArray();
        playlistSongsStore[playlistId] = resultArray;
        await PersistenceManager.SavePersistedData();

        return resultArray;
    }
}