using PokeTokenBar.Core.Game;
using PokeTokenBar.Services.Gameplay;
using PokeTokenBar.Services.Settings;

namespace PokeTokenBar.Services.Notifications;

public static class GameplayNotificationMapper
{
    public static IEnumerable<AppNotification> Map(
        IReadOnlyList<CompanionProgressEvent> events,
        GameSaveState state,
        AppSettings settings)
    {
        if (!settings.NotificationsEnabled)
        {
            yield break;
        }

        foreach (var domainEvent in events)
        {
            switch (domainEvent)
            {
                case Hatched hatched when hatched.IsShiny && settings.ShinyNotifications:
                    yield return new AppNotification("Shiny Pokemon!", $"A shiny {NameFor(hatched.SpeciesId, state)} hatched.", AppNotificationKind.Shiny);
                    break;
                case Hatched hatched when settings.HatchNotifications:
                    yield return new AppNotification("Your egg hatched!", $"{NameFor(hatched.SpeciesId, state)} joined you.", AppNotificationKind.Hatch);
                    break;
                case Evolved evolved when settings.EvolutionNotifications:
                    yield return new AppNotification($"{NameFor(evolved.FromSpeciesId, state)} evolved!", $"Your companion is now {NameFor(evolved.ToSpeciesId, state)}.", AppNotificationKind.Evolution);
                    break;
                case Graduated graduated when settings.GraduationNotifications:
                    yield return new AppNotification($"{NameFor(graduated.Companion.FinalSpeciesId, state)} graduated!", "Added to your Catch Log.", AppNotificationKind.Graduation);
                    break;
            }
        }
    }

    private static string NameFor(int speciesId, GameSaveState state)
    {
        return state.SpeciesNames.TryGetValue(speciesId, out var name) ? name : $"#{speciesId:000}";
    }
}
