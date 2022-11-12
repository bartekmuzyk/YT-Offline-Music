namespace YT_Offline_Music.Persistence.Types;

public class Preferences
{
    public string? DownloadDir { get; set; }

    public FtpConfig FtpConfig { get; set; } = new();

    public DownloadConfiguration DefaultDownloadConfiguration { get; set; } = new();
}