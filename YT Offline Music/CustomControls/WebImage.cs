using System.Net.Http;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using YT_Offline_Music.Utils;

namespace YT_Offline_Music.CustomControls;

public class WebImage : Image
{
    private string sourceUrl;

    public string SourceUrl
    {
        get => sourceUrl;
        set
        {
            SetAndRaise(SourceUrlProperty, ref sourceUrl, value);
#pragma warning disable CS4014
            DownloadImage();
#pragma warning restore CS4014
        }
    }

    // ReSharper disable once MemberCanBePrivate.Global
    public static readonly AvaloniaProperty<string> SourceUrlProperty =
        AvaloniaProperty.RegisterDirect<WebImage, string>(
            nameof(SourceUrl),
            o => o.SourceUrl,
            (o, v) => o.SourceUrl = v
        );

    private async Task DownloadImage()
    {
        var bitmap = await ImageDownloader.Download(sourceUrl);

        if (bitmap != null) Source = bitmap;
    }
}