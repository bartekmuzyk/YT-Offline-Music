using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using AvaloniaEdit.Utils;
using DynamicData;
using LibMtpSharp;
using LibMtpSharp.Structs;
using Material.Icons;
using ReactiveUI;
using TagLib.Riff;
using YT_Offline_Music.Extensions;
using YT_Offline_Music.GitHub.Models;
using YT_Offline_Music.Models;
using YT_Offline_Music.Persistence;
using YT_Offline_Music.Persistence.Types;
using YT_Offline_Music.Utils;
using YT_Offline_Music.YouTube;
using YT_Offline_Music.YouTube.Models;

namespace YT_Offline_Music.ViewModels;

public class MainViewModel : ViewModelBase
{
    private PopupPromptInfo popupPromptInfo = new();

    public PopupPromptInfo PopupPromptInfo
    {
        get => popupPromptInfo;
        private set => this.RaiseAndSetIfChanged(ref popupPromptInfo, value);
    }

    private string popupPromptTextBoxContent = "";

    public string PopupPromptTextBoxContent
    {
        get => popupPromptTextBoxContent;
        set
        {
            PopupPromptError = "";
            this.RaiseAndSetIfChanged(ref popupPromptTextBoxContent, value);
        }
    }

    private string popupPromptError = "";

    public string PopupPromptError
    {
        get => popupPromptError;
        set => this.RaiseAndSetIfChanged(ref popupPromptError, value);
    }

    private CurrentOperation currentOperation = new();

    public CurrentOperation CurrentOperation
    {
        get => currentOperation;
        private set => this.RaiseAndSetIfChanged(ref currentOperation, value);
    }

    private int? currentOperationProgress;

    public int? CurrentOperationProgress
    {
        get => currentOperationProgress;
        set => this.RaiseAndSetIfChanged(ref currentOperationProgress, value);
    }

    public ObservableCollection<Playlist> Playlists { get; } = new();

    private Playlist? selectedPlaylist;

    public Playlist? SelectedPlaylist
    {
        get => selectedPlaylist;
        set
        {
            this.RaiseAndSetIfChanged(ref selectedPlaylist, value); 
#pragma warning disable CS4014
            if (selectedPlaylist != null) OnPlaylistSelected(selectedPlaylist);
#pragma warning restore CS4014
        }
    }

    private Video[]? displayedVideos;

    public Video[]? DisplayedVideos
    {
        get => displayedVideos;
        set => this.RaiseAndSetIfChanged(ref displayedVideos, value);
    }

    private bool playlistActionsLocked;

    public bool PlaylistActionsLocked
    {
        get => playlistActionsLocked;
        set => this.RaiseAndSetIfChanged(ref playlistActionsLocked, value);
    }

    public DownloadConfiguration DownloadConfiguration { get; set; }

    public FtpConfig FtpConfig { get; }

    private bool loginAnonymouslyToFtp = true;

    public bool LoginAnonymouslyToFtp
    {
        get => loginAnonymouslyToFtp;
        set
        {
            FtpConfig.LoginAnonymously = value;
            this.RaiseAndSetIfChanged(ref loginAnonymouslyToFtp, value);
        }
    }

    private bool testingFtpConnectionInProgress;

    public bool TestingFtpConnectionInProgress
    {
        get => testingFtpConnectionInProgress;
        set => this.RaiseAndSetIfChanged(ref testingFtpConnectionInProgress, value);
    }

    private bool rememberDownloadConfig;

    public bool RememberDownloadConfig
    {
        get => rememberDownloadConfig;
        set => this.RaiseAndSetIfChanged(ref rememberDownloadConfig, value);
    }

    private MtpDevice[] mtpDevicesList;

    public MtpDevice[] MtpDevicesList
    {
        get => mtpDevicesList;
        set => this.RaiseAndSetIfChanged(ref mtpDevicesList, value);
    }

    private MtpDevice? selectedMtpDevice;

    public MtpDevice? SelectedMtpDevice
    {
        get => selectedMtpDevice;
        set => this.RaiseAndSetIfChanged(ref selectedMtpDevice, value);
    }

    private MtpDevice? usedMtpDevice;

    public MtpDevice? UsedMtpDevice
    {
        get => usedMtpDevice;
        set => this.RaiseAndSetIfChanged(ref usedMtpDevice, value);
    }

