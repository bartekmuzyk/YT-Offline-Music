using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using ReactiveUI;
using YT_Offline_Music.Persistence;

namespace YT_Offline_Music.ViewModels;

public class SelectDownloadDirectoryViewModel : ViewModelBase
{
#pragma warning disable CS8618
    public Action ContinueCallback;
#pragma warning restore CS8618
    
    private string selectedPath = "";

    public string SelectedPath
    {
        get => selectedPath;
        set => this.RaiseAndSetIfChanged(ref selectedPath, value);
    }

    private bool continueButtonEnabled;

    public bool ContinueButtonEnabled
    {
        get => continueButtonEnabled;
        set => this.RaiseAndSetIfChanged(ref continueButtonEnabled, value);
    }

    public async Task OnContinueButtonClicked()
    {
        ContinueButtonEnabled = false;
        PersistenceManager.PersistedData.Preferences.DownloadDir = selectedPath;
        await PersistenceManager.SavePersistedData();
        ContinueCallback();
    }
}