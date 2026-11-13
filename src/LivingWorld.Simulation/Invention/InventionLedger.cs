using System.Text.Json.Serialization;
using LivingWorld.Domain.Invention;

namespace LivingWorld.Simulation.Invention;

/// <summary>Estado de quem quer resolver a água (AD-141): frustração vivida em minutos perdidos (com o tick da última conta, decai por
/// mês), até quando está ocupado num protótipo ou numa construção, tentativas e falhas, a última falha e o protótipo que falhou
/// (de onde a próxima tentativa parte) e o material em que deixou de confiar (o que a falha ensinou).</summary>
public readonly record struct InventionMind(
    long NpcId, int FrustrationMinutes, long BusyUntilTick, int Attempts, int Failures, FailureMode LastFailure,
    string? LastTried, int DistrustedMaterial);

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

    private Dictionary<int, Design>? _designById;
    private Dictionary<long, int>? _bestCraftByHousehold;
    private readonly Dictionary<int, Performance> _performance = [];

    [JsonIgnore] internal long Version { get; private set; }

    internal void Touch()
    {
        Version++;
        _designById = null;
        _bestCraftByHousehold = null;
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

    /// <summary>Velocidade (mm/s) da melhor embarcação que funciona do domicílio; 0 = nenhuma.</summary>
    internal int BestCraftSpeed(long householdId, InventionRules rules)
    {
        if (Items.Count == 0) return 0;
        if (_bestCraftByHousehold is null)
        {
            _bestCraftByHousehold = [];
            foreach (var item in Items)
            {
                if (FindDesign(item.DesignId) is not { } design) continue;
                var perf = PerformanceOf(design, rules);
                if (!perf.Works) continue;
                int speed = (int)((long)perf.SpeedMmS * item.QualityPm / 1000);
                if (speed > _bestCraftByHousehold.GetValueOrDefault(item.HouseholdId)) _bestCraftByHousehold[item.HouseholdId] = speed;
            }
        }
        return _bestCraftByHousehold.GetValueOrDefault(householdId);
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
}
