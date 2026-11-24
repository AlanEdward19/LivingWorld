using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Cities.Buildings;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Population;
using LivingWorld.Simulation.Cities;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Players;
using static LivingWorld.Tests.Unit.Players.PlayerControlTests;

namespace LivingWorld.Tests.Unit.Players;

/// <summary>AD-197j: o passo no tempo do corpo (cadência, carga, ferimento, cansaço, energia), a escada e os andares de cima, e o que
/// mais o corpo alcança (fonte, lavoura, espólio, furto) pelos sistemas de qualquer NPC.</summary>
public class PlayerBodyTests
{
    private static readonly CellCoord[] Around = [new(1, 0), new(-1, 0), new(0, 1), new(0, -1), new(1, 1), new(-1, -1), new(1, -1), new(-1, 1)];

    /// <summary>Uma célula aberta (fora de prédio, sem ninguém) com uma vizinha aberta.</summary>
    private static (CellCoord From, CellCoord To) OpenPair(WorldState w, Npc npc)
    {
        npc.ExitBuilding(); // o par é do térreo
        var homes = w.Households.Select(h => h.Location).ToHashSet(); // não a célula de uma casa (o espólio em casa não fica no chão)
        foreach (var cell in w.Map.Cells.Select(c => c.Coord).OrderBy(c => Math.Abs(c.X - npc.CurrentLocation.X) + Math.Abs(c.Y - npc.CurrentLocation.Y)))
        {
            if (homes.Contains(cell) || PlayerGround.At(w, cell) is not null || PlayerGround.PersonAt(w, npc, cell) is not null
                || w.Map.TryGetCell(cell, out var c0) && c0.HasWater)
                continue;
            foreach (var d in Around)
            {
                var to = new CellCoord(cell.X + d.X, cell.Y + d.Y);
                if (w.Map.TryGetCell(to, out var c1) && !c1.HasWater && PlayerGround.Step(w, npc, cell, to).IsSuccess) return (cell, to);
            }
        }
        throw new InvalidOperationException("sem chão aberto");
    }

    [Fact]
    public void A_step_before_the_body_finishes_the_last_one_is_refused_and_walking_spends_energy()
    {
        var (arm, npc, service) = Body(1, rules: PlayerRules.Default with { StepSleepPerHour = 600 }); // energia visível no inteiro
        var w = arm.World;
        var (from, to) = OpenPair(w, npc);
        Place(w, npc, from);
        npc.SetSleep(80, w.CurrentDate.TotalHours);
        long now = w.CurrentDate.TotalMilliseconds;
        double sleepBefore = npc.SleepAt(w.CurrentDate.TotalHours);

        Assert.True(service.Move(w, "alan", to.X - from.X, to.Y - from.Y).IsSuccess);
        Assert.True(npc.StepReadyAtMs > now, "o passo leva tempo");
        string hash = WorldSnapshot.CanonicalHash(w);
        var early = service.Move(w, "alan", from.X - to.X, from.Y - to.Y);
        Assert.False(early.IsSuccess);
        Assert.Contains("passo anterior", early.Error, StringComparison.Ordinal);
        Assert.Equal(hash, WorldSnapshot.CanonicalHash(w)); // a recusa não muda o mundo
        Assert.True(npc.SleepAt(w.CurrentDate.TotalHours) < sleepBefore, "andar gasta energia");
    }

    [Fact]
    public void The_pace_slows_with_load_injury_and_fatigue_in_at_least_18_of_20_seeds()
    {
        int load = 0, injury = 0, fatigue = 0;
        for (ulong seed = 1; seed <= 20; seed++)
        {
            var (arm, npc, _) = Body(seed, rules: PlayerRules.Default);
            var w = arm.World;
            var (from, to) = OpenPair(w, npc);
            npc.SetHealth(100);
            npc.SetSleep(90, w.CurrentDate.TotalHours);
            long free = PlayerCommands.PaceMs(w, npc, from, to);
            Assert.True(npc.PickUp(new ResourceType(w.EconomyRules.FoodResourceId), Npc.DefaultCarryCapacity).IsSuccess);
            if (PlayerCommands.PaceMs(w, npc, from, to) > free) load++;
            npc.TakeCarried();
            npc.SetHealth(25);
            if (PlayerCommands.PaceMs(w, npc, from, to) > free) injury++;
            npc.SetHealth(100);
            npc.SetSleep(5, w.CurrentDate.TotalHours);
            if (PlayerCommands.PaceMs(w, npc, from, to) > free) fatigue++;
        }
        Assert.True(load >= 18 && injury >= 18 && fatigue >= 18, $"carga {load}, ferimento {injury}, cansaço {fatigue}");
    }

    [Fact]
    public void The_stairs_lead_to_the_floor_above_where_one_walks_only_inside_and_comes_back_down()
    {
        var (arm, npc, service) = Body(2);
        var w = arm.World;
        var (house, stair) = w.Buildings.Where(b => b.Position is not null && b.CompletedAtTick <= w.CurrentDate.TotalHours
                && BuildingLayoutCache.Of(b).Layout.Floors > 1).OrderBy(b => b.Id.Value)
            .Select(b => (b, BuildingLayoutCache.Of(b).Layout.FloorLayouts[0].Cells.First(c => c.Material == BuildingMaterial.Stair).Cell))
            .Select(x => (x.b, new CellCoord(x.b.Position!.Value.X + x.Item2.X, x.b.Position!.Value.Y + x.Item2.Y)))
            .First(x => PlayerGround.PersonAt(w, npc, x.Item2) is null && PlayerGround.PersonAt(w, npc, x.Item2, 1, x.b.Id) is null);
        Place(w, npc, stair);

        Assert.Contains(PlayerService.Reach(w, "alan")!, o => o.Kind == "stairs" && o.Verbs.Contains("up"));
        Assert.True(service.Interact(w, "alan", "stairs", house.Id.Value, "up").IsSuccess);
        Assert.Equal((1, house.Id), (PlayerGround.FloorOf(npc), npc.Interior!.Building));
        var origin = house.Position!.Value;
        var upper = BuildingLayoutCache.Of(house).Layout.FloorLayouts[1].Cells;
        var inside = Around.Select(d => new CellCoord(stair.X + d.X, stair.Y + d.Y)).FirstOrDefault(c =>
            upper.Any(u => u.Cell == new CellCoord(c.X - origin.X, c.Y - origin.Y) && InteriorWalkability.IsWalkable(u.Material))
            && PlayerGround.Step(w, npc, stair, c).IsSuccess);
        if (inside != default)
        {
            Assert.True(service.Move(w, "alan", inside.X - stair.X, inside.Y - stair.Y).IsSuccess);
            Assert.Equal(new CellCoord(inside.X - origin.X, inside.Y - origin.Y), npc.Interior!.LocalCell);
            Assert.True(service.Move(w, "alan", stair.X - inside.X, stair.Y - inside.Y).IsSuccess);
        }
        var outside = Around.Select(d => new CellCoord(stair.X + d.X, stair.Y + d.Y)).Where(c => PlayerGround.At(w, c) is null).ToList();
        foreach (var c in outside) Assert.False(PlayerGround.Step(w, npc, stair, c).IsSuccess); // no andar de cima não se sai do prédio
        Assert.True(service.Interact(w, "alan", "stairs", house.Id.Value, "down").IsSuccess);
        Assert.Equal(0, PlayerGround.FloorOf(npc));
        Assert.Null(npc.Interior);
    }

    [Fact]
    public void At_a_water_source_one_drinks_and_fills_and_the_water_reaches_the_pantry_at_home()
    {
        var (arm, npc, service) = Body(3);
        var w = arm.World;
        var home = w.FindHousehold(npc.Household!.Value)!;
        var water = new ResourceType(w.EconomyRules.WaterResourceId);
        var shore = w.Map.Cells.Where(c => c.HasWater).Select(c => c.Coord)
            .OrderBy(c => Math.Abs(c.X - home.Location.X) + Math.Abs(c.Y - home.Location.Y))
            .SelectMany(c => Around.Select(d => new CellCoord(c.X + d.X, c.Y + d.Y)))
            .First(c => w.Map.TryGetCell(c, out var cell) && !cell.HasWater && PlayerGround.At(w, c) is null);
        Place(w, npc, shore);
        home.Withdraw(water, home.Stock.GetValueOrDefault(water));
        npc.SetThirst(10, w.CurrentDate.TotalHours);
        var offer = PlayerService.Reach(w, "alan")!.Single(o => o.Kind == "water");

        Assert.True(service.Interact(w, "alan", "water", offer.Id, "drink").IsSuccess);
        Assert.True(npc.ThirstAt(w.CurrentDate.TotalHours) > 10);
        Assert.True(service.Interact(w, "alan", "water", offer.Id, "fill").IsSuccess);
        Assert.True(w.Invention!.CarriedQuantity(npc.Id.Value, water.Id) > 0);
        Place(w, npc, home.Location);
        Assert.True(service.Interact(w, "alan", "stock", home.Id.Value, "deposit").IsSuccess);
        Assert.True(home.Stock.GetValueOrDefault(water) > 0);
    }

    [Fact]
    public void Stealing_from_another_house_or_a_field_goes_through_the_theft_of_any_npc()
    {
        int moved = 0, logged = 0;
        for (ulong seed = 1; seed <= 6; seed++)
        {
            var (arm, npc, service) = Body(seed);
            var w = arm.World;
            var food = new ResourceType(w.EconomyRules.FoodResourceId);
            var other = w.Households.Where(h => h.Id != npc.Household && h.City == npc.City && !h.IsEmpty).OrderBy(h => h.Id.Value).First();
            other.Deposit(food, 20);
            Place(w, npc, other.Location);
            var offer = PlayerService.Reach(w, "alan")!.Single(o => o.Kind == "stock" && o.Id == other.Id.Value);
            Assert.Equal(["steal"], offer.Verbs);
            long before = other.Stock.GetValueOrDefault(food);
            Assert.True(service.Interact(w, "alan", "stock", other.Id.Value, "steal", food.Id).IsSuccess);
            if (arm.Sink.Events.Any(e => e.Kind == WorldEventKind.TheftCommitted)) logged++;
            if (other.Stock.GetValueOrDefault(food) < before) moved++;
        }
        Assert.Equal(6, logged);
        Assert.True(moved >= 1, "algum furto levou de fato (o sucesso é o da guarda)");

        var (farmArm, worker, farmService) = Body(4, w => w.Npcs.First(n => n.IsAlive && n.Employer is { } e
            && w.FindWorkplace(e) is { } p && p.LocationType.Id == LivingWorld.Simulation.Economy.Production.CropSystem.DefaultFarmLocationTypeId));
        var farm = farmArm.World.FindWorkplace(worker.Employer!.Value)!;
        Place(farmArm.World, worker, farm.Location);
        Assert.Contains(PlayerService.Reach(farmArm.World, "alan")!, o => o.Kind == "field" && o.Verbs.Contains("work"));
        Assert.True(farmService.Interact(farmArm.World, "alan", "field", farm.Id.Value, "work").IsSuccess);
        Assert.Equal(ActionType.Work, worker.ControlledAction);
    }

    [Fact]
    public void The_leavings_of_someone_of_the_house_are_collected_and_carried_home()
    {
        var (arm, npc, service) = Body(5);
        var w = arm.World;
        var kin = w.FindHousehold(npc.Household!.Value)!.Members.Where(m => m != npc.Id).Select(m => w.FindNpc(m)!).First(m => m.IsAlive);
        var (spot, near) = OpenPair(w, npc);
        Place(w, kin, spot);
        w.Invention!.Carry(kin.Id.Value, w.EconomyRules.FoodResourceId, 3);
        var ctx = new LivingWorld.Simulation.Scheduling.TickContext(w, w.Rng, w.Scheduler, arm.Sink);
        Assert.True(LivingWorld.Simulation.Invention.Leavings.Drop(w, ctx, kin.Id.Value, null));
        Place(w, npc, near);
        var offer = PlayerService.Reach(w, "alan")!.Single(o => o.Kind == "leaving");
        Assert.Equal(["collect"], offer.Verbs);

        Assert.True(service.Interact(w, "alan", "leaving", offer.Id, "collect").IsSuccess);
        Assert.True(w.Invention.CarriedQuantity(npc.Id.Value, w.EconomyRules.FoodResourceId) > 0); // o que coube na carga
        Assert.Contains(arm.Sink.Events, e => e.Kind == WorldEventKind.LeavingsCollected);
    }
}
