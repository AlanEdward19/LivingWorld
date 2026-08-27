using LivingWorld.Domain;

namespace LivingWorld.Simulation.Geography;

/// <summary>Aplica delta sazonal por região/bioma via <see
/// cref="EnvironmentTemperatureAdjustment"/> (Fase 16.4, REALISM-12..15).</summary>
public sealed class TemperatureSeasonSystem : ISimulationSystem
{
    public const string SystemName = "temperature-season";
    internal const long SeasonalUntilTick = long.MaxValue;

    public string Name => SystemName;
    public TickFrequency Frequency => TickFrequency.Daily;

    public void Tick(WorldState world, TickContext ctx)
    {
        if (world.BiomeSeasonTemperatureRules.Count == 0)
            return;

        bool isMonthStart = world.CurrentDate.Day == 0 && world.CurrentDate.Hour == 0;
        bool needsBootstrap = !world.EnvironmentTemperatureAdjustments.Any(
            adjustment => adjustment.UntilTick == SeasonalUntilTick);
        if (!needsBootstrap && !isMonthStart)
            return;

        int month = world.CurrentDate.Month;
        int season = SeasonIndex(world.Calendar, month);
        if (!needsBootstrap)
        {
            int previousMonth = (month + world.Calendar.MonthsPerYear - 1) % world.Calendar.MonthsPerYear;
            if (season == SeasonIndex(world.Calendar, previousMonth))
                return;
        }

        ApplySeason(world, season);
    }

    internal static void ApplySeason(WorldState world, int seasonIndex)
    {
        var rulesByBiome = world.BiomeSeasonTemperatureRules.ToDictionary(rule => rule.BiomeId);
        var replacements = new List<EnvironmentTemperatureAdjustment>();

        foreach (var region in world.Map.Regions.OrderBy(region => region.Id.Value))
        {
            int biomeId = RepresentativeBiomeId(world, region);
            if (!rulesByBiome.TryGetValue(biomeId, out var rules))
                continue;

            if (seasonIndex < 0 || seasonIndex >= rules.SeasonDeltas.Count)
                continue;

            float delta = rules.SeasonDeltas[seasonIndex];
            replacements.Add(new EnvironmentTemperatureAdjustment(region.Id, delta, SeasonalUntilTick));
        }

        world.ReplaceSeasonalEnvironmentTemperatureAdjustments(replacements);
    }

    internal static int SeasonIndex(WorldCalendar calendar, int month) =>
        month / (calendar.MonthsPerYear / 4);

    private static int RepresentativeBiomeId(WorldState world, Region region)
    {
        var coord = region.Cells.OrderBy(cell => cell.X).ThenBy(cell => cell.Y).First();
        return world.Map.CellAt(coord).Biome.Id;
    }
}
