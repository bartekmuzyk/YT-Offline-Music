using System;
using System.Text.Json.Serialization;

namespace YT_Offline_Music.GitHub.Models;

public class Release
{
    [JsonPropertyName("html_url")]
    public string HtmlUrl { get; set; }
    
    [JsonPropertyName("tag_name")]
    public string TagName { get; set; }
    
    [JsonPropertyName("name")]
    public string Name { get; set; }
    
    [JsonPropertyName("body")]
    public string Body { get; set; }

    [JsonPropertyName("prerelease")]
    public bool PreRelease { get; set; }
    
    [JsonPropertyName("published_at")]
    public string PublishedAtIso { get; set; }

    public DateTime PublishedAt => DateTime.Parse(PublishedAtIso);
    
    [JsonPropertyName("assets")]
    public ReleaseAsset[] Assets { get; set; }
}