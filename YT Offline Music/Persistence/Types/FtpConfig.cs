namespace YT_Offline_Music.Persistence.Types;

public class FtpConfig
{
    public string Address { get; set; } = "";

    public int Port { get; set; } = 21;

    public string Username { get; set; } = "";

    public string Password { get; set; } = "";

    public bool LoginAnonymously { get; set; } = true;
}