using System.IO;
using YT_Offline_Music.Persistence;

namespace YT_Offline_Music.Utils;

public static class PathGenerator
{
    private static string DownloadDir => PersistenceManager.PersistedData.Preferences.DownloadDir!;

    public static string PlaylistDir(string playlistId) => Path.Join(DownloadDir, playlistId);

    public static string SongMp3File(string playlistId, string videoId) => Path.Join(DownloadDir, playlistId, $"{videoId}.mp3");
}