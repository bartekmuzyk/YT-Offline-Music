using System;
using System.Collections.Specialized;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Web;
using YT_Offline_Music.GitHub.Models;

namespace YT_Offline_Music.GitHub;

public class Client
{
    private readonly HttpClient httpClient;

    public Client(string accessToken)
    {
        httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Add("accept", "application/vnd.github+json");
        httpClient.DefaultRequestHeaders.Add("authorization", $"Bearer {accessToken}");
        httpClient.BaseAddress = new Uri("https://api.github.com");
    }

    private static NameValueCollection GetQueryParamsObject() => HttpUtility.ParseQueryString(string.Empty);

    public async Task<Release[]> GetReleases(string repo, int perPage, int page)
    {
        var queryParams = GetQueryParamsObject();
        queryParams.Add("per_page", perPage.ToString());
        queryParams.Add("page", page.ToString());
        
        var response = await httpClient.GetStringAsync($"/repos/{repo}/releases?{queryParams}");
        
        return JsonSerializer.Deserialize<Release[]>(response)!;
    } 
}