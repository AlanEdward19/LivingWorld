using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Economy;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Behavior.Resolvers;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Economy.Market;
using LivingWorld.Simulation.Extraordinary.Engine;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Behavior.Decision;

// Parte de BehaviorDecisionSystem — efeitos da ação concluída: comer, comprar, dormir, poder.
public sealed partial class BehaviorDecisionSystem
{
    private static void ApplyActionEffect(
        WorldState world, Npc npc, ActionType action, MarketIndex marketIndex, long tick, TickContext ctx)
    {
        switch (action)
        {
            case ActionType.Eat:
                ApplyFoodPlan(world, npc, marketIndex, tick, primary: ActionType.Eat);
                break;
            case ActionType.Sleep:
                ApplySleep(world, npc, tick);
                break;
            case ActionType.Socialize:
                npc.SetSocial(100, tick);
                break;
            case ActionType.Buy:
                ApplyFoodPlan(world, npc, marketIndex, tick, primary: ActionType.Buy);
                break;
            case ActionType.UsePower:
                ApplyUsePower(world, npc, tick, ctx);
                break;
        }
    }

    /// <summary>Executa o PendingPowerInvocation via motor extraordinário e registra
    /// <see cref="WorldEventKind.PowerInvoked"/> com proveniência (COH-33).</summary>
    private static void ApplyUsePower(WorldState world, Npc npc, long tick, TickContext ctx)
    {
        var pending = npc.PendingPowerInvocation;
        npc.PendingPowerInvocation = null;
        if (pending is null) return;

        // Decisão (raiz) + PowerInvoked com CauseEventId apontando pra ela (COH-33 AC).
        long decisionEventId = ctx.LogEvent(
            WorldEventKind.PowerInvoked,
            $"{npc.Id.Value}|{pending.PowerId}|decision",
            SystemName,
            causeEventId: null);

        long powerInvokedId = ctx.LogEvent(
            WorldEventKind.PowerInvoked,
            $"{npc.Id.Value}|{pending.PowerId}|{pending.MechanicToken}",
            SystemName,
            causeEventId: decisionEventId);

        NpcId targetId = pending.SuggestedTarget ?? npc.Id;
        CellCoord? targetCell = null;
        if (pending.MechanicToken.StartsWith("npc.teleport", StringComparison.Ordinal))
            targetCell = ResolveTeleportTargetCell(world, npc);

        var invocation = new ExtraordinaryInvocation(
            world.NextEventId,
            npc.Id,
            pending.PowerId,
            targetId,
            Resolution: null,
            TargetCell: targetCell,
            Origin: ExtraordinaryInvocationOrigin.Authored);

        ExtraordinaryInvocationEngine.Invoke(world, ctx, invocation, causeEventId: powerInvokedId);
        _ = tick;
    }

    private static CellCoord? ResolveTeleportTargetCell(WorldState world, Npc npc)
    {
        // Destino adjacente vazio preferido; senão a própria célula (Invoke pode falhar — ok).
        foreach (var delta in new (int X, int Y)[] { (1, 0), (-1, 0), (0, 1), (0, -1), (1, 1) })
        {
            var candidate = new CellCoord(npc.CurrentLocation.X + delta.X, npc.CurrentLocation.Y + delta.Y);
            if (!world.Map.TryGetCell(candidate, out _)) continue;
            if (world.IsExtraordinaryConstructCell(candidate)) continue;
            if (world.Npcs.Any(o => o.IsAlive && o.CurrentLocation == candidate)) continue;
            return candidate;
        }

        return npc.CurrentLocation;
    }

    private static void ApplySleep(WorldState world, Npc npc, long tick)
    {
        var rest = RestPlaceResolver.Resolve(world, npc);
        if (rest.Location != npc.CurrentLocation) return;

        npc.SetSleep((int)(100 * rest.RecoveryEfficiency), tick);
    }

    /// <summary>COH-42: falha local tenta alternativa (Buy↔Eat/estoque) antes de
    /// <see cref="IntentStatus.Invalidated"/>.</summary>
    private static void ApplyFoodPlan(
        WorldState world, Npc npc, MarketIndex marketIndex, long tick, ActionType primary) =>
        ResolveFoodPlan(world, npc, marketIndex, tick, primary);

    /// <summary>Resolve Buy/Eat com alternativas; retorna o status final do Intent quando
    /// um plano alimentar estava em curso (para testes T26).</summary>
    internal static IntentStatus? ResolveFoodPlan(
        WorldState world, Npc npc, MarketIndex marketIndex, long tick, ActionType primary)
    {
        EnsureFoodIntent(npc, primary, tick);

        bool primaryOk = primary == ActionType.Buy
            ? TryApplyBuy(world, npc, marketIndex)
            : TryApplyEat(world, npc, tick);

        if (primaryOk)
        {
            if (npc.IntentStatus == IntentStatus.Active)
                npc.CompleteIntent();
            return npc.IntentStatus;
        }

        bool alternativeOk = primary == ActionType.Buy
            ? TryApplyEat(world, npc, tick)
            : TryApplyBuy(world, npc, marketIndex);

        if (alternativeOk)
        {
            if (npc.IntentStatus == IntentStatus.Active)
                npc.CompleteIntent();
            return npc.IntentStatus;
        }

        if (npc.IntentStatus == IntentStatus.Active)
            npc.InvalidateIntent();
        return npc.IntentStatus;
    }

    private static void EnsureFoodIntent(Npc npc, ActionType foodAction, long tick)
    {
        if (npc.IntentStatus == IntentStatus.Active
            && npc.CurrentIntent is ActionType.Buy or ActionType.Eat)
            return;

        npc.SetIntent(foodAction, tick);
    }

    private static bool TryApplyEat(WorldState world, Npc npc, long tick)
    {
        var econ = world.EconomyRules;
        if (!econ.Enabled)
        {
            npc.SetHunger(100, tick);
            npc.SetThirst(100, tick);
            return true;
        }

        var household = npc.Household is { } householdId ? world.FindHousehold(householdId) : null;
        bool fed = false;
        var meal = household is null ? new ResourceType(econ.FoodResourceId) : FoodResolver.ResolveMeal(world, household);
        if (meal.Id > 0 && (household?.Withdraw(meal, 1).IsSuccess == true || TryRation(world, npc, meal)))
        {
            npc.SetHunger(100, tick);
            world.RecordResourceConsumed(meal, 1);
            fed = true;
        }

        var water = new ResourceType(econ.WaterResourceId);
        if (household?.Withdraw(water, 1).IsSuccess == true || TryRation(world, npc, water))
        {
            npc.SetThirst(100, tick);
            world.RecordResourceConsumed(water, 1);
            fed = true;
        }

        return fed;
    }

    /// <summary>Ração do empregador: quem está no próprio local de trabalho e a despensa de casa não tem o que comer ou
    /// beber retira uma unidade do estoque da oficina (o poço e o celeiro da propriedade). Sem isso o lavrador morria de
    /// sede ao lado de mil de água por estar sem moeda no bolso. Só serve quem está lá e só o que a oficina tem.</summary>
    private static bool TryRation(WorldState world, Npc npc, ResourceType resource)
    {
        if (npc.Employer is not { } employerId || world.FindWorkplace(employerId) is not { } workplace) return false;
        if (workplace.Location != npc.CurrentLocation) return false;
        return workplace.Withdraw(resource, 1).IsSuccess;
    }

    private static bool TryApplyBuy(WorldState world, Npc npc, MarketIndex marketIndex)
    {
        if (!world.EconomyRules.Enabled) return false;
        var market = NearestEssentialsMarket(world, npc, marketIndex);
        if (market is null) return false; // vendedor indisponível
        if (npc.Household is not { } householdId || world.FindHousehold(householdId) is not { } household)
            return false;

        var econ = world.EconomyRules;
        bool bought = false;
        foreach (var resourceId in new[] { econ.FoodResourceId, econ.WaterResourceId })
        {
            var resource = new ResourceType(resourceId);
            if (!market.Prices.TryGetValue(resource, out var unitPrice) || unitPrice <= 0) continue;

            // Compra de domicílio, como uma ida real ao mercado: cobre ~4 dias de cada membro
            // (mínimo 10), limitada ao que a carteira paga.
            long quantity = Math.Max(MinimumPurchaseQuantity, household.Members.Count * PurchaseDaysOfSupply);
            quantity = Math.Max(1, Math.Min(quantity, npc.Wallet.Amount / unitPrice));
            var ctx = new TransactionContext(
                npc.Wallet, market.Treasury, market.Stock.GetValueOrDefault(resource),
                household.Stock.GetValueOrDefault(resource), resource, unitPrice, quantity);

            if (!MarketTransaction.Execute(ctx).IsSuccess) continue;

            var price = new Money(unitPrice * quantity);
            npc.TryDebitWallet(price);
            market.Withdraw(resource, quantity);
            market.CreditTreasury(price);
            household.Deposit(resource, quantity);
            bought = true;
            NoteTrade(world, npc, market, resource, quantity, price.Amount);
        }

        return bought;
    }

    /// <summary>Registra a compra que fechou: soma ao agregado do dia da oficina, e o comprador passa a ter relação de
    /// negócio com quem atende (Trade). O primeiro contato do par vira evento, na hora seguinte, pelo
    /// <c>TradeLedgerSystem</c>. Sem atendente presente, só o livro.</summary>
    private static void NoteTrade(WorldState world, Npc buyer, Workplace market, ResourceType resource, long quantity, long value)
    {
        long hours = world.CurrentDate.TotalHours;
        world.RecordTrade(market.Id, resource, hours / world.CurrentDate.Calendar.HoursPerDay, quantity, value);

        var attendant = LivingWorld.Simulation.Economy.Trade.TradeLedgerSystem.AttendantAt(world, market);
        if (attendant is null || attendant.Id == buyer.Id) return;

        var key = new LivingWorld.Domain.Population.Family.RelationshipKey(buyer.Id, attendant.Id);
        bool firstContact = !world.Relationships.ContainsKey(key);
        var relationship = world.GetOrCreateRelationship(key, hours);
        relationship.ApplyEvent(LivingWorld.Domain.Population.Family.RelationshipEventType.Trade, world.FamilyRules);
        relationship.MarkContact(hours);
        if (firstContact)
            world.PendingTradeContacts.Add(new(buyer.Id, attendant.Id, market.Id, resource.Id, value));
    }

    private static ActionType RefineForLocation(WorldState world, Npc npc, ActionType candidate, MarketIndex marketIndex) => candidate switch
    {
        ActionType.UsePower => ActionType.UsePower,
        ActionType.Sleep when SleepDestinationOf(world, npc) is { } dest && dest != npc.CurrentLocation => ActionType.Travel,
        ActionType.Work when WorkDestinationOf(world, npc) is { } dest && dest != npc.CurrentLocation => ActionType.Travel,
        ActionType.Buy when BuyDestinationOf(world, npc, marketIndex) is { } dest && dest != npc.CurrentLocation => ActionType.Travel,
        _ when RelocationDestinationOf(world, npc) is { } relocation && relocation != npc.CurrentLocation => ActionType.Travel,
        _ => candidate,
    };
}
