using System.Text.Json.Serialization;
using LivingWorld.Domain.Invention;
using LivingWorld.Domain.Shared;

namespace LivingWorld.Simulation.Invention;

/// <summary>Estado de quem quer resolver a água (AD-141): frustração vivida em minutos perdidos (com o tick da última conta, decai por
/// mês), até quando está ocupado num protótipo ou numa construção, tentativas e falhas, a última falha e o protótipo que falhou
/// (de onde a próxima tentativa parte) e o material em que deixou de confiar (o que a falha ensinou).</summary>
/// <see cref="SeenDesignId"/>: a peça de outra casa que a pessoa viu em uso (AD-143), de onde vem a cópia; 0 = nenhuma.
/// <see cref="NeedMl"/> e <see cref="NeedsLiquid"/> (só no problema de carregar, AD-147): o volume que faltou levar e se era líquido.
public readonly record struct InventionMind(
    long NpcId, int FrustrationMinutes, long BusyUntilTick, int Attempts, int Failures, FailureMode LastFailure,
    string? LastTried, int DistrustedMaterial, int SeenDesignId = 0, int NeedMl = 0, bool NeedsLiquid = false);

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

    /// <summary>O que cada pessoa leva consigo (por pessoa e recurso) e as mentes ocupadas com o problema de carregar: frustração
    /// de quem teve a compra cortada pelo que cabe nos braços ou ficou com sede ou fome longe de casa (AD-147, por pessoa).</summary>
    public List<CarriedLoad> Carried { get; init; } = [];
    public List<InventionMind> CarryMinds { get; init; } = [];

    /// <summary>Designs concebidos para carregar (ordenados): a função de um design é o problema de quem o concebeu (AD-147).</summary>
    public List<int> CarryDesigns { get; init; } = [];

    private Dictionary<int, Design>? _designById;
    private Dictionary<long, Craft>? _bestCraftByHousehold;
    private Dictionary<long, int>? _listingsByMarket;
    private Dictionary<long, Carrier>? _bestCarrierByHousehold;

    /// <summary>A melhor peça que funciona de uma casa: velocidade (com a qualidade), carga útil com a pessoa de referência,
    /// estabilidade e o design (volátil, derivado dos itens).</summary>
    internal readonly record struct Craft(int SpeedMmS, long PayloadG, int MetacentricMm, int DesignId);
    private readonly Dictionary<int, Performance> _performance = [];
    private readonly Dictionary<int, Containment> _containment = [];

    /// <summary>O melhor recipiente que funciona de uma casa (AD-147): carga que aguenta (com a qualidade), volume oco, a parte
    /// estanque, massa própria, se se prende ao corpo e o design (volátil, derivado dos itens).</summary>
    internal readonly record struct Carrier(long CapacityG, long InnerMl, long WatertightMl, long MassG, bool Worn, int DesignId)
    {
        public bool Exists => DesignId != 0;
    }

    [JsonIgnore] internal long Version { get; private set; }

    internal void Touch()
    {
        Version++;
        _designById = null;
        _bestCraftByHousehold = null;
        _listingsByMarket = null;
        _bestCarrierByHousehold = null;
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

    /// <summary>Como coisa que se leva, medido com a natureza, memorizado por id (volátil: recomputável).</summary>
    internal Containment ContainmentOf(Design design, InventionRules rules)
    {
        if (!_containment.TryGetValue(design.Id, out var c))
            _containment[design.Id] = c = PhysicsEvaluator.Contain(design.Parts, rules);
        return c;
    }

    /// <summary>Para que o design serve (AD-147): 1 = carregar (concebido para isso), 0 = o resto (a travessia de antes).</summary>
    internal byte FunctionOf(int designId) => CarryDesigns.Count > 0 && CarryDesigns.BinarySearch(designId) >= 0 ? (byte)1 : (byte)0;

    internal void MarkCarryDesign(int designId)
    {
        int i = CarryDesigns.BinarySearch(designId);
        if (i >= 0) return;
        CarryDesigns.Insert(~i, designId);
        Touch();
    }

    /// <summary>O melhor recipiente em uso do domicílio (a maior carga); padrão = nenhum. Item exposto à venda não conta.</summary>
    private static long Reach(long capacityG, bool worn, InventionRules rules) =>
        capacityG + (worn ? (long)rules.HandsVolumeMl * rules.WaterDensityKgM3 / 1000 : 0);

    internal Carrier BestCarrier(long householdId, InventionRules rules)
    {
        if (Items.Count == 0) return default;
        if (_bestCarrierByHousehold is null)
        {
            _bestCarrierByHousehold = [];
            foreach (var item in Items)
            {
                if (item.ForSale || FindDesign(item.DesignId) is not { } design || FunctionOf(design.Id) != 1) continue;
                var c = ContainmentOf(design, rules);
                long capacity = c.CapacityG * item.QualityPm / 1000;
                // AD-149: a peça presa ao corpo vale também as mãos livres (o que elas ainda levam), como na pontuação da ideia
                var held = _bestCarrierByHousehold.GetValueOrDefault(item.HouseholdId);
                if (Reach(capacity, c.Worn, rules) > Reach(held.CapacityG, held.Worn, rules))
                    _bestCarrierByHousehold[item.HouseholdId] = new Carrier(capacity, c.InnerMl, c.WatertightMl, c.MassG, c.Worn, design.Id);
            }
        }
        return _bestCarrierByHousehold.GetValueOrDefault(householdId);
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

    /// <summary>Peças expostas à venda num mercado (derivado). Com recipientes no mundo (AD-147), por função: a chave junta o mercado
    /// e a função (0 = travessia, 1 = carregar).</summary>
    internal int ListingsAt(long marketId, byte function = 0)
    {
        if (_listingsByMarket is null)
        {
            _listingsByMarket = [];
            foreach (var item in Items)
                if (item.MarketId is { } m)
                {
                    long key = m * 2 + FunctionOf(item.DesignId);
                    _listingsByMarket[key] = _listingsByMarket.GetValueOrDefault(key) + 1;
                }
        }
        return _listingsByMarket.GetValueOrDefault(marketId * 2 + function);
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

    /// <summary>Casa que quis comprar no mês (uma linha por casa e função; levar marca <see cref="DesignWant.Bought"/>).</summary>
    internal void Want(long householdId, CityId city, bool bought, byte function = 0)
    {
        int lo = 0, hi = Wants.Count - 1;
        while (lo <= hi)
        {
            int mid = (lo + hi) >> 1;
            var w = Wants[mid];
            int cmp = w.HouseholdId != householdId ? w.HouseholdId.CompareTo(householdId) : w.Function.CompareTo(function);
            if (cmp == 0)
            {
                if (bought && !w.Bought) { Wants[mid] = w with { Bought = true }; Version++; }
                return;
            }
            if (cmp < 0) lo = mid + 1; else hi = mid - 1;
        }
        Wants.Insert(lo, new DesignWant(householdId, city, bought, function));
        Version++;
    }

    internal int DemandIn(CityId city, byte function = 0)
    {
        foreach (var d in Demand) if (d.City == city && d.Function == function) return d.Households;
        return 0;
    }

    // --- o que a pessoa leva (AD-147) ---

    internal int CarriedIndex(long npcId, int resourceId)
    {
        int lo = 0, hi = Carried.Count - 1;
        while (lo <= hi)
        {
            int mid = (lo + hi) >> 1;
            var c = Carried[mid];
            int cmp = c.NpcId != npcId ? c.NpcId.CompareTo(npcId) : c.ResourceId.CompareTo(resourceId);
            if (cmp == 0) return mid;
            if (cmp < 0) lo = mid + 1; else hi = mid - 1;
        }
        return ~lo;
    }

    internal int CarriedQuantity(long npcId, int resourceId)
    {
        if (Carried.Count == 0) return 0;
        int i = CarriedIndex(npcId, resourceId);
        return i >= 0 ? Carried[i].Quantity : 0;
    }

    internal bool CarriesAnything(long npcId)
    {
        if (Carried.Count == 0) return false;
        int i = CarriedIndex(npcId, int.MinValue);
        i = i >= 0 ? i : ~i;
        return i < Carried.Count && Carried[i].NpcId == npcId;
    }

    /// <summary>Soma (ou tira) unidades do que a pessoa leva; linha vazia sai.</summary>
    internal void Carry(long npcId, int resourceId, int delta)
    {
        if (delta == 0) return;
        int i = CarriedIndex(npcId, resourceId);
        int now = (i >= 0 ? Carried[i].Quantity : 0) + delta;
        if (now <= 0) { if (i >= 0) Carried.RemoveAt(i); }
        else if (i >= 0) Carried[i] = Carried[i] with { Quantity = now };
        else Carried.Insert(~i, new CarriedLoad(npcId, resourceId, now));
        Version++;
    }

    public int CarryMindIndex(long npcId)
    {
        int lo = 0, hi = CarryMinds.Count - 1;
        while (lo <= hi)
        {
            int mid = (lo + hi) >> 1;
            long id = CarryMinds[mid].NpcId;
            if (id == npcId) return mid;
            if (id < npcId) lo = mid + 1; else hi = mid - 1;
        }
        return ~lo;
    }

    internal void RemoveCarryMind(int index)
    {
        CarryMinds.RemoveAt(index);
        Version++;
    }

    internal void PutCarryMind(InventionMind mind)
    {
        int index = CarryMindIndex(mind.NpcId);
        if (index >= 0) CarryMinds[index] = mind; else CarryMinds.Insert(~index, mind);
        Version++;
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
    // AD-147: o que a pessoa leva
    public long CarryAttempts, CarryInventions, CarryPrototypeFailures, CarryIdeasDiscarded, CarryCopies, CarryTaught, CarryBuilt, CarrySales;
    public long PackTrips, PackUnits, PackEaten, PackDrunk, PackReturned, CriticalAwayFromPack;
    public long HomeForMeal, CarrierCargoTrips, CarrierCargoGrams, CargoFrustrations, AwayFrustrations, StarvedAway, DehydratedAway;
}
