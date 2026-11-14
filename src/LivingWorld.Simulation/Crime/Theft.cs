using LivingWorld.Domain.Crime;
using LivingWorld.Domain.Economy;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Crime;

/// <summary>Por que alguém furta (AD-150).</summary>
public enum TheftMotive : byte
{
    Hunger = 1,
    Poverty = 2,
    Opportunity = 3,
}

internal enum TheftTargetKind : byte
{
    Person = 1,
    House = 2,
    Stall = 3,
    Till = 4,
}

/// <summary>O que um furto pode levar: de quem (pessoa, casa, banca ou caixa de oficina), o quê e quanto, e onde acontece.</summary>
internal readonly record struct TheftTarget(
    TheftTargetKind Kind, Npc? Person, Household? House, Workplace? Shop, int ResourceId, long Units, long Coin, long Value,
    CellCoord Where)
{
    public long VictimId => Kind switch
    {
        TheftTargetKind.Person => Person!.Id.Value,
        TheftTargetKind.House => Person?.Id.Value ?? House!.Head.Value,
        _ => Shop!.Owner?.Value ?? -1,
    };
}

/// <summary>Furto realista (AD-150): escolhe o alvo pelo motivo entre todos os bens que existem (moeda de quem está ao lado ou
/// guardada em casa, comida da despensa, estoque e caixa de oficina e banca), executa (sucesso reduzido pela guarda), move o
/// que foi levado (a moeda e o recurso só mudam de dono: a vítima sente a falta, o ladrão come, usa ou vende a um receptador),
/// registra o furto e entrega o caso à justiça. Ordem de id em toda escolha; sorteio só do RNG semeado.</summary>
internal static class Theft
{
    public const string SourceName = "CrimeSystem";

    public static string MotiveName(TheftMotive motive) => motive switch
    {
        TheftMotive.Hunger => "fome",
        TheftMotive.Poverty => "miseria",
        _ => "oportunidade",
    };

    /// <summary>Quanto a fome aperta (0 no limite do desespero, 1 com fome ou sede em zero); negativo = não está desesperado.</summary>
    public static double NeedOf(TheftRules rules, Npc npc, long now)
    {
        int worst = Math.Min(npc.HungerAt(now), npc.ThirstAt(now));
        if (worst > rules.DesperateBelow) return -1;
        return rules.DesperateBelow <= 0 ? 1.0 : 1.0 - worst / (double)rules.DesperateBelow;
    }

    public static bool HasFoodAtHome(WorldState world, Npc npc)
    {
        if (npc.Household is not { } id || world.FindHousehold(id) is not { } household) return false;
        var economy = world.EconomyRules;
        return household.Stock.GetValueOrDefault(new ResourceType(economy.FoodResourceId)) >= 1
            || household.Stock.GetValueOrDefault(new ResourceType(economy.WaterResourceId)) >= 1;
    }

    /// <summary>Pode comprar comida agora: tem moeda para o preço mais barato de uma banca da cidade que ainda tem comida.</summary>
    public static bool CanBuyFood(WorldState world, Npc npc)
    {
        if (npc.Wallet.Amount < 1) return false;
        var food = new ResourceType(world.EconomyRules.FoodResourceId);
        foreach (var shop in world.Workplaces)
        {
            if (shop.City != npc.City || shop.Stock.GetValueOrDefault(food) < 1) continue;
            if (npc.Wallet.Amount >= Math.Max(1, shop.Prices.GetValueOrDefault(food))) return true;
        }
        return false;
    }

    /// <summary>Medo da punição (0..1) de quem pensa em furtar ou agredir: o exemplo recente na cidade, a própria punição
    /// lembrada e os guardas, o maior dos três, vezes o peso do cenário.</summary>
    public static double Deterrence(WorldState world, Npc npc, long now) => CrimeSystem.DeterrenceOf(world, npc, now);

    public static int GuardsIn(WorldState world, CityId city)
    {
        int guardId = world.TheftRules.GuardProfessionId;
        if (guardId < 0) return 0;
        int guards = 0;
        foreach (var npc in world.AliveNpcIndex.Alive)
            if (npc.City == city && npc.Profession.Id == guardId) guards++;
        return guards;
    }

