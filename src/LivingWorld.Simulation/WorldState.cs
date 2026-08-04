using LivingWorld.Domain;
using LivingWorld.Domain.Llm;

using LivingWorld.Simulation.History;
using LivingWorld.Simulation.Population;

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

    /// <summary>Parâmetros cenário-driven da economia (Fase 5, T2) — capacidade, perda, salário,
    /// preço. <see cref="EconomyRules.Enabled"/> falso equivale a "economia não existe" (ECON-05:
    /// desligar muda o hash porque as coleções abaixo ficam sempre vazias nesse cenário).</summary>
    [Canonical] public EconomyRules EconomyRules { get; }

    /// <summary>Recipe de produção por local e vínculo profissão→local do cenário (Fase 5, T3).</summary>
    [Canonical] public EconomyCatalog EconomyCatalog { get; }

    /// <summary>Todo peso/limiar/duração/flag da Fase 7 (Relações e Famílias, T4) — mesmo grupo de
    /// <see cref="NeedsRules"/>/<see cref="EconomyRules"/>: cenário-driven, entra no hash canônico
    /// porque decide todo evento de relação/cortejo/concepção.</summary>
    [Canonical] public FamilyRules FamilyRules { get; }

    // SPEC_DEVIATION (Fase 8, T9): design.md/tasks.md pressupõem `world.CityRules`/
    // `world.CityCatalog` (todo sistema da Fase 8 lê threshold/receita por eles), mas T1-T8
    // (Foundation) nunca os wireou em WorldState — só criaram os tipos e o loader. Sem isso,
    // nenhum sistema de T9 em diante teria como ler CityRules.Enabled/limiares. Mesmo padrão de
    // EconomyRules/EconomyCatalog acima.

    /// <summary>Todo limiar/peso/duração de crescimento/migração/fundação/materialização da Fase
    /// 8 (T2) — cenário-driven, mesmo grupo de <see cref="FamilyRules"/>.</summary>
    [Canonical] public CityRules CityRules { get; }

    /// <summary>Receita de construção por tipo de edifício da Fase 8 (T3).</summary>
    [Canonical] public CityCatalog CityCatalog { get; }

    /// <summary>Tetos de custo e arquivamento frio (Fase 9, PERF-03) — cenário-driven.</summary>
    [Canonical] public PerfRules PerfRules { get; }

    /// <summary>Parâmetros cenário-driven da história degradável (Fase 10, HIST-08) — limiar,
    /// cânone, meios e distorção. <see cref="HistoryRules.Enabled"/> falso equivale a "história
    /// desligada".</summary>
    [Canonical] public HistoryRules HistoryRules { get; }

    private readonly List<Fact> _facts;
    private long _nextFactId;

    /// <summary>Fatos imutáveis do esqueleto (Fase 10, HIST-01) — append-only na simulação.</summary>
    [Canonical] public IReadOnlyList<Fact> Facts => _facts;

    [Canonical] public long NextFactId => _nextFactId;

    private long _nextReportId;

    [Canonical] public long NextReportId => _nextReportId;

    private readonly List<ReportState> _reports;
    private readonly List<Book> _books;
    private long _nextBookId;

    /// <summary>Relatos registrados no mundo (Fase 10) — inclui relatos fora do cânone vivo
    /// (ex.: referenciados por <see cref="Books"/> após despejo).</summary>
    [Canonical] public IReadOnlyList<ReportState> Reports => _reports;

    /// <summary>Livros como objetos do mundo (Fase 10, HIST-09).</summary>
    [Canonical] public IReadOnlyList<Book> Books => _books;

    [Canonical] public long NextBookId => _nextBookId;

    private readonly List<NpcMemory> _canonicalMemories;
    private readonly List<NpcMemory> _volatileMemories;
    private long _nextMemoryId;

    /// <summary>Memórias de NPC (Fase 11, roadmap itens 1/2) com importância >= limiar do
    /// cenário no momento em que foram registradas (ADR-0014) — alimenta <c>Recall</c> e o
    /// prompt da LLM, por isso decide.</summary>
    [Canonical] public IReadOnlyList<NpcMemory> CanonicalMemories => _canonicalMemories;

    /// <summary>Memórias abaixo do limiar canônico — compactáveis livremente (T10, futuro) sem
    /// tocar o hash canônico.</summary>
    [Volatile] public IReadOnlyList<NpcMemory> VolatileMemories => _volatileMemories;

    [Canonical] public long NextMemoryId => _nextMemoryId;

    [Volatile] public AliveNpcIndex AliveNpcIndex { get; private set; }

    [Volatile] public HistoryIndex HistoryIndex { get; private set; }

    private readonly List<Npc> _npcWakeBatch = [];
    private readonly Dictionary<long, long> _npcWakeEventIdByNpc = [];

    /// <summary>NPCs que acordam neste tick (PERF-08) — derivado, fora do hash canônico.</summary>
    [Volatile] public IReadOnlyList<Npc> NpcWakeBatch => _npcWakeBatch;

    [Volatile] public ColdTierArchive ColdArchive { get; private set; }

    private readonly Dictionary<RelationshipKey, Relationship> _relationships;

    /// <summary>Uma <see cref="Relationship"/> por par ordenado, criada sob demanda (Fase 7, T8,
    /// AD-052: "quem nunca se encontra nunca se conhece" — FAM-02). Este dicionário **é** a
    /// coleção canônica, sem lista paralela: não há iteração ordenada por id sequencial fazendo
    /// sentido para um par (quem precisar de determinismo ordena por
    /// <c>(From.Value, To.Value)</c> na hora, ex. <c>RelationshipSystem</c>/hash).</summary>
    [Canonical] public IReadOnlyDictionary<RelationshipKey, Relationship> Relationships => _relationships;

    /// <summary>Único ponto de criação de uma <see cref="Relationship"/> (AD-052) — cria só na
    /// primeira chamada para a chave; chamadas seguintes devolvem a mesma instância.</summary>
    internal Relationship GetOrCreateRelationship(RelationshipKey key, long now)
    {
        if (!_relationships.TryGetValue(key, out var relationship))
        {
            relationship = Relationship.Initial(now);
            _relationships[key] = relationship;
        }
        return relationship;
    }

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

    private readonly Dictionary<ResourceType, long> _resourceProduced = [];
    private readonly Dictionary<ResourceType, long> _resourceConsumed = [];

    /// <summary>Contador auditável de produção bruta por recurso desde a origem do mundo (Fase 5,
    /// T24, ECON-15) — nunca influencia decisão nenhuma (por isso <see cref="VolatileAttribute"/>,
    /// não entra no hash canônico), só sustenta a invariante
    /// <c>produzido == consumido + estocado + perdido</c> que os testes de conservação checam.
    /// Incrementado por <c>ProductionSystem</c> a cada depósito bem-sucedido.</summary>
    [Volatile] public IReadOnlyDictionary<ResourceType, long> ResourceProduced => _resourceProduced;

    /// <summary>Contador auditável de consumo (destruição real do recurso, não transferência de
    /// estoque — <c>Buy</c> move estoque entre Workplace/Household e não conta aqui) desde a
    /// origem do mundo. Incrementado por <c>BehaviorDecisionSystem.ApplyEat</c> a cada retirada
    /// bem-sucedida.</summary>
    [Volatile] public IReadOnlyDictionary<ResourceType, long> ResourceConsumed => _resourceConsumed;

    public void RecordResourceProduced(ResourceType resource, long amount) =>
        _resourceProduced[resource] = _resourceProduced.GetValueOrDefault(resource) + amount;

    public void RecordResourceConsumed(ResourceType resource, long amount) =>
        _resourceConsumed[resource] = _resourceConsumed.GetValueOrDefault(resource) + amount;

    /// <summary>Todo NPC já existiu, vivo ou morto (Fase 3) — referência histórica não pode
    /// virar ponteiro solto (critério "nenhum evento após tick de morte referencia o NPC" exige
    /// que o NPC continue existindo para o sweep referencial provar isso).</summary>
    [Canonical] public IReadOnlyList<Npc> Npcs => _npcs;

    /// <summary>Households vivos — dissolvido (<see cref="Household.IsEmpty"/>) sai da lista
    /// (task 3).</summary>
    [Canonical] public IReadOnlyList<Household> Households => _households;

    [Canonical] public long NextNpcId => _nextNpcId;
    [Canonical] public long NextHouseholdId => _nextHouseholdId;

    private readonly List<Workplace> _workplaces;
    private readonly Dictionary<WorkplaceId, Workplace> _workplaceById;
    private long _nextWorkplaceId;

    /// <summary>Local de produção/estoque/mercado (Fase 5, T4) — mesmo molde de <see
    /// cref="Households"/> (lista + dono canônico).</summary>
    [Canonical] public IReadOnlyList<Workplace> Workplaces => _workplaces;

    [Canonical] public long NextWorkplaceId => _nextWorkplaceId;

    private readonly List<City> _cities;
    private readonly Dictionary<CityId, City> _cityById;
    private readonly List<Building> _buildings;
    private readonly Dictionary<BuildingId, Building> _buildingById;
    private long _nextBuildingId;

    /// <summary>Cidade (Fase 8, T5) — mesmo molde de <see cref="Households"/>/<see
    /// cref="Workplaces"/> (lista + dono canônico).</summary>
    [Canonical] public IReadOnlyList<City> Cities => _cities;

    /// <summary>Edifício concluído (Fase 8, T5) — mesmo molde de <see cref="Cities"/>.</summary>
    [Canonical] public IReadOnlyList<Building> Buildings => _buildings;

    /// <summary><see cref="BuildingId"/> é monotônico como <see cref="WorkplaceId"/> — só este
    /// contador precisa sobreviver ao snapshot. <see cref="CityId"/>/<see cref="LocationId"/> não
    /// têm contador: nascem do stream de RNG dedicado (<see cref="NextCityId"/>), já coberto pelo
    /// snapshot de <see cref="RngStreams"/>.</summary>
    [Canonical] public long NextBuildingId => _nextBuildingId;

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
        NeedsRules needsRules, ActionCatalog actionCatalog, LifeStageRules lifeStageRules, BranchId branchId = default,
        EconomyRules? economyRules = null, EconomyCatalog? economyCatalog = null, FamilyRules? familyRules = null,
        CityRules? cityRules = null, CityCatalog? cityCatalog = null, PerfRules? perfRules = null,
        HistoryRules? historyRules = null)
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
        EconomyRules = economyRules ?? EconomyRules.Disabled;
        EconomyCatalog = economyCatalog ?? EconomyCatalog.Empty;
        FamilyRules = familyRules ?? FamilyRules.Disabled;
        CityRules = cityRules ?? CityRules.Disabled;
        CityCatalog = cityCatalog ?? CityCatalog.Empty;
        PerfRules = perfRules ?? PerfRules.Default;
        HistoryRules = historyRules ?? HistoryRules.Disabled;
        _facts = [];
        _reports = [];
        _books = [];
        _canonicalMemories = [];
        _volatileMemories = [];
        _rng = new WorldRngRegistry(seed);
        _scheduler = new EventScheduler();
        _npcs = [];
        _npcById = [];
        _households = [];
        _householdById = [];
        _workplaces = [];
        _workplaceById = [];
        _relationships = [];
        _cities = [];
        _cityById = [];
        _buildings = [];
        _buildingById = [];
        AliveNpcIndex = AliveNpcIndex.RebuildFrom(this);
        HistoryIndex = HistoryIndex.RebuildFrom(this);
        ColdArchive = new ColdTierArchive();
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
        Money moneyDestroyed = default,
        EconomyRules? economyRules = null,
        EconomyCatalog? economyCatalog = null,
        IReadOnlyList<Workplace>? workplaces = null,
        long nextWorkplaceId = 0,
        FamilyRules? familyRules = null,
        IReadOnlyDictionary<RelationshipKey, Relationship>? relationships = null,
        IReadOnlyList<City>? cities = null,
        IReadOnlyList<Building>? buildings = null,
        long nextBuildingId = 0,
        CityRules? cityRules = null,
        CityCatalog? cityCatalog = null,
        PerfRules? perfRules = null,
        HistoryRules? historyRules = null,
        IReadOnlyList<Fact>? facts = null,
        long nextFactId = 0,
        long nextReportId = 0,
        IReadOnlyList<ReportState>? reports = null,
        IReadOnlyList<Book>? books = null,
        long nextBookId = 0,
        IReadOnlyList<NpcMemory>? canonicalMemories = null,
        IReadOnlyList<NpcMemory>? volatileMemories = null,
        long nextMemoryId = 0)
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
        EconomyRules = economyRules ?? EconomyRules.Disabled;
        EconomyCatalog = economyCatalog ?? EconomyCatalog.Empty;
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
        _workplaces = (workplaces ?? []).ToList();
        _workplaceById = ToLookup(_workplaces, w => w.Id);
        _nextWorkplaceId = nextWorkplaceId;
        FamilyRules = familyRules ?? FamilyRules.Disabled;
        _relationships = relationships is null ? [] : new Dictionary<RelationshipKey, Relationship>(relationships);
        _cities = (cities ?? []).ToList();
        _cityById = ToLookup(_cities, c => c.Id);
        _buildings = (buildings ?? []).ToList();
        _buildingById = ToLookup(_buildings, b => b.Id);
        _nextBuildingId = nextBuildingId;
        CityRules = cityRules ?? CityRules.Disabled;
        CityCatalog = cityCatalog ?? CityCatalog.Empty;
        PerfRules = perfRules ?? PerfRules.Default;
        HistoryRules = historyRules ?? HistoryRules.Disabled;
        _facts = (facts ?? []).ToList();
        _nextFactId = nextFactId;
        _nextReportId = nextReportId;
        _reports = (reports ?? []).ToList();
        _books = (books ?? []).ToList();
        _nextBookId = nextBookId;
        _canonicalMemories = (canonicalMemories ?? []).ToList();
        _volatileMemories = (volatileMemories ?? []).ToList();
        _nextMemoryId = nextMemoryId;
        AliveNpcIndex = AliveNpcIndex.RebuildFrom(this);
        HistoryIndex = HistoryIndex.RebuildFrom(this);
        ColdArchive = new ColdTierArchive();
    }

    internal WorldRngRegistry Rng => _rng;
    internal EventScheduler Scheduler => _scheduler;

    internal long NextEventIdAndAdvance() => _nextEventId++;

    internal void IncrementExampleCount(TickFrequency frequency) => _exampleTickCounts[frequency]++;

    internal void ClearNpcWakeBatch() => _npcWakeBatch.Clear();

    internal void AddNpcWake(Npc npc) => _npcWakeBatch.Add(npc);

    internal void ReplaceNpcWake(TickContext ctx, long npcId, long targetTick)
    {
        if (_npcWakeEventIdByNpc.TryGetValue(npcId, out var oldId))
            _scheduler.Cancel(oldId);

        var evt = ctx.ScheduleEvent(targetTick, Behavior.NpcWakeScheduler.SystemName, npcId.ToString());
        _npcWakeEventIdByNpc[npcId] = evt.Id;
    }

    internal void ClearNpcWakeEvent(long npcId) => _npcWakeEventIdByNpc.Remove(npcId);

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
        AliveNpcIndex.OnBorn(npc);
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

    // SPEC_DEVIATION (Fase 8, T9): design.md não previa remover uma linha de Npc — mas
    // Dematerialize (approach A) exige exatamente isso ("remover a linha do store"). Mirror de
    // RemoveHousehold; só MaterializationSystem chama.
    internal void RemoveNpc(NpcId id)
    {
        if (_npcById.TryGetValue(id, out var npc) && npc.IsAlive)
            AliveNpcIndex.OnDied(npc);
        _npcs.RemoveAll(n => n.Id == id);
        _npcById.Remove(id);
    }

    internal Npc? FindNpc(NpcId id) => _npcById.GetValueOrDefault(id);
    internal Household? FindHousehold(HouseholdId id) => _householdById.GetValueOrDefault(id);

    internal WorkplaceId NextWorkplaceIdAndAdvance() => new(_nextWorkplaceId++);

    public void AddWorkplace(Workplace workplace)
    {
        _workplaces.Add(workplace);
        _workplaceById[workplace.Id] = workplace;
    }

    public Workplace? FindWorkplace(WorkplaceId id) => _workplaceById.GetValueOrDefault(id);

    internal BuildingId NextBuildingIdAndAdvance() => new(_nextBuildingId++);

    public void AddCity(City city)
    {
        _cities.Add(city);
        _cityById[city.Id] = city;
    }

    public void AddBuilding(Building building)
    {
        _buildings.Add(building);
        _buildingById[building.Id] = building;
    }

    public City? FindCity(CityId id) => _cityById.GetValueOrDefault(id);
    public Building? FindBuilding(BuildingId id) => _buildingById.GetValueOrDefault(id);

    internal FactId NextFactIdAndAdvance() => new(_nextFactId++);

    internal ReportId NextReportIdAndAdvance() => new(_nextReportId++);

    internal BookId NextBookIdAndAdvance() => new(_nextBookId++);

    internal void RegisterReport(ReportState report)
    {
        _reports.Add(report);
        HistoryIndex.OnReportAdded(report, this);
    }

    internal ReportState? FindReport(ReportId id)
    {
        foreach (var report in _reports)
        {
            if (report.Id == id)
                return report;
        }
        return null;
    }

    internal void AddBook(Book book) => _books.Add(book);

    internal Book? FindBook(BookId id)
    {
        foreach (var book in _books)
        {
            if (book.Id == id)
                return book;
        }
        return null;
    }

    internal void ReplaceBook(Book updated)
    {
        for (int i = 0; i < _books.Count; i++)
        {
            if (_books[i].Id == updated.Id)
            {
                _books[i] = updated;
                return;
            }
        }
    }

    internal void AddFact(Fact fact)
    {
        _facts.Add(fact);
        HistoryIndex.OnFactAdded(fact);
    }

    internal Fact? FindFact(FactId id)
    {
        foreach (var fact in _facts)
        {
            if (fact.Id == id)
                return fact;
        }
        return null;
    }

    /// <summary>Único ponto de criação de <see cref="NpcMemory"/> (Fase 11, roadmap itens 1/2)
    /// — a classificação canônico/volátil (ADR-0014) é decidida aqui, uma vez, contra
    /// <paramref name="canonicalImportanceThreshold"/> (vem de <c>LlmRules</c> do cenário no
    /// chamador, mesmo padrão de <c>ConversationAvailabilityPolicy</c>: a regra nunca mora em
    /// <see cref="WorldState"/>, só o efeito de aplicá-la).</summary>
    internal void AddNpcMemory(
        NpcId ownerId, MemoryCategory category, string content, int importance, long originTick,
        IReadOnlyList<NpcId> participants, CellCoord location, int canonicalImportanceThreshold)
    {
        var memory = new NpcMemory(_nextMemoryId++, ownerId, category, content, importance, originTick, participants, location);
        if (importance >= canonicalImportanceThreshold)
            _canonicalMemories.Add(memory);
        else
            _volatileMemories.Add(memory);
    }

    /// <summary>Deriva um <see cref="CityId"/> novo a partir do stream de RNG dedicado
    /// <c>"city-founding"</c> (Fase 8, T5) — <c>Guid.NewGuid()</c> é banido em Domain/Simulation
    /// (rules/simulation-determinism.md); mesma seed produz sempre o mesmo <see cref="CityId"/>
    /// na mesma posição do stream.</summary>
    public CityId NextCityId() => new(NextGuidFromRng());

    /// <summary>Espelha <see cref="NextCityId"/> para <see cref="LocationId"/> — mesmo stream
    /// dedicado, próximo par de sorteios.</summary>
    public LocationId NextLocationId() => new(NextGuidFromRng());

    private Guid NextGuidFromRng()
    {
        var rng = _rng.Stream("city-founding");
        var bytes = new byte[16];
        BitConverter.GetBytes(rng.NextDouble()).CopyTo(bytes, 0);
        BitConverter.GetBytes(rng.NextDouble()).CopyTo(bytes, 8);
        return new Guid(bytes);
    }

    /// <summary>Cunhagem explícita e rara (task 10) — nunca chamada implicitamente por
    /// <see cref="MarketTransaction"/>/salário, só por um evento nomeado (AD-042).</summary>
    public void Mint(TickContext ctx, Money amount, string reason)
    {
        _moneyMinted += amount;
        ctx.LogEvent(WorldEventKind.Minted, $"{amount.Amount}|{reason}");
    }

    /// <summary>Retira massa da circulação (ex.: arquivo frio) — incrementa
    /// <see cref="MoneyDestroyed"/> sem o teto de <see cref="Destroy"/> (que só desfaz cunhagem).</summary>
    internal void BurnCirculatingMoney(TickContext ctx, Money amount, string reason)
    {
        if (amount.Amount <= 0) return;
        _moneyDestroyed += amount;
        ctx.LogEvent(WorldEventKind.Destroyed, $"{amount.Amount}|{reason}");
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
