using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Economy;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Objects;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Behavior.Resolvers;
using LivingWorld.Simulation.Cities;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Economy.Market;
using LivingWorld.Simulation.Emergence;
using LivingWorld.Simulation.Extraordinary.Engine;
using LivingWorld.Simulation.Objects;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Behavior.Decision;

// Parte de BehaviorDecisionSystem — efeitos da ação concluída: comer, comprar, dormir, poder.
public sealed partial class BehaviorDecisionSystem
{
    private static void ApplyActionEffect(
        WorldState world, Npc npc, ActionType action, MarketIndex marketIndex, long tick, TickContext ctx,
        TheftIndex? theftIndex)
    {
        switch (action)
        {
            case ActionType.Eat:
                ApplyFoodPlan(world, npc, marketIndex, tick, primary: ActionType.Eat, ctx);
                break;
            case ActionType.Sleep:
                ApplySleep(world, npc, tick);
                break;
            case ActionType.Socialize:
                ApplySocialize(world, npc, tick, ctx);
                break;
            case ActionType.Buy:
                ApplyFoodPlan(world, npc, marketIndex, tick, primary: ActionType.Buy, ctx);
                break;
            case ActionType.UsePower:
                ApplyUsePower(world, npc, tick, ctx);
                break;
            case ActionType.Steal:
                TheftResolver.Resolve(world, npc, tick, ctx, theftIndex);
                break;
        }
    }

    /// <summary>Executa o PendingPowerInvocation via motor extraordinário e registra
    /// <see cref="WorldEventKind.PowerInvoked"/> com proveniência.</summary>
    private static void ApplyUsePower(WorldState world, Npc npc, long tick, TickContext ctx)
    {
        var pending = npc.PendingPowerInvocation;
        npc.PendingPowerInvocation = null;
        if (pending is null) return;

        // Decisão (raiz) + PowerInvoked com CauseEventId apontando pra ela.
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

        // Diária de hospedagem (lodging-and-rent): paga ao fim da noite; sem pagar, dorme no chão onde está.
        if (rest.Kind == RestPlaceKind.Lodging
            && !(GuestLodging.FindRoom(world, npc, world.FindHousehold(npc.Household!.Value)!.Location) is { } room && GuestLodging.TryCharge(npc, room)))
        {
            npc.SetSleep((int)(100 * world.RestPlaceCatalog.GroundEfficiency), tick);
            return;
        }

        npc.SetSleep((int)(100 * rest.RecoveryEfficiency), tick);
    }

    /// <summary>Falha local tenta alternativa (Buy↔Eat/estoque) antes de
    /// <see cref="IntentStatus.Invalidated"/>.</summary>
    private static void ApplyFoodPlan(
        WorldState world, Npc npc, MarketIndex marketIndex, long tick, ActionType primary, TickContext ctx) =>
        ResolveFoodPlan(world, npc, marketIndex, tick, primary, ctx);

    /// <summary>Resolve Buy/Eat com alternativas; retorna o status final do Intent quando
    /// um plano alimentar estava em curso (para testes).</summary>
    internal static IntentStatus? ResolveFoodPlan(
        WorldState world, Npc npc, MarketIndex marketIndex, long tick, ActionType primary, TickContext? ctx = null)
    {
        EnsureFoodIntent(npc, primary, tick);

        bool primaryOk = primary == ActionType.Buy
            ? TryApplyBuy(world, npc, marketIndex, ctx)
            : TryApplyEat(world, npc, tick);

        if (primaryOk)
        {
            if (npc.IntentStatus == IntentStatus.Active)
                npc.CompleteIntent();
            return npc.IntentStatus;
        }

        bool alternativeOk = primary == ActionType.Buy
            ? TryApplyEat(world, npc, tick)
            : TryApplyBuy(world, npc, marketIndex, ctx);

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

        // AD-134: o lactente mama; no desmame, parte das refeições já é papa da despensa. Sem leite, só a papa (com risco).
        bool nursed = LivingWorld.Simulation.Health.Lactation.TryNurse(world, npc, tick);
        if (nursed && TakesMilkThisFeed(world, npc, tick))
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
            npc.SetHunger(Math.Max(npc.HungerAt(tick), ObjectUse.ConsumeLevel(world, meal, ObjectEffectTargets.Hunger)), tick);
            world.RecordResourceConsumed(meal, 1);
            fed = true;
        }

        var water = new ResourceType(econ.WaterResourceId);
        if (household?.Withdraw(water, 1).IsSuccess == true || TryRation(world, npc, water))
        {
            npc.SetThirst(Math.Max(npc.ThirstAt(tick), ObjectUse.ConsumeLevel(world, water, ObjectEffectTargets.Thirst)), tick);
            world.RecordResourceConsumed(water, 1);
            fed = true;
        }

        if (nursed && !fed)
        {
            // sem papa na despensa, o peito ainda cobre a refeição
            npc.SetHunger(100, tick);
            npc.SetThirst(100, tick);
            fed = true;
        }

        return fed;
    }

    /// <summary>No exclusivo toda mamada alimenta; no desmame, a parte do leite cai com a idade (sorteio por bebê e tick).</summary>
    private static bool TakesMilkThisFeed(WorldState world, Npc baby, long tick)
    {
        double share = LivingWorld.Simulation.Health.Lactation.MilkShare(world, baby, tick);
        if (share >= 1.0) return true;
        if (share <= 0.0) return false;
        return world.Rng.StreamFor("weaning", unchecked(baby.Id.Value * 1_000_003L + tick)).NextDouble() < share;
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

    private static bool TryApplyBuy(WorldState world, Npc npc, MarketIndex marketIndex, TickContext? ctx)
    {
        if (!world.EconomyRules.Enabled) return false;
        var market = NearestEssentialsMarket(world, npc, marketIndex);
        if (market is null) return false; // vendedor indisponível
        if (market.Location != npc.CurrentLocation) return false; // comprar é estar no mercado, não pedir de longe
        if (npc.Household is not { } householdId || world.FindHousehold(householdId) is not { } household)
            return false;

        var econ = world.EconomyRules;
        bool bought = false;
        // AD-143: com água entre a casa e o mercado, leva-se só o que cabe (a pé ou na peça da casa); e quem precisa de com que
        // atravessar olha o que está exposto
        long cargo = LivingWorld.Simulation.Invention.InventionMarket.CargoGrams(world, household, market.Location);
        if (world.InventionOn) LivingWorld.Simulation.Invention.InventionMarket.AtMarket(world, ctx ?? new TickContext(world, world.Rng, world.Scheduler), npc, household, market);
        foreach (var resourceId in new[] { econ.FoodResourceId, econ.WaterResourceId })
        {
            var resource = new ResourceType(resourceId);
            if (!market.Prices.TryGetValue(resource, out var unitPrice) || unitPrice <= 0) continue;

            // Água é bem comum: quem não tem uma moeda para pagar ainda enche o balde no poço da aldeia (a oficina que a
            // tira da terra), em vez de morrer de sede ao lado dela. Comida continua paga.
            if (resourceId == econ.WaterResourceId && npc.Wallet.Amount < unitPrice)
            {
                bought |= DrawFromWell(world, market, household, resource, ref cargo);
                continue;
            }

            // Compra de domicílio, como uma ida real ao mercado: cobre ~4 dias de cada membro
            // (mínimo 10) e, com o mercado longe de casa, a ida e a volta; limitada ao que a carteira paga.
            int purchaseDays = PurchaseDaysFor(
                Mobility.HouseholdTicks(world, household, household.Location, market.Location),
                world.CurrentDate.Calendar.HoursPerDay);
            // AD-138: quem passou fome compra para mais dias
            purchaseDays = (int)Math.Ceiling(purchaseDays * LivingWorld.Simulation.Cognition.PsycheMechanic.HoardFactor(world, npc, world.CurrentDate.TotalHours));
            // AD-145: compra o que falta para a meta, não a meta inteira por cima do que já há. Antes cada ida ao mercado por
            // comida trazia a água da meta de novo, e a casa ao lado do rio (que já bebe de graça) juntava milhares de água.
            long quantity = Math.Max(MinimumPurchaseQuantity, household.Members.Count * purchaseDays);
            if (world.FamilyRules.Pressure?.TopUpPurchases == true) quantity -= household.Stock.GetValueOrDefault(resource);
            if (quantity <= 0) continue;
            quantity = Math.Max(1, Math.Min(quantity, npc.Wallet.Amount / unitPrice));
            quantity = LivingWorld.Simulation.Invention.InventionMarket.Fit(world, resourceId, quantity, ref cargo);
            if (quantity <= 0) continue;
            var tx = new TransactionContext(
                npc.Wallet, market.Treasury, market.Stock.GetValueOrDefault(resource),
                household.Stock.GetValueOrDefault(resource), resource, unitPrice, quantity);

            if (!MarketTransaction.Execute(tx).IsSuccess) continue;

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

    /// <summary>Dias de despensa que uma compra cobre: o mínimo de sempre, ou a meta mais a ida e a volta (e uma folga) quando
    /// o mercado fica longe de casa, limitado a <see cref="MaxProvisionDays"/>.</summary>
    internal static int PurchaseDaysFor(long homeTicks, int hoursPerDay)
    {
        double tripDays = 2.0 * homeTicks / hoursPerDay;
        return (int)Math.Min(MaxProvisionDays, Math.Max(PurchaseDaysOfSupply, Math.Ceiling(ProvisionTargetDays + tripDays) + ProvisionSlackDays));
    }

    /// <summary>Poço da aldeia: sem dinheiro, a casa leva água para uns dias de graça, só o que o estoque da oficina tem, e só de
    /// uma oficina cuja receita produz água (um mercado qualquer não dá água). O estoque muda de lugar (oficina para despensa),
    /// nada some nem nasce, e a retirada entra no livro de trocas com valor 0 (não é venda, mas é movimento).</summary>
    private static bool DrawFromWell(
        WorldState world, Workplace well, LivingWorld.Domain.Population.Family.Household household, ResourceType water, ref long cargo)
    {
        if (!world.EconomyCatalog.Recipes.TryGetValue(well.LocationType.Id, out var recipe) || !recipe.Outputs.ContainsKey(water.Id))
            return false;

        // só enche o que falta: casa com a água dos próximos dias não esvazia o estoque do dono do poço
        long wanted = Math.Max(WellMinimumDraw, household.Members.Count * WellDaysOfSupply) - household.Stock.GetValueOrDefault(water);
        long quantity = Math.Min(well.Stock.GetValueOrDefault(water), wanted);
        if (quantity > 0) quantity = LivingWorld.Simulation.Invention.InventionMarket.Fit(world, water.Id, quantity, ref cargo);
        if (quantity <= 0 || !well.Withdraw(water, quantity).IsSuccess) return false;

        household.Deposit(water, quantity);
        world.RecordTrade(well.Id, water, world.CurrentDate.TotalHours / world.CurrentDate.Calendar.HoursPerDay, quantity, 0);
        return true;
    }

    /// <summary>Conversar só mata a vontade de convívio com alguém (AD-138): sem ninguém por perto, a necessidade social continua
    /// e a solidão sobe (pesa no humor e na esperança de achar alguém da próxima vez); com alguém, ela se alivia. Mundo sem regras
    /// de vínculo (ninguém é avaliado) ou com a regra desligada mantém o efeito antigo.</summary>
    internal static void ApplySocialize(WorldState world, Npc npc, long tick, TickContext ctx)
    {
        var psyche = world.NeedsRules.Psyche;
        bool? company = SocializeWithSomeoneNearby(world, npc, tick, ctx);
        if (company != false || !psyche.SocializeNeedsCompany)
            npc.SetSocial(100, tick);
        if (!psyche.Enabled || company is null) return;
        if (company == true)
        {
            if (npc.Psyche is { Loneliness: > 0 } p && tick < p.CalmFromTick)
                LivingWorld.Simulation.Cognition.PsycheMechanic.Feel(world, npc, tick, default, loneliness: -psyche.LonelinessReliefPerContact);
        }
        else
            LivingWorld.Simulation.Cognition.PsycheMechanic.Feel(world, npc, tick, default, loneliness: psyche.LonelinessPerSolitude);
    }

    /// <summary>Quem socializa conversa com alguém que está por perto (AD-128): entre os vizinhos, o de quem mais gosta ou com
    /// quem mais combina. É assim que gente de oficinas e casas diferentes passa a se conhecer. Ninguém por perto: nada.</summary>
    private static bool? SocializeWithSomeoneNearby(WorldState world, Npc npc, long tick, TickContext ctx)
    {
        if (world.FamilyRules.Bonds is not { } bonds) return null;

        ulong salt = (ulong)(ctx.StreamFor("socialize", unchecked(npc.Id.Value * 1_000_003L + tick)).NextDouble() * (1UL << 53));
        int radius = bonds.SocializeRadius;

        // Desconhecidos: uma amostra pequena de quem está por perto, escolhida pelo menor hash (não depende da ordem dos
        // baldes do índice espacial, que muda ao reidratar). Numa multidão ninguém avalia a multidão inteira.
        Span<long> sampleIds = stackalloc long[StrangersConsidered];
        Span<ulong> sampleKeys = stackalloc ulong[StrangersConsidered];
        int sampled = 0;
        for (int dy = -radius; dy <= radius; dy++)
            for (int dx = -radius; dx <= radius; dx++)
            {
                var bucket = world.AliveNpcIndex.At(new CellCoord(npc.CurrentLocation.X + dx, npc.CurrentLocation.Y + dy));
                for (int i = 0; i < bucket.Count; i++)
                {
                    var other = bucket[i];
                    if (other.Id == npc.Id || other.City != npc.City) continue;
                    ulong key = LivingWorld.Domain.Cities.Spatial.StableHash.Mix(other.Id.Value ^ (long)salt);
                    if (sampled < StrangersConsidered)
                    {
                        sampleIds[sampled] = other.Id.Value;
                        sampleKeys[sampled++] = key;
                        continue;
                    }

                    int worst = 0;
                    for (int k = 1; k < sampled; k++)
                        if (sampleKeys[k] > sampleKeys[worst] || (sampleKeys[k] == sampleKeys[worst] && sampleIds[k] > sampleIds[worst])) worst = k;
                    if (key < sampleKeys[worst] || (key == sampleKeys[worst] && other.Id.Value < sampleIds[worst]))
                    {
                        sampleIds[worst] = other.Id.Value;
                        sampleKeys[worst] = key;
                    }
                }
            }

        Npc? best = null;
        double bestAppeal = double.NegativeInfinity;
        var leaning = LivingWorld.Simulation.Cognition.SocialLeaning.Of(world, npc, tick);
        void Consider(Npc other, double affection)
        {
            double chance = (LivingWorld.Domain.Cities.Spatial.StableHash.Mix(other.Id.Value ^ (long)salt) >> 11) * (1.0 / (1UL << 53));
            double appeal = affection
                + 50.0 * LivingWorld.Domain.Population.Family.SocialBondRules.Likability(npc.Personality, other.Personality)
                + 20.0 * chance
                + leaning.AppealToward(world, npc, other, affection, tick);
            if (appeal > bestAppeal || (appeal == bestAppeal && best is not null && other.Id.Value < best.Id.Value))
            {
                bestAppeal = appeal;
                best = other;
            }
        }

        for (int k = 0; k < sampled; k++)
            if (world.FindNpc(new NpcId(sampleIds[k])) is { } stranger)
                Consider(stranger, world.Relationships.TryGetValue(
                    new LivingWorld.Domain.Population.Family.RelationshipKey(npc.Id, stranger.Id), out var known) ? known.Affection : 0.0);

        // Conhecidos que estão por perto entram sempre: quem tem amigo ao lado conversa com o amigo.
        var targets = world.RelationshipTargetsOf(npc.Id);
        var owned = world.RelationshipsFrom(npc.Id);
        for (int i = 0; i < targets.Count; i++)
            if (world.FindNpc(targets[i]) is { IsAlive: true } friend && friend.City == npc.City
                && Math.Abs(friend.CurrentLocation.X - npc.CurrentLocation.X) <= radius
                && Math.Abs(friend.CurrentLocation.Y - npc.CurrentLocation.Y) <= radius)
                Consider(friend, owned![i].Affection);

        if (best is null) return false;
        var type = LivingWorld.Domain.Population.Family.RelationshipEventType.Conversation;
        LivingWorld.Simulation.Population.Family.RelationshipSystem.Bond(world, world.FamilyRules, bonds, tick, npc, best, sameHousehold: false, type);
        LivingWorld.Simulation.Population.Family.RelationshipSystem.Bond(world, world.FamilyRules, bonds, tick, best, npc, sameHousehold: false, type);
        return true;
    }

    private const int StrangersConsidered = 8;

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
