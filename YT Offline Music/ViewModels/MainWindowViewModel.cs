using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using ReactiveUI;
using YT_Offline_Music.Persistence;

namespace YT_Offline_Music.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private ViewModelBase content = new LoadingViewModel();

    public ViewModelBase Content
    {
        get => content;
        set => this.RaiseAndSetIfChanged(ref content, value);
    }

    public MainWindowViewModel()
    {
        var loadPersistedDataTask = LoadPersistedData();
        loadPersistedDataTask.GetAwaiter().OnCompleted(() =>
        {
            if (PersistenceManager.PersistedData.Preferences.DownloadDir == null)
            {
                var newVm = new SelectDownloadDirectoryViewModel
                {
                    ContinueCallback = () =>
                    {
                        Content = new MainViewModel();
                    }
                };
                Content = newVm;
                
                return;
            }
            
            Content = new MainViewModel();
        });
    }

    // ReSharper disable once MemberCanBeMadeStatic.Local
    private async Task LoadPersistedData()
    {
        var persistentDataFilePath = Path.Combine(
            Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)!,
            "persist.json"
        );
        await PersistenceManager.LoadPersistedData(persistentDataFilePath);
    }
}