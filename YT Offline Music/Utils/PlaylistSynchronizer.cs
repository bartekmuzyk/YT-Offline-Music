using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentFTP;
using LibMtpSharp;
using LibMtpSharp.Structs;
using YT_Offline_Music.Persistence;
using YT_Offline_Music.YouTube;
using YT_Offline_Music.YouTube.Downloader;
using YT_Offline_Music.YouTube.Models;

namespace YT_Offline_Music.Utils;

public class PlaylistSynchronizer
{
    private readonly string playlistId;
    
    private readonly Video[] videos;

    public PlaylistSynchronizer(string playlistId, Video[] videos)
    {
        this.playlistId = playlistId;
        this.videos = videos;
    }
    
    public async Task Sync(AsyncFtpClient connection, Action<int> progressCollectorCallback)
    {
        var progressCollector = new ProgressCollector(progressCollectorCallback);
                    
        for (var fileIndex = 0; fileIndex < videos.Length; fileIndex++)
            progressCollector.AddTask($"upload{fileIndex}");

        await connection.UploadDirectory(
            PathGenerator.PlaylistDir(playlistId),
            $"/{playlistId}",
            mode: FtpFolderSyncMode.Mirror,
            existsMode: FtpRemoteExists.Overwrite,
            progress: new Progress<FtpProgress>(progress =>
            {
                progressCollector.SetProgress($"upload{progress.FileIndex}", (int)progress.Progress);
            })
        );
    }

    public async Task Sync(OpenedMtpDevice mtpDevice, Action<int> progressCollectorCallback)
    {
        var progressCollector = new ProgressCollector(progressCollectorCallback);
        
        foreach (var videoId in videos.Select(video => video.VideoId))
            progressCollector.AddTask(videoId);

        foreach (var video in videos)
        {
            var trackFileInfo = new FileInfo(PathGenerator.SongMp3File(playlistId, video.VideoId));
            
            if (trackFileInfo.Extension != ".mp3") continue;

            TrackStruct track;

            using (var taggedFile = TagLib.File.Create(trackFileInfo.FullName))
            {
                track = new TrackStruct
                {
                    Title = taggedFile.Tag.Title,
                    Album = taggedFile.Tag.Album,
                    Artist = taggedFile.Tag.Performers[0],
                    FileName = trackFileInfo.Name
                };
            }
            
            var trackReader = new TrackReader(trackFileInfo);

            await Task.Run(() =>
            {
                mtpDevice.SendTrack(
                    ref track,
                    trackReader.GetDataFunction,
                    progress =>
                    {
                        progressCollector.SetProgress(video.VideoId, (int)(progress * 100.0));

                        return false;
                    }
                );
            });
            
            trackReader.CloseFileHandle();
        }
    }
}