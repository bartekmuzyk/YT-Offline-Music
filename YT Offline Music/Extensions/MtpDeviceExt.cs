using LibMtpSharp;
using YT_Offline_Music.Models;

namespace YT_Offline_Music.Extensions;

public static class MtpDeviceExt
{
    public static OpenedMtpDevice OpenDevice(this MtpDevice mtpDevice)
    {
        var rawDevice = mtpDevice.RawDevice;
        
        return new OpenedMtpDevice(ref rawDevice, false);
    }
}