using PokeTokenBar.Core.Game;

namespace PokeTokenBar.Services.Gameplay;

public enum InventoryItemKind
{
    RareCandy,
    Mint,
    ShinyCharm
}

public enum ShopItemKind
{
    RareCandy,
    Mint,
    ShinyCharm,
    NormalEgg,
    UncommonEgg,
    RareEgg
}

public sealed record InventoryState
{
    public int RareCandyCount { get; init; }

    public int MintCount { get; init; }

    public bool HasShinyCharm { get; init; }
}

public sealed record ShopCatalogItem(
    ShopItemKind Kind,
    string DisplayName,
    string Description,
    long Price,
    InventoryItemKind? InventoryItem,
    EggTier? EggTier);

public sealed record EconomyActionResult(
    bool Success,
    GameSaveState State,
    string Message,
    IReadOnlyList<CompanionProgressEvent>? Events = null,
    bool RequiresConfirmation = false,
    bool ShinyDiscardWarning = false);

public static class EconomyCatalog
{
    public const long RareCandyPrice = 500_000_000;
    public const long MintPrice = 100_000_000;
    public const long ShinyCharmPrice = 3_000_000_000;
    public const long NormalEggPrice = 1_000_000_000;
    public const long UncommonEggPrice = 2_500_000_000;
    public const long RareEggPrice = 4_000_000_000;

    public static IReadOnlyList<ShopCatalogItem> Items { get; } =
    [
        new(ShopItemKind.Mint, "Mint", "Reroll Nature", MintPrice, InventoryItemKind.Mint, null),
        new(ShopItemKind.RareCandy, "Rare Candy", "+100M growth", RareCandyPrice, InventoryItemKind.RareCandy, null),
        new(ShopItemKind.NormalEgg, "Normal Pokémon Egg", "Fresh random Pokémon", NormalEggPrice, null, EggTier.Normal),
        new(ShopItemKind.UncommonEgg, "Uncommon Egg", "Uncommon or better", UncommonEggPrice, null, EggTier.Uncommon),
        new(ShopItemKind.ShinyCharm, "Shiny Charm", "Better shiny odds", ShinyCharmPrice, InventoryItemKind.ShinyCharm, null),
        new(ShopItemKind.RareEgg, "Rare Egg", "Rare or Legendary", RareEggPrice, null, EggTier.Rare)
    ];

    public static ShopCatalogItem Get(ShopItemKind kind) => Items.Single(item => item.Kind == kind);
}
