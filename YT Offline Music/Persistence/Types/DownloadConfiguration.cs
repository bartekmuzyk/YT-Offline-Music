using System;

namespace YT_Offline_Music.Persistence.Types;

public class DownloadConfiguration
{
    public bool DownloadAll { get; set; }
    
    public bool OnlyDownloadNew { get; set; }

    public bool DownloadNewAndDeleteNonExistent { get; set; } = true;

    public bool SendViaFtp { get; set; } = true;

    public bool SendViaMtp { get; set; } = true;
    
    public bool CreateZipArchive { get; set; }

    public DownloadConfiguration Clone()
    {
        return (DownloadConfiguration)MemberwiseClone();
    }
}