    public enum UpdateAvailabilityState
    {
        DidntCheckYet,
        Checking,
        NoneAvailable,
        Available
    }

    private UpdateAvailabilityState updateAvailability = UpdateAvailabilityState.DidntCheckYet;

    public UpdateAvailabilityState UpdateAvailability
    {
        get => updateAvailability;
        set => this.RaiseAndSetIfChanged(ref updateAvailability, value);
    }

    public bool CheckedForUpdates => UpdateAvailability is UpdateAvailabilityState.NoneAvailable or UpdateAvailabilityState.Available;

    public bool CheckingForUpdates => UpdateAvailability == UpdateAvailabilityState.Checking;

    private Release? availableUpdates;

    public Release? AvailableUpdates
    {
        get => availableUpdates;
        set => this.RaiseAndSetIfChanged(ref availableUpdates, value);
    }

    public MainViewModel()
    {
        FtpConfig = PersistenceManager.PersistedData.Preferences.FtpConfig;
        LoginAnonymouslyToFtp = FtpConfig.LoginAnonymously;
        ShowOperation(
            MaterialIconKind.Sync,
            "Wczytywanie zapisanych playlist...",
            true
        );
        Playlists.AddRange(PersistenceManager.PersistedData.PlaylistDataStore.Values);
        ClearCurrentOperation();
        
        // SetupMtpDeviceScanner();
    }

    private void SetupMtpDeviceScanner()
    {
        Task.Run(async () =>
        {
            while (true)
            {
                if (SelectedMtpDevice == null)
                {
                    using var rawDeviceList = new RawDeviceList();

                    MtpDevicesList = rawDeviceList.Select(rawDevice =>
                    {
                        var device = new MtpDevice(rawDevice);
                        using var openedMtpDevice = device.OpenDevice();
                        device.BatteryLevel = (int)openedMtpDevice.GetBatteryLevel(false);

                        return device;
                    }).ToArray();
                }

                await Task.Delay(TimeSpan.FromSeconds(3));
            }
        });
    }

    public void ClosePopupPrompt()
    {
        PopupPromptInfo = new PopupPromptInfo();
    }

    public void ShowPopupPrompt(MaterialIconKind icon, string title, string text, string promptWatermark,
        string acceptButtonText, Func<string, Task> acceptButtonCallback)
    {
        PopupPromptTextBoxContent = "";
        PopupPromptInfo = new PopupPromptInfo
        {
            IsOpened = true,
            Icon = icon.ToString(),
            Title = title,
            Text = text,
            Watermark = promptWatermark,
            AcceptButtonText = acceptButtonText,
            AcceptButtonCallback = acceptButtonCallback
        };
    }

    public void ShowOperation(MaterialIconKind icon, string description, bool indeterminate)
    {
        CurrentOperation = new CurrentOperation
        {
            InProgress = true,
            Icon = icon.ToString(),
            Description = description
        };
        CurrentOperationProgress = indeterminate ? null : 0;
    }

    public void UpdateOperation(int progress)
    {
        if (CurrentOperation.InProgress) CurrentOperationProgress = progress;
    }

    public void UpdateOperation(string description)
    {
        if (!CurrentOperation.InProgress) return;
        
        var cloned = CurrentOperation.From(CurrentOperation);
        cloned.Description = description;
        CurrentOperation = cloned;
    }

    public void UpdateOperation(int progress, string description)
    {
        if (!CurrentOperation.InProgress) return;
        
        var cloned = CurrentOperation.From(CurrentOperation);
        cloned.Description = description;
        CurrentOperation = cloned;
        UpdateOperation(progress);
    }

    public void ClearCurrentOperation()
    {
        CurrentOperation = new CurrentOperation();
    }

    public void DisplayVideosIfSelectedPlaylistIdIs(string playlistId, Video[]? videos)
    {
        if (SelectedPlaylist?.Id == playlistId) DisplayedVideos = videos;
    }

    private async Task OnPlaylistSelected(Playlist playlist)
    {
        DisplayedVideos = null;

        var videos = await YouTubeAPI.Instance.GetVideosInPlaylist(playlist.Id, YouTubeAPI.FetchMode.Store);
        DisplayVideosIfSelectedPlaylistIdIs(playlist.Id, videos);
    }
}