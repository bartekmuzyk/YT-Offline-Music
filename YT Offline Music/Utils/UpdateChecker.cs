using System;
using System.Threading.Tasks;
using YT_Offline_Music.GitHub;
using YT_Offline_Music.GitHub.Models;

namespace YT_Offline_Music.Utils;

public static class UpdateChecker
{
    public class UpdateCheckerException : Exception
    {
        public UpdateCheckerException(string message) : base(message)
        {
            Console.WriteLine($"UpdateChecker error: {this}");
        }
    }
    
    public const string CURRENT_VERSION_TAG = "v1.0";

    private const string REPO = "szkolny-eu/szkolny-android";
    
    #if DEBUG
    private static readonly Client gitHubClient = new(Secrets.Get("github_access_token"));
    #else
    private static readonly Client gitHubClient = new("PUT YOUR GITHUB API ACCESS TOKEN HERE");
    #endif

    public static async Task<Release?> CheckIfUpdateIsAvailable()
    {
        Release[] releases;

        try
        {
            releases = await gitHubClient.GetReleases(REPO, 1, 1);
        }
        catch (Exception e)
        {
            throw new UpdateCheckerException(e.Message);
        }

        if (releases.Length == 0) return null;

        var newestRelease = releases[0];

        return newestRelease.TagName != CURRENT_VERSION_TAG ? newestRelease : null;
    }
}