using System.Collections.Generic;
using YT_Offline_Music.Persistence.Types;
using YT_Offline_Music.YouTube.Models;

namespace YT_Offline_Music.Persistence;

public class PersistentData
{
    public Preferences Preferences { get; set; } = new();

    public Store<Playlist> PlaylistDataStore { get; set; } = new();

    public Store<Video[]> PlaylistSongsStore { get; set; } = new();
    
    public Store<Video> VideoDataStore { get; set; } = new();
}