using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace YT_Offline_Music;

#if DEBUG
public static class Secrets
{
    private static Dictionary<string, string> values = new();

    public static void Load()
    {
        if (!File.Exists("secrets.json")) return;
        
        var content = File.ReadAllText("secrets.json");
        values = JsonSerializer.Deserialize<Dictionary<string, string>>(content)!;
    }

    public static string Get(string key)
    {
        return values[key];
    }
}
#endif
