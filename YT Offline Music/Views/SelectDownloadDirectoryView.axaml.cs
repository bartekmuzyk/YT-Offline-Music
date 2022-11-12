using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

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
}