    /// <summary>Alvo do furto conforme o motivo. Fome: comida da banca ou de outra casa (a mais perto), senão a moeda de quem
    /// está ao lado. Miséria: a moeda (de quem está ao lado ou guardada na casa mais rica e perto), senão comida. Oportunidade:
    /// o que valer mais, descontada a distância (casa, caixa de oficina, estoque de oficina).</summary>
    public static TheftTarget? FindTarget(WorldState world, Npc thief, TheftMotive motive, bool foodOnly = false, WorldRng? rng = null)
    {
        var pocket = Pocket(world, thief);
        if (motive == TheftMotive.Hunger)
        {
            if (thief.Household is not null && (Food(world, thief) is { } food)) return food;
            return foodOnly ? null : pocket;
        }

        if (motive == TheftMotive.Poverty)
            return pocket ?? Purse(world, thief) ?? (thief.Household is not null ? Food(world, thief) : null);

        // oportunidade: o que estiver à mão e valer a pena, na chance do valor descontada a distância (sem sorteio, o maior)
        Span<double> scores = stackalloc double[4];
        var candidates = new[] { pocket, Purse(world, thief), Till(world, thief), Goods(world, thief) };
        double total = 0;
        for (int i = 0; i < candidates.Length; i++)
        {
            scores[i] = candidates[i] is { } c ? c.Value / (1.0 + Distance(thief.CurrentLocation, c.Where) / 20.0) : 0;
            total += scores[i];
        }
        if (total <= 0) return null;
        if (rng is not null)
        {
            double pick = rng.NextDouble() * total;
            for (int i = 0; i < candidates.Length; i++)
                if ((pick -= scores[i]) < 0 && candidates[i] is not null) return candidates[i];
        }
        int best = 0;
        for (int i = 1; i < candidates.Length; i++) if (scores[i] > scores[best]) best = i;
        return candidates[best];
    }

