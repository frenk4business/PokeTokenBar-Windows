namespace PokeTokenBar.Services.Gameplay;

public sealed record PokedexFilter(string SearchText = "", string Generation = "All", string Rarity = "All", string Ownership = "All");

public static class PokedexQuery
{
    public static bool Passes(int speciesId, string displayName, string rarity, string generation, bool owned, bool shinyOwned, PokedexFilter filter)
    {
        if (!string.IsNullOrWhiteSpace(filter.SearchText))
        {
            var term = filter.SearchText.Trim();
            if (!displayName.Contains(term, StringComparison.OrdinalIgnoreCase)
                && !speciesId.ToString().Contains(term, StringComparison.OrdinalIgnoreCase)
                && !$"#{speciesId:000}".Contains(term, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        if (filter.Generation != "All" && generation != filter.Generation)
        {
            return false;
        }

        if (filter.Rarity != "All" && rarity != filter.Rarity)
        {
            return false;
        }

        return filter.Ownership switch
        {
            "Owned" => owned,
            "Missing" => !owned,
            "Shiny owned" => shinyOwned,
            _ => true
        };
    }
}
