using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Web;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using FluentFTP;
using LibMtpSharp;
using Material.Icons;
using MessageBox.Avalonia.Enums;
using YT_Offline_Music.CustomControls;
using YT_Offline_Music.Extensions;
using YT_Offline_Music.Models;
using YT_Offline_Music.Persistence;
using YT_Offline_Music.Persistence.Types;
using YT_Offline_Music.Utils;
using YT_Offline_Music.ViewModels;
using YT_Offline_Music.YouTube;
using YT_Offline_Music.YouTube.Downloader;
using YT_Offline_Music.YouTube.Models;
using FtpConfig = YT_Offline_Music.Persistence.Types.FtpConfig;

namespace YT_Offline_Music.Views;

public partial class MainView : UserControl
{
    private const string DOWNLOADING_MESSAGE_FORMAT = "Pobieranie {0} ({1}/{2})";
    
    #if DEBUG
    private readonly YouTubeAPI ytApi = new(Secrets.Get("yt_api_key"));
    #else
    private readonly YouTubeAPI ytApi = new("PUT YOUR YOUTUBE DATA API V3 KEY HERE");
    #endif

    private AsyncFtpClient ftp;

#pragma warning disable CS8618
    public MainView()
#pragma warning restore CS8618
    {
        InitializeComponent();
        SetupFtpClient(PersistenceManager.PersistedData.Preferences.FtpConfig);
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void SetupFtpClient(FtpConfig config)
    {
        var fluentConfig = new FluentFTP.FtpConfig
        {
            LogToConsole = true,
            LogHost = true
        };

        #if DEBUG
        fluentConfig.LogUserName = fluentConfig.LogPassword = true;
        #endif

        ftp = new AsyncFtpClient(
            host: config.Address,
            port: config.Port,
            config: fluentConfig
        );

        if (!config.LoginAnonymously)
        {
            ftp.Credentials = new NetworkCredential(config.Username, config.Password);
        }
    }

    public void OnPopupPromptAcceptButtonClick(object? sender, RoutedEventArgs e)
    {
        var vm = this.GetViewModel<MainViewModel>();
        vm.PopupPromptInfo.AcceptButtonCallback(vm.PopupPromptTextBoxContent);
    }

    private void OnAddPlaylistButtonClick(object? sender, RoutedEventArgs e)
    {
        var vm = this.GetViewModel<MainViewModel>();
        vm.ShowPopupPrompt(
            MaterialIconKind.PlaylistAdd,
            "Dodawanie playlisty",
            "Wklej link do playlisty aby dodać ją do listy i umożliwić jej pobranie.",
            "https://www.youtube.com/playlist?list=...",
            "Dodaj",
            async enteredUrl =>
            {
                if (!Uri.IsWellFormedUriString(enteredUrl, UriKind.Absolute) || !enteredUrl.Contains("youtube.com/playlist?list="))
                {
                    vm.PopupPromptError = "Niepoprawny adres URL.";
                    return;
                }
                
                var url = new Uri(enteredUrl);
                var queryParams = HttpUtility.ParseQueryString(url.Query);
                var playlistId = queryParams["list"]!;
                vm.ClosePopupPrompt();

                // Check if a playlist with the given ID is already present
                if (vm.Playlists.Any(loadedPlaylist => loadedPlaylist.Id == playlistId))
                {
                    SimpleMessageBox.Alert(
                        "Dodawanie przerwane",
                        "Podana playlista jest już dodana do listy.",
                        Icon.Error
                    );
                    return;
                }
                
                vm.ShowOperation(MaterialIconKind.PlaylistAdd, "Dodawanie " + playlistId, true);

                var playlist = await ytApi.GetPlaylist(playlistId, YouTubeAPI.FetchMode.Api);

                if (playlist == null)
                {
                    SimpleMessageBox.Alert(
                        "Błąd playlisty",
                        $"Playlista o ID {playlistId} nie istnieje.\n\nUpewnij się, że nie jest ona prywatna i " +
                        "spróbuj ponownie.",
                        Icon.Error
                    );
                    vm.ClearCurrentOperation();
                    return;
                }
                
                vm.Playlists.Add(playlist);
                vm.ClearCurrentOperation();
            }
        );
    }

    private void OnPopupPromptCancelButtonClick(object? sender, RoutedEventArgs e)
    {
        var vm = this.GetViewModel<MainViewModel>();
        vm.ClosePopupPrompt();
        vm.PopupPromptTextBoxContent = "";
        vm.PopupPromptError = "";
    }

    private void OnPlaylistSyncButtonClick(object? sender, RoutedEventArgs e)
    {
        var vm = this.GetViewModel<MainViewModel>();

        vm.PlaylistActionsLocked = true;
        vm.ShowOperation(MaterialIconKind.Sync, $"Synchronizowanie {vm.SelectedPlaylist!.Title}...", true);

        var playlistId = vm.SelectedPlaylist.Id;
        var getVideosInPlaylistTask = ytApi.GetVideosInPlaylist(playlistId, YouTubeAPI.FetchMode.Api);
        getVideosInPlaylistTask.GetAwaiter().OnCompleted(() =>
        {
            Video[] playlistVideos;
            
            try
            {
                playlistVideos = getVideosInPlaylistTask.Result!;
            }
            catch (AggregateException exception)
            {
                SimpleMessageBox.Alert(
                    "Błąd synchronizacji",
                    "Nie udało się uzyskać informacji o zawartości playlist'y, ponieważ serwer Google zwrócił " +
                    $"błąd podczas zapytania o nią.\n\nWiadomość błędu:\n\n`{exception.GetBaseException().Message}`",
                    Icon.Error
                );
                vm.ClearCurrentOperation();
                vm.PlaylistActionsLocked = false;

                return;
            }
            
            vm.ClearCurrentOperation();
            vm.DisplayVideosIfSelectedPlaylistIdIs(playlistId, playlistVideos);
            vm.PlaylistActionsLocked = false;
        });
    }

    private async Task SyncPlaylistThroughFtp(PlaylistSynchronizer playlistSynchronizer)
    {
        var vm = this.GetViewModel<MainViewModel>();
        
        vm.ShowOperation(MaterialIconKind.FolderNetwork, "Łączenie z serwerem FTP...", true);
        SetupFtpClient(PersistenceManager.PersistedData.Preferences.FtpConfig);

        bool retry;
        do
        {
            try
            {
                await ftp.Connect();
                retry = false;
            }
            catch (Exception exception)
            {
                Console.WriteLine($"Connection with FTP server to move playlist files failed: {exception}");
                await ftp.Disconnect();
                
                retry = exception switch
                {
                    FtpAuthenticationException authenticationException => await SimpleMessageBox.Confirm(
                        "Błąd łączenia z serwerem FTP",
                        "Nie udało się połączyć z serwerem, ponieważ wystąpił problem z " +
                        $"uwierzytelnieniem:\n\n{authenticationException.Message}\n\nPonowić próbę?",
                        Icon.Warning
                    ),
                    SocketException socketException => await SimpleMessageBox.Confirm(
                        "Błąd łączenia z serwerem FTP",
                        $"Nie udało się połączyć z serwerem:\n\n{socketException.Message}\n\n" +
                        "Ponowić próbę?",
                        Icon.Warning
                    ),
                    _ => await SimpleMessageBox.Confirm(
                        "Błąd łączenia z serwerem FTP",
                        "Nie udało się połączyć z serwerem przez niezydentyfikowany błąd. Ponowić próbę?",
                        Icon.Warning
                    )
                };
            }
        } while (retry);

        if (ftp.IsConnected)
        {
            vm.ShowOperation(MaterialIconKind.Upload, "Synchronizowanie zawartości serwera FTP...", false);
            await playlistSynchronizer.Sync(ftp, vm.UpdateOperation);
            vm.ClearCurrentOperation();
        }
    }

    private async Task SyncPlaylistThroughMtp(PlaylistSynchronizer playlistSynchronizer)
    {
        var vm = this.GetViewModel<MainViewModel>();
        
        var device = vm.UsedMtpDevice!;
        OpenedMtpDevice? openDevice = null;
        
        vm.ShowOperation(MaterialIconKind.UsbPort, "Łączenie z urządzeniem MTP...", true);

        bool retry;
        do
        {
            try
            {
                openDevice = device.OpenDevice();
                retry = false;
            }
            catch (Exception exception)
            {
                Console.WriteLine($"Connection with MTP device to move playlist files failed: {exception}");
                await ftp.Disconnect();
                
                retry = exception switch
                {
                    _ => await SimpleMessageBox.Confirm(
                        "Błąd łączenia z urządzeniem MTP",
                        "Nie udało się połączyć z urządzeniem przez niezydentyfikowany błąd. Ponowić próbę?",
                        Icon.Warning
                    )
                };
            }
        } while (retry);

        if (openDevice == null) return;

        // var targetDirectoryId = openDevice.DirectoryExistsInInternalStorage("YT Offline Music")
        //     ? openDevice.GetIdOfDirectoryInInternalStorage("YT Offline Music")
        //     : openDevice.CreateDirectoryInInternalStorage("YT Offline Music");

        await playlistSynchronizer.Sync(openDevice, vm.UpdateOperation);

        openDevice.Dispose();
    }

    private async Task DownloadCurrentPlaylist(DownloadConfiguration config)
    {
        var vm = this.GetViewModel<MainViewModel>();
        
        var playlist = vm.SelectedPlaylist!;
        var truncatedPlaylistName = Ellipsis.Apply(playlist.Title, 20);
        vm.PlaylistActionsLocked = true;
        
        vm.ShowOperation(
            MaterialIconKind.Download,
            string.Format(DOWNLOADING_MESSAGE_FORMAT, truncatedPlaylistName, "-", "-"),
            false
        );

        var videos = await ytApi.GetVideosInPlaylist(playlist.Id, YouTubeAPI.FetchMode.Store);
            
        if (videos == null) return;
            
        vm.UpdateOperation(0, string.Format(DOWNLOADING_MESSAGE_FORMAT, truncatedPlaylistName, 0, videos.Length));
        
        var playlistDir = PathGenerator.PlaylistDir(playlist.Id);

        if (!Directory.Exists(playlistDir)) Directory.CreateDirectory(playlistDir);

        try
        {
            var newlyDownloadedVideoIds = await YouTubeDownloader.DownloadVideos(
                playlistDir,
                playlist.Title,
                videos,
                downloadedVideosCount => vm.UpdateOperation(
                    string.Format(DOWNLOADING_MESSAGE_FORMAT, truncatedPlaylistName, downloadedVideosCount,
                        videos.Length)
                ),
                videoDownloadProgress =>
                {
                    vm.UpdateOperation(videoDownloadProgress);
                },
                config.DownloadNewAndDeleteNonExistent || config.OnlyDownloadNew
            );

            var videoIdsOfSongsToDelete = Array.Empty<string>();

            if (config.DownloadNewAndDeleteNonExistent)
            {
                var requestedVideoIds = videos.Select(video => video.VideoId);
                videoIdsOfSongsToDelete = Directory.GetFiles(playlistDir)
                    .Where(path => !requestedVideoIds.Contains(Path.GetFileNameWithoutExtension(path)))
                    .Select(Path.GetFileNameWithoutExtension)
                    .ToArray()!;
            }

            foreach (var songFilePath in videoIdsOfSongsToDelete.Select(videoId => PathGenerator.SongMp3File(playlist.Id, videoId)))
                File.Delete(songFilePath);

            if (newlyDownloadedVideoIds.Length == 0 && videoIdsOfSongsToDelete.Length == 0)
            {
                SimpleMessageBox.Alert(
                    "Pobieranie przerwane",
                    "Wszystkie piosenki są aktualne. Nie ma nic do zrobienia.",
                    Icon.Warning
                );
                return;
            }

            var playlistSynchronizer = new PlaylistSynchronizer(playlist.Id, videos);
            
            // Send to FTP server if requested and configuration is available
            if (config.SendViaFtp && !string.IsNullOrEmpty(PersistenceManager.PersistedData.Preferences.FtpConfig.Address))
            {
                await SyncPlaylistThroughFtp(playlistSynchronizer);
            }

            if (config.SendViaMtp && vm.UsedMtpDevice != null)
            {
                await SyncPlaylistThroughMtp(playlistSynchronizer);
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"Downloader error: {e}");
            SimpleMessageBox.Alert(
                "Błąd pobieracza",
                "Wystąpił nieoczekiwany błąd. Zobacz wyjście standardowe (stdout) po więcej informacji.",
                Icon.Warning
            );
        }
        finally
        {
            vm.ClearCurrentOperation();
            vm.PlaylistActionsLocked = false;
        }
    }

    private void OnPlaylistDownloadButtonClick(object? sender, RoutedEventArgs e)
    {
        var vm = this.GetViewModel<MainViewModel>();

        if (vm.RememberDownloadConfig)
        {
            PersistenceManager.PersistedData.Preferences.DefaultDownloadConfiguration = vm.DownloadConfiguration.Clone();
#pragma warning disable CS4014
            PersistenceManager.SavePersistedData();
        }

        DownloadCurrentPlaylist(vm.DownloadConfiguration);
#pragma warning restore CS4014
        this.GetControl<Button>("DownloadPlaylistButton").Flyout.Hide();
        vm.RememberDownloadConfig = false;
    }

    private void OnOpenPlaylistInBrowserButtonClick(object? sender, RoutedEventArgs e)
    {
        var vm = this.GetViewModel<MainViewModel>();
        BrowserOpener.OpenUrl($"https://www.youtube.com/playlist?list={vm.SelectedPlaylist!.Id}");
    }

    // ReSharper disable once MemberCanBeMadeStatic.Local
    private async Task DeletePlaylist(Playlist playlist)
    {
        var playlistDir = PathGenerator.PlaylistDir(playlist.Id);

        if (Directory.Exists(playlistDir))
        {
            try
            {
                Directory.Delete(playlistDir, true);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Failed to delete directory of playlist with ID {playlist.Id}: {e}");
                SimpleMessageBox.Alert(
                    "Błąd usuwania",
                    "Nie udało się usunąć folderu playlisty. Zobacz wyjście standardowe (stdout) po więcej informacji.",
                    Icon.Warning
                );
                return;
            }
        }

        PersistenceManager.PersistedData.PlaylistDataStore.Remove(playlist.Id);

        if (PersistenceManager.PersistedData.PlaylistSongsStore.ContainsKey(playlist.Id))
        {
            foreach (var video in PersistenceManager.PersistedData.PlaylistSongsStore[playlist.Id])
            {
                PersistenceManager.PersistedData.VideoDataStore.Remove(video.VideoId);
            }
        
            PersistenceManager.PersistedData.PlaylistSongsStore.Remove(playlist.Id);    
        }

        await PersistenceManager.SavePersistedData();
    }

    private void OnDeletePlaylistButtonClick(object? sender, RoutedEventArgs e)
    {
        var vm = this.GetViewModel<MainViewModel>();
        var playlist = vm.SelectedPlaylist!;
        var confirmTask = SimpleMessageBox.Confirm(
            "Usuwanie playlisty",
            "Czy na pewno chcesz usunąć tę playlistę lokalnie?",
            Icon.Question
        );
        confirmTask.GetAwaiter().OnCompleted(() =>
        {
            if (!confirmTask.Result) return;

            vm.PlaylistActionsLocked = true;
            vm.ShowOperation(
                MaterialIconKind.Delete,
                $"Usuwanie {playlist.Title}...",
                true
            );
            
            DeletePlaylist(playlist).GetAwaiter().OnCompleted(() =>
            {
                if (vm.SelectedPlaylist?.Id == playlist.Id) vm.SelectedPlaylist = null;
                vm.Playlists.Remove(playlist);
                
                vm.ClearCurrentOperation();
                vm.PlaylistActionsLocked = false;
            });
        });
    }

    private void OnSaveFtpConfigurationButtonClick(object? sender, RoutedEventArgs e)
    {
        var vm = this.GetViewModel<MainViewModel>();

        vm.PlaylistActionsLocked = true;
        vm.ShowOperation(MaterialIconKind.ContentSave, "Zapisywanie konfiguracji FTP...", true);
        PersistenceManager.PersistedData.Preferences.FtpConfig = vm.FtpConfig;
        PersistenceManager.SavePersistedData().GetAwaiter().OnCompleted(() =>
        {
            vm.ClearCurrentOperation();
            vm.PlaylistActionsLocked = false;
        });
    }
    
    private void OnTestFtpConnectionButtonClick(object? sender, RoutedEventArgs e)
    {
        var vm = this.GetViewModel<MainViewModel>();
        var self = (Button)sender!;
        
        vm.TestingFtpConnectionInProgress = true;

        var ftpConfig = vm.FtpConfig;
        try
        {
            SetupFtpClient(ftpConfig);
        }
        catch (Exception setupException)
        {
            Console.WriteLine($"FTP client setup error: {setupException}");
            
            vm.TestingFtpConnectionInProgress = false;
            IconStatusFlyout.ShowAtControlAndHide(
                new IconStatusFlyout(MaterialIconKind.AlertCircle, "Niepoprawna konfiguracja", Color.Parse("#F44336")),
                self,
                2
            );
            return;
        }

        var connectTask = ftp.Connect();
        connectTask.GetAwaiter().OnCompleted(() =>
        {
            try
            {
                if (connectTask.Exception != null) throw connectTask.Exception.GetBaseException();
            }
            catch (Exception exception)
            {
                Console.WriteLine($"Failed to connect to {ftpConfig.Address}:{ftpConfig.Port}: {exception}");
        
                var errorDescription = exception switch
                {
                    SocketException socketException => socketException.Message,
                    FtpAuthenticationException ftpAuthenticationException => ftpAuthenticationException.Message,
                    _ => exception.ToString()
                };
        
                IconStatusFlyout.ShowAtControlAndHide(
                    new IconStatusFlyout(
                        MaterialIconKind.AlertCircle, 
                        $"Nie udało się połączyć: {errorDescription}",
                        Color.Parse("#F44336")
                    ),
                    self,
                    5
                );
                vm.TestingFtpConnectionInProgress = false;
                return;
            }

            ftp.Disconnect();
            IconStatusFlyout.ShowAtControlAndHide(
                new IconStatusFlyout(MaterialIconKind.Check, "Połączenie udane!", Color.Parse("#8BC34A")),
                self,
                2
            );
            vm.TestingFtpConnectionInProgress = false;
        });
    }

    private async Task ForceFtpSync()
    {
        var vm = this.GetViewModel<MainViewModel>();

        var playlist = vm.SelectedPlaylist!;
        
        vm.ShowOperation(MaterialIconKind.FolderNetwork, "Łączenie z serwerem FTP...", true);
        SetupFtpClient(PersistenceManager.PersistedData.Preferences.FtpConfig);
        
        try
        {
            await ftp.Connect();
        }
        catch (Exception exception)
        {
            Console.WriteLine($"Connection with FTP server to move playlist files failed: {exception}");
            ftp.Disconnect().Wait();

            switch (exception)
            {
                case FtpAuthenticationException authenticationException:
                    SimpleMessageBox.Alert(
                        "Błąd łączenia z serwerem FTP",
                        "Nie udało się połączyć z serwerem, ponieważ wystąpił problem z " +
                        $"uwierzytelnieniem:\n\n{authenticationException.Message}",
                        Icon.Warning
                    );
                    vm.ClearCurrentOperation();
                    return;
                case SocketException socketException:
                    SimpleMessageBox.Alert(
                        "Błąd łączenia z serwerem FTP",
                        $"Nie udało się połączyć z serwerem:\n\n{socketException.Message}",
                        Icon.Warning
                    );
                    vm.ClearCurrentOperation();
                    return;
                default:
                    SimpleMessageBox.Alert(
                        "Błąd łączenia z serwerem FTP",
                        "Nie udało się połączyć z serwerem przez niezydentyfikowany błąd",
                        Icon.Warning
                    );
                    vm.ClearCurrentOperation();
                    return;
            }
        }

        var videos = await ytApi.GetVideosInPlaylist(playlist.Id, YouTubeAPI.FetchMode.Store);

        if (videos == null) return;
        
        var playlistSynchronizer = new PlaylistSynchronizer(playlist.Id, videos);
        vm.ShowOperation(MaterialIconKind.Upload, "Synchronizowanie zawartości serwera FTP...", false);
        await playlistSynchronizer.Sync(ftp, vm.UpdateOperation);
        vm.ClearCurrentOperation();
    }

    private void OnForceFtpSyncButtonClick(object? sender, RoutedEventArgs e)
    {
#pragma warning disable CS4014
        ForceFtpSync();
#pragma warning restore CS4014
    }

    private void DownloadPlaylistButton_OnClick(object? sender, RoutedEventArgs e)
    {
        var vm = this.GetViewModel<MainViewModel>();
        var self = (Button)sender!;

        vm.DownloadConfiguration = PersistenceManager.PersistedData.Preferences.DefaultDownloadConfiguration.Clone();
        self.Flyout.Hide();
        self.Flyout.ShowAt(self);
    }

    private void OnScanMtpDevicesButtonClick(object? sender, RoutedEventArgs e)
    {
        var vm = this.GetViewModel<MainViewModel>();

        vm.MtpDevicesList = Array.Empty<MtpDevice>();
    }

    private void OnUseThisMtpDeviceButtonClick(object? sender, RoutedEventArgs e)
    {
        var vm = this.GetViewModel<MainViewModel>();

        vm.UsedMtpDevice = vm.SelectedMtpDevice;
        vm.MtpDevicesList = Array.Empty<MtpDevice>();
    }

    private void OnGoBackToMtpDeviceListButtonClick(object? sender, RoutedEventArgs e)
    {
        var vm = this.GetViewModel<MainViewModel>();

        vm.UsedMtpDevice = null;
    }
}