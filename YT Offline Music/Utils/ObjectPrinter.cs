using System;
using System.Text.Json;

namespace YT_Offline_Music.Utils;

#if DEBUG
/// <summary>
/// Use for debugging purposes.
/// </summary>
public static class ObjectPrinter
{
    public static void PrintObjectToConsole(object? obj)
    {
        if (obj == null)
        {
            Console.WriteLine("null");
            return;
        }
        
        Console.WriteLine(JsonSerializer.Serialize(obj, new JsonSerializerOptions
        {
            WriteIndented = true
        }));
    }
}
#endif