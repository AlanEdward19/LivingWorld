using LivingWorld.Api.Visual.Observable;
using LivingWorld.Api.Visual.Scope;
using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Geography;
using LivingWorld.Simulation.Behavior.Decision;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Economy.Market;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Tests.Integration.Visual;

/// <summary>AD-189: o que o cliente desenhava sem o motor dizer vem da projeção, lido do estado do motor: a direção do vento e a
/// folhagem no tempo do escopo, a curva do dia por hora (o clima não volta a cada tick), a luz do prédio e o alvo da intenção.</summary>
public sealed class EngineTruthProjectionTests
{
    private static (WorldState World, WorldClock Clock, VisualScope Scope) Running(ulong seed, int population, int days)
    {
        var (world, clock) = ScenarioRunner.Create(seed, initialPopulation: population);
        clock.Run(world, days * 24L);
        var scope = new VisualScope(VisualScopeKind.City, world.ActiveCities().First().Id.ToString());
        return (world, clock, scope);
    }

    [Fact]
    public void Scope_weather_carries_the_wind_direction_and_the_foliage_of_the_engine()
    {
        var (world, _, scope) = Running(5, 10, 140);
        var weather = LivingScopeProjector.Build(world, scope).Climate!.Weather!;
        var row = LivingWorld.Simulation.Climate.WeatherNow.RowAt(world, world.ActiveCities().First().Location)!;

        Assert.Equal(row.WindFromDeg, weather.WindFromDeg);
        Assert.NotNull(weather.Foliage);
        Assert.Equal(Math.Round(row.LeafPm / 1000.0, 2), weather.Foliage!.Fraction);
        Assert.Contains(weather.Foliage.Phase, new[] { "bare", "budding", "full", "falling" });
    }

    [Fact]
    public void The_climate_is_not_resent_when_only_the_hour_changed_and_the_client_reads_the_hour_from_the_curve()
    {
        var (world, clock, scope) = Running(6, 10, 30);
        clock.Run(world, 3); // longe da virada do dia
        var before = LivingScopeProjector.Build(world, scope);
        clock.Run(world, 1);
        var after = LivingScopeProjector.Build(world, scope);
        var delta = ScopeDeltaBuilder.Diff(world.CurrentDate.TotalHours, before, after);
        var weather = after.Climate!.Weather!;
        int hour = (int)(world.CurrentDate.TotalHours % world.Calendar.HoursPerDay);

        Assert.Null(delta.Climate);
        Assert.Equal(world.Calendar.HoursPerDay, weather.HourlyC!.Count);
        Assert.Equal(weather.TemperatureNowC, weather.HourlyC[hour]);
        Assert.Equal(weather.FeelsLikeC, weather.HourlyFeelsLikeC![hour]);
        Assert.Equal(after.Climate, LivingDeltaReducer.Apply(before, delta).Climate);
    }

    [Fact]
    public void The_climate_comes_back_when_the_weather_day_turns()
    {
        var (world, clock, scope) = Running(6, 10, 30);
        clock.Run(world, world.Calendar.HoursPerDay - 1 - world.CurrentDate.TotalHours % world.Calendar.HoursPerDay); // última hora
        var before = LivingScopeProjector.Build(world, scope);
        clock.Run(world, 1);

        Assert.NotNull(ScopeDeltaBuilder.Diff(1, before, LivingScopeProjector.Build(world, scope)).Climate);
    }

    [Fact]
    public void A_house_is_lit_after_sunset_only_while_someone_inside_is_awake()
    {
        var (world, clock, scope) = Running(7, 20, 20);
        // vai até as 22 h: noite em qualquer estação de Oxford
        clock.Run(world, (22 - world.CurrentDate.TotalHours % 24 + 24) % 24);
        var household = world.Households.Where(h => h.Dwelling is not null && h.Members.Count > 0).OrderBy(h => h.Id.Value).First();
        var members = household.Members.Select(m => world.FindNpc(m)!).Where(n => n.IsAlive).ToList();
        long tick = world.CurrentDate.TotalHours;
        foreach (var npc in members) { npc.MoveTo(household.Location, tick); npc.SetCurrentAction(ActionType.Sleep, tick); }
        bool Lit() => LivingScopeProjector.Build(world, scope).BuildingStates.Single(b => b.BuildingId == household.Dwelling!.Value.Value).Lit;

        Assert.True(ObservableProjector.IsNight(world));
        Assert.False(Lit());
        members[0].SetCurrentAction(ActionType.Idle, tick);
        Assert.True(Lit());
        members[0].MoveTo(new CellCoord(household.Location.X + 3, household.Location.Y), tick);
        Assert.False(Lit());
    }

    [Fact]
    public void No_building_is_lit_at_noon()
    {
        var (world, clock, scope) = Running(7, 20, 20);
        clock.Run(world, (12 - world.CurrentDate.TotalHours % 24 + 24) % 24);

        Assert.False(ObservableProjector.IsNight(world));
        Assert.DoesNotContain(LivingScopeProjector.Build(world, scope).BuildingStates, b => b.Lit);
    }

    [Fact]
    public void Every_traveller_carries_an_intent_target_that_is_where_its_route_ends()
    {
        var (world, clock, scope) = Running(8, 40, 3);
        int checkedTravellers = 0;
        var kinds = new HashSet<string>();
        for (int hour = 0; hour < 72; hour++)
        {
            clock.Run(world, 1);
            var state = LivingScopeProjector.Build(world, scope);
            var markets = MarketIndex.BuildForTick(world);
            foreach (var visual in state.Npcs.Where(n => n.Route is { Count: > 0 }))
            {
                var npc = world.FindNpc(visual.Id)!;
                Assert.NotNull(visual.IntentTarget);
                Assert.Matches("^(workplace|household|building|npc|city|water|site|leaving):.+$", visual.IntentTarget!);
                Assert.Equal(BehaviorDecisionSystem.TravelIntentTargetFor(world, npc, markets), visual.IntentTarget);
                Assert.Contains(BehaviorDecisionSystem.TravelDestinationFor(world, npc, markets), CellsOf(world, visual.IntentTarget!, npc));
                kinds.Add(visual.IntentTarget!.Split(':')[0]);
                checkedTravellers++;
            }
        }

        Assert.True(checkedTravellers > 20, $"viajantes conferidos: {checkedTravellers}");
        Assert.True(kinds.Count >= 2, "tipos de alvo vistos: " + string.Join(",", kinds));
    }

    /// <summary>As células do alvo "tipo:id", lidas do mundo (a casa vale na porta e na cama dela), para conferir contra o destino.</summary>
    private static List<CellCoord?> CellsOf(WorldState world, string target, LivingWorld.Domain.Population.Npc npc)
    {
        var (kind, id) = (target.Split(':')[0], target[(target.IndexOf(':') + 1)..]);
        var rest = LivingWorld.Simulation.Behavior.Resolvers.RestPlaceResolver.Resolve(world, npc).Location;
        switch (kind)
        {
            case "workplace": return [world.FindWorkplace(new LivingWorld.Domain.Shared.WorkplaceId(long.Parse(id)))?.Location];
            case "household":
                var h = world.FindHousehold(new LivingWorld.Domain.Shared.HouseholdId(long.Parse(id)));
                return [h?.Location, rest];
            case "building": return [rest];
            case "city": return [world.Cities.FirstOrDefault(c => c.Id.Value.ToString() == id)?.Location];
            case "leaving":
                return [world.Invention?.Leavings?.FirstOrDefault(l => l.Id == long.Parse(id)) is { } l ? new CellCoord(l.X, l.Y) : null];
            default:
                var xy = id.Split(',');
                return [new CellCoord(int.Parse(xy[0]), int.Parse(xy[1]))];
        }
    }
}
