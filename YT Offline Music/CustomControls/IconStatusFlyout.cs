using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Material.Icons;
using Material.Icons.Avalonia;

namespace YT_Offline_Music.CustomControls;

public class IconStatusFlyout : Flyout
{
    public IconStatusFlyout(MaterialIconKind icon, string description, Color color)
    {
        var brush = new SolidColorBrush(color);
        
        Placement = FlyoutPlacementMode.Top;
        Content = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Children =
            {
                new MaterialIcon
                {
                    Width = 20,
                    Height = 20,
                    Kind = icon,
                    Foreground = brush,
                    VerticalAlignment = VerticalAlignment.Center
                },
                new WrapPanel
                {
                    VerticalAlignment = VerticalAlignment.Center,
                    MaxWidth = 300,
                    Children =
                    {
                        new TextBlock
                        {
                            FontSize = 12,
                            Text = description,
                            Foreground = brush,
                            VerticalAlignment = VerticalAlignment.Center,
                            Margin = new Thickness(10,0,0,0),
                            TextWrapping = TextWrapping.Wrap
                        }
                    }
                }
            }
        };
    }

    public static void ShowAtControlAndHide(IconStatusFlyout flyout, Control control, int hideAfterSeconds)
    {
        flyout.ShowAt(control);
        Task.Delay(TimeSpan.FromSeconds(hideAfterSeconds)).GetAwaiter().OnCompleted(flyout.Hide);
    }
}