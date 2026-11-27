using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Cities.Works;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Players;
using static LivingWorld.Tests.Unit.Players.PlayerAbsenceTests;
using static LivingWorld.Tests.Unit.Players.PlayerControlTests;

namespace LivingWorld.Tests.Unit.Players;

/// <summary>#237: a água só se oferece e se aceita com o corpo ao alcance dela (uma célula, da posição fina à borda). #239: a mata ao
/// alcance oferece buscar material pela conta dos NPCs da obra (horas de cortar, carga), com o corpo ocupado esse tempo.</summary>
public class PlayerNatureTests
{
    private static bool Open(WorldState w, CellCoord c) =>
        w.Map.TryGetCell(c, out var cell) && !cell.HasWater && PlayerGround.At(w, c) is null;

    private static CellCoord NearWater(WorldState w, Npc npc, int away)
    {
        var home = w.FindHousehold(npc.Household!.Value)!.Location;
        foreach (var water in w.Map.Cells.Where(c => c.HasWater).Select(c => c.Coord)
                     .OrderBy(c => Math.Abs(c.X - home.X) + Math.Abs(c.Y - home.Y)))
            for (int dy = -away; dy <= away; dy++)
                for (int dx = -away; dx <= away; dx++)
                {
                    var cell = new CellCoord(water.X + dx, water.Y + dy);
                    if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != away || !Open(w, cell)) continue;
                    bool clear = true; // nenhuma outra água a uma célula do corpo
                    for (int y = -1; y <= 1; y++)
                        for (int x = -1; x <= 1; x++)
                            if (w.Map.TryGetCell(new CellCoord(cell.X + x, cell.Y + y), out var near) && near.HasWater && away > 1) clear = false;
                    if (clear) return cell;
                }
        throw new InvalidOperationException("sem água");
    }

    [Fact]
    public void Water_is_offered_only_beside_it_and_the_command_refuses_from_afar()
    {
        var (arm, npc, service) = Body(3);
        var w = arm.World;
        var far = NearWater(w, npc, 2);
        Place(w, npc, far);
        Assert.DoesNotContain(PlayerService.Reach(w, "alan")!, o => o.Kind == "water");
        var someWater = w.Map.Cells.First(c => c.HasWater).Coord;
        string hash = WorldSnapshot.CanonicalHash(w);
        var refused = service.Interact(w, "alan", "water", PlayerInteractions.WaterId(someWater), "drink");
        Assert.False(refused.IsSuccess);
        Assert.Equal(hash, WorldSnapshot.CanonicalHash(w));

        var beside = NearWater(w, npc, 1);
        Place(w, npc, beside);
        var offer = Assert.Single(PlayerService.Reach(w, "alan")!, o => o.Kind == "water");
        Assert.True(service.Interact(w, "alan", "water", offer.Id, "drink").IsSuccess);
    }

    private static (Arm Arm, Npc Npc, PlayerService Service, CellCoord Wood) InTheWoods(ulong seed)
    {
        var (arm, npc, service) = Body(seed);
        var w = arm.World;
        var wood = new ResourceType(w.StreetRules.HouseMaterialResourceId);
        var cell = w.Map.Cells.Where(c => Open(w, c.Coord) && w.ActiveCities().All(city => !LivingWorld.Simulation.Cities.CityOccupancy
                .ResolveGrownBounds(w, city, LivingWorld.Simulation.Cities.Queries.CityPopulationQuery.Population(w, city.Id)).Bounds.Contains(c.Coord)))
            .OrderBy(c => c.Coord.Y).ThenBy(c => c.Coord.X).First().Coord;
        Place(w, npc, cell);
        return (arm, npc, service, cell);
    }

    [Fact]
    public void In_the_woods_gathering_costs_the_npc_cut_hours_and_loads_the_hands_then_the_pantry()
    {
        var (arm, npc, service, at) = InTheWoods(3);
        var w = arm.World;
        var wood = new ResourceType(w.StreetRules.HouseMaterialResourceId);
        var offer = Assert.Single(PlayerService.Reach(w, "alan")!, o => o.Kind == "nature");
        Assert.Equal(["gather"], offer.Verbs);
        long units = WorkHaul.CapacityUnits(w, npc, wood);
        Assert.Equal(units, offer.Resources.Single().Quantity);

        long now = w.CurrentDate.TotalMilliseconds;
        var home = w.FindHousehold(npc.Household!.Value)!;
        long hours = WorkHaul.CutHours(w, home.Location, new CellCoord(offer.X, offer.Y), units);
        var done = service.Interact(w, "alan", "nature", offer.Id, "gather");
        Assert.True(done.IsSuccess, done.Error);
        Assert.Equal(units, done.Value!.Quantity);
        Assert.Equal(wood.Id, npc.CarriedResourceId);
        Assert.Equal(units, npc.CarriedQuantity);
        Assert.Equal(now + hours * WorldMillis.PerHour, npc.StepReadyAtMs);

        // ocupado: nem anda nem busca de novo
        Assert.DoesNotContain(PlayerService.Reach(w, "alan")!, o => o.Kind == "nature");
        var step = service.Walk(w, "alan", 1000, 0);
        Assert.False(step.IsSuccess);

        Place(w, npc, home.Location);
        long before = home.Stock.GetValueOrDefault(wood);
        Assert.True(service.Interact(w, "alan", "stock", home.Id.Value, "deposit").IsSuccess);
        Assert.Equal(before + units, home.Stock.GetValueOrDefault(wood));
    }

    [Fact]
    public void Inside_the_town_there_is_no_woods_and_the_command_is_refused()
    {
        var (arm, npc, service) = Body(3);
        var w = arm.World;
        var city = w.ActiveCities().First();
        var bounds = LivingWorld.Simulation.Cities.CityOccupancy
            .ResolveGrownBounds(w, city, LivingWorld.Simulation.Cities.Queries.CityPopulationQuery.Population(w, city.Id)).Bounds;
        var inner = w.Map.Cells.Select(c => c.Coord).Where(c => Open(w, c)
            && Enumerable.Range(-1, 3).All(y => Enumerable.Range(-1, 3).All(x => bounds.Contains(new CellCoord(c.X + x, c.Y + y))))).First();
        Place(w, npc, inner);
        Assert.DoesNotContain(PlayerService.Reach(w, "alan")!, o => o.Kind == "nature");
        string hash = WorldSnapshot.CanonicalHash(w);
        Assert.False(service.Interact(w, "alan", "nature", PlayerInteractions.WaterId(inner), "gather").IsSuccess);
        Assert.Equal(hash, WorldSnapshot.CanonicalHash(w));
    }

    [Fact]
    public void Gathering_is_deterministic()
    {
        string Run()
        {
            var (arm, _, service, _) = InTheWoods(5);
            var w = arm.World;
            var offer = Assert.Single(PlayerService.Reach(w, "alan")!, o => o.Kind == "nature");
            Assert.True(service.Interact(w, "alan", "nature", offer.Id, "gather").IsSuccess);
            return WorldSnapshot.CanonicalHash(w);
        }
        Assert.Equal(Run(), Run());
    }
}
