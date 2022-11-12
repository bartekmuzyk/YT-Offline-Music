using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using YT_Offline_Music.Extensions;
using YT_Offline_Music.Persistence;
using YT_Offline_Music.ViewModels;
using AppContext = YT_Offline_Music.Utils.AppContext;

namespace YT_Offline_Music.Views;

public partial class SelectDownloadDirectoryView : UserControl
{
    public SelectDownloadDirectoryView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
    
    public void OnChooseButtonClick(object? sender, RoutedEventArgs e)
    {
        var vm = this.GetViewModel<SelectDownloadDirectoryViewModel>();
        var openFolderDialog = new OpenFolderDialog { Title = "Folder dla pobranych piosenek" };
        var openFolderDialogTask = openFolderDialog.ShowAsync(AppContext.MainWindow);
        
        openFolderDialogTask.GetAwaiter().OnCompleted(() =>
        {
            var openedFolder = openFolderDialogTask.Result;

            if (openedFolder == null) return;
            
            vm.SelectedPath = openedFolder;
            vm.ContinueButtonEnabled = true;
        });
    }

    public void OnContinueButtonClick(object? sender, RoutedEventArgs e)
    {
#pragma warning disable CS4014
        this.GetViewModel<SelectDownloadDirectoryViewModel>().OnContinueButtonClicked();
#pragma warning restore CS4014
    }
}