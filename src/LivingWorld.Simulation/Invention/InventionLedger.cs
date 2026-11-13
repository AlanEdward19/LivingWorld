using System.Text.Json.Serialization;
using LivingWorld.Domain.Invention;
using LivingWorld.Domain.Shared;

namespace LivingWorld.Simulation.Invention;

/// <summary>Estado de quem quer resolver a água (AD-141): frustração vivida em minutos perdidos (com o tick da última conta, decai por
/// mês), até quando está ocupado num protótipo ou numa construção, tentativas e falhas, a última falha e o protótipo que falhou
/// (de onde a próxima tentativa parte) e o material em que deixou de confiar (o que a falha ensinou).</summary>
/// <see cref="SeenDesignId"/>: a peça de outra casa que a pessoa viu em uso (AD-143), de onde vem a cópia; 0 = nenhuma.
public readonly record struct InventionMind(
    long NpcId, int FrustrationMinutes, long BusyUntilTick, int Attempts, int Failures, FailureMode LastFailure,
    string? LastTried, int DistrustedMaterial, int SeenDesignId = 0);

/// <summary>Tudo o que a invenção guarda no mundo (canônico, ADR-0014): designs vivos em ordem de nascimento, lápides, quem sabe o
/// quê (ordenado por pessoa e design), instâncias e as mentes ocupadas com o problema (ordenadas por pessoa). Sem nada no tick 0:
/// o cenário não tem bloco de designs. Os índices são derivados e reconstruídos ao mudar.</summary>
public sealed class InventionLedger
{
    public List<Design> Designs { get; init; } = [];
    public List<DesignTombstone> Tombstones { get; init; } = [];
    public List<KnownDesign> Known { get; init; } = [];
    public List<CraftedItem> Items { get; init; } = [];
    public List<InventionMind> Minds { get; init; } = [];
    public long NextItemId { get; set; } = 1;

    /// <summary>Material guardado por casa (ordenado por casa e material), encomendas na oficina (por pessoa), quem quis comprar
    /// no mês (por casa), a demanda do mês passado e o peixe por cidade (AD-143).</summary>
    public List<HouseholdMaterial> Stored { get; init; } = [];
    public List<DesignOrder> Orders { get; init; } = [];
    public List<DesignWant> Wants { get; init; } = [];
    public List<DesignDemand> Demand { get; init; } = [];
    public List<FishStock> Fish { get; init; } = [];

    private Dictionary<int, Design>? _designById;
    private Dictionary<long, Craft>? _bestCraftByHousehold;
    private Dictionary<long, int>? _listingsByMarket;

    /// <summary>A melhor peça que funciona de uma casa: velocidade (com a qualidade), carga útil com a pessoa de referência,
    /// estabilidade e o design (volátil, derivado dos itens).</summary>
    internal readonly record struct Craft(int SpeedMmS, long PayloadG, int MetacentricMm, int DesignId);
    private readonly Dictionary<int, Performance> _performance = [];

    [JsonIgnore] internal long Version { get; private set; }

    internal void Touch()
    {
        Version++;
        _designById = null;
        _bestCraftByHousehold = null;
        _listingsByMarket = null;
    }

    public Design? FindDesign(int id)
    {
        _designById ??= Designs.ToDictionary(d => d.Id);
        return _designById.GetValueOrDefault(id);
    }

    /// <summary>Desempenho real com a pessoa de referência, memorizado por id (volátil: recomputável).</summary>
    internal Performance PerformanceOf(Design design, InventionRules rules)
    {
        if (!_performance.TryGetValue(design.Id, out var perf))
            _performance[design.Id] = perf = PhysicsEvaluator.Evaluate(design.Parts, rules, rules.ReferenceRiderKg);
        return perf;
    }

    /// <summary>Velocidade (mm/s) da melhor peça que funciona do domicílio; 0 = nenhuma.</summary>
    internal int BestCraftSpeed(long householdId, InventionRules rules) => BestCraft(householdId, rules).SpeedMmS;

    /// <summary>A melhor peça em uso do domicílio (a mais rápida); padrão = nenhuma. Item exposto à venda não conta.</summary>
    internal Craft BestCraft(long householdId, InventionRules rules)
    {
        if (Items.Count == 0) return default;
        if (_bestCraftByHousehold is null)
        {
            _bestCraftByHousehold = [];
            foreach (var item in Items)
            {
                if (item.ForSale || FindDesign(item.DesignId) is not { } design) continue;
                var perf = PerformanceOf(design, rules);
                if (!perf.Works) continue;
                int speed = (int)((long)perf.SpeedMmS * item.QualityPm / 1000);
                if (speed > _bestCraftByHousehold.GetValueOrDefault(item.HouseholdId).SpeedMmS)
                    _bestCraftByHousehold[item.HouseholdId] = new Craft(speed, perf.PayloadG * item.QualityPm / 1000, perf.MetacentricMm, design.Id);
            }
        }
        return _bestCraftByHousehold.GetValueOrDefault(householdId);
    }

    /// <summary>Peças expostas à venda num mercado (derivado).</summary>
    internal int ListingsAt(long marketId)
    {
        if (_listingsByMarket is null)
        {
            _listingsByMarket = [];
            foreach (var item in Items)
                if (item.MarketId is { } m) _listingsByMarket[m] = _listingsByMarket.GetValueOrDefault(m) + 1;
        }
        return _listingsByMarket.GetValueOrDefault(marketId);
    }

    internal long StoredGrams(long householdId, int materialId)
    {
        int i = StoredIndex(householdId, materialId);
        return i >= 0 ? Stored[i].Grams : 0;
    }

