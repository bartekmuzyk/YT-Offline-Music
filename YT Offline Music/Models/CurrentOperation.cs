namespace YT_Offline_Music.Models;

public class CurrentOperation
{
    public bool InProgress { get; set; }

    public string Icon { get; set; } = "";

    public string Description { get; set; } = "";

    public static CurrentOperation From(CurrentOperation obj) => (CurrentOperation)obj.MemberwiseClone();
}