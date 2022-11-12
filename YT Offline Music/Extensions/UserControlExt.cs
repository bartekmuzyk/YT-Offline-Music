using Avalonia.Controls;
using Material.Dialog;

namespace YT_Offline_Music.Extensions;

public static class UserControlExt
{
    public static T GetViewModel<T>(this UserControl userControl)
    {
        return (T)userControl.DataContext!;
    }
}