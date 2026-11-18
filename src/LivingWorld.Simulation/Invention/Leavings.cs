using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Economy;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Invention;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Crime;
using LivingWorld.Simulation.Emergence;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Invention;

/// <summary>O espólio de quem morre longe de casa (AD-168, pedido do dono: "se morreu longe da casa, fica lá, para alguém buscar
/// ou ser roubado"). Morto em casa, a carga e o recipiente ficam com a casa (como antes). Longe, ficam no chão onde ele caiu, com a
/// casa a que pertencem e a do herdeiro. Quem estava junto e é da casa recolhe na hora; depois a casa sabe da morte e do lugar (o
/// luto já chega na hora: dívida de realismo) e alguém dela vai buscar numa viagem de verdade, levando o que couber; quem passa e
/// não é da casa pode furtar pelo caminho de sempre do furto. O perecível estraga como na despensa. Tudo por evento (morte, chegada,
/// decisão de quem tem tempo livre, o mês): nenhuma varredura por pessoa.</summary>
public static class Leavings
{
    private const string Source = InventionSystem.SystemName;

    private static bool On(WorldState world) => world.InventionOn && world.InventionRules!.Leavings.Enabled;

    /// <summary>A morte: devolve verdadeiro se a carga e o recipiente do morto ficaram no chão (morreu longe com algo); falso = o
    /// chamador segue como antes (em casa, sem casa a que voltar, ou regra desligada).</summary>
    internal static bool Drop(WorldState world, TickContext ctx, long deadId, long? cause)
    {
        if (!On(world) || world.FindNpc(new NpcId(deadId)) is not { } dead) return false;
        var ledger = world.Invention!;
        var home = dead.Household is { } h ? world.FindHousehold(h) : null;
        if (home is { IsHomeless: true }) return false; // sem chão de casa, a casa é onde ela está
        var at = dead.CurrentLocation;
        bool atHome = home is not null && at == home.Location;
        long held = ledger.HeldBy(deadId);
        bool carries = ledger.CarriesAnything(deadId);
        if (!carries && held == 0) { Reground(world, ctx, dead, atHome); return false; }
        if (atHome)
        {
            world.InventionTally.LeavingsAtHome++;
            Reground(world, ctx, dead, atHome);
            return false;
        }

        long id = ++ledger.LastLeavingId;
        long heir = HeirHousehold(world, dead, home);
        ledger.Hold(deadId, 0);
        ledger.PutLeaving(new Leaving(id, at.X, at.Y, home?.Id.Value ?? -1, heir, deadId, ctx.CurrentTick, held));
        int units = 0;
        int i = ledger.CarriedIndex(deadId, int.MinValue);
        i = i >= 0 ? i : ~i;
        while (i < ledger.Carried.Count && ledger.Carried[i].NpcId == deadId)
        {
            var load = ledger.Carried[i];
            ledger.Leave(id, load.ResourceId, load.Quantity);
            units += load.Quantity;
            ledger.Carry(deadId, load.ResourceId, -load.Quantity);
        }
        Reground(world, ctx, dead, atHome);
        world.InventionTally.LeavingsLeft++;
        world.InventionTally.LeftUnits += units;
        ctx.LogEvent(WorldEventKind.LeavingsLeft,
            $"{id}|{deadId}|{home?.Id.Value ?? -1}|{heir}|{at.X}|{at.Y}|{units}|{held}", Source, cause);
        // quem estava junto e é da casa recolhe na hora (o primeiro por id que pode)
        foreach (var other in world.AliveNpcIndex.At(at).OrderBy(n => n.Id.Value))
        {
            if (ledger.LeavingIndex(id) < 0) break;
            if (other.CurrentAction != ActionType.Sleep && ClaimKind(world, ledger.Leavings![ledger.LeavingIndex(id)], other) is { } how
                && how != "achado" && Grown(world, other))
                Collect(world, ctx, other, id, how);
        }
        return true;
    }

    /// <summary>O recipiente de outro espólio que o morto levava para casa volta ao chão onde ele caiu (ou, morto em casa, fica nela).</summary>
    private static void Reground(WorldState world, TickContext ctx, Npc dead, bool atHome)
    {
        var ledger = world.Invention!;
        if (ledger.Leavings is not { Count: > 0 } list) return;
        foreach (var l in list.Where(l => l.CarrierId == dead.Id.Value).ToList())
        {
            if (atHome) { ledger.PutLeaving(l with { ItemId = 0, CarrierId = -1 }); DropIfEmpty(world, l.Id); }
            else ledger.PutLeaving(l with { X = dead.CurrentLocation.X, Y = dead.CurrentLocation.Y, CarrierId = -1, FetcherId = -1, FetchTick = -1 });
        }
        _ = ctx;
    }

    /// <summary>A casa do primeiro herdeiro (o do espólio, <see cref="Population.Lifecycle.EstateSystem"/>) que não é a do morto:
    /// quem busca se a casa se desfez. Só se procura quando o morto era o último da casa (a busca pelos vivos é rara: a morte longe
    /// com carga).</summary>
    private static long HeirHousehold(WorldState world, Npc dead, Household? home)
    {
        if (home is not null && home.Members.Any(m => m != dead.Id && world.FindNpc(m) is { IsAlive: true })) return -1;
        foreach (var heir in Population.Lifecycle.EstateSystem.HeirsOf(world, dead))
            if (heir.Household is { } hh && hh != dead.Household) return hh.Value;
        return -1;
    }

    /// <summary>Quem pode recolher sem furtar: "casa" (mora na casa do morto), "herdeiro" (a casa se desfez e mora na do herdeiro),
    /// "achado" (não há dono vivo nenhum); nulo = é de outra casa (levar é furto).</summary>
    internal static string? ClaimKind(WorldState world, in Leaving leaving, Npc npc)
    {
        long owner = ClaimantHousehold(world, leaving);
        if (owner < 0) return "achado";
        if (npc.Household is not { } h || h.Value != owner) return null;
        return owner == leaving.HouseholdId ? "casa" : "herdeiro";
    }

    /// <summary>A casa que pode buscar: a do morto enquanto tem gente viva, senão a do herdeiro; -1 = ninguém.</summary>
    internal static long ClaimantHousehold(WorldState world, in Leaving leaving)
    {
        if (Lives(world, leaving.HouseholdId)) return leaving.HouseholdId;
        return Lives(world, leaving.HeirHouseholdId) ? leaving.HeirHouseholdId : -1;
    }

    private static bool Lives(WorldState world, long householdId)
    {
        if (householdId < 0 || world.FindHousehold(new HouseholdId(householdId)) is not { IsEmpty: false } home) return false;
        foreach (var m in home.Members) if (world.FindNpc(m) is { IsAlive: true }) return true;
        return false;
    }

    private static bool Grown(WorldState world, Npc npc) =>
        npc.AgeYears(world.CurrentDate) >= world.InventionRules!.Leavings.MinFetcherAgeYears;

    /// <summary>A chegada a uma célula: quem é da casa (ou acha algo sem dono) recolhe; quem é de outra casa pode furtar.</summary>
    public static void OnArrived(WorldState world, TickContext ctx, Npc npc, CellCoord cell)
    {
        if (!On(world) || world.Invention!.Leavings is not { Count: > 0 } list) return;
        List<long>? here = null;
        foreach (var l in list) if (l.CarrierId < 0 && l.X == cell.X && l.Y == cell.Y) (here ??= []).Add(l.Id);
        if (here is null || !Grown(world, npc)) return;
        foreach (long id in here)
        {
            int i = world.Invention.LeavingIndex(id);
            if (i < 0) continue;
            var leaving = world.Invention.Leavings![i];
            if (ClaimKind(world, leaving, npc) is { } how) Collect(world, ctx, npc, id, how);
            else MaybeSteal(world, ctx, npc, leaving);
        }
    }

    /// <summary>Quem tem tempo livre e é da casa que pode buscar vai: o espólio mais antigo que a casa sabe onde está, ao alcance
    /// de <see cref="LeavingsRules.MaxFetchHours"/>, sem ninguém já a caminho, e de que ele consegue levar algo. Marca a ida (a
    /// viagem começa nesta hora); devolve verdadeiro para a decisão virar a viagem.</summary>
    internal static bool WantsToFetch(WorldState world, Npc npc, long now)
    {
        if (!On(world) || world.Invention!.Leavings is not { Count: > 0 } list || npc.Household is not { } h) return false;
        if (npc.CurrentAction == ActionType.Travel || !Grown(world, npc)) return false;
        var rules = world.InventionRules!.Leavings;
        foreach (var l in list)
        {
            if (l.CarrierId >= 0 || FetchActive(world, l) || ClaimantHousehold(world, l) != h.Value) continue;
            var cell = new CellCoord(l.X, l.Y);
            if (cell == npc.CurrentLocation || !CanTakeSomething(world, npc, l)) continue;
            if (Mobility.TravelTicks(world, npc, npc.CurrentLocation, cell, 1.0) > rules.MaxFetchHours) continue;
            world.Invention.PutLeaving(l with { FetcherId = npc.Id.Value, FetchTick = now });
            world.InventionTally.LeavingsFetches++;
            return true;
        }
        return false;
    }

    /// <summary>A ida de busca ainda vale: quem foi está vivo e viajando desde a hora em que decidiu ir.</summary>
    private static bool FetchActive(WorldState world, in Leaving l) =>
        l.FetcherId >= 0 && world.FindNpc(new NpcId(l.FetcherId)) is { IsAlive: true } f
        && f.CurrentAction == ActionType.Travel && f.ActionStartedAtTick >= l.FetchTick;

    /// <summary>O destino da viagem de quem saiu para buscar um espólio; nulo = não está numa ida de busca.</summary>
    internal static CellCoord? ErrandDestination(WorldState world, Npc npc)
    {
        if (world.Invention?.Leavings is not { Count: > 0 } list) return null;
        foreach (var l in list)
            if (l.FetcherId == npc.Id.Value && FetchActive(world, l)) return new CellCoord(l.X, l.Y);
        return null;
    }

    /// <summary>AD-189: o espólio que a pessoa saiu para buscar (o mesmo de <see cref="ErrandDestination"/>); nulo fora de uma ida.</summary>
    internal static long? ErrandLeavingId(WorldState world, Npc npc)
    {
        if (world.Invention?.Leavings is not { Count: > 0 } list) return null;
        foreach (var l in list)
            if (l.FetcherId == npc.Id.Value && FetchActive(world, l)) return l.Id;
        return null;
    }

    /// <summary>Em casa, o que voltou com a pessoa: o recipiente que ela trouxe sem poder usar fica com a casa.</summary>
    internal static void OnUnpack(WorldState world, Npc npc)
    {
        var ledger = world.Invention!;
        if (ledger.Leavings is not { Count: > 0 } list) return;
        foreach (var l in list.Where(l => l.CarrierId == npc.Id.Value).ToList())
        {
            ledger.PutLeaving(l with { ItemId = 0, CarrierId = -1 });
            DropIfEmpty(world, l.Id);
        }
    }

    /// <summary>Espaço que sobra: o do corpo e do recipiente menos o que já leva.</summary>
    private static (long WeightG, long DryMl, long WetMl) FreeRoom(WorldState world, Npc npc)
    {
        var rules = world.InventionRules!;
        var room = PersonalCarry.RoomOf(world, npc);
        long weight = room.WeightG, dry = room.DryMl, wet = room.WetMl;
        var ledger = world.Invention!;
        int i = ledger.CarriedIndex(npc.Id.Value, int.MinValue);
        for (i = i >= 0 ? i : ~i; i < ledger.Carried.Count && ledger.Carried[i].NpcId == npc.Id.Value; i++)
        {
            var c = ledger.Carried[i];
            weight -= (long)c.Quantity * rules.UnitGramsOf(c.ResourceId);
            long ml = (long)c.Quantity * rules.UnitMlOf(c.ResourceId);
            dry -= ml;
            if (rules.IsLiquid(c.ResourceId)) wet -= ml;
        }
        return (Math.Max(0, weight), Math.Max(0, dry), Math.Max(0, Math.Min(wet, dry)));
    }