    private static int Distance(CellCoord a, CellCoord b) => Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);

    private static bool SameHome(Npc thief, Npc other) => thief.Household is { } home && other.Household == home;

    private static long PriceOf(WorldState world, CityId city, int resourceId)
    {
        var resource = new ResourceType(resourceId);
        long best = 0;
        foreach (var shop in world.Workplaces)
            if (shop.City == city && shop.Prices.TryGetValue(resource, out long price) && price > best) best = price;
        return Math.Max(1, best);
    }

    /// <summary>A parte da moeda que se leva (a fração do cenário), até o teto do que se acha e carrega num furto.</summary>
    private static long CoinTaken(TheftRules rules, long amount) =>
        Math.Min(rules.MaxCoinTaken, Math.Min(amount, Math.Max(1, (long)(amount * rules.MaxLootFraction))));

    /// <summary>Quem tem mais moeda na mesma célula, fora da própria casa (empate: menor id).</summary>
    private static TheftTarget? Pocket(WorldState world, Npc thief)
    {
        Npc? best = null;
        foreach (var other in world.AliveNpcIndex.At(thief.CurrentLocation))
        {
            if (other.Id == thief.Id || other.Wallet.Amount < 1 || SameHome(thief, other)) continue;
            if (best is null || other.Wallet.Amount > best.Wallet.Amount
                || (other.Wallet.Amount == best.Wallet.Amount && other.Id.Value < best.Id.Value))
                best = other;
        }
        if (best is null) return null;
        long coin = CoinTaken(world.TheftRules, best.Wallet.Amount);
        return new TheftTarget(TheftTargetKind.Person, best, null, null, 0, 0, coin, coin, thief.CurrentLocation);
    }

    /// <summary>Comida: a banca da cidade com comida (menor id), senão a casa com comida mais perto da do ladrão.</summary>
    private static TheftTarget? Food(WorldState world, Npc thief)
    {
        var rules = world.TheftRules;
        int foodId = world.EconomyRules.FoodResourceId;
        var food = new ResourceType(foodId);
        long want = rules.FoodDaysTaken * (long)Math.Max(1, HomeSize(world, thief));
        long price = PriceOf(world, thief.City, foodId);
        Workplace? stall = null;
        foreach (var shop in world.Workplaces)
            if (shop.City == thief.City && shop.Stock.GetValueOrDefault(food) >= 1 && (stall is null || shop.Id.Value < stall.Id.Value))
                stall = shop;
        if (stall is not null)
        {
            long units = Math.Min(want, stall.Stock.GetValueOrDefault(food));
            return new TheftTarget(TheftTargetKind.Stall, null, null, stall, foodId, units, 0, units * price, stall.Location);
        }

        var origin = HomeCell(world, thief);
        Household? house = null;
        int bestDistance = int.MaxValue;
        foreach (var h in world.Households)
        {
            if (h.City != thief.City || h.Id == thief.Household || h.IsEmpty || h.Stock.GetValueOrDefault(food) < 1) continue;
            int d = Distance(origin, h.Location);
            if (d < bestDistance || (d == bestDistance && h.Id.Value < house!.Id.Value)) { house = h; bestDistance = d; }
        }
        if (house is null) return null;
        long taken = Math.Min(want, house.Stock.GetValueOrDefault(food));
        return new TheftTarget(TheftTargetKind.House, world.FindNpc(house.Head), house, null, foodId, taken, 0, taken * price, house.Location);
    }

    /// <summary>A moeda guardada em casa: a casa (de outra família) cujo membro mais rico tem mais moeda, descontada a distância.</summary>
    private static TheftTarget? Purse(WorldState world, Npc thief)
    {
        var origin = HomeCell(world, thief);
        Household? house = null;
        Npc? keeper = null;
        double bestScore = 0;
        foreach (var h in world.Households)
        {
            if (h.City != thief.City || h.Id == thief.Household || h.IsEmpty) continue;
            Npc? richest = null;
            foreach (var memberId in h.Members)
                if (world.FindNpc(memberId) is { IsAlive: true } m && m.Wallet.Amount >= 1
                    && (richest is null || m.Wallet.Amount > richest.Wallet.Amount
                        || (m.Wallet.Amount == richest.Wallet.Amount && m.Id.Value < richest.Id.Value)))
                    richest = m;
            if (richest is null) continue;
            double score = richest.Wallet.Amount / (1.0 + Distance(origin, h.Location) / 20.0);
            if (score > bestScore || (score == bestScore && house is not null && h.Id.Value < house.Id.Value))
            {
                house = h; keeper = richest; bestScore = score;
            }
        }
        if (house is null || keeper is null) return null;
        long coin = CoinTaken(world.TheftRules, keeper.Wallet.Amount);
        return new TheftTarget(TheftTargetKind.House, keeper, house, null, 0, 0, coin, coin, house.Location);
    }

    /// <summary>A caixa de uma oficina da cidade (a mais cheia): leva-se o que está à mão, até o teto do cenário.</summary>
    private static TheftTarget? Till(WorldState world, Npc thief)
    {
        Workplace? best = null;
        foreach (var shop in world.Workplaces)
            if (shop.City == thief.City && shop.Treasury.Amount >= 1 && shop.Owner != thief.Id
                && !shop.Employees.Contains(thief.Id)
                && (best is null || shop.Treasury.Amount > best.Treasury.Amount))
                best = shop;
        if (best is null) return null;
        long coin = CoinTaken(world.TheftRules, best.Treasury.Amount);
        return new TheftTarget(TheftTargetKind.Till, null, null, best, 0, 0, coin, coin, best.Location);
    }

    /// <summary>Estoque de oficina que não é comida nem água (ferramenta, ferro): o de maior valor à mão.</summary>
    private static TheftTarget? Goods(WorldState world, Npc thief)
    {
        var economy = world.EconomyRules;
        TheftTarget? best = null;
        foreach (var shop in world.Workplaces)
        {
            if (shop.City != thief.City || shop.Owner == thief.Id || shop.Employees.Contains(thief.Id)) continue;
            foreach (var (resource, stock) in shop.Stock.OrderBy(p => p.Key.Id))
            {
                if (stock < 1 || resource.Id == economy.FoodResourceId || resource.Id == economy.WaterResourceId) continue;
                long units = Math.Min(stock, world.TheftRules.GoodsUnitsTaken);
                long value = units * PriceOf(world, thief.City, resource.Id);
                if (best is null || value > best.Value.Value)
                    best = new TheftTarget(TheftTargetKind.Stall, null, null, shop, resource.Id, units, 0, value, shop.Location);
            }
        }
        return best;
    }

    private static int HomeSize(WorldState world, Npc npc) =>
        npc.Household is { } id && world.FindHousehold(id) is { } h ? h.Members.Count : 1;

    private static CellCoord HomeCell(WorldState world, Npc npc) =>
        npc.Household is { } id && world.FindHousehold(id) is { } h ? h.Location : npc.CurrentLocation;

    /// <summary>Executa o furto: sucesso (reduzido pela guarda), testemunhas, o que muda de mão, o evento e o caso.</summary>
    public static void Commit(WorldState world, TickContext ctx, Npc thief, TheftMotive motive, TheftTarget target, WorldRng rng)
    {
        var rules = world.TheftRules;
        long tick = ctx.CurrentTick;
        double success = rules.BaseSuccess * Math.Max(0.0, 1.0 - GuardsIn(world, thief.City) * rules.GuardEffect);
        bool took = rng.NextDouble() < success;
        long qty = 0, value = 0;
        if (took) (qty, value) = Move(world, ctx, thief, target);
        if (qty <= 0) took = false;
        if (!took) value = target.Value;

        var victim = target.Kind is TheftTargetKind.Person or TheftTargetKind.House ? target.Person : null;
        string good = GoodName(world, target);
        long caseId = -1;
        int seers = 0;
        long accused = -1;
        if (world.CrimeOn && world.CrimeRules!.Justice.Enabled)
        {
            var outcome = Justice.OnCrime(world, ctx, CrimeKind.Theft, thief, victim, target.VictimId, target.Where, value, good,
                MotiveName(motive), caught: !took, rng);
            caseId = outcome.CaseId; seers = outcome.Seers; accused = outcome.Accused;
        }
        else if (!took || rng.NextDouble() < rules.DiscoveryChance)
        {
            accused = thief.Id.Value; seers = 1;
        }

        ctx.LogEvent(WorldEventKind.TheftCommitted,
            $"{thief.Id.Value}|{target.VictimId}|{KindName(target.Kind)}|{MotiveName(motive)}|{good}|{(took ? qty : 0)}|{value}|{(took ? "levou" : "falhou")}|{seers}|{caseId}",
            SourceName);
        if (victim is not null && accused >= 0)
            Discovered(world, ctx, world.FindNpc(new NpcId(accused)), victim, took ? target.Coin : 0, took);

        if (!took && victim is { IsAlive: true } && world.CrimeOn && world.CrimeRules!.Violence.Enabled
            && victim.CurrentLocation == target.Where && rng.NextDouble() < rules.CaughtFightChance)
            Violence.Fight(world, ctx, victim, thief, "flagrante", rng); // pego no ato: a vítima reage
    }

    /// <summary>A vítima soube (ou acredita saber) quem foi: perde confiança nele; quem confiava sente a traição (mágoa).</summary>
    public static void Discovered(WorldState world, TickContext ctx, Npc? accused, Npc victim, long coin, bool took)
    {
        if (accused is null || accused.Id == victim.Id) return;
        long tick = ctx.CurrentTick;
        bool known = world.Relationships.TryGetValue(new RelationshipKey(victim.Id, accused.Id), out var before);
        double trustBefore = known ? before!.Trust : 0;
        var relationship = world.GetOrCreateRelationship(new RelationshipKey(victim.Id, accused.Id), tick);
        relationship.ApplyEvent(RelationshipEventType.Betrayal, world.FamilyRules);
        relationship.MarkContact(tick);
        if (world.CrimeOn && world.CrimeRules!.Violence.Enabled && trustBefore >= world.CrimeRules.Violence.BetrayalTrust)
            Violence.AddGrudge(world, victim.Id.Value, accused.Id.Value, "traicao", world.CrimeRules.Violence.BetrayalTheftGrudge, tick);
        ctx.LogEvent(WorldEventKind.TheftDiscovered,
            $"{accused.Id.Value}|{victim.Id.Value}|{coin}|{(took ? 1 : 0)}", SourceName);
    }

    private static string KindName(TheftTargetKind kind) => kind switch
    {
        TheftTargetKind.Person => "pessoa",
        TheftTargetKind.House => "casa",
        TheftTargetKind.Stall => "banca",
        _ => "caixa",
    };

    private static string GoodName(WorldState world, in TheftTarget target)
    {
        if (target.ResourceId == 0) return "moeda";
        var economy = world.EconomyRules;
        if (target.ResourceId == economy.FoodResourceId) return "comida";
        if (target.ResourceId == economy.WaterResourceId) return "agua";
        foreach (var recipe in world.EconomyCatalog.Recipes.Values)
            if (recipe.Tool?.ResourceId == target.ResourceId) return "ferramenta";
        return $"recurso-{target.ResourceId}";
    }

    /// <summary>Move o que foi levado; devolve (quantidade, valor). Moeda vai à carteira; comida, à despensa do ladrão; outro
    /// bem é vendido a um receptador (oficina da cidade que o usa) por uma fração do preço, ou guardado em casa.</summary>
    private static (long Qty, long Value) Move(WorldState world, TickContext ctx, Npc thief, in TheftTarget target)
    {
        if (target.ResourceId == 0)
        {
            long coin = target.Coin;
            if (target.Kind == TheftTargetKind.Till)
            {
                if (!target.Shop!.TryDebitTreasury(new Money(coin)).IsSuccess) return (0, 0);
            }
            else if (!target.Person!.TryDebitWallet(new Money(coin)).IsSuccess) return (0, 0);
            thief.CreditWallet(new Money(coin));
            return (coin, coin);
        }

        var resource = new ResourceType(target.ResourceId);
        var withdrawn = target.Kind == TheftTargetKind.House
            ? target.House!.Withdraw(resource, target.Units)
            : target.Shop!.Withdraw(resource, target.Units);
        long units = withdrawn.IsSuccess ? withdrawn.Value : 0;
        if (units <= 0) return (0, 0);
        long value = units * PriceOf(world, thief.City, target.ResourceId);
        if (target.ResourceId != world.EconomyRules.FoodResourceId && target.ResourceId != world.EconomyRules.WaterResourceId
            && Fence(world, thief, target.Shop, resource, units, value))
            return (units, value);
        if (thief.Household is { } home && world.FindHousehold(home) is { } household) household.Deposit(resource, units);
        return (units, value);
    }

    /// <summary>Receptação: uma oficina da cidade (que não a roubada) que usa o bem como ferramenta ou insumo compra o furto
    /// por <see cref="TheftRules.FenceShare"/> do valor, se tem caixa e espaço. A moeda só muda de mão.</summary>
    private static bool Fence(WorldState world, Npc thief, Workplace? source, ResourceType resource, long units, long value)
    {
        long pay = Math.Max(1, (long)(value * world.TheftRules.FenceShare));
        foreach (var buyer in world.Workplaces.OrderBy(w => w.Id.Value))
        {
            if (buyer.City != thief.City || buyer == source || buyer.Treasury.Amount < pay) continue;
            if (!world.EconomyCatalog.Recipes.TryGetValue(buyer.LocationType.Id, out var recipe)) continue;
            if (recipe.Tool?.ResourceId != resource.Id && !recipe.Inputs.ContainsKey(resource.Id)) continue;
            if (world.EconomyRules.CapacityOf(resource, buyer.LocationType) - buyer.Stock.GetValueOrDefault(resource) < units) continue;
            if (!buyer.TryDebitTreasury(new Money(pay)).IsSuccess) continue;
            buyer.Deposit(resource, units, world.EconomyRules);
            thief.CreditWallet(new Money(pay));
            return true;
        }
        return false;
    }
}
