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

    private static (Arm Arm, Npc Npc, PlayerService Service, CellCoord Tree) InTheWoods(ulong seed)
    {
        var (arm, npc, service) = Body(seed);
        var w = arm.World;
        var cell = w.Map.Cells.Select(c => c.Coord).Where(c => PlayerNature.TreeAt(w, c) is not null)
            .OrderBy(c => c.Y).ThenBy(c => c.X).First();
        Place(w, npc, cell);
        return (arm, npc, service, cell);
    }

    [Fact]
    public void In_the_woods_gathering_takes_one_work_shift_not_days_and_loads_the_hands_then_the_pantry()
    {
        var (arm, npc, service, at) = InTheWoods(3);
        var w = arm.World;
        var wood = new ResourceType(w.StreetRules.HouseMaterialResourceId);
        var offer = Assert.Single(PlayerService.Reach(w, "alan")!, o => o.Kind == "nature");
        Assert.Equal(["gather"], offer.Verbs);
        Assert.Equal("Árvore", offer.Label);
        Assert.NotNull(offer.Fx);

        long now = w.CurrentDate.TotalMilliseconds;
        var home = w.FindHousehold(npc.Household!.Value)!;
        long cap = WorkHaul.CapacityUnits(w, npc, wood);
        double perUnitMs = (double)WorkHaul.CutHours(w, home.Location, new CellCoord(offer.X, offer.Y), cap) * WorldMillis.PerHour / cap;
        var done = service.Interact(w, "alan", "nature", offer.Id, "gather");
        Assert.True(done.IsSuccess, done.Error);
        long units = done.Value!.Quantity;
        Assert.Equal(offer.Resources.Single().Quantity, units);
        Assert.InRange(units, 1, cap);
        Assert.Equal(wood.Id, npc.CarriedResourceId);
        Assert.Equal(units, npc.CarriedQuantity);

        // #241: só o corte da carga, na taxa dos NPCs, e no máximo uma jornada (não os ~67 h do lote inteiro da obra)
        long duration = npc.StepReadyAtMs - now;
        Assert.Equal((long)Math.Ceiling(perUnitMs * units), duration);
        Assert.True(duration <= w.StreetRules.CrewHoursPerDay * (long)WorldMillis.PerHour + 1);
        var busy = PlayerTasks.Of(w, npc)!;
        Assert.Equal(("gather", now + duration, duration, duration), (busy.Action, busy.UntilMs, busy.TotalMs, busy.RemainingMs));

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
    public void The_offer_exists_only_within_one_and_a_half_meters_of_a_tree_that_the_client_draws()
    {
        var (arm, npc, service, at) = InTheWoods(3);
        var w = arm.World;
        // a árvore do cliente: a mesma célula e o mesmo tronco (paridade com scenery.gd, também travada no teste Godot)
        Assert.Equal(0.9912, PlayerNature.CellHash(new CellCoord(3, 5)), 4);
        Assert.Equal(0.8887, PlayerNature.CellHash(new CellCoord(12, 40), 1), 4);
        Assert.Equal(0.3875, PlayerNature.CellHash(new CellCoord(-4, 7), 2), 4);

        // longe de qualquer árvore (terra seca fora da cidade, sem tronco a 1,5 m): nada para cortar, comando recusado sem mudar o mundo
        var bare = w.Map.Cells.Select(c => c.Coord).Where(c => !w.Map.CellAt(c).HasWater && PlayerGround.At(w, c) is null
            && PlayerNature.NearestWithinReach(w, c, null) is null
            && w.ActiveCities().All(city => !LivingWorld.Simulation.Cities.CityOccupancy
                .ResolveGrownBounds(w, city, LivingWorld.Simulation.Cities.Queries.CityPopulationQuery.Population(w, city.Id)).Bounds.Contains(c)))
            .OrderBy(c => c.Y).ThenBy(c => c.X).First();
        Place(w, npc, bare);
        Assert.DoesNotContain(PlayerService.Reach(w, "alan")!, o => o.Kind == "nature");
        string hash = WorldSnapshot.CanonicalHash(w);
        Assert.False(service.Interact(w, "alan", "nature", PlayerInteractions.WaterId(bare), "gather").IsSuccess);
        Assert.Equal(hash, WorldSnapshot.CanonicalHash(w));

        // junto da árvore: oferta; um passo além do alcance: some (a distância é ao tronco, em metros)
        Place(w, npc, at);
        var tree = PlayerNature.TreeAt(w, at)!.Value;
        Assert.Contains(PlayerService.Reach(w, "alan")!, o => o.Kind == "nature" && o.X == at.X && o.Y == at.Y);
        double reach = PlayerNature.ReachMeters / w.Players.CellM * FinePose.PerCell;
        var near = new FinePose(tree.FineX + (long)reach - 5, tree.FineY, 0, 0, 0);
        Assert.Equal(at, PlayerNature.NearestWithinReach(w, new CellCoord(near.CellX, near.CellY), near)?.Cell);
        var away = new FinePose(tree.FineX + (long)reach + 5, tree.FineY, 0, 0, 0);
        Assert.NotEqual(at, PlayerNature.NearestWithinReach(w, new CellCoord(away.CellX, away.CellY), away)?.Cell); // outra árvore pode estar perto; esta não
    }

    [Fact]
    public void Inside_the_town_there_is_no_tree_and_the_command_is_refused()
    {
        var (arm, npc, service) = Body(3);
        var w = arm.World;
        var city = w.ActiveCities().First();
        var bounds = LivingWorld.Simulation.Cities.CityOccupancy
            .ResolveGrownBounds(w, city, LivingWorld.Simulation.Cities.Queries.CityPopulationQuery.Population(w, city.Id)).Bounds;
        var inner = w.Map.Cells.Select(c => c.Coord).Where(c => Open(w, c)
            && Enumerable.Range(-2, 5).All(y => Enumerable.Range(-2, 5).All(x => bounds.Contains(new CellCoord(c.X + x, c.Y + y))))).First();
        Place(w, npc, inner);
        Assert.DoesNotContain(PlayerService.Reach(w, "alan")!, o => o.Kind == "nature");
        string hash = WorldSnapshot.CanonicalHash(w);
        Assert.False(service.Interact(w, "alan", "nature", PlayerInteractions.WaterId(inner), "gather").IsSuccess);
        Assert.Equal(hash, WorldSnapshot.CanonicalHash(w));
    }

    [Fact]
    public void Interrupting_leaves_the_proportional_load_and_frees_the_body()
    {
        var (arm, npc, service, _) = InTheWoods(3);
        var w = arm.World;
        var wood = new ResourceType(w.StreetRules.HouseMaterialResourceId);
        Assert.False(service.Interrupt(w, "alan").IsSuccess); // nada em andamento: recusa, mundo igual
        var offer = Assert.Single(PlayerService.Reach(w, "alan")!, o => o.Kind == "nature");
        Assert.True(service.Interact(w, "alan", "nature", offer.Id, "gather").IsSuccess);
        var task = npc.BodyTask!;
        long produced = w.ResourceProduced.GetValueOrDefault(wood);
        arm.Clock.Run(w, 1); // uma hora de mundo de corte
        long elapsed = w.CurrentDate.TotalMilliseconds - task.StartedAtMs;
        long total = task.UntilMs - task.StartedAtMs;
        long expected = task.Units * Math.Clamp(elapsed, 0, total) / total;
        Assert.True(service.Interrupt(w, "alan").IsSuccess);
        Assert.Null(PlayerTasks.Of(w, npc));
        Assert.Equal(expected, npc.CarriedQuantity);
        Assert.Equal(produced - (task.Units - expected), w.ResourceProduced.GetValueOrDefault(wood));
        Assert.True(service.Walk(w, "alan", 1000, 0).IsSuccess); // o corpo volta a andar
        Assert.False(service.Interrupt(w, "alan").IsSuccess);
    }

    [Fact]
    public void Gathering_and_interrupting_are_deterministic()
    {
        string Run()
        {
            var (arm, _, service, _) = InTheWoods(5);
            var w = arm.World;
            var offer = Assert.Single(PlayerService.Reach(w, "alan")!, o => o.Kind == "nature");
            Assert.True(service.Interact(w, "alan", "nature", offer.Id, "gather").IsSuccess);
            arm.Clock.Run(w, 2);
            Assert.True(service.Interrupt(w, "alan").IsSuccess);
            return WorldSnapshot.CanonicalHash(w);
        }
        Assert.Equal(Run(), Run());
    }
}