    private static bool CanTakeSomething(WorldState world, Npc npc, in Leaving l)
    {
        if (l.ItemId != 0) return true;
        var rules = world.InventionRules!;
        var (weight, dry, wet) = FreeRoom(world, npc);
        foreach (var load in world.Invention!.LeftLoads ?? [])
        {
            if (load.LeavingId != l.Id) continue;
            long volume = rules.IsLiquid(load.ResourceId) ? wet : dry;
            if (volume >= rules.UnitMlOf(load.ResourceId) && weight >= rules.UnitGramsOf(load.ResourceId)) return true;
        }
        return false;
    }

    /// <summary>Leva do espólio o recipiente (segura-o se não leva outro; se já leva, carrega-o para casa sem usar) e o que couber,
    /// a água primeiro (mata antes); devolve as unidades levadas e se levou a peça. O dono da peça passa a ser a casa de quem leva.</summary>
    private static (int Units, bool Item) Take(WorldState world, Npc npc, long leavingId)
    {
        var ledger = world.Invention!;
        var rules = world.InventionRules!;
        int i = ledger.LeavingIndex(leavingId);
        if (i < 0) return (0, false);
        var l = ledger.Leavings![i];
        bool item = false;
        if (l.ItemId != 0)
        {
            if (!ledger.Items.Any(x => x.Id == l.ItemId)) ledger.PutLeaving(l = l with { ItemId = 0 }); // gastou no chão
            else
            {
                item = true;
                if (npc.Household is { } hh) ledger.GiveItem(l.ItemId, hh.Value, npc.City);
                if (ledger.HeldBy(npc.Id.Value) == 0)
                {
                    long itemId = l.ItemId;
                    ledger.PutLeaving(l = l with { ItemId = 0 });
                    ledger.Hold(npc.Id.Value, itemId);
                }
                else ledger.PutLeaving(l = l with { CarrierId = npc.Id.Value });
            }
        }
        var (weight, dry, wet) = FreeRoom(world, npc);
        int units = 0;
        var loads = (ledger.LeftLoads ?? []).Where(x => x.LeavingId == leavingId)
            .OrderByDescending(x => rules.IsLiquid(x.ResourceId)).ThenBy(x => x.ResourceId).ToList();
        foreach (var load in loads)
        {
            bool liquid = rules.IsLiquid(load.ResourceId);
            long unitMl = rules.UnitMlOf(load.ResourceId), unitG = rules.UnitGramsOf(load.ResourceId);
            long n = Math.Min(load.Quantity, Math.Min((liquid ? wet : dry) / unitMl, weight / unitG));
            if (n <= 0) continue;
            ledger.Leave(leavingId, load.ResourceId, -(int)n);
            ledger.Carry(npc.Id.Value, load.ResourceId, (int)n);
            weight -= n * unitG;
            dry -= n * unitMl;
            if (liquid) wet -= n * unitMl;
            wet = Math.Min(wet, dry);
            units += (int)n;
        }
        return (units, item);
    }

    /// <summary>Recolhe sem furto (a casa, o herdeiro ou o achado sem dono): o evento e, se nada sobrou, o espólio sai.</summary>
    internal static void Collect(WorldState world, TickContext ctx, Npc npc, long leavingId, string how)
    {
        var ledger = world.Invention!;
        int i = ledger.LeavingIndex(leavingId);
        if (i < 0) return;
        long dead = ledger.Leavings![i].DeadId;
        var (units, item) = Take(world, npc, leavingId);
        i = ledger.LeavingIndex(leavingId);
        if (i >= 0 && ledger.Leavings![i].FetcherId >= 0) ledger.PutLeaving(ledger.Leavings[i] with { FetcherId = -1, FetchTick = -1 });
        DropIfEmpty(world, leavingId);
        if (units == 0 && !item) return;
        var t = world.InventionTally;
        if (how == "achado") t.LeavingsFound++; else t.LeavingsCollected++;
        t.CollectedUnits += units;
        ctx.LogEvent(WorldEventKind.LeavingsCollected, $"{leavingId}|{npc.Id.Value}|{dead}|{units}|{(item ? 1 : 0)}|{how}", Source);
    }

    /// <summary>Quem passa e é de outra casa pode levar (AD-150, o furto de sempre): ninguém da casa ali de guarda, o temperamento
    /// contra a barreira (a fome a rebaixa; a dissuasão e cada pessoa acordada no lugar a sobem) e o sorteio semeado.</summary>
    private static void MaybeSteal(WorldState world, TickContext ctx, Npc npc, Leaving leaving)
    {
        var theft = world.TheftRules;
        if (!theft.Enabled || npc.AgeYears(world.CurrentDate) < theft.MinAgeYears
            || Behavior.Decision.BehaviorDecisionSystem.ConstrainByDevelopment(world, npc, ActionType.Steal) != ActionType.Steal)
            return;
        long now = ctx.CurrentTick;
        var cell = new CellCoord(leaving.X, leaving.Y);
        int witnesses = 0;
        foreach (var other in world.AliveNpcIndex.At(cell))
        {
            if (other.Id == npc.Id || other.CurrentAction == ActionType.Sleep) continue;
            if (ClaimKind(world, leaving, other) is "casa" or "herdeiro") return; // a casa está ali: ninguém leva
            witnesses++;
        }
        if (!CanTakeSomething(world, npc, leaving)) return;
        var rules = world.InventionRules!.Leavings;
        double deterrence = Theft.Deterrence(world, npc, now);
        double need = Theft.NeedOf(theft, npc, now);
        double barrier = (need >= 0 ? theft.NeedBarrier(need, deterrence) : theft.PlannedBarrier(deterrence))
            + Math.Min(3, witnesses) * rules.SeenBarrierPerWitness
            + rules.KnownDeadBarrier * BondWithDead(world, npc, leaving); // AD-182: o que era de um conhecido não se leva
        double inclination = theft.Inclination(PersonalityWeighting.WeightOf(npc.Personality, ActionType.Steal), barrier);
        if (inclination <= 0) return;
        var rng = ctx.OneShot($"leaving-theft-{npc.Id.Value}-{leaving.Id}-{now}");
        if (rng.NextDouble() * 1000 >= Math.Min(1000, rules.TheftChancePm * inclination)) return;
        long owner = ClaimantHousehold(world, leaving);
        var house = owner >= 0 ? world.FindHousehold(new HouseholdId(owner)) : null;
        var victim = house is not null && world.FindNpc(house.Head) is { IsAlive: true } head ? head : null;
        var food = world.EconomyRules.FoodResourceId;
        int resource = world.Invention!.LeftQuantity(leaving.Id, food) > 0 ? food : (world.Invention.LeftLoads ?? []).FirstOrDefault(x => x.LeavingId == leaving.Id).ResourceId;
        int units = 0;
        foreach (var l in world.Invention.LeftLoads ?? []) if (l.LeavingId == leaving.Id) units += l.Quantity;
        var target = new TheftTarget(TheftTargetKind.Ground, victim, house, null, resource, units, 0,
            units * Theft.PriceOf(world, npc.City, resource), cell, leaving.Id);
        Theft.Commit(world, ctx, npc, TheftMotive.Opportunity, target, rng);
    }

    /// <summary>AD-182 (08c item 155): o laço de quem passa com o morto do espólio, 0–1 (o maior entre confiança e afeto).</summary>
    private static double BondWithDead(WorldState world, Npc npc, Leaving leaving)
    {
        if (!world.Relationships.TryGetValue(new RelationshipKey(npc.Id, new NpcId(leaving.DeadId)), out var rel)) return 0;
        return Math.Clamp(Math.Max(rel.Get(RelationshipAxis.Trust), rel.Get(RelationshipAxis.Affection)) / 100.0, 0, 1);
    }

    /// <summary>O furto executado (<see cref="Theft"/>): leva o que couber e a peça; devolve (unidades, valor).</summary>
    internal static (long Qty, long Value) Loot(WorldState world, Npc thief, long leavingId)
    {
        var (units, item) = Take(world, thief, leavingId);
        DropIfEmpty(world, leavingId);
        if (units == 0 && !item) return (0, 0);
        world.InventionTally.LeavingsStolen++;
        world.InventionTally.StolenUnits += units;
        long value = units * Theft.PriceOf(world, thief.City, world.EconomyRules.FoodResourceId);
        return (Math.Max(units, item ? 1 : 0), Math.Max(value, item ? 1 : 0));
    }

    private static void DropIfEmpty(WorldState world, long leavingId)
    {
        var ledger = world.Invention!;
        int i = ledger.LeavingIndex(leavingId);
        if (i < 0) return;
        var l = ledger.Leavings![i];
        if (l.ItemId == 0 && l.CarrierId < 0 && !ledger.HasLoads(leavingId)) ledger.RemoveLeaving(leavingId);
    }

    /// <summary>O mês: o perecível no chão estraga na taxa da despensa vezes <see cref="LeavingsRules.GroundSpoilagePm"/> (ao tempo e
    /// aos bichos, mais que na despensa), e a água se perde a <see cref="LeavingsRules.GroundWaterLossPerDayPm"/> por dia (evapora e
    /// vaza do recipiente largado). AD-182 (08c item 156): as duas taxas crescem com o calor do lugar (fator
    /// <see cref="LeavingsRules.GroundQ10"/> a cada 10 °C acima de <see cref="LeavingsRules.GroundReferenceCelsius"/>) e contam só
    /// os dias que o espólio passou no chão. Pelo mesmo evento de perda (a conservação o conta).</summary>
    internal static void Spoil(WorldState world, TickContext ctx, EconomyRules economy, int days)
    {
        if (!On(world) || world.Invention!.LeftLoads is not { Count: > 0 } loads) return;
        var rules = world.InventionRules!.Leavings;
        double factor = rules.GroundSpoilagePm / 1000.0;
        int hoursPerDay = world.Calendar.HoursPerDay;
        var leavings = world.Invention.Leavings;
        foreach (var load in loads.ToList())
        {
            double daily;
            if (economy.SpoilagePerDayByResource.TryGetValue(load.ResourceId, out var rate) && rate > 0) daily = rate * factor;
            else if (load.ResourceId == economy.WaterResourceId && rules.GroundWaterLossPerDayPm > 0) daily = rules.GroundWaterLossPerDayPm / 1000.0;
            else continue;
            int exposed = days;
            if (leavings is not null && world.Invention.LeavingIndex(load.LeavingId) is var at and >= 0)
            {
                var leaving = leavings[at];
                if (rules.CountDaysOnGround) exposed = (int)Math.Clamp((ctx.CurrentTick - leaving.Tick) / hoursPerDay, 0, days);
                daily *= HeatFactor(world, rules, new CellCoord(leaving.X, leaving.Y), ctx.CurrentTick);
            }
            if (exposed <= 0) continue;
            daily = Math.Min(daily, 0.999999);
            double lost = 1 - DeterministicMath.Exp(exposed * DeterministicMath.Log(1 - daily));
            long spoiled = Math.Min(load.Quantity, Economy.Production.CropSystem.RoundStochastic(load.Quantity * lost, ctx,
                "leaving-spoilage", load.LeavingId * 64 + load.ResourceId, ctx.CurrentTick));
            if (spoiled <= 0) continue;
            world.Invention.Leave(load.LeavingId, load.ResourceId, -(int)spoiled);
            if (load.ResourceId == economy.WaterResourceId) world.InventionTally.GroundWaterLost += spoiled;
            else world.InventionTally.SpoiledUnits += spoiled;
            ctx.LogEvent(WorldEventKind.ResourceLost, $"espolio-{load.LeavingId}|{load.ResourceId}|{spoiled}", sourceSystem: Source);
            DropIfEmpty(world, load.LeavingId);
        }
    }

    /// <summary>Quanto o calor do lugar acelera a perda ao relento (Q10): 1 na temperatura de referência, ×Q10 a cada 10 °C a mais,
    /// ÷Q10 a cada 10 °C a menos (o frio conserva).</summary>
    internal static double HeatFactor(WorldState world, LeavingsRules rules, CellCoord cell, long tick)
    {
        if (rules.GroundQ10 <= 1) return 1;
        double celsius = LivingWorld.Simulation.Extraordinary.Mechanics.EnvironmentTemperatureMechanic.EffectiveTemperature(world, cell, tick);
        return DeterministicMath.Exp((celsius - rules.GroundReferenceCelsius) / 10.0 * DeterministicMath.Log(rules.GroundQ10));
    }
}
