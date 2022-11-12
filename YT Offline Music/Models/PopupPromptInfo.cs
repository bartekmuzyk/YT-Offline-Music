using System;
using System.Threading.Tasks;

namespace YT_Offline_Music.Models;

public class PopupPromptInfo
{
    public bool IsOpened { get; set; }
    
    public string Icon { get; set; } = "";

    public string Title { get; set; } = "";

    public string Text { get; set; } = "";

    public string Watermark { get; set; } = "";

    public string AcceptButtonText { get; set; } = "";

    public Func<string, Task> AcceptButtonCallback { get; set; } = _ => Task.CompletedTask;
}