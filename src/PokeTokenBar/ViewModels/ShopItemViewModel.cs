using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PokeTokenBar.ViewModels;

public sealed record ShopItemViewModel(
    string Kind,
    string Icon,
    string DisplayName,
    string Description,
    string Price,
    bool CanBuy,
    string ButtonText,
    bool RequiresEggConfirmation,
    bool ShinyWarning,
    ImageSource? IconImage = null)
{
    public ImageSource? DisplayIconImage => IconImage;

    public string BadgeText => Kind switch
    {
        "RareCandy" => "RC",
        "Mint" => "M",
        "ShinyCharm" => "SC",
        "NormalEgg" or "UncommonEgg" or "RareEgg" => "Egg",
        _ => Icon
    };
}

public sealed record BagItemViewModel(
    string Kind,
    string Icon,
    string DisplayName,
    string Description,
    string CountText,
    bool CanUse,
    string ButtonText,
    ImageSource? IconImage = null)
{
    public ImageSource? DisplayIconImage => IconImage ?? CachedBagItemIconLoader.Load(Kind);

    public string BadgeText => Kind switch
    {
        "RareCandy" => "RC",
        "Mint" => "M",
        "ShinyCharm" => "SC",
        _ => Icon
    };
}

internal static class CachedBagItemIconLoader
{
    public static ImageSource? Load(string kind)
    {
        var fileName = kind switch
        {
            "RareCandy" => "item-rare-candy.png",
            "Mint" => "item-modest-mint.png",
            "ShinyCharm" => "item-shiny-charm.png",
            _ => null
        };

        if (fileName is null)
        {
            return null;
        }

        var path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PokeTokenBar",
            "Cache",
            "sprites",
            fileName);
        if (!File.Exists(path) && kind == "Mint")
        {
            path = Path.Combine(AppContext.BaseDirectory, "Resources", "Items", "Mint.png");
        }

        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.UriSource = new global::System.Uri(path, global::System.UriKind.Absolute);
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch
        {
            return null;
        }
    }
}
