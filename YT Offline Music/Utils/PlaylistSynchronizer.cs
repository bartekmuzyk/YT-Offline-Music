using System;
using System.IO;
using System.Threading.Tasks;
using FluentFTP;
using YT_Offline_Music.Persistence;
using YT_Offline_Music.YouTube;
using YT_Offline_Music.YouTube.Downloader;
using YT_Offline_Music.YouTube.Models;

namespace YT_Offline_Music.Utils;

public static class PlaylistSynchronizer
{
    public static async Task Sync(AsyncFtpClient connection, string playlistId, Video[] videos, Action<int> progressCollectorCallback)
    {
        var progressCollector = new ProgressCollector(progressCollectorCallback);
                    
        for (var fileIndex = 0; fileIndex < videos.Length; fileIndex++)
            progressCollector.AddTask($"upload{fileIndex}");

        await connection.UploadDirectory(
            Path.Join(PersistenceManager.PersistedData.Preferences.DownloadDir, playlistId),
            $"/{playlistId}",
            mode: FtpFolderSyncMode.Mirror,
            existsMode: FtpRemoteExists.Overwrite,
            progress: new Progress<FtpProgress>(progress =>
            {
                progressCollector.SetProgress($"upload{progress.FileIndex}", (int)progress.Progress);
            })
        );
    }
}