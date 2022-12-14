using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;
using Mono.Unix;
using TagLib;
using Xabe.FFmpeg;
using Xabe.FFmpeg.Downloader;
using YoutubeExplode;
using YoutubeExplode.Videos.Streams;
using YT_Offline_Music.Extensions;
using YT_Offline_Music.YouTube.Models;
using File = System.IO.File;

namespace YT_Offline_Music.YouTube.Downloader;

public static class YouTubeDownloader
{
    private const string FFMPEG_DIR = "FFmpeg";
    
    private class DownloadProgressReporter : IProgress<double>
    {
        private readonly Action<int> callback;

        public DownloadProgressReporter(Action<int> callback)
        {
            this.callback = callback;
        }
        
        public void Report(double value) => callback((int)(value * 100.0));
    }

    private static readonly YoutubeClient youtube = new();

    private static readonly HttpClient httpClient = new();

    private static async Task SetupFFmpeg()
    {
        if (!Directory.Exists(FFMPEG_DIR))
        {
            Console.WriteLine("Downloading FFmpeg");
            Directory.CreateDirectory(FFMPEG_DIR);
            await FFmpegDownloader.GetLatestVersion(FFmpegVersion.Official, FFMPEG_DIR);
        }
        
        Console.WriteLine($"FFmpeg found at {FFMPEG_DIR}");

        if (OperatingSystem.IsLinux())
        {
            Console.WriteLine("Linux detected. Setting executable permissions.");

            foreach (var filename in new[] { "ffmpeg", "ffprobe" })
            {
                // ReSharper disable once UseObjectOrCollectionInitializer
                var unixFileInfo = new UnixFileInfo(Path.Join(FFMPEG_DIR, filename));
                unixFileInfo.FileAccessPermissions = FileAccessPermissions.AllPermissions;
            }
        }
        
        FFmpeg.SetExecutablesPath(FFMPEG_DIR);
    }

    public static async Task<string[]> DownloadVideos(
        string destinationDirectoryPath, string playlistName, IReadOnlyList<Video> videos,
        Action<int> onPlaylistProgress, Action<int> onSongDownloadProgress, bool skipIfTargetFileExists)
    {
        await SetupFFmpeg();
        SetupTempDir();

        try
        {
            var progressCollector = new ProgressCollector(onSongDownloadProgress);

            foreach (var videoId in videos.Select(video => video.VideoId))
            {
                progressCollector.AddTask($"{videoId}.download");
                progressCollector.AddTask($"{videoId}.convert");
            }

            var downloadedVideoIds = new List<string>();
            var processedVideoCount = 0;
            var taskList = new List<Task>();
            
            onPlaylistProgress(processedVideoCount);

            for (var i = 0; i < videos.Count; i++)
            {
                var video = videos[i];
                var trackNumber = (uint)(i + 1);
                
                var task = Task.Run(async () =>
                {
                    var targetMp3FilePath = Path.Join(destinationDirectoryPath, $"{video.VideoId}.mp3");

                    if (skipIfTargetFileExists && File.Exists(targetMp3FilePath))
                    {
                        progressCollector.SetProgress($"{video.VideoId}.download", 100);
                        progressCollector.SetProgress($"{video.VideoId}.convert", 100);
                        processedVideoCount++;
                        onPlaylistProgress(processedVideoCount);
                        return;
                    }
                    
                    // Downloading audio stream
                    Console.WriteLine($"{video.VideoId}: Downloading audio");
                    var streamManifest = await youtube.Videos.Streams.GetManifestAsync(video.VideoId);
                    var streamInfo = streamManifest.GetAudioOnlyStreams()
                        .Where(stream => stream.Container == Container.WebM)
                        .GetWithHighestBitrate();
                    var tempWebmPath = $"tmp/{video.VideoId}.webm";
                    await youtube.Videos.Streams.DownloadAsync(streamInfo, tempWebmPath, new DownloadProgressReporter(
                        progress =>
                        {
                            progressCollector.SetProgress($"{video.VideoId}.download", progress);
                        }));

                    // Converting audio stream from .webm to .mp3
                    Console.WriteLine($"{video.VideoId}: Converting audio");
                    var tempMp3Path = $"tmp/{video.VideoId}.mp3";
                    var conversion = await FFmpeg.Conversions.FromSnippet.ExtractAudio(tempWebmPath, tempMp3Path);
                    conversion.OnProgress += (_, args) =>
                    {
                        progressCollector.SetProgress($"{video.VideoId}.convert", args.Percent);
                    };

                    await conversion.Start();

                    // Download thumbnail
                    Console.WriteLine($"{video.VideoId}: Downloading thumbnail");
                    var tempThumbPath = $"tmp/{video.VideoId}.jpg";
                    var thumbResponseStream = await httpClient.GetStreamAsync(video.ThumbnailUrl);

                    await using (var thumbFileWriteStream = File.OpenWrite(tempThumbPath))
                    {
                        await thumbResponseStream.CopyToAsync(thumbFileWriteStream);
                    }
                    
                    // Set MP3 tags
                    Console.WriteLine($"{video.VideoId}: Setting tags");
                    using (var file = TagLib.File.Create(tempMp3Path))
                    {
                        file.Tag.Title = video.Title;
                        file.Tag.Album = playlistName;
                        file.Tag.AlbumArtists = new[] { "YouTube" };
                        file.Tag.Performers = new[] { video.Channel };
                        file.Tag.Pictures = new IPicture[]
                        {
                            new Picture(tempThumbPath)
                            {
                                Type = PictureType.FrontCover, Description = "Cover",
                                MimeType = MediaTypeNames.Image.Jpeg
                            }
                        };
                        file.Tag.Track = trackNumber;

                        file.Save();
                    }

                    // Copy to destination directory
                    Console.WriteLine($"{tempMp3Path} -> {targetMp3FilePath}");
                    File.Copy(tempMp3Path, targetMp3FilePath, true);

                    // Clean up
                    #if !DEBUG
                    Console.WriteLine("Cleaning up.");
                    foreach (var filename in new[] { tempThumbPath, tempWebmPath, tempMp3Path })
                        File.Delete(filename);
                    #endif

                    downloadedVideoIds.Add(video.VideoId);
                    onPlaylistProgress(downloadedVideoIds.Count);
                });
                taskList.Add(task);
            }
            
            await Task.WhenAll(taskList);

            return downloadedVideoIds.ToArray();
        }
        // ReSharper disable once RedundantCatchClause
        catch (Exception)
        {
            #if !DEBUG
            CleanCache();
            #endif
            throw;
        }
    }

    private static void CleanCache()
    {
        if (Directory.Exists("tmp")) Directory.Delete("tmp", true);
    }

    private static void SetupTempDir()
    {
        CleanCache();
        if (!Directory.Exists("tmp")) Directory.CreateDirectory("tmp");
    }
}