    /// <summary>Soma (ou tira, com <paramref name="grams"/> negativo) material do estoque da casa; linha vazia sai.</summary>
    internal void Store(long householdId, int materialId, long grams)
    {
        if (grams == 0) return;
        int i = StoredIndex(householdId, materialId);
        long now = (i >= 0 ? Stored[i].Grams : 0) + grams;
        if (now <= 0) { if (i >= 0) Stored.RemoveAt(i); }
        else if (i >= 0) Stored[i] = Stored[i] with { Grams = now };
        else Stored.Insert(~i, new HouseholdMaterial(householdId, materialId, now));
        Version++;
    }

    private int StoredIndex(long householdId, int materialId)
    {
        int lo = 0, hi = Stored.Count - 1;
        while (lo <= hi)
        {
            int mid = (lo + hi) >> 1;
            var m = Stored[mid];
            int cmp = m.HouseholdId != householdId ? m.HouseholdId.CompareTo(householdId) : m.MaterialId.CompareTo(materialId);
            if (cmp == 0) return mid;
            if (cmp < 0) lo = mid + 1; else hi = mid - 1;
        }
        return ~lo;
    }

    internal int OrderIndex(long npcId)
    {
        int lo = 0, hi = Orders.Count - 1;
        while (lo <= hi)
        {
            int mid = (lo + hi) >> 1;
            long id = Orders[mid].NpcId;
            if (id == npcId) return mid;
            if (id < npcId) lo = mid + 1; else hi = mid - 1;
        }
        return ~lo;
    }

    internal void PutOrder(DesignOrder order)
    {
        int i = OrderIndex(order.NpcId);
        if (i >= 0) Orders[i] = order; else Orders.Insert(~i, order);
        Version++;
    }

    /// <summary>Casa que quis comprar no mês (uma linha por casa; levar marca <see cref="DesignWant.Bought"/>).</summary>
    internal void Want(long householdId, CityId city, bool bought)
    {
        int lo = 0, hi = Wants.Count - 1;
        while (lo <= hi)
        {
            int mid = (lo + hi) >> 1;
            long id = Wants[mid].HouseholdId;
            if (id == householdId)
            {
                if (bought && !Wants[mid].Bought) { Wants[mid] = Wants[mid] with { Bought = true }; Version++; }
                return;
            }
            if (id < householdId) lo = mid + 1; else hi = mid - 1;
        }
        Wants.Insert(lo, new DesignWant(householdId, city, bought));
        Version++;
    }

    internal int DemandIn(CityId city)
    {
        foreach (var d in Demand) if (d.City == city) return d.Households;
        return 0;
    }

    public int MindIndex(long npcId)
    {
        int lo = 0, hi = Minds.Count - 1;
        while (lo <= hi)
        {
            int mid = (lo + hi) >> 1;
            long id = Minds[mid].NpcId;
            if (id == npcId) return mid;
            if (id < npcId) lo = mid + 1; else hi = mid - 1;
        }
        return ~lo;
    }

    internal void PutMind(InventionMind mind)
    {
        int index = MindIndex(mind.NpcId);
        if (index >= 0) Minds[index] = mind; else Minds.Insert(~index, mind);
        Version++;
    }

    public bool Knows(long npcId, int designId) => KnownIndex(npcId, designId) >= 0;

    internal int KnownIndex(long npcId, int designId)
    {
        int lo = 0, hi = Known.Count - 1;
        while (lo <= hi)
        {
            int mid = (lo + hi) >> 1;
            var k = Known[mid];
            int cmp = k.NpcId != npcId ? k.NpcId.CompareTo(npcId) : k.DesignId.CompareTo(designId);
            if (cmp == 0) return mid;
            if (cmp < 0) lo = mid + 1; else hi = mid - 1;
        }
        return ~lo;
    }

    internal void Learn(KnownDesign known)
    {
        int index = KnownIndex(known.NpcId, known.DesignId);
        if (index >= 0)
        {
            if (Known[index].FidelityPm < known.FidelityPm) Known[index] = known;
        }
        else Known.Insert(~index, known);
        Touch();
    }

    internal IEnumerable<KnownDesign> KnownBy(long npcId)
    {
        int index = KnownIndex(npcId, 0);
        for (int i = index >= 0 ? index : ~index; i < Known.Count && Known[i].NpcId == npcId; i++)
            yield return Known[i];
    }

    internal void AddDesign(Design design)
    {
        if (FindDesign(design.Id) is not null) return;
        Designs.Add(design);
        Touch();
    }
}

/// <summary>Contagem de medição (volátil e efêmera: só o relatório lê, nenhum sistema decide por ela).</summary>
public sealed class InventionTally
{
    public long Attempts;
    public long IdeasDiscarded;
    public long PrototypeFailures;
    public long Sank, Capsized, Broke, TooSlow, Invalid;
    public long Inventions;
    public long Taught, Copied, ReverseEngineered;
    public long Built, BuildFailures;
    public long Lost;
    public long FootWaterTrips, CraftWaterTrips;
    public long HoursSavedX10;
    // AD-143
    public long FootCargoCapped, CraftCargoTrips, CargoUnitsCut;
    public long Listed, Sales, SaleCoins, SaleBuildFailures, Repriced;
    public long StockGramsUsed, MarketGramsBought, SalvagedGrams, GatherMinutesSaved;
    public long FishingMonths, FishGrams, FishFoodUnits;
    public long Sightings, CopiesFromSight;
    public long TombstonesWithAccounts, RumorBoosts;
}
