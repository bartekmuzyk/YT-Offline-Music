using System.IO;
using ReactiveUI;

namespace YT_Offline_Music.ViewModels;

public class LoadingViewModel : ViewModelBase
{
    private string loadingText = "";

    public string LoadingText
    {
        get => loadingText;
        set => this.RaiseAndSetIfChanged(ref loadingText, value);
    }

    public LoadingViewModel()
    {
        LoadingText = "Wczytywanie ustawień...";
    }
}