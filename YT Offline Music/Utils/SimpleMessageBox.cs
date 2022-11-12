using System.Threading.Tasks;
using MessageBox.Avalonia;
using MessageBox.Avalonia.DTO;
using MessageBox.Avalonia.Enums;

namespace YT_Offline_Music.Utils;

public static class SimpleMessageBox
{
    public static void Alert(string title, string content, Icon icon)
    {
        MessageBoxManager.GetMessageBoxStandardWindow(new MessageBoxStandardParams
        {
            ContentTitle = title,
            ContentMessage = content,
            Icon = icon,
            Markdown = true
        }).Show();
    }

    public static async Task<bool> Confirm(string title, string content, Icon icon)
    {
        var result = await MessageBoxManager.GetMessageBoxStandardWindow(new MessageBoxStandardParams
        {
            ContentTitle = title,
            ContentMessage = content,
            Icon = icon,
            Markdown = true,
            ButtonDefinitions = ButtonEnum.YesNo
        }).Show();

        return result == ButtonResult.Yes;
    }
}