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
    /// <summary>Espaço para levar: peso de conteúdo (g), volume seco (ml), a parte estanque dele (ml) e o recipiente (item; 0 = mãos).</summary>
    internal readonly record struct Room(long WeightG, long DryMl, long WetMl, long ItemId = 0);

    /// <summary>AD-157b: a comida comida antes da hora (desjejum, ceia) só quando a fome chegaria a este déficit (o crítico da decisão);
    /// a água, mais barata (o poço é de todos), já na urgência.</summary>
    internal const int MealAheadDeficit = 80;

    /// <summary>AD-157b: com a despensa em casa, a refeição de quem só foi beber leva comida só a partir desta fome (déficit).</summary>
    internal const int AppetiteDeficit = 50;

    /// <summary>AD-157 (backlog 05 item 12c): o recipiente é de quem o leva. Quem está fora usa o que levou; quem está em casa, o
    /// melhor que ninguém da casa levou (vários saindo, cada um com o seu, e o último de mãos nuas).</summary>
    internal static Room RoomOf(WorldState world, Npc npc)
    {
        var rules = world.InventionRules!;
        long body = rules.FootCarryKg * 1000L;
        long itemId = 0;
        var carrier = npc.Household is { } h ? world.Invention!.CarrierFor(npc.Id.Value, h.Value, rules, out itemId) : default;
        if (!carrier.Exists) return new Room(body, rules.HandsVolumeMl, 0);
        long hands = carrier.Worn ? rules.HandsVolumeMl : 0; // segurar o recipiente ocupa as mãos
        long weight = Math.Min(body - carrier.MassG, carrier.CapacityG + hands * rules.WaterDensityKgM3 / 1000);
        return new Room(Math.Max(0, weight), hands + carrier.InnerMl, carrier.WatertightMl, itemId);
    }

    /// <summary>A despensa não alcança: a casa (não sem-teto) fica a mais de uma hora de caminho de onde a pessoa está.</summary>
    public static bool PantryOutOfReach(WorldState world, Npc npc, Household home)
    {
        if (!world.InventionOn || !world.InventionRules!.PantryNeedsPresence || home.IsHomeless || npc.CurrentLocation == home.Location)
            return false;
        return FarFromHome(world, npc, home, npc.CurrentLocation, outbound: false);
    }

    /// <summary>Mais de uma hora entre a casa e <paramref name="cell"/> (de lá para casa, ou de casa para lá se
    /// <paramref name="outbound"/>). AD-151/157: a mesma pergunta volta várias vezes (ao acordar, ao comer, ao escolher onde comer,
    /// ao sair); a resposta só muda com o lugar da casa, as peças da casa (embarcação) ou a tecnologia de caminho da cidade, e fica
    /// guardada até um deles mudar (antes, só na hora: 30% das perguntas refaziam a conta). Chave inteira (casa, célula, sentido).
    /// Só memória: não entra no snapshot nem no hash.</summary>
    private static bool FarFromHome(WorldState world, Npc npc, Household home, CellCoord cell, bool outbound)
    {
        var memo = world.Invention!.ReachMemo;
        long touches = world.Invention.Touches;
        int bonus = Mobility.BonusPct(world, npc.City);
        long id = home.Id.Value;
        bool keyed = id is >= 0 and < (1L << 29);
        long key = ((outbound ? 1L : 0L) << 61) | (id << 32) | ((long)(ushort)cell.X << 16) | (ushort)cell.Y;
        if (keyed && memo.TryGetValue(key, out var known) && known.Home == home.Location && known.City == npc.City
            && known.Touches == touches && known.Bonus == bonus)
            return known.Out;
        bool far = (outbound
            ? Mobility.TravelTicks(world, npc, home.Location, cell, 1.0)
            : Mobility.TravelTicks(world, npc, cell, home.Location, 1.0)) > 1;
        if (!keyed) return far;
        if (memo.Count >= 8192) memo.Clear();
        memo[key] = new ReachKnown(home.Location, npc.City, touches, bonus, far);
        return far;
    }

    /// <summary>AD-157b: o passo do passeio (ócio e conversa; andar no trabalho fica como está) com a despensa em casa. O sorteado vale se fica ao
    /// alcance da despensa; se sairia dele, a pessoa não dá o passo; quem já está fora volta um passo para perto de casa. Antes, o
    /// passeio ao acaso levava gente para longe sem nada (85% das voltas para comer saíam de célula sem local nenhum).</summary>
    internal static CellCoord? StrollStep(WorldState world, Npc npc, LivingWorld.Domain.Behavior.ActionType action, ReadOnlySpan<CellCoord> candidates, int index)
    {
        var picked = candidates[index];
        if (action == LivingWorld.Domain.Behavior.ActionType.Work || !world.InventionOn || !world.InventionRules!.PantryNeedsPresence || npc.Household is not { } h
            || world.FindHousehold(h) is not { IsHomeless: false } home || picked == home.Location)
            return picked;
        bool fromOutside = npc.CurrentLocation != home.Location && FarFromHome(world, npc, home, npc.CurrentLocation, outbound: false);
        if (!fromOutside)
            return FarFromHome(world, npc, home, picked, outbound: false) ? null : picked;
        long best = long.MaxValue;
        CellCoord? closer = null;
        long here = Distance2(npc.CurrentLocation, home.Location);
        foreach (var c in candidates)
        {
            long d = Distance2(c, home.Location);
            if (d < here && d < best) { best = d; closer = c; }
        }
        return closer;
    }

    private static long Distance2(CellCoord a, CellCoord b)
    {
        long dx = a.X - b.X, dy = a.Y - b.Y;
        return dx * dx + dy * dy;
    }

    /// <summary>AD-151: a saída deixa a despensa para trás quando o destino fica a mais de uma hora de casa (a medida de
    /// <see cref="PantryOutOfReach"/>). Perto, a pessoa come em casa; arrumar a carga só tiraria da despensa para devolver na volta
    /// (e custava a conta da carga em toda saída).</summary>
    public static bool LeavesPantryBehind(WorldState world, Npc npc, Household home, CellCoord destination) =>
        !home.IsHomeless && destination != home.Location && FarFromHome(world, npc, home, destination, outbound: true);

    /// <summary>Unidades para manter a necessidade acima da linha de urgência por <paramref name="hours"/>: cada unidade a leva ao
    /// nível que ela dá e dura até cair de novo à linha.</summary>
    internal static int UnitsFor(int value, double decayPerHour, long hours, int line, int level)
    {
        if (decayPerHour <= 0 || value - decayPerHour * hours >= line) return 0;
        double untilLine = Math.Max(0, (value - line) / decayPerHour);
        double perUnit = Math.Max(1, (level - line) / decayPerHour);
        return (int)Math.Ceiling((hours - untilLine) / perUnit);
    }

    /// <summary>Ao sair de casa: leva a água e a comida que a sede e a fome vão pedir até voltar (<paramref name="hoursAway"/>).</summary>
    public static void PackForTrip(WorldState world, Npc npc, Household home, long hoursAway, long now) =>
        PackForTrip(world, npc, home, hoursAway, hoursAway, now);

    /// <summary>Ao sair de casa: leva a água para <paramref name="waterHours"/> e a comida para <paramref name="foodHours"/> (AD-157: quem
    /// tem a refeição no trabalho leva só para o caminho), no que couber (água só no volume estanque, primeiro, porque mata antes).
    /// Quem fica come primeiro: a casa guarda, de cada outro morador, o que ele vai comer e beber enquanto a pessoa está fora (pelo
    /// menos uma unidade por dia). Levando algo, o recipiente sai com a pessoa e fica dela até voltar (backlog 05 item 12c).</summary>
    public static void PackForTrip(WorldState world, Npc npc, Household home, long waterHours, long foodHours, long now)
    {
        long hoursAway = Math.Max(waterHours, foodHours);
        if (hoursAway <= 0 || home.IsHomeless) return;
        var needs = world.NeedsRules;
        var econ = world.EconomyRules;
        var rules = world.InventionRules!;
        int line = 100 - needs.UrgencyThreshold;
        var water = new ResourceType(econ.WaterResourceId);
        var food = new ResourceType(econ.FoodResourceId);
        int waterLevel = ObjectUse.ConsumeLevel(world, water, ObjectEffectTargets.Thirst);
        int foodLevel = ObjectUse.ConsumeLevel(world, food, ObjectEffectTargets.Hunger);
        int wantWater = UnitsFor(npc.ThirstAt(now), needs.ThirstDecayPerHour, waterHours, line, waterLevel);
        int wantFood = UnitsFor(npc.HungerAt(now), needs.HungerDecayPerHour, foodHours, line, foodLevel);
        if (wantWater == 0 && wantFood == 0) return;

        var ledger = world.Invention!;
        wantWater -= ledger.CarriedQuantity(npc.Id.Value, water.Id);
        wantFood -= ledger.CarriedQuantity(npc.Id.Value, food.Id);
        if (wantWater <= 0 && wantFood <= 0) return;
        long days = Math.Max(1, (hoursAway + world.Calendar.HoursPerDay - 1) / world.Calendar.HoursPerDay);
        long keepWater = 0, keepFood = 0;
        foreach (var memberId in home.Members)
        {
            if (memberId == npc.Id || world.FindNpc(memberId) is not { IsAlive: true } other) continue;
            keepWater += Math.Max(days, UnitsFor(other.ThirstAt(now), needs.ThirstDecayPerHour, hoursAway, line, waterLevel));
            keepFood += Math.Max(days, UnitsFor(other.HungerAt(now), needs.HungerDecayPerHour, hoursAway, line, foodLevel));
        }
        // AD-157b: o desjejum. Quem vai ficar fora mais do que a fome e a sede esperam come e bebe em casa antes de sair (do que
        // sobra depois da reserva dos outros), em vez de partir e voltar com fome; leva só o que ainda faltar.
        if (wantWater > 0 && home.Stock.GetValueOrDefault(water) > keepWater && npc.ThirstAt(now) < waterLevel && home.Withdraw(water, 1).IsSuccess)
        {
            npc.SetThirst(waterLevel, now);
            world.RecordResourceConsumed(water, 1);
            world.InventionTally.DrankBeforeLeaving++;
            wantWater = UnitsFor(npc.ThirstAt(now), needs.ThirstDecayPerHour, waterHours, line, waterLevel) - ledger.CarriedQuantity(npc.Id.Value, water.Id);
        }
        var meal = LivingWorld.Simulation.Behavior.Resolvers.FoodResolver.ResolveMeal(world, home);
        // a comida é cara: só come antes quem chegaria à fome crítica fora (a unidade inteira gasta antes da hora esvaziava a despensa)
        bool foodCritical = npc.HungerAt(now) - needs.HungerDecayPerHour * foodHours < 100 - MealAheadDeficit;
        if (wantFood > 0 && foodCritical && meal.Id > 0 && home.Stock.GetValueOrDefault(meal) > (meal == food ? keepFood : 0) && npc.HungerAt(now) < foodLevel
            && home.Withdraw(meal, 1).IsSuccess)
        {
            npc.SetHunger(Math.Max(npc.HungerAt(now), ObjectUse.ConsumeLevel(world, meal, ObjectEffectTargets.Hunger)), now);
            world.RecordResourceConsumed(meal, 1);
            world.InventionTally.AteBeforeLeaving++;
            wantFood = UnitsFor(npc.HungerAt(now), needs.HungerDecayPerHour, foodHours, line, foodLevel) - ledger.CarriedQuantity(npc.Id.Value, food.Id);
        }
        if (wantWater <= 0 && wantFood <= 0) return;
        var room = RoomOf(world, npc);
        long weight = room.WeightG, dry = room.DryMl, wet = room.WetMl;

        int Take(ResourceType resource, int want, long volume, long keep)
        {
            if (want <= 0) return 0;
            long unitMl = rules.UnitMlOf(resource.Id), unitG = rules.UnitGramsOf(resource.Id);
            long spare = home.Stock.GetValueOrDefault(resource) - keep;
            long n = Math.Min(Math.Min(want, spare), Math.Min(volume / unitMl, weight / unitG));
            if (n <= 0 || !home.Withdraw(resource, n).IsSuccess) return 0;
            ledger.Carry(npc.Id.Value, resource.Id, (int)n);
            weight -= n * unitG;
            dry -= n * unitMl;
            wet -= n * unitMl;
            return (int)n;
        }

        int took = Take(water, wantWater, rules.IsLiquid(water.Id) ? wet : dry, keepWater);
        took += Take(food, wantFood, rules.IsLiquid(food.Id) ? wet : dry, keepFood);
        if (took == 0) return;
        if (room.ItemId != 0 && ledger.HeldBy(npc.Id.Value) != room.ItemId) ledger.Hold(npc.Id.Value, room.ItemId);
        world.InventionTally.PackTrips++;
        world.InventionTally.PackUnits += took;
    }

    /// <summary>Em casa: o que sobrou do que levou volta para a despensa, e o recipiente volta para a casa.</summary>
    public static void Unpack(WorldState world, Npc npc, Household home)
    {
        var ledger = world.Invention!;
        ledger.Hold(npc.Id.Value, 0);
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

    /// <summary>A compra cortada pelo que cabe (AD-143): o que ficou no mercado custa outra ida e volta, na proporção do cortado.
    /// AD-153 (<see cref="Domain.Invention.InventionRules.WholeTripsForCut"/>): uma ida e volta inteira para cada carga que ficou.</summary>
    public static void FeltCargoCut(WorldState world, Npc npc, Household home, CellCoord market, long cutG, long carriedG)
    {
        var rules = world.InventionRules!;
        if (cutG <= 0 || npc.AgeYears(world.CurrentDate) < rules.MinInventorAgeYears) return;
        long trip = 2 * Mobility.HouseholdTicks(world, home, home.Location, market) * 60;
        long minutes = rules.WholeTripsForCut
            ? trip * ((cutG + Math.Max(1, carriedG) - 1) / Math.Max(1, carriedG))
            : trip * cutG / Math.Max(1, carriedG + cutG);
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
        ledger.Hold(npcId, 0); // o recipiente fica com a casa
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
