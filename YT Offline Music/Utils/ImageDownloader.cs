using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;

namespace YT_Offline_Music.Utils;

public static class ImageDownloader
{
    private static readonly HttpClient httpClient = new();

    private static readonly Dictionary<string, Bitmap> cache = new();

    public static async Task<Bitmap?> Download(string url)
    {
        if (cache.ContainsKey(url)) return cache[url];

        var response = await httpClient.GetAsync(url);
        
        if (!response.IsSuccessStatusCode) return null;
        
        var bitmap = new Bitmap(await response.Content.ReadAsStreamAsync());
        cache[url] = bitmap;

        return bitmap;

    }
}