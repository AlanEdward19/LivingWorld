using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Objects;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Emergence;
using LivingWorld.Simulation.Objects;

namespace LivingWorld.Simulation.Invention;

/// <summary>O que a pessoa leva consigo (AD-147). A despensa fica em casa: quem está a mais de uma hora de caminho só come e bebe
/// do que leva (ou volta). De mãos nuas cabe pouco (<see cref="Domain.Invention.InventionRules.HandsVolumeMl"/>) e nada que escorra;
/// o recipiente da casa (um <see cref="Domain.Invention.Design"/> que cumpre a sonda de conter) dá volume, parede estanque e, se se
/// prende ao corpo, deixa as mãos livres. O peso é sempre o teto da carga a pé (<c>FootCarryKg</c>). Ao sair de casa a pessoa leva o
/// que a sede e a fome vão pedir até voltar, sem deixar a casa sem; com sede ou fome crítica longe de casa, bebe e come do que leva
/// antes de tudo; em casa, o que sobrou volta para a despensa. Tudo por evento (saída, chegada, decisão): nenhuma varredura.</summary>
public static class PersonalCarry
{
    /// <summary>Espaço para levar: peso de conteúdo (g), volume seco (ml) e a parte estanque dele (ml).</summary>
    internal readonly record struct Room(long WeightG, long DryMl, long WetMl);

    internal static Room RoomOf(WorldState world, Npc npc)
    {
        var rules = world.InventionRules!;
        long body = rules.FootCarryKg * 1000L;
        var carrier = npc.Household is { } h ? world.Invention!.BestCarrier(h.Value, rules) : default;
        if (!carrier.Exists) return new Room(body, rules.HandsVolumeMl, 0);
        long hands = carrier.Worn ? rules.HandsVolumeMl : 0; // segurar o recipiente ocupa as mãos
        long weight = Math.Min(body - carrier.MassG, carrier.CapacityG + hands * rules.WaterDensityKgM3 / 1000);
        return new Room(Math.Max(0, weight), hands + carrier.InnerMl, carrier.WatertightMl);
    }

    /// <summary>A despensa não alcança: a casa (não sem-teto) fica a mais de uma hora de caminho de onde a pessoa está.</summary>
    public static bool PantryOutOfReach(WorldState world, Npc npc, Household home)
    {
        if (!world.InventionOn || !world.InventionRules!.PantryNeedsPresence || home.IsHomeless || npc.CurrentLocation == home.Location)
            return false;
        return Mobility.TravelTicks(world, npc, npc.CurrentLocation, home.Location, 1.0) > 1;
    }

    /// <summary>Unidades para manter a necessidade acima da linha de urgência por <paramref name="hours"/>: cada unidade a leva ao
    /// nível que ela dá e dura até cair de novo à linha.</summary>
    internal static int UnitsFor(int value, double decayPerHour, long hours, int line, int level)
    {
        if (decayPerHour <= 0 || value - decayPerHour * hours >= line) return 0;
        double untilLine = Math.Max(0, (value - line) / decayPerHour);
        double perUnit = Math.Max(1, (level - line) / decayPerHour);
        return (int)Math.Ceiling((hours - untilLine) / perUnit);
    }

    /// <summary>Ao sair de casa: leva a água e a comida que a sede e a fome vão pedir até voltar (<paramref name="hoursAway"/>), no
    /// que couber (água só no volume estanque, primeiro, porque mata antes) e sem tirar da casa uma unidade por dia fora de cada
    /// outro morador.</summary>
    public static void PackForTrip(WorldState world, Npc npc, Household home, long hoursAway, long now)
    {
        if (hoursAway <= 0 || home.IsHomeless) return;
        var needs = world.NeedsRules;
        var econ = world.EconomyRules;
        var rules = world.InventionRules!;
        int line = 100 - needs.UrgencyThreshold;
        var water = new ResourceType(econ.WaterResourceId);
        var food = new ResourceType(econ.FoodResourceId);
        int wantWater = UnitsFor(npc.ThirstAt(now), needs.ThirstDecayPerHour, hoursAway, line,
            ObjectUse.ConsumeLevel(world, water, ObjectEffectTargets.Thirst));
        int wantFood = UnitsFor(npc.HungerAt(now), needs.HungerDecayPerHour, hoursAway, line,
            ObjectUse.ConsumeLevel(world, food, ObjectEffectTargets.Hunger));
        if (wantWater == 0 && wantFood == 0) return;

        var ledger = world.Invention!;
        wantWater -= ledger.CarriedQuantity(npc.Id.Value, water.Id);
        wantFood -= ledger.CarriedQuantity(npc.Id.Value, food.Id);
        int others = Math.Max(0, home.Members.Count - 1);
        long days = Math.Max(1, (hoursAway + world.Calendar.HoursPerDay - 1) / world.Calendar.HoursPerDay);
        long reserve = others * days;
        var room = RoomOf(world, npc);
        long weight = room.WeightG, dry = room.DryMl, wet = room.WetMl;

        int Take(ResourceType resource, int want, long volume)
        {
            if (want <= 0) return 0;
            long unitMl = rules.UnitMlOf(resource.Id), unitG = rules.UnitGramsOf(resource.Id);
            long spare = home.Stock.GetValueOrDefault(resource) - reserve;
            long n = Math.Min(Math.Min(want, spare), Math.Min(volume / unitMl, weight / unitG));
            if (n <= 0 || !home.Withdraw(resource, n).IsSuccess) return 0;
            ledger.Carry(npc.Id.Value, resource.Id, (int)n);
            weight -= n * unitG;
            dry -= n * unitMl;
            wet -= n * unitMl;
            return (int)n;
        }

        int took = Take(water, wantWater, rules.IsLiquid(water.Id) ? wet : dry);
        took += Take(food, wantFood, rules.IsLiquid(food.Id) ? wet : dry);
        if (took == 0) return;
        world.InventionTally.PackTrips++;
        world.InventionTally.PackUnits += took;
    }

    /// <summary>Em casa: o que sobrou do que levou volta para a despensa.</summary>
    public static void Unpack(WorldState world, Npc npc, Household home)
    {
        var ledger = world.Invention!;
        int i = ledger.CarriedIndex(npc.Id.Value, int.MinValue);
        i = i >= 0 ? i : ~i;
        while (i < ledger.Carried.Count && ledger.Carried[i].NpcId == npc.Id.Value)
        {
            var load = ledger.Carried[i];
            home.Deposit(new ResourceType(load.ResourceId), load.Quantity);
            world.InventionTally.PackReturned += load.Quantity;
            ledger.Carry(load.NpcId, load.ResourceId, -load.Quantity);
        }
    }

    /// <summary>Come e bebe do que leva: uma unidade de cada recurso cuja necessidade passou do <paramref name="deficit"/>.</summary>
    public static bool ConsumeFromPack(WorldState world, Npc npc, long tick, double deficit)
    {
        var econ = world.EconomyRules;
        var ledger = world.Invention!;
        bool any = false;
        var water = new ResourceType(econ.WaterResourceId);
        if (100 - npc.ThirstAt(tick) > deficit && ledger.CarriedQuantity(npc.Id.Value, water.Id) > 0)
        {
            ledger.Carry(npc.Id.Value, water.Id, -1);
            npc.SetThirst(Math.Max(npc.ThirstAt(tick), ObjectUse.ConsumeLevel(world, water, ObjectEffectTargets.Thirst)), tick);
            world.RecordResourceConsumed(water, 1);
            world.InventionTally.PackDrunk++;
            any = true;
        }
        var food = new ResourceType(econ.FoodResourceId);
        if (100 - npc.HungerAt(tick) > deficit && ledger.CarriedQuantity(npc.Id.Value, food.Id) > 0)
        {
            ledger.Carry(npc.Id.Value, food.Id, -1);
            npc.SetHunger(Math.Max(npc.HungerAt(tick), ObjectUse.ConsumeLevel(world, food, ObjectEffectTargets.Hunger)), tick);
            world.RecordResourceConsumed(food, 1);
            world.InventionTally.PackEaten++;
            any = true;
        }
        return any;
    }

    /// <summary>O que levar resolve a necessidade urgente de agora (sede com água no que leva, fome com comida).</summary>
    public static bool PackServes(WorldState world, Npc npc, long tick, double urgencyThreshold)
    {
        var ledger = world.Invention!;
        if (!ledger.CarriesAnything(npc.Id.Value)) return false;
        var econ = world.EconomyRules;
        return (100 - npc.ThirstAt(tick) > urgencyThreshold && ledger.CarriedQuantity(npc.Id.Value, econ.WaterResourceId) > 0)
            || (100 - npc.HungerAt(tick) > urgencyThreshold && ledger.CarriedQuantity(npc.Id.Value, econ.FoodResourceId) > 0);
    }

    /// <summary>Sede ou fome urgente longe de casa sem nada no que leva: o tempo de voltar vira frustração do problema de carregar.</summary>
    public static void FeltAway(WorldState world, Npc npc, long ticksHome)
    {
        var rules = world.InventionRules!;
        if (ticksHome <= 0 || npc.AgeYears(world.CurrentDate) < rules.MinInventorAgeYears) return;
        var econ = world.EconomyRules;
        bool thirst = npc.ThirstAt(world.CurrentDate.TotalHours) <= npc.HungerAt(world.CurrentDate.TotalHours);
        int needMl = rules.UnitMlOf(econ.WaterResourceId) + rules.UnitMlOf(econ.FoodResourceId);
        Frustrate(world, npc, (int)Math.Min(int.MaxValue / 4, ticksHome * 60), needMl, thirst && rules.IsLiquid(econ.WaterResourceId));
        world.InventionTally.AwayFrustrations++;
    }

    /// <summary>A compra cortada pelo que cabe (AD-143): o que ficou no mercado custa outra ida e volta, na proporção do cortado.</summary>
    public static void FeltCargoCut(WorldState world, Npc npc, Household home, CellCoord market, long cutG, long carriedG)
    {
        var rules = world.InventionRules!;
        if (cutG <= 0 || npc.AgeYears(world.CurrentDate) < rules.MinInventorAgeYears) return;
        long trip = 2 * Mobility.HouseholdTicks(world, home, home.Location, market) * 60;
        long minutes = trip * cutG / Math.Max(1, carriedG + cutG);
        if (minutes <= 0) return;
        Frustrate(world, npc, (int)Math.Min(int.MaxValue / 4, minutes), (int)Math.Min(int.MaxValue / 4, cutG * 1000 / rules.WaterDensityKgM3), false);
        world.InventionTally.CargoFrustrations++;
    }

    private static void Frustrate(WorldState world, Npc npc, int minutes, int needMl, bool liquid)
    {
        var ledger = world.Invention!;
        int index = ledger.CarryMindIndex(npc.Id.Value);
        var mind = index >= 0 ? ledger.CarryMinds[index] : new InventionMind(npc.Id.Value, 0, 0, 0, 0, Domain.Invention.FailureMode.None, null, 0);
        ledger.PutCarryMind(mind with
        {
            FrustrationMinutes = (int)Math.Min(int.MaxValue / 2, (long)mind.FrustrationMinutes + minutes),
            NeedMl = Math.Max(mind.NeedMl, needMl),
            NeedsLiquid = mind.NeedsLiquid || liquid,
        });
    }

    /// <summary>Quem morreu ou saiu: o que levava fica com a casa (se ainda há casa) e a mente se vai.</summary>
    internal static void Forget(WorldState world, long npcId)
    {
        var ledger = world.Invention!;
        int mind = ledger.CarryMindIndex(npcId);
        if (mind >= 0) ledger.RemoveCarryMind(mind);
        if (!ledger.CarriesAnything(npcId)) return;
        var home = world.FindNpc(new LivingWorld.Domain.Shared.NpcId(npcId)) is { Household: { } h } ? world.FindHousehold(h) : null;
        int i = ledger.CarriedIndex(npcId, int.MinValue);
        i = i >= 0 ? i : ~i;
        while (i < ledger.Carried.Count && ledger.Carried[i].NpcId == npcId)
        {
            var load = ledger.Carried[i];
            home?.Deposit(new ResourceType(load.ResourceId), load.Quantity);
            ledger.Carry(npcId, load.ResourceId, -load.Quantity);
        }
    }
}
