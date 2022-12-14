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
using YT_Offline_Music.GitHub.Models;
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
            "Wklej link do playlisty aby doda?? j?? do listy i umo??liwi?? jej pobranie.",
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
                        "Podana playlista jest ju?? dodana do listy.",
                        Icon.Error
                    );
                    return;
                }
                
                vm.ShowOperation(MaterialIconKind.PlaylistAdd, "Dodawanie " + playlistId, true);

                var playlist = await ytApi.GetPlaylist(playlistId, YouTubeAPI.FetchMode.Api);

                if (playlist == null)
                {
                    SimpleMessageBox.Alert(
                        "B????d playlisty",
                        $"Playlista o ID {playlistId} nie istnieje.\n\nUpewnij si??, ??e nie jest ona prywatna i " +
                        "spr??buj ponownie.",
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
                    "B????d synchronizacji",
                    "Nie uda??o si?? uzyska?? informacji o zawarto??ci playlist'y, poniewa?? serwer Google zwr??ci?? " +
                    $"b????d podczas zapytania o ni??.\n\nWiadomo???? b????du:\n\n`{exception.GetBaseException().Message}`",
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
        
        vm.ShowOperation(MaterialIconKind.FolderNetwork, "????czenie z serwerem FTP...", true);
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
                        "B????d ????czenia z serwerem FTP",
                        "Nie uda??o si?? po????czy?? z serwerem, poniewa?? wyst??pi?? problem z " +
                        $"uwierzytelnieniem:\n\n{authenticationException.Message}\n\nPonowi?? pr??b???",
                        Icon.Warning
                    ),
                    SocketException socketException => await SimpleMessageBox.Confirm(
                        "B????d ????czenia z serwerem FTP",
                        $"Nie uda??o si?? po????czy?? z serwerem:\n\n{socketException.Message}\n\n" +
                        "Ponowi?? pr??b???",
                        Icon.Warning
                    ),
                    _ => await SimpleMessageBox.Confirm(
                        "B????d ????czenia z serwerem FTP",
                        "Nie uda??o si?? po????czy?? z serwerem przez niezydentyfikowany b????d. Ponowi?? pr??b???",
                        Icon.Warning
                    )
                };
            }
        } while (retry);

        if (ftp.IsConnected)
        {
            vm.ShowOperation(MaterialIconKind.Upload, "Synchronizowanie zawarto??ci serwera FTP...", false);
            await playlistSynchronizer.Sync(ftp, vm.UpdateOperation);
            vm.ClearCurrentOperation();
        }
    }

    private async Task SyncPlaylistThroughMtp(PlaylistSynchronizer playlistSynchronizer)
    {
        var vm = this.GetViewModel<MainViewModel>();
        
        var device = vm.UsedMtpDevice!;
        OpenedMtpDevice? openDevice = null;
        
        vm.ShowOperation(MaterialIconKind.UsbPort, "????czenie z urz??dzeniem MTP...", true);

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
                        "B????d ????czenia z urz??dzeniem MTP",
                        "Nie uda??o si?? po????czy?? z urz??dzeniem przez niezydentyfikowany b????d. Ponowi?? pr??b???",
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
                    "Wszystkie piosenki s?? aktualne. Nie ma nic do zrobienia.",
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
                "B????d pobieracza",
                "Wyst??pi?? nieoczekiwany b????d. Zobacz wyj??cie standardowe (stdout) po wi??cej informacji.",
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
                    "B????d usuwania",
                    "Nie uda??o si?? usun???? folderu playlisty. Zobacz wyj??cie standardowe (stdout) po wi??cej informacji.",
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
            "Czy na pewno chcesz usun???? t?? playlist?? lokalnie?",
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
                        $"Nie uda??o si?? po????czy??: {errorDescription}",
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
                new IconStatusFlyout(MaterialIconKind.Check, "Po????czenie udane!", Color.Parse("#8BC34A")),
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
        
        vm.ShowOperation(MaterialIconKind.FolderNetwork, "????czenie z serwerem FTP...", true);
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
                        "B????d ????czenia z serwerem FTP",
                        "Nie uda??o si?? po????czy?? z serwerem, poniewa?? wyst??pi?? problem z " +
                        $"uwierzytelnieniem:\n\n{authenticationException.Message}",
                        Icon.Warning
                    );
                    vm.ClearCurrentOperation();
                    return;
                case SocketException socketException:
                    SimpleMessageBox.Alert(
                        "B????d ????czenia z serwerem FTP",
                        $"Nie uda??o si?? po????czy?? z serwerem:\n\n{socketException.Message}",
                        Icon.Warning
                    );
                    vm.ClearCurrentOperation();
                    return;
                default:
                    SimpleMessageBox.Alert(
                        "B????d ????czenia z serwerem FTP",
                        "Nie uda??o si?? po????czy?? z serwerem przez niezydentyfikowany b????d",
                        Icon.Warning
                    );
                    vm.ClearCurrentOperation();
                    return;
            }
        }

        var videos = await ytApi.GetVideosInPlaylist(playlist.Id, YouTubeAPI.FetchMode.Store);

        if (videos == null) return;
        
        var playlistSynchronizer = new PlaylistSynchronizer(playlist.Id, videos);
        vm.ShowOperation(MaterialIconKind.Upload, "Synchronizowanie zawarto??ci serwera FTP...", false);
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

    private void OnCheckUpdatesButtonClick(object? sender, RoutedEventArgs e)
    {
        var vm = this.GetViewModel<MainViewModel>();

        vm.CheckingForUpdates = true;

        var updateCheckTask = UpdateChecker.CheckIfUpdateIsAvailable();
        updateCheckTask.GetAwaiter().OnCompleted(() =>
        {
            Release? release;

            try
            {
                release = updateCheckTask.Result;
            }
            catch (AggregateException aggregateException)
            {
                Console.WriteLine($"UpdateChecker error: {aggregateException.GetBaseException()}");
                SimpleMessageBox.Alert(
                    "B????d sprawdzania aktualizacji",
                    "Nie uda??o si?? sprawdzi?? aktualizacji. Sprawd?? po????czenie z internetem i spr??buj ponownie p????niej.",
                    Icon.Error
                );
                return;
            }
            finally
            {
                vm.CheckingForUpdates = false;
            }

            vm.AvailableUpdateRelease = release;
            
            if (release == null)
            {
                SimpleMessageBox.Alert(
                    "Brak aktualizacji",
                    "Posiadasz najnowsz?? wersj?? oprogramowania.",
                    Icon.Success
                );
            }
        });
    }
}