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

    /// <summary>O recipiente que cada pessoa levou consigo (AD-157, ordenado por pessoa): é dela até voltar para casa. Nulo enquanto
    /// ninguém levou nada: o snapshot antigo abre igual e o mundo com a carga desligada não ganha o campo.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<HeldItem>? Held { get => _held; set { _held = value; _entriesByNpc = null; } }

    private List<HeldItem>? _held;

    /// <summary>AD-167: quantas linhas (carga e recipiente) cada pessoa tem, para a pergunta de toda decisão "leva alguma coisa?" sem
    /// duas buscas binárias. Só memória: montada na primeira pergunta a partir das listas e mantida por <see cref="Carry"/> e
    /// <see cref="Hold"/>, as únicas que as mudam.</summary>
    private Dictionary<long, int>? _entriesByNpc;

    internal bool CarriesOrHolds(long npcId)
    {
        if (Carried.Count == 0 && _held is not { Count: > 0 }) return false;
        return (_entriesByNpc ??= BuildEntries()).ContainsKey(npcId);
    }

    private Dictionary<long, int> BuildEntries()
    {
        var map = new Dictionary<long, int>();
        foreach (var load in Carried) map[load.NpcId] = map.GetValueOrDefault(load.NpcId) + 1;
        if (_held is not null) foreach (var held in _held) map[held.NpcId] = map.GetValueOrDefault(held.NpcId) + 1;
        return map;
    }

    private void CountEntry(long npcId, int delta)
    {
        if (_entriesByNpc is not { } map) return;
        int now = map.GetValueOrDefault(npcId) + delta;
        if (now <= 0) map.Remove(npcId); else map[npcId] = now;
    }

    /// <summary>Casas cuja última compra a carga cortou (AD-157b, ordenadas): o resto ficou na banca e a casa volta por ele. Nulo
    /// enquanto nenhuma: o snapshot antigo abre igual.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<long>? LeftAtMarket { get; set; }

    internal bool IsLeftAtMarket(long householdId) => LeftAtMarket is { Count: > 0 } left && left.BinarySearch(householdId) >= 0;

    internal void LeaveAtMarket(long householdId, bool left)
    {
        if (!left && LeftAtMarket is not { Count: > 0 }) return;
        LeftAtMarket ??= [];
        int i = LeftAtMarket.BinarySearch(householdId);
        if (left && i < 0) LeftAtMarket.Insert(~i, householdId);
        else if (!left && i >= 0) LeftAtMarket.RemoveAt(i);
        else return;
        if (LeftAtMarket.Count == 0) LeftAtMarket = null;
        Version++;
    }

    /// <summary>Espólios no chão (AD-168, ordenados por id) e as unidades de cada um (por espólio e recurso). Nulos enquanto nenhum:
    /// o snapshot antigo abre igual. <see cref="LastLeavingId"/>: o último id dado (0 = nenhum ainda, fora do snapshot).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<Leaving>? Leavings { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<LeftLoad>? LeftLoads { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public long LastLeavingId { get; set; }

    /// <summary>Tudo o que está no chão de um recurso (AD-168): estoque do mundo, entra na conservação.</summary>
    public long GroundTotal(int resourceId)
    {
        if (LeftLoads is not { Count: > 0 } loads) return 0;
        long total = 0;
        foreach (var load in loads) if (load.ResourceId == resourceId) total += load.Quantity;
        return total;
    }

    internal int LeavingIndex(long id)
    {
        if (Leavings is not { } list) return -1;
        int lo = 0, hi = list.Count - 1;
        while (lo <= hi)
        {
            int mid = (lo + hi) >> 1;
            long at = list[mid].Id;
            if (at == id) return mid;
            if (at < id) lo = mid + 1; else hi = mid - 1;
        }
        return ~lo;
    }

    internal void PutLeaving(Leaving leaving)
    {
        Leavings ??= [];
        int i = LeavingIndex(leaving.Id);
        bool groundBefore = i >= 0 && Leavings[i].ItemId != 0;
        if (i >= 0) Leavings[i] = leaving; else Leavings.Insert(~i, leaving);
        if (groundBefore != (leaving.ItemId != 0)) Touch(); else Version++; // o recipiente no chão some (ou volta) das peças da casa
    }

    internal void RemoveLeaving(long id)
    {
        int i = LeavingIndex(id);
        if (i < 0) return;
        bool item = Leavings![i].ItemId != 0;
        Leavings.RemoveAt(i);
        if (Leavings.Count == 0) Leavings = null;
        if (LeftLoads is { } loads)
        {
            loads.RemoveAll(l => l.LeavingId == id);
            if (loads.Count == 0) LeftLoads = null;
        }
        if (item) Touch(); else Version++;
    }

    internal int LeftQuantity(long leavingId, int resourceId)
    {
        if (LeftLoads is not { } loads) return 0;
        foreach (var l in loads) if (l.LeavingId == leavingId && l.ResourceId == resourceId) return l.Quantity;
        return 0;
    }

    internal bool HasLoads(long leavingId)
    {
        if (LeftLoads is not { } loads) return false;
        foreach (var l in loads) if (l.LeavingId == leavingId) return true;
        return false;
    }

    /// <summary>Soma (ou tira) unidades de um espólio; linha vazia sai. Ordem por espólio e recurso.</summary>
    internal void Leave(long leavingId, int resourceId, int delta)
    {
        if (delta == 0) return;
        LeftLoads ??= [];
        int lo = 0, hi = LeftLoads.Count - 1, at = -1;
        while (lo <= hi)
        {
            int mid = (lo + hi) >> 1;
            var l = LeftLoads[mid];
            int cmp = l.LeavingId != leavingId ? l.LeavingId.CompareTo(leavingId) : l.ResourceId.CompareTo(resourceId);
            if (cmp == 0) { at = mid; break; }
            if (cmp < 0) lo = mid + 1; else hi = mid - 1;
        }
        int now = (at >= 0 ? LeftLoads[at].Quantity : 0) + delta;
        if (now <= 0) { if (at >= 0) LeftLoads.RemoveAt(at); }
        else if (at >= 0) LeftLoads[at] = LeftLoads[at] with { Quantity = now };
        else LeftLoads.Insert(lo, new LeftLoad(leavingId, resourceId, now));
        if (LeftLoads.Count == 0) LeftLoads = null;
        Version++;
    }

    /// <summary>O recipiente está num espólio (no chão ou a caminho de casa com quem não pode usá-lo): a casa não o tem.</summary>
    private bool InLeaving(long itemId)
    {
        if (Leavings is not { } list) return false;
        foreach (var l in list) if (l.ItemId == itemId) return true;
        return false;
    }

    /// <summary>Muda o dono de uma peça (o herdeiro que a buscou, o ladrão que a levou).</summary>
    internal void GiveItem(long itemId, long householdId, CityId city)
    {
        for (int i = 0; i < Items.Count; i++)
            if (Items[i].Id == itemId)
            {
                if (Items[i].HouseholdId == householdId) return;
                Items[i] = Items[i] with { HouseholdId = householdId, City = city };
                Touch();
                return;
            }
    }

    /// <summary>Designs concebidos para carregar (ordenados): a função de um design é o problema de quem o concebeu (AD-147).</summary>
    public List<int> CarryDesigns { get; init; } = [];

    /// <summary>Designs concebidos para levar por terra (AD-153, ordenados): a sonda de terra mostrou que puxam mais que os braços.
    /// Nulo enquanto vazio: o snapshot só ganha o campo quando alguém inventa.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<int>? HaulDesigns { get; set; }

    private Dictionary<int, Design>? _designById;
    private Dictionary<long, Craft>? _bestCraftByHousehold;
    private Dictionary<long, int>? _listingsByMarket;
    private Dictionary<long, Carrier>? _bestCarrierByHousehold;
    private Dictionary<long, List<(long ItemId, Carrier Carrier)>>? _carriersByHousehold;
    private Dictionary<long, Hauler>? _bestHaulerByHousehold;

    /// <summary>AD-151: memória volátil de <see cref="PersonalCarry.PantryOutOfReach"/> por (casa, cidade, célula).</summary>
    [JsonIgnore] internal Dictionary<long, ReachKnown> ReachMemo { get; } = [];

    /// <summary>A melhor peça que funciona de uma casa: velocidade (com a qualidade), carga útil com a pessoa de referência,
    /// estabilidade e o design (volátil, derivado dos itens).</summary>
    internal readonly record struct Craft(int SpeedMmS, long PayloadG, int MetacentricMm, int DesignId);
    private readonly Dictionary<int, Performance> _performance = [];
    private readonly Dictionary<int, Containment> _containment = [];
    private readonly Dictionary<int, Haulage> _haulage = [];

    /// <summary>O melhor veículo de terra que funciona de uma casa (AD-153): carga que uma pessoa puxa nele (com a qualidade), como toca
    /// o chão e o design (volátil, derivado dos itens).</summary>
    internal readonly record struct Hauler(long CapacityG, GroundMode Mode, int DesignId)
    {
        public bool Exists => DesignId != 0;
    }

    /// <summary>O melhor recipiente que funciona de uma casa (AD-147): carga que aguenta (com a qualidade), volume oco, a parte
    /// estanque, massa própria, se se prende ao corpo e o design (volátil, derivado dos itens).</summary>
    internal readonly record struct Carrier(long CapacityG, long InnerMl, long WatertightMl, long MassG, bool Worn, int DesignId)
    {
        public bool Exists => DesignId != 0;
    }

    [JsonIgnore] internal long Version { get; private set; }

    /// <summary>Quantas vezes os derivados das peças (embarcação, recipiente) foram invalidados: muda quando o trajeto da casa pode
    /// mudar; não muda com o que a pessoa leva (AD-151, memória do alcance da despensa).</summary>
    [JsonIgnore] internal long Touches { get; private set; }

    internal void Touch()
    {
        Version++;
        Touches++;
        _designById = null;
        _bestCraftByHousehold = null;
        _listingsByMarket = null;
        _bestCarrierByHousehold = null;
        _carriersByHousehold = null;
        _bestHaulerByHousehold = null;
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

    /// <summary>Por terra, medido com a natureza, memorizado por id (AD-153, volátil: recomputável).</summary>
    internal Haulage HaulageOf(Design design, InventionRules rules)
    {
        if (!_haulage.TryGetValue(design.Id, out var h))
            _haulage[design.Id] = h = PhysicsEvaluator.Haul(design.Parts, rules);
        return h;
    }

    /// <summary>Para que o design serve (AD-147): 1 = carregar (concebido para isso), 2 = levar por terra (AD-153), 0 = o resto (a
    /// travessia de antes).</summary>
    internal byte FunctionOf(int designId)
    {
        if (HaulDesigns is { Count: > 0 } haul && haul.BinarySearch(designId) >= 0) return 2;
        return CarryDesigns.Count > 0 && CarryDesigns.BinarySearch(designId) >= 0 ? (byte)1 : (byte)0;
    }

    internal void MarkHaulDesign(int designId)
    {
        HaulDesigns ??= [];
        int i = HaulDesigns.BinarySearch(designId);
        if (i >= 0) return;
        HaulDesigns.Insert(~i, designId);
        Touch();
    }

    /// <summary>O melhor veículo de terra em uso do domicílio (a maior carga); padrão = nenhum. Item exposto à venda não conta.</summary>
    internal Hauler BestHauler(long householdId, InventionRules rules)
    {
        if (Items.Count == 0 || HaulDesigns is not { Count: > 0 }) return default;
        if (_bestHaulerByHousehold is null)
        {
            _bestHaulerByHousehold = [];
            foreach (var item in Items)
            {
                if (item.ForSale || FunctionOf(item.DesignId) != 2 || FindDesign(item.DesignId) is not { } design) continue;
                var h = HaulageOf(design, rules);
                if (!h.Works) continue;
                long capacity = h.CapacityG * item.QualityPm / 1000;
                if (capacity > _bestHaulerByHousehold.GetValueOrDefault(item.HouseholdId).CapacityG)
                    _bestHaulerByHousehold[item.HouseholdId] = new Hauler(capacity, h.Mode, design.Id);
            }
        }
        return _bestHaulerByHousehold.GetValueOrDefault(householdId);
    }

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
        if (_bestCarrierByHousehold is null) BuildCarriers(rules);
        return _bestCarrierByHousehold!.GetValueOrDefault(householdId);
    }

    private void BuildCarriers(InventionRules rules)
    {
        _bestCarrierByHousehold = [];
        _carriersByHousehold = [];
        foreach (var item in Items)
        {
            if (item.ForSale || FindDesign(item.DesignId) is not { } design || FunctionOf(design.Id) != 1 || InLeaving(item.Id)) continue;
            var c = ContainmentOf(design, rules);
            long capacity = c.CapacityG * item.QualityPm / 1000;
            var carrier = new Carrier(capacity, c.InnerMl, c.WatertightMl, c.MassG, c.Worn, design.Id);
            // AD-149: a peça presa ao corpo vale também as mãos livres (o que elas ainda levam), como na pontuação da ideia
            var held = _bestCarrierByHousehold.GetValueOrDefault(item.HouseholdId);
            if (Reach(capacity, c.Worn, rules) > Reach(held.CapacityG, held.Worn, rules))
                _bestCarrierByHousehold[item.HouseholdId] = carrier;
            if (!_carriersByHousehold.TryGetValue(item.HouseholdId, out var list)) _carriersByHousehold[item.HouseholdId] = list = [];
            list.Add((item.Id, carrier));
        }
        // a melhor primeiro (empate: o item mais antigo), para quem sai pegar a melhor que ainda está em casa
        foreach (var list in _carriersByHousehold.Values)
            list.Sort((a, b) =>
            {
                int cmp = Reach(b.Carrier.CapacityG, b.Carrier.Worn, rules).CompareTo(Reach(a.Carrier.CapacityG, a.Carrier.Worn, rules));
                return cmp != 0 ? cmp : a.ItemId.CompareTo(b.ItemId);
            });
    }

    /// <summary>O recipiente de uma pessoa (AD-157): o que ela levou de casa ou, em casa, o melhor que ninguém da casa levou; padrão =
    /// nenhum (mãos nuas). <paramref name="itemId"/> é o item, para quem vai sair com ele.</summary>
    internal Carrier CarrierFor(long npcId, long householdId, InventionRules rules, out long itemId)
    {
        itemId = 0;
        if (Items.Count == 0) return default;
        if (_carriersByHousehold is null) BuildCarriers(rules);
        if (!_carriersByHousehold!.TryGetValue(householdId, out var list)) return default;
        long mine = HeldBy(npcId);
        if (mine != 0)
        {
            foreach (var (id, c) in list) if (id == mine) { itemId = id; return c; }
            return default; // o que levou não é desta casa (mudou de casa) ou gastou
        }
        foreach (var (id, c) in list)
            if (!IsHeld(id)) { itemId = id; return c; }
        return default;
    }

    /// <summary>O item que a pessoa levou; 0 = nenhum.</summary>
    internal long HeldBy(long npcId)
    {
        if (Held is not { Count: > 0 } held) return 0;
        int i = HeldIndex(held, npcId);
        return i >= 0 ? held[i].ItemId : 0;
    }

    private bool IsHeld(long itemId)
    {
        if (Held is not { Count: > 0 } held) return false;
        foreach (var h in held) if (h.ItemId == itemId) return true; // poucos fora de casa ao mesmo tempo
        return false;
    }

    /// <summary>A pessoa sai com o item (<paramref name="itemId"/> &gt; 0) ou o devolve à casa (0).</summary>
    internal void Hold(long npcId, long itemId)
    {
        if (itemId == 0 && _held is not { Count: > 0 }) return;
        _held ??= [];
        int i = HeldIndex(_held, npcId);
        if (itemId == 0) { if (i >= 0) { _held.RemoveAt(i); CountEntry(npcId, -1); } }
        else if (i >= 0) _held[i] = new HeldItem(npcId, itemId);
        else { _held.Insert(~i, new HeldItem(npcId, itemId)); CountEntry(npcId, 1); }
        if (_held.Count == 0) _held = null;
        Version++;
    }

    private static int HeldIndex(List<HeldItem> held, long npcId)
    {
        int lo = 0, hi = held.Count - 1;
        while (lo <= hi)
        {
            int mid = (lo + hi) >> 1;
            long id = held[mid].NpcId;
            if (id == npcId) return mid;
            if (id < npcId) lo = mid + 1; else hi = mid - 1;
        }
        return ~lo;
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
    /// e a função (0 = travessia, 1 = carregar, 2 = levar por terra).</summary>
    internal int ListingsAt(long marketId, byte function = 0)
    {
        if (_listingsByMarket is null)
        {
            _listingsByMarket = [];
            foreach (var item in Items)
                if (item.MarketId is { } m)
                {
                    long key = m * 4 + FunctionOf(item.DesignId);
                    _listingsByMarket[key] = _listingsByMarket.GetValueOrDefault(key) + 1;
                }
        }
        return _listingsByMarket.GetValueOrDefault(marketId * 4 + function);
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

    /// <summary>Tudo o que as pessoas levam de um recurso (AD-157b): é estoque do mundo, na carga de quem saiu, e entra na conservação.</summary>
    public long CarriedTotal(int resourceId)
    {
        long total = 0;
        foreach (var load in Carried) if (load.ResourceId == resourceId) total += load.Quantity;
        return total;
    }

    /// <summary>Soma (ou tira) unidades do que a pessoa leva; linha vazia sai.</summary>
    internal void Carry(long npcId, int resourceId, int delta)
    {
        if (delta == 0) return;
        int i = CarriedIndex(npcId, resourceId);
        int now = (i >= 0 ? Carried[i].Quantity : 0) + delta;
        if (now <= 0) { if (i >= 0) { Carried.RemoveAt(i); CountEntry(npcId, -1); } }
        else if (i >= 0) Carried[i] = Carried[i] with { Quantity = now };
        else { Carried.Insert(~i, new CarriedLoad(npcId, resourceId, now)); CountEntry(npcId, 1); }
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

/// <summary>Uma resposta guardada do alcance da despensa (AD-157): onde a casa estava, a cidade, as peças e a tecnologia de então.</summary>
internal readonly record struct ReachKnown(LivingWorld.Domain.Geography.CellCoord Home, CityId City, long Touches, int Bonus, bool Out);

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
    public long AteBeforeLeaving, DrankBeforeLeaving, SupperBeforeSleep, DrankBeforeSleep, MealTimeAtHome, WorkBreakMeals; // AD-157b: o desjejum e a ceia
    public long HomeForMealFromWork, HomeForMealFromOtherPlace, HomeForMealThirsty, HomeForMealFromOtherHome, HomeForMealSocial, HomeForMealIdle, HomeForMealTravel; // AD-157b: só diagnóstico
    public long HomeForMeal, CarrierCargoTrips, CarrierCargoGrams, CargoFrustrations, AwayFrustrations, StarvedAway, DehydratedAway;
    // AD-153: levar por terra
    public long LandCargoCapped, LandCargoGramsCut, HaulAttempts, HaulInventions, HaulPrototypeFailures, HaulTaught, HaulBuilt, HaulSales;
    public long HaulTrips, HaulGramsBeyondBody, HaulDrag, HaulSkid, HaulRoll;
    public long HaulerRouteBlocked; // AD-176: compras em que a rota tinha terreno sem caminho demais para o veículo de terra
    // AD-168: o espólio de quem morre longe (deixados, mortos em casa com carga, idas para buscar, recolhidos pela casa, achados sem
    // dono, furtados; unidades deixadas, recolhidas, furtadas e estragadas)
    public long LeavingsLeft, LeavingsAtHome, LeavingsFetches, LeavingsCollected, LeavingsFound, LeavingsStolen;
    public long LeftUnits, CollectedUnits, StolenUnits, SpoiledUnits;
}
