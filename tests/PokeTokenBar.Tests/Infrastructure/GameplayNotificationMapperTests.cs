using PokeTokenBar.Core.Game;
using PokeTokenBar.Services.Gameplay;
using PokeTokenBar.Services.Notifications;
using PokeTokenBar.Services.Settings;

namespace PokeTokenBar.Tests.Infrastructure;

public sealed class GameplayNotificationMapperTests
{
    [Fact]
    public void HatchedEventCreatesHatchNotification()
    {
        var notifications = GameplayNotificationMapper.Map(
            new CompanionProgressEvent[] { new Hatched(5_000_000, 1, Rarity.Common, PokemonNature.Hardy, false) },
            StateWithNames((1, "Bulbasaur")),
            AppSettings.Default);

        var notification = Assert.Single(notifications);
        Assert.Equal(AppNotificationKind.Hatch, notification.Kind);
        Assert.Contains("Bulbasaur", notification.Message);
    }

    [Fact]
    public void ShinyHatchUsesShinyNotificationToggle()
    {
        var settings = AppSettings.Default with { ShinyNotifications = false };

        var notifications = GameplayNotificationMapper.Map(
            new CompanionProgressEvent[] { new Hatched(5_000_000, 133, Rarity.Uncommon, PokemonNature.Jolly, true) },
            StateWithNames((133, "Eevee")),
            settings).ToArray();

        var notification = Assert.Single(notifications);
        Assert.Equal(AppNotificationKind.Hatch, notification.Kind);
    }

    [Fact]
    public void ShinyHatchUsesShinyNotificationWhenEnabled()
    {
        var notifications = GameplayNotificationMapper.Map(
            new CompanionProgressEvent[] { new Hatched(5_000_000, 133, Rarity.Uncommon, PokemonNature.Jolly, true) },
            StateWithNames((133, "Eevee")),
            AppSettings.Default).ToArray();

        var notification = Assert.Single(notifications);
        Assert.Equal(AppNotificationKind.Shiny, notification.Kind);
    }

    [Fact]
    public void EvolutionAndGraduationCreateNotifications()
    {
        var graduated = new GraduatedCompanion(
            1,
            3,
            new[] { 1, 2, 3 },
            new[] { 1, 2, 3 },
            Rarity.Common,
            PokemonNature.Hardy,
            false,
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow,
            750_000_000);

        var notifications = GameplayNotificationMapper.Map(
            new CompanionProgressEvent[]
            {
                new Evolved(1, 1, 2, 1),
                new Graduated(1, graduated)
            },
            StateWithNames((1, "Bulbasaur"), (2, "Ivysaur"), (3, "Venusaur")),
            AppSettings.Default).ToArray();

        Assert.Equal(2, notifications.Length);
        Assert.Equal(AppNotificationKind.Evolution, notifications[0].Kind);
        Assert.Equal(AppNotificationKind.Graduation, notifications[1].Kind);
    }

    [Fact]
    public void GlobalNotificationToggleSuppressesAllNotifications()
    {
        var notifications = GameplayNotificationMapper.Map(
            new CompanionProgressEvent[] { new Evolved(1, 1, 2, 1) },
            StateWithNames((2, "Ivysaur")),
            AppSettings.Default with { NotificationsEnabled = false });

        Assert.Empty(notifications);
    }

    private static GameSaveState StateWithNames(params (int Id, string Name)[] names)
    {
        return GameSaveState.New() with
        {
            SpeciesNames = names.ToDictionary(name => name.Id, name => name.Name)
        };
    }
}
