using System.Text.Json.Serialization;

namespace YT_Offline_Music.GitHub.Models;

public class ReleaseAsset
{
    [JsonPropertyName("browser_download_url")]
    public string DownloadUrl { get; set; }
    
    [JsonPropertyName("name")]
    public string Name { get; set; }
    
    [JsonPropertyName("content_type")]
    public string ContentType { get; set; }
    
    [JsonPropertyName("size")]
    public int Size { get; set; }
}