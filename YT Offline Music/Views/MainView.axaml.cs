using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace YT_Offline_Music.Views;

public partial class MainView : UserControl
{
    public MainView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}