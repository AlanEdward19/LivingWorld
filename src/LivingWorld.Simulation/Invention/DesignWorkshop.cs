using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Invention;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Behavior.Needs;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Extraordinary.Mechanics;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Invention;

/// <summary>Reprodução de um design conhecido como produção (AD-143): a receita é gerada do próprio design
/// (<see cref="PhysicsEvaluator.Recipe"/> para o material, <see cref="Performance.BuildMinutes"/> para o trabalho). Abrir a encomenda
/// tira o material do estoque da casa e, se o material circula na economia, da despensa e do mercado (pagando o preço do dia) antes
/// de ir à natureza: o que já está guardado não precisa ser colhido. A <c>ProductionSystem</c> avança as encomendas uma vez por dia
/// com as horas livres de quem faz, escaladas pela força e pela capacidade de trabalho como a produção das oficinas; na conclusão, a
/// peça como saiu das mãos passa pelo avaliador real: vai para a casa ou para o mercado (com o preço da hora de quem fez), ou falha
/// e devolve parte do material ao estoque.</summary>
public static class DesignWorkshop
{
    /// <summary>Abre a encomenda: material do estoque (casa, despensa, mercado), o resto colhido; devolve os minutos de trabalho.</summary>
    internal static long Open(WorldState world, Npc npc, Design design, int fidelity, long? marketId, long now)
    {
        var rules = world.InventionRules!;
        var ledger = world.Invention!;
        var perf = PhysicsEvaluator.Evaluate(design.Parts, rules, rules.ReferenceRiderKg);
        long minutes = Math.Max(60, perf.BuildMinutes - FromStock(world, npc, design));
        long coins = 0;
        if (world.EconomyRules.Enabled) coins = FromMarket(world, npc, design, ref minutes);
        ledger.PutOrder(new DesignOrder(npc.Id.Value, design.Id, minutes, marketId, (short)Math.Clamp(fidelity, 0, 1000), now, coins));
        return minutes;
    }

    /// <summary>Minutos de coleta poupados pelo material que a casa já guarda, retirado do estoque.</summary>
    private static long FromStock(WorldState world, Npc npc, Design design)
    {
        if (npc.Household is not { } h) return 0;
        var rules = world.InventionRules!;
        var ledger = world.Invention!;
        var gather = PhysicsEvaluator.GatherMinutes(design.Parts, rules);
        long saved = 0;
        foreach (var (material, grams) in PhysicsEvaluator.Recipe(design.Parts, rules))
        {
            long have = ledger.StoredGrams(h.Value, material);
            if (have <= 0 || grams <= 0) continue;
            long take = Math.Min(have, grams);
            ledger.Store(h.Value, material, -take);
            long gatherMinutes = gather.FirstOrDefault(g => g.MaterialId == material).Minutes;
            saved += gatherMinutes * take / grams;
            world.InventionTally.StockGramsUsed += take;
        }
        world.InventionTally.GatherMinutesSaved += saved;
        return saved;
    }

    /// <summary>Material que circula na economia (<see cref="MaterialSpec.ResourceId"/>): primeiro a despensa da casa, depois o
    /// mercado mais perto (pagando o preço do dia, se a carteira dá). Devolve as moedas gastas e desconta a coleta poupada.</summary>
    private static long FromMarket(WorldState world, Npc npc, Design design, ref long minutes)
    {
        var rules = world.InventionRules!;
        if (npc.Household is not { } h || world.FindHousehold(h) is not { } home) return 0;
        var gather = PhysicsEvaluator.GatherMinutes(design.Parts, rules);
        var market = InventionMarket.MarketFor(world, npc);
        long coins = 0;
        foreach (var (materialId, grams) in PhysicsEvaluator.Recipe(design.Parts, rules))
        {
            if (rules.Material(materialId) is not { ResourceId: > 0 } m || grams <= 0) continue;
            var resource = new ResourceType(m.ResourceId);
            int unit = rules.UnitGramsOf(m.ResourceId);
            long units = (grams + unit - 1) / unit;
            long fromHome = Math.Min(units, home.Stock.GetValueOrDefault(resource));
            if (fromHome > 0) home.Withdraw(resource, fromHome);
            long bought = 0;
            if (units > fromHome && market is not null && market.Prices.TryGetValue(resource, out long price) && price > 0)
            {
                bought = Math.Min(Math.Min(units - fromHome, market.Stock.GetValueOrDefault(resource)), npc.Wallet.Amount / price);
                if (bought > 0 && npc.TryDebitWallet(new Money(bought * price)).IsSuccess)
                {
                    market.Withdraw(resource, bought);
                    market.CreditTreasury(new Money(bought * price));
                    coins += bought * price;
                }
                else bought = 0;
            }
            long got = Math.Min(grams, (fromHome + bought) * unit);
            if (got <= 0) continue;
            long gatherMinutes = gather.FirstOrDefault(g => g.MaterialId == materialId).Minutes;
            long saved = gatherMinutes * got / grams;
            minutes = Math.Max(60, minutes - saved);
            world.InventionTally.MarketGramsBought += got;
            world.InventionTally.GatherMinutesSaved += saved;
        }
        return coins;
    }

    /// <summary>Chamado pela <c>ProductionSystem</c> uma vez por dia: cada encomenda anda as horas livres de quem faz.</summary>
    public static void Advance(WorldState world, TickContext ctx)
    {
        if (!world.InventionOn || world.Invention!.Orders.Count == 0) return;
        var ledger = world.Invention!;
        var rules = world.InventionRules!;
        foreach (var order in ledger.Orders.ToList())
        {
            if (world.FindNpc(new NpcId(order.NpcId)) is not { IsAlive: true } npc)
            {
                ledger.Orders.RemoveAt(ledger.OrderIndex(order.NpcId));
                continue;
            }
            int free = npc.Employer is null ? rules.FreeHoursIdle : rules.FreeHoursEmployed;
            double pace = AttributeMechanic.StrengthMultiplier(world, npc) * BodyMechanic.WorkCapacityMultiplier(world, npc);
            long done = Math.Max(1, (long)(free * 60 * pace));
            if (order.MinutesLeft > done) { ledger.PutOrder(order with { MinutesLeft = order.MinutesLeft - done }); continue; }
            ledger.Orders.RemoveAt(ledger.OrderIndex(order.NpcId));
            ledger.Touch();
            if (ledger.FindDesign(order.DesignId) is { } design) Complete(world, ctx, npc, design, order);
        }
    }

    private static void Complete(WorldState world, TickContext ctx, Npc npc, Design design, DesignOrder order)
    {
        var rules = world.InventionRules!;
        var ledger = world.Invention!;
        long now = ctx.CurrentTick;
        double skill = InventionSystem.Skill(npc, rules);
        var rng = ctx.OneShot($"build-{npc.Id.Value}-{now}");
        int noise = (int)(rules.ManufacturingNoisePm * (100 - skill) / 100 + (1000 - order.FidelityPm) / 4);
        var built = DesignSearch.AsBuilt(design.Parts, rules.Materials, noise, rng);
        var perf = PhysicsEvaluator.Evaluate(built, rules, InventionSystem.RiderKg(npc, rules));
        int index = ledger.MindIndex(npc.Id.Value);
        var mind = index >= 0 ? ledger.Minds[index] : new InventionMind(npc.Id.Value, 0, 0, 0, 0, FailureMode.None, null, 0);
        if (!perf.Works)
        {
            world.InventionTally.BuildFailures++;
            if (order.MarketId is not null) world.InventionTally.SaleBuildFailures++;
            Salvage(world, npc, design);
            InventionSystem.Fail(world, ctx, npc, mind, perf.Failure, null, now, build: true);
            int i = ledger.MindIndex(npc.Id.Value); // desanima: só volta a tentar daqui a uns meses
            ledger.PutMind(ledger.Minds[i] with { BusyUntilTick = now + rules.RetryAfterFailureMonths * world.Calendar.HoursPerMonth });
            return;
        }

        world.InventionTally.Built++;
        if (order.MarketId is { } marketId && world.FindWorkplace(new WorkplaceId(marketId)) is { } market && npc.Household is { } home)
        {
            var item = InventionSystem.NewItem(world, npc, design, skill, now);
            // preço pedido = as horas que a peça levou × o preço da hora de quem fez + o material pago (o mercado o move dali)
            long hours = Math.Max(1, PhysicsEvaluator.Evaluate(design.Parts, rules, rules.ReferenceRiderKg).BuildMinutes / 60);
            long ask = Math.Max(1, (long)Math.Ceiling(hours * InventionMarket.HourlyWage(world, npc)) + order.Coins);
            ledger.Items.Add(item with { HouseholdId = home.Value, SellerId = npc.Id.Value, MarketId = market.Id.Value, Price = ask });
            ledger.Touch();
            world.InventionTally.Listed++;
            ledger.PutMind(mind with { BusyUntilTick = now });
            return;
        }
        InventionSystem.AddItem(world, npc, design, skill, now);
        ledger.PutMind(mind with { FrustrationMinutes = 0, BusyUntilTick = now });
    }

    /// <summary>Construção que falhou: parte do material volta ao estoque da casa (o resto foi cavaco e quebra).</summary>
    private static void Salvage(WorldState world, Npc npc, Design design)
    {
        if (npc.Household is not { } h) return;
        var rules = world.InventionRules!;
        foreach (var (material, grams) in PhysicsEvaluator.Recipe(design.Parts, rules))
        {
            long back = grams * rules.SalvagePm / 1000;
            if (back <= 0) continue;
            world.Invention!.Store(h.Value, material, back);
            world.InventionTally.SalvagedGrams += back;
        }
    }
}
