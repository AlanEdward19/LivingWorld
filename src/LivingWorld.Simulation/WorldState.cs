using LivingWorld.Domain;

namespace LivingWorld.Simulation;

/// <summary>Estado do mundo — tudo que precisa sobreviver a um snapshot (task 7). Controles de
/// host (pausa, velocidade) ficam fora de propósito: são estado do hospedeiro, não do mundo
/// (ver <see cref="SimulationHost"/>).</summary>
public sealed class WorldState
{
    private readonly WorldRngRegistry _rng;
    private readonly EventScheduler _scheduler;
    private long _nextEventId;

    [Canonical] public WorldCalendar Calendar { get; }
    [Canonical] public WorldDate CurrentDate { get; internal set; }
    [Canonical] public long NextEventId => _nextEventId;

    /// <summary>Linha temporal deste mundo (ADR-0009). O hash canônico é por branch: dois
    /// branches com conteúdo idêntico têm hashes distintos.</summary>
    [Canonical] public BranchId BranchId { get; }

    /// <summary>Seed raiz do mundo. Precisa sobreviver ao snapshot: sem ela, um stream de RNG
    /// pedido pela primeira vez depois de uma rehidratação derivaria de uma raiz diferente.</summary>
    [Canonical] public ulong Seed { get; }

    [Canonical]
    public IReadOnlyList<RngStreamState> RngStreams => _rng.Snapshot();

    [Canonical]
    public IReadOnlyList<ScheduledEvent> PendingEvents => _scheduler.Snapshot();

    /// <summary>Geografia do mundo (Fase 2) — grid, regiões, custo de deslocamento. Entra no
    /// hash canônico: mudar o mapa muda o mundo (task 7).</summary>
    [Canonical] public WorldMap Map { get; }

    /// <summary>Ids de cultura/profissão/tipo-de-local válidos para este cenário (Fase 3, task 7)
    /// — decide quem pode nascer com qual cultura e barra literal de conteúdo em C#.</summary>
    [Canonical] public PopulationCatalog PopulationCatalog { get; }

    /// <summary>Tabela de vida e janela de fertilidade do cenário (Fase 3, task 5) — alimenta
    /// toda decisão de nascimento e morte.</summary>
    [Canonical] public PopulationRules PopulationRules { get; }

    /// <summary>Parâmetros cenário-driven do utility AI/necessidades (Fase 4, task 4/9) —
    /// decaimento, limiar de urgência, histerese. Entra no hash canônico: desligar/mudar o
    /// utility AI muda o mundo (NEEDS-04).</summary>
    [Canonical] public NeedsRules NeedsRules { get; }

    /// <summary>Catálogo de ações e rotina diária do cenário (Fase 4, task 2/9).</summary>
    [Canonical] public ActionCatalog ActionCatalog { get; }

    /// <summary>Limiares de idade que resolvem <see cref="LifeStage"/> do cenário (Fase 4, task
    /// 3/12) — nunca hardcoded (R3). Necessário para a rotina diária (<see cref="BehaviorDecisionSystem"/>)
    /// resolver `(Profession, LifeStage, hora)`.</summary>
    [Canonical] public LifeStageRules LifeStageRules { get; }

    private readonly List<Npc> _npcs;
    private readonly Dictionary<NpcId, Npc> _npcById;
    private readonly List<Household> _households;
    private readonly Dictionary<HouseholdId, Household> _householdById;
    private long _nextNpcId;
    private long _nextHouseholdId;

    private Money _moneyMinted;
    private Money _moneyDestroyed;

    /// <summary>Massa monetária cunhada desde a origem do mundo (Fase 5, ECON-26/27) — nunca
    /// alterada implicitamente por transação/salário, só por <see cref="Mint"/> nomeado.</summary>
    [Canonical] public Money MoneyMinted => _moneyMinted;

    /// <summary>Massa monetária destruída desde a origem do mundo (Fase 5, ECON-26/27) — a
    /// invariante de conservação é <c>saldo_total == inicial + MoneyMinted - MoneyDestroyed</c>.</summary>
    [Canonical] public Money MoneyDestroyed => _moneyDestroyed;

    /// <summary>Todo NPC já existiu, vivo ou morto (Fase 3) — referência histórica não pode
    /// virar ponteiro solto (critério "nenhum evento após tick de morte referencia o NPC" exige
    /// que o NPC continue existindo para o sweep referencial provar isso).</summary>
    [Canonical] public IReadOnlyList<Npc> Npcs => _npcs;

    /// <summary>Households vivos — dissolvido (<see cref="Household.IsEmpty"/>) sai da lista
    /// (task 3).</summary>
    [Canonical] public IReadOnlyList<Household> Households => _households;

    [Canonical] public long NextNpcId => _nextNpcId;
    [Canonical] public long NextHouseholdId => _nextHouseholdId;

    /// <summary>Contador do sistema de exemplo (task 11) — descartável na Fase 3. Nenhuma
    /// decisão lê este campo, por isso é volátil.</summary>
    [Volatile]
    public IReadOnlyDictionary<TickFrequency, long> ExampleTickCounts => _exampleTickCounts;

    private readonly Dictionary<TickFrequency, long> _exampleTickCounts = new()
    {
        [TickFrequency.Hourly] = 0,
        [TickFrequency.Daily] = 0,
        [TickFrequency.Monthly] = 0,
        [TickFrequency.Yearly] = 0,
    };

    public WorldState(
        WorldCalendar calendar, ulong seed, WorldMap map,
        PopulationCatalog populationCatalog, PopulationRules populationRules,
        NeedsRules needsRules, ActionCatalog actionCatalog, LifeStageRules lifeStageRules, BranchId branchId = default)
    {
        Calendar = calendar;
        CurrentDate = WorldDate.Epoch(calendar);
        Seed = seed;
        Map = map;
        PopulationCatalog = populationCatalog;
        PopulationRules = populationRules;
        NeedsRules = needsRules;
        ActionCatalog = actionCatalog;
        LifeStageRules = lifeStageRules;
        BranchId = branchId;
        _rng = new WorldRngRegistry(seed);
        _scheduler = new EventScheduler();
        _npcs = [];
        _npcById = [];
        _households = [];
        _householdById = [];
    }

    /// <summary>Reconstrói a partir de um snapshot (task 7/8) — rehidratação.</summary>
    public WorldState(
        WorldCalendar calendar,
        WorldDate currentDate,
        ulong seed,
        WorldMap map,
        PopulationCatalog populationCatalog,
        PopulationRules populationRules,
        NeedsRules needsRules,
        ActionCatalog actionCatalog,
        LifeStageRules lifeStageRules,
        IReadOnlyList<RngStreamState> rngStreams,
        IReadOnlyList<ScheduledEvent> pendingEvents,
        long nextEventId,
        IReadOnlyDictionary<TickFrequency, long> exampleTickCounts,
        IReadOnlyList<Npc> npcs,
        IReadOnlyList<Household> households,
        long nextNpcId,
        long nextHouseholdId,
        BranchId branchId = default,
        Money moneyMinted = default,
        Money moneyDestroyed = default)
    {
        Calendar = calendar;
        CurrentDate = currentDate;
        Seed = seed;
        Map = map;
        PopulationCatalog = populationCatalog;
        PopulationRules = populationRules;
        NeedsRules = needsRules;
        ActionCatalog = actionCatalog;
        LifeStageRules = lifeStageRules;
        BranchId = branchId;
        _rng = new WorldRngRegistry(seed, rngStreams);
        _scheduler = new EventScheduler(pendingEvents);
        _nextEventId = nextEventId;
        _exampleTickCounts = new Dictionary<TickFrequency, long>(exampleTickCounts);
        _npcs = npcs.ToList();
        _npcById = ToLookup(_npcs, n => n.Id);
        _households = households.ToList();
        _householdById = ToLookup(_households, h => h.Id);
        _nextNpcId = nextNpcId;
        _nextHouseholdId = nextHouseholdId;
        _moneyMinted = moneyMinted;
        _moneyDestroyed = moneyDestroyed;
    }

    internal WorldRngRegistry Rng => _rng;
    internal EventScheduler Scheduler => _scheduler;

    internal long NextEventIdAndAdvance() => _nextEventId++;

    internal void IncrementExampleCount(TickFrequency frequency) => _exampleTickCounts[frequency]++;

    internal NpcId NextNpcIdAndAdvance() => new(_nextNpcId++);
    internal HouseholdId NextHouseholdIdAndAdvance() => new(_nextHouseholdId++);

    /// <summary>Sincroniza o contador depois de um lote gerado fora do tick (seed inicial),
    /// que consome ids diretamente do <see cref="PopulationGenerator"/> em vez de um por vez.</summary>
    internal void AdvanceNpcIdTo(long value) => _nextNpcId = Math.Max(_nextNpcId, value);
    internal void AdvanceHouseholdIdTo(long value) => _nextHouseholdId = Math.Max(_nextHouseholdId, value);

    internal void AddNpc(Npc npc)
    {
        _npcs.Add(npc);
        _npcById[npc.Id] = npc;
    }

    internal void AddHousehold(Household household)
    {
        _households.Add(household);
        _householdById[household.Id] = household;
    }

    /// <summary>Household sem membros é dissolvido (task 3) — sai da lista canônica.</summary>
    internal void RemoveHousehold(HouseholdId id)
    {
        _households.RemoveAll(h => h.Id == id);
        _householdById.Remove(id);
    }

    internal Npc? FindNpc(NpcId id) => _npcById.GetValueOrDefault(id);
    internal Household? FindHousehold(HouseholdId id) => _householdById.GetValueOrDefault(id);

    /// <summary>Cunhagem explícita e rara (task 10) — nunca chamada implicitamente por
    /// <see cref="MarketTransaction"/>/salário, só por um evento nomeado (AD-042).</summary>
    public void Mint(TickContext ctx, Money amount, string reason)
    {
        _moneyMinted += amount;
        ctx.LogEvent(WorldEventKind.Minted, $"{amount.Amount}|{reason}");
    }

    /// <summary>Destruição explícita e rara — falha (mesmo padrão de <see
    /// cref="Money.TryDebit"/>) se exceder a massa monetária líquida já cunhada
    /// (<see cref="MoneyMinted"/> - <see cref="MoneyDestroyed"/>), nunca altera o contador nesse
    /// caso.</summary>
    public Result<Unit> Destroy(TickContext ctx, Money amount, string reason)
    {
        var netSupply = _moneyMinted.Amount - _moneyDestroyed.Amount;
        if (amount.Amount > netSupply)
            return Result<Unit>.Fail("insufficient_money_supply");

        _moneyDestroyed += amount;
        ctx.LogEvent(WorldEventKind.Destroyed, $"{amount.Amount}|{reason}");
        return Result<Unit>.Ok(Unit.Value);
    }

    /// <summary>Último-vence em vez de <c>ToDictionary</c> (que reprova em id duplicado) — a
    /// entrada de borda (JSON) já valida unicidade; aqui só reidrata.</summary>
    private static Dictionary<TKey, TValue> ToLookup<TValue, TKey>(IEnumerable<TValue> values, Func<TValue, TKey> keyOf)
        where TKey : notnull
    {
        var dict = new Dictionary<TKey, TValue>();
        foreach (var value in values)
            dict[keyOf(value)] = value;
        return dict;
    }
}
