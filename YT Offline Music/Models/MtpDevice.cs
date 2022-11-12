using LibMtpSharp.Structs;

namespace YT_Offline_Music.Models;

public class MtpDevice
{
    public RawDevice RawDevice { get; }

    public string Vendor => RawDevice.DeviceEntry.Vendor ?? "Niezidentyfikowany producent";

    public string ProductName => RawDevice.DeviceEntry.Product ?? "Niezidentyfikowany produkt";

    public int BatteryLevel { get; set; }

    public MtpDevice(RawDevice rawDevice)
    {
        RawDevice = rawDevice;
    }
}