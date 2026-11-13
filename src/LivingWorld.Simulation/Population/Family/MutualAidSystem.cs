using LivingWorld.Domain.Geography;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Population.Family;

/// <summary>Ajuda mútua, <c>Daily</c>: parentes e gente de afeto socorrem a casa que passa fome sem ter com o
/// que comprar. Quem ajuda é decidido pelo caráter — a probabilidade diária cresce com o altruísmo e com a
/// proximidade (parente > afeto), e só se dá o que passa da própria reserva, uma fração maior para os mais
/// generosos. Egoístas deixam parentes passarem necessidade; generosos se empobrecem pelos seus.
/// O dinheiro só muda de carteira (nada é cunhado) e cada ajuda grava um evento com a origem.
/// Consumidor causal: a carteira recebida vira compra de comida pela decisão de abastecimento.</summary>
[LivingCapability("RELATIONSHIPS")]
[RunsAfter(typeof(RelationshipSystem))]
public sealed class MutualAidSystem : ISimulationSystem
{
    public const string SystemName = "population-mutual-aid";

    /// <summary>Dias de despensa abaixo dos quais a casa é considerada em necessidade.</summary>
    private const int NeedyStockDays = 1;
    /// <summary>Dias de compra que a casa precisa ter em carteira para não ser socorrida.</summary>
    private const int NeedyWalletDays = 4;
    /// <summary>Dias de compra que a ajuda tenta devolver à casa (uma semana).</summary>
    private const int RelieveDays = 7;
    /// <summary>Dias de gasto próprio que o ajudante preserva antes de dar qualquer coisa.</summary>
    private const int HelperReserveDays = 14;
    /// <summary>Afeto mínimo (0–100) para quem não é parente se sentir chamado a ajudar.</summary>
    private const double MinimumAffection = 50;
    private const int MaxCandidatesPerHousehold = 40;

    public string Name => SystemName;
    public TickFrequency Frequency => TickFrequency.Daily;

    public void Tick(WorldState world, TickContext ctx)
    {
        var economy = world.EconomyRules;
        if (!economy.Enabled) return;

        long unitPrice = Math.Max(1, economy.PriceFloor.GetValueOrDefault(economy.FoodResourceId, 1));
        long dailyCostPerPerson = 2 * unitPrice; // comida + água, uma unidade de cada por dia
        var food = new ResourceType(economy.FoodResourceId);
        var water = new ResourceType(economy.WaterResourceId);
        long day = ctx.CurrentTick / world.CurrentDate.Calendar.HoursPerDay;
        _alms.Clear();

        foreach (var household in world.Households.OrderBy(h => h.Id.Value))
        {
            var members = household.Members
                .Select(id => world.FindNpc(id))
                .Where(npc => npc is { IsAlive: true })
                .Select(npc => npc!)
                .ToList();
            if (members.Count == 0) continue;

            long count = members.Count;
            bool scarce = household.Stock.GetValueOrDefault(food) < count * NeedyStockDays
                          || household.Stock.GetValueOrDefault(water) < count * NeedyStockDays;
            if (!scarce) continue;

            // Quem faz as compras da casa: o chefe se for adulto ou idoso, senão o primeiro adulto ou idoso, senão o chefe.
            var head = members.FirstOrDefault(m => m.Id == household.Head) ?? members[0];
            var recipient = IsGrown(world, head) ? head : members.FirstOrDefault(m => IsGrown(world, m)) ?? head;
            ShareWithinHousehold(world, members, recipient, count * dailyCostPerPerson * RelieveDays, dailyCostPerPerson);

            long wallets = members.Sum(m => m.Wallet.Amount);
            if (wallets >= count * dailyCostPerPerson * NeedyWalletDays) continue;

            long deficit = count * dailyCostPerPerson * RelieveDays - wallets;
            RelieveHousehold(world, ctx, household, members, recipient, deficit, dailyCostPerPerson, unitPrice, day, food);
        }

        GiveRationedAlms(world, ctx);
    }

    /// <summary>Pedidos de esmola do dia (quem recebe, quanto falta, cidade que dá), em ordem de casa. Só vive dentro do tick.</summary>
    private readonly List<(Npc Recipient, long Deficit, LivingWorld.Domain.Cities.City City)> _alms = [];

    /// <summary>Quem dá a esmola: a cidade da casa se tiver caixa; senão a cidade do mercado mais próximo, que é onde a casa
    /// compra. Povoado recém-fundado nasce sem tesouro e continua na órbita da vila de onde saiu.</summary>
    private static LivingWorld.Domain.Cities.City? AlmsGiver(WorldState world, Household household)
    {
        if (world.FindCity(household.City) is { } own && own.Treasury.Amount > 0) return own;

        LivingWorld.Domain.Cities.City? best = null;
        long bestDistance = long.MaxValue, bestId = long.MaxValue;
        foreach (var market in world.Workplaces)
        {
            if (market.Prices.Count == 0 || world.FindCity(market.City) is not { } city || city.Treasury.Amount <= 0) continue;
            long distance = Math.Abs(market.Location.X - household.Location.X) + Math.Abs(market.Location.Y - household.Location.Y);
            if (distance < bestDistance || (distance == bestDistance && market.Id.Value < bestId))
            {
                bestDistance = distance;
                bestId = market.Id.Value;
                best = city;
            }
        }
        return best;
    }

    private static bool IsGrown(WorldState world, Npc npc) =>
        world.LifeStageRules.LifeStageOf(npc.AgeYears(world.CurrentDate)) != LivingWorld.Domain.Population.Body.LifeStage.Child;

    /// <summary>Com a despensa vazia, o dinheiro da casa vai para quem compra (AD-128): cada morador passa o que excede a
    /// própria reserva de poucos dias (a criança passa tudo) até cobrir a semana da casa. Antes cada carteira era só de
    /// quem a tinha, e um adulto morria de fome ao lado de um filho com moedas que não sabia gastar.</summary>
    private static void ShareWithinHousehold(
        WorldState world, List<Npc> members, Npc recipient, long target, long dailyCostPerPerson)
    {
        foreach (var donor in members.OrderBy(m => m.Id.Value))
        {
            long missing = target - recipient.Wallet.Amount;
            if (missing <= 0) return;
            if (donor.Id == recipient.Id) continue;

            long keep = IsGrown(world, donor) ? dailyCostPerPerson * NeedyWalletDays : 0;
            long give = Math.Min(missing, donor.Wallet.Amount - keep);
            if (give <= 0 || !donor.TryDebitWallet(new Money(give)).IsSuccess) continue;
            recipient.CreditWallet(new Money(give));
        }
    }

    private void RelieveHousehold(
        WorldState world, TickContext ctx, Household household, List<Npc> members, Npc recipient,
        long deficit, long dailyCostPerPerson, long unitPrice, long day, ResourceType food)
    {
        bool kinAid = world.FamilyRules.Pressure?.KinFoodAid == true;
        var memberIds = members.Select(m => m.Id).ToHashSet();
        var candidates = new SortedSet<NpcId>(Comparer<NpcId>.Create((a, b) => a.Value.CompareTo(b.Value)));
        foreach (var member in members)
        {
            if (member.MotherId is { } mother) { candidates.Add(mother); if (kinAid) AddParentsOf(world, mother, candidates); }
            if (member.FatherId is { } father) { candidates.Add(father); if (kinAid) AddParentsOf(world, father, candidates); }
            if (member.Spouse is { } spouse) candidates.Add(spouse);
            foreach (var contact in world.RelationshipTargetsOf(member.Id))
                candidates.Add(contact);
        }

        // AD-145: a comida que falta para a semana; a família de origem manda da própria despensa o que passa da reserva dela
        long foodNeed = Math.Max(0, members.Count * (long)RelieveDays - household.Stock.GetValueOrDefault(food));
        var fedFrom = new HashSet<HouseholdId>();
        int considered = 0;
        foreach (var helperId in candidates)
        {
            if (deficit <= 0 || considered >= MaxCandidatesPerHousehold) break;
            if (memberIds.Contains(helperId)) continue;
            if (world.FindNpc(helperId) is not { IsAlive: true } helper) continue;
            var stage = world.LifeStageRules.LifeStageOf(helper.AgeYears(world.CurrentDate));
            bool kin = kinAid ? IsKinOf(world, helper, members) : ClosenessTo(world, helper, members) >= 1.0;
            // parente idoso (avô, avó) também socorre a casa dos seus; quem não é parente, só o adulto
            if (stage == LivingWorld.Domain.Population.Body.LifeStage.Child
                || (stage == LivingWorld.Domain.Population.Body.LifeStage.Elder && !(kin && kinAid)))
                continue;

            double closeness = kin ? 1.0 : ClosenessTo(world, helper, members);
            if (closeness <= 0) continue;
            considered++;

            long surplus = helper.Wallet.Amount - HelperReserve(world, helper, dailyCostPerPerson);
            long spareFood = kinAid && kin && foodNeed > 0 ? SpareFood(world, helper, household, fedFrom, food) : 0;
            if (surplus < dailyCostPerPerson && spareFood <= 0) continue;

            double altruism = helper.Personality.Altruism / 100.0;
            double chance = altruism * (0.4 + 0.6 * closeness);
            // AD-138: quem foi ajudado retribui mais (a esta casa, sobretudo) — a lembrança da ajuda é o consumidor
            chance *= 1 + world.NeedsRules.Psyche.ReciprocityGain
                * LivingWorld.Simulation.Cognition.PsycheMechanic.Gratitude(world, helper, members, ctx.CurrentTick);
            // Uma rolagem por (ajudante, dia): o mesmo fluxo atende, em ordem de id, as casas que precisam.
            var rng = ctx.StreamFor("mutual-aid", helper.Id.Value * 1_000_003L + day);
            if (rng.NextDouble() >= chance) continue;

            if (spareFood > 0 && world.FindHousehold(helper.Household!.Value) is { } origin)
            {
                long sent = Math.Min(foodNeed, (long)Math.Ceiling(spareFood * (0.3 + 0.5 * altruism)));
                if (sent > 0 && origin.Withdraw(food, sent).IsSuccess)
                {
                    household.Deposit(food, sent);
                    fedFrom.Add(origin.Id);
                    foodNeed -= sent;
                    deficit -= sent * unitPrice; // a comida que chegou é a que não precisa ser comprada
                    Thank(world, ctx, recipient, helper);
                    ctx.LogEvent(WorldEventKind.AidGiven, $"{helper.Id.Value}|{recipient.Id.Value}|{sent}|food", sourceSystem: SystemName);
                }
                if (deficit <= 0) break;
            }

            if (surplus < dailyCostPerPerson) continue;
            long give = Math.Min(deficit, (long)Math.Floor(surplus * (0.15 + 0.35 * altruism)));
            if (give <= 0) continue;
            if (!helper.TryDebitWallet(new Money(give)).IsSuccess) continue;

            recipient.CreditWallet(new Money(give));
            deficit -= give;
            Thank(world, ctx, recipient, helper);
            ctx.LogEvent(WorldEventKind.AidGiven, $"{helper.Id.Value}|{recipient.Id.Value}|{give}", sourceSystem: SystemName);
        }

        if (deficit <= 0 || members.Sum(m => m.Wallet.Amount) >= members.Count * dailyCostPerPerson) return;
        if (AlmsGiver(world, household) is not { } city) return;
        if (world.FamilyRules.Pressure?.RationedAlms == true) _alms.Add((recipient, deficit, city));
        else GiveAlms(world, ctx, city, recipient, deficit);
    }

    private static void Thank(WorldState world, TickContext ctx, Npc recipient, Npc helper)
    {
        var gratitude = world.GetOrCreateRelationship(new RelationshipKey(recipient.Id, helper.Id), ctx.CurrentTick);
        gratitude.ApplyEvent(RelationshipEventType.Help, world.FamilyRules);
        gratitude.MarkContact(ctx.CurrentTick);
    }

    /// <summary>Pais de <paramref name="parentId"/> (os avós de quem pede) entram na lista de quem pode socorrer (AD-145).</summary>
    private static void AddParentsOf(WorldState world, NpcId parentId, SortedSet<NpcId> into)
    {
        if (world.FindNpc(parentId) is not { } parent) return;
        if (parent.MotherId is { } m) into.Add(m);
        if (parent.FatherId is { } f) into.Add(f);
    }

    /// <summary>Comida que a casa do parente pode mandar hoje (AD-145): o que passa de <see cref="HelperReserveDays"/> dias dos
    /// moradores dela. Uma remessa por casa de origem por dia; a própria casa necessitada não conta.</summary>
    private static long SpareFood(WorldState world, Npc helper, Household needy, HashSet<HouseholdId> fedFrom, ResourceType food)
    {
        if (helper.Household is not { } id || id == needy.Id || fedFrom.Contains(id) || world.FindHousehold(id) is not { } origin) return 0;
        return origin.Stock.GetValueOrDefault(food) - Math.Max(1, origin.Members.Count) * (long)HelperReserveDays;
    }

    /// <summary>Esmola da cidade (AD-128), racionada (AD-145): a casa que continua sem um dia de compra na carteira depois de
    /// parentes e amigos pede ao tesouro o que falta para a semana. O tesouro só se enche na virada do mês (imposto e dízimo), então
    /// a cidade reparte o caixa pelos dias que faltam até lá: cada pedido do dia leva a mesma fração do que falta, até a cota do
    /// dia. Antes a primeira casa a pedir levava a semana inteira e o tesouro secava no meio do mês. A moeda só muda de dono;
    /// tesouro vazio, não há esmola.</summary>
    /// <summary>Esmola de antes do AD-145: a casa leva do tesouro o que falta para a semana, até o caixa acabar.</summary>
    private static void GiveAlms(WorldState world, TickContext ctx, LivingWorld.Domain.Cities.City city, Npc recipient, long deficit)
    {
        long give = Math.Min(deficit, city.Treasury.Amount);
        if (give <= 0 || !city.TryDebitTreasury(new Money(give)).IsSuccess) return;
        recipient.CreditWallet(new Money(give));
        world.RecordTreasury(city.Id, ctx.CurrentTick, revenue: 0, spending: give);
        ctx.LogEvent(WorldEventKind.AidGiven, $"city-{city.Id.Value}|{recipient.Id.Value}|{give}", sourceSystem: SystemName);
    }

    private void GiveRationedAlms(WorldState world, TickContext ctx)
    {
        if (_alms.Count == 0) return;
        long daysLeft = Math.Max(1, world.CurrentDate.Calendar.DaysPerMonth - world.CurrentDate.Day);
        foreach (var city in _alms.Select(a => a.City).DistinctBy(c => c.Id).OrderBy(c => c.Id.Value).ToList())
        {
            long asked = 0;
            foreach (var request in _alms)
                if (request.City.Id == city.Id) asked += request.Deficit;
            long budget = Math.Min(asked, (city.Treasury.Amount + daysLeft - 1) / daysLeft);
            if (budget <= 0) continue;
            foreach (var (recipient, deficit, giver) in _alms)
            {
                if (giver.Id != city.Id) continue;
                long give = Math.Min(city.Treasury.Amount, (long)Math.Ceiling(deficit * (double)budget / asked));
                if (give <= 0 || !city.TryDebitTreasury(new Money(give)).IsSuccess) continue;
                recipient.CreditWallet(new Money(give));
                world.RecordTreasury(city.Id, ctx.CurrentTick, revenue: 0, spending: give);
                ctx.LogEvent(WorldEventKind.AidGiven, $"city-{city.Id.Value}|{recipient.Id.Value}|{give}", sourceSystem: SystemName);
            }
        }
        _alms.Clear();
    }

    /// <summary>1 para parente; senão afeto (0–1) a partir do mínimo; 0 se não há laço que chame à ajuda.</summary>
    private static double ClosenessTo(WorldState world, Npc helper, List<Npc> members)
    {
        double best = 0;
        foreach (var member in members)
        {
            if (IsKin(helper, member)) return 1.0;
            if (world.Relationships.TryGetValue(new RelationshipKey(helper.Id, member.Id), out var relation)
                && relation.Affection >= MinimumAffection)
                best = Math.Max(best, relation.Affection / 100.0);
        }
        return best;
    }

    /// <summary>Parente de algum morador: pais, filhos, cônjuge, irmãos e, pela genealogia, avós e netos (AD-145).</summary>
    private static bool IsKinOf(WorldState world, Npc helper, List<Npc> members)
    {
        foreach (var member in members)
            if (IsKin(helper, member) || IsGrandparent(world, helper, member) || IsGrandparent(world, member, helper)) return true;
        return false;
    }

    private static bool IsGrandparent(WorldState world, Npc elder, Npc young)
    {
        foreach (var parentId in new[] { young.MotherId, young.FatherId })
            if (parentId is { } id && world.FindNpc(id) is { } parent && (parent.MotherId == elder.Id || parent.FatherId == elder.Id))
                return true;
        return false;
    }

    private static bool IsKin(Npc a, Npc b) =>
        a.MotherId == b.Id || a.FatherId == b.Id || b.MotherId == a.Id || b.FatherId == a.Id
        || a.Spouse == b.Id || b.Spouse == a.Id
        || (a.MotherId is { } motherA && motherA == b.MotherId)
        || (a.FatherId is { } fatherA && fatherA == b.FatherId);

    private static long HelperReserve(WorldState world, Npc helper, long dailyCostPerPerson)
    {
        long dependants = helper.Household is { } id && world.FindHousehold(id) is { } own
            ? Math.Max(1, own.Members.Count)
            : 1;
        return dependants * dailyCostPerPerson * HelperReserveDays;
    }
}
