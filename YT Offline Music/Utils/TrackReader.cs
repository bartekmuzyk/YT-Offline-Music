using System;
using System.Collections.Generic;
using System.IO;

namespace YT_Offline_Music.Utils;

public class TrackReader
{
    private readonly FileStream fileStream;

    public Func<int, IList<byte>?> GetDataFunction => howMuchToRead =>
    {
        var buffer = new byte[howMuchToRead];
        var bytesRead = fileStream.Read(buffer, 0, howMuchToRead);

        return bytesRead == 0 ? null : buffer;
    };

    // ReSharper disable once SuggestBaseTypeForParameterInConstructor
    public TrackReader(FileInfo trackFileInfo)
    {
        fileStream = File.OpenRead(trackFileInfo.FullName);
    }

    public void CloseFileHandle()
    {
        fileStream.Close();
    }
}