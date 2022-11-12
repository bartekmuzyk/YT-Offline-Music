using System;
using Newtonsoft.Json;

namespace YT_Offline_Music.GitHub.Models;

public class Release
{
    [JsonProperty("html_url")]
    public string HtmlUrl { get; set; }
    
    [JsonProperty("tag_name")]
    public string TagName { get; set; }
    
    [JsonProperty("name")]
    public string Name { get; set; }
    
    [JsonProperty("prerelease")]
    public bool PreRelease { get; set; }
    
    [JsonProperty("published_at")]
    public string PublishedAtIso { get; set; }

    public DateTime PublishedAt => DateTime.Parse(PublishedAtIso);
}