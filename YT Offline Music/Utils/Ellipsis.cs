namespace YT_Offline_Music.Utils;

public static class Ellipsis
{
    public static string Apply(string source, int maxLength) => 
        source.Length <= maxLength ?
            source
            :
            source[..(maxLength - 3)] + "...";
}