using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Cities;
using LivingWorld.Domain.Cities.Buildings;
using LivingWorld.Domain.Cognition;
using LivingWorld.Domain.Ecology;
using LivingWorld.Domain.Economy;
using LivingWorld.Domain.Extraordinary;
using LivingWorld.Domain.Fauna;
using LivingWorld.Domain.Flora;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Geography.Map;
using LivingWorld.Domain.Geography.Spatial;
using LivingWorld.Domain.History;
using LivingWorld.Domain.History.Distortion;
using LivingWorld.Domain.Llm;
using LivingWorld.Domain.Performance;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Body;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Behavior.Decision;
using LivingWorld.Simulation.Behavior.Needs;
using LivingWorld.Simulation.Extraordinary.Mechanics;
using LivingWorld.Simulation.History;
using LivingWorld.Simulation.Hosting;
using LivingWorld.Simulation.Observation;
using LivingWorld.Simulation.Population.Archive;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Simulation.Scheduling;
using LivingWorld.Simulation.Snapshot;

namespace LivingWorld.Simulation.Core;

/// <summary>Estado do mundo — tudo que precisa sobreviver a um snapshot (task 7). Controles de
/// host (pausa, velocidade) ficam fora de propósito: são estado do hospedeiro, não do mundo
/// (ver <see cref="SimulationHost"/>).</summary>
public sealed class WorldState
{
    private readonly WorldRngRegistry _rng;
    private readonly EventScheduler _scheduler;
    private long _nextEventId;
    private long _nextHistoryEventId;

    [Canonical] public WorldCalendar Calendar { get; }
    [Canonical] public WorldDate CurrentDate { get; internal set; }
    [Canonical] public long NextEventId => _nextEventId;

    /// <summary>Contador monotônico de <see cref="WorldEvent.EventId"/> (COH-01 / AD-013) —
    /// irmão de <see cref="NextEventId"/>; nunca reaproveita o contador de <c>ScheduledEvent</c>.</summary>
    [Canonical] public long NextHistoryEventId => _nextHistoryEventId;

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

    /// <summary>Eficiência de recuperação por tipo de lugar de descanso (Fase 15.1, Stage 4,
    /// T12, LWV-03.1). <see cref="RestPlaceCatalog.GroundEfficiency"/> é o destino in-place de
    /// <see cref="NeedsRules.HomelessSleepEfficiency"/>.</summary>
    [Canonical] public RestPlaceCatalog RestPlaceCatalog { get; }

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

    /// <summary>Parâmetros cenário-driven do corpo mínimo causal (Fase 16.3, COH-21) —
    /// distribuição de Height/Weight/MuscleMass. <see cref="BodyRules.Enabled"/> falso
    /// mantém os campos gerados mas multiplicadores de BodyMechanic neutros 1.0.</summary>
    [Canonical] public BodyRules BodyRules { get; }

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

    /// <summary>Descritores extraordinários do cenário; desligado continua estado explícito vazio.</summary>
    [Canonical] public ExtraordinaryScenarioData Extraordinary { get; }

    private readonly List<ExtraordinaryCarrierState> _extraordinaryCarriers;

    /// <summary>Estado resolvido consultável por sistemas e projeções, ordenado por portador.</summary>
    [Canonical] public IReadOnlyList<ExtraordinaryCarrierState> ExtraordinaryCarriers => _extraordinaryCarriers;

    private readonly List<ExtraordinaryConstruct> _extraordinaryConstructs;
    private long _nextExtraordinaryConstructId;

    [Canonical] public IReadOnlyList<ExtraordinaryConstruct> ExtraordinaryConstructs => _extraordinaryConstructs;
    [Canonical] public long NextExtraordinaryConstructId => _nextExtraordinaryConstructId;

    private readonly List<Animal> _fauna;
    private readonly Dictionary<AnimalId, Animal> _faunaById;
    private long _nextAnimalId;

    /// <summary>Animais simulados mínimos (PWR-77). Não herdam IA de <see cref="Npc"/>.</summary>
    [Canonical] public IReadOnlyList<Animal> Fauna => _fauna;
    [Canonical] public long NextAnimalId => _nextAnimalId;

    private readonly List<Plant> _flora;
    private readonly Dictionary<PlantId, Plant> _floraById;
    private long _nextPlantId;

    /// <summary>Plantas individuais (PWR-101). Distintas do estoque de <c>CropSystem</c>.</summary>
    [Canonical] public IReadOnlyList<Plant> Flora => _flora;
    [Canonical] public long NextPlantId => _nextPlantId;

    private readonly List<EnvironmentTemperatureAdjustment> _environmentTemperatureAdjustments;

    /// <summary>Ajustes regionais de temperatura (PWR-75). Overlay de duração; a base mora em
    /// <see cref="MapCell.Temperature"/>.</summary>
    [Canonical]
    public IReadOnlyList<EnvironmentTemperatureAdjustment> EnvironmentTemperatureAdjustments =>
        _environmentTemperatureAdjustments;

    /// <summary>Regras de ciclo de vida por espécie animal (Fase 16.4) — config de cenário.</summary>
    [Canonical] public IReadOnlyList<AnimalSpeciesRules> AnimalSpeciesRules { get; }

    /// <summary>Regras de ciclo de vida por espécie vegetal (Fase 16.4) — config de cenário.</summary>
    [Canonical] public IReadOnlyList<PlantSpeciesRules> PlantSpeciesRules { get; }

    /// <summary>Curvas sazonais de delta de temperatura por bioma (Fase 16.4).</summary>
    [Canonical] public IReadOnlyList<BiomeSeasonTemperatureRules> BiomeSeasonTemperatureRules { get; }

    private readonly List<CombatEncounter> _combatEncounters;
    private readonly Dictionary<CombatEncounterId, CombatEncounter> _combatEncountersById;
    private long _nextCombatEncounterId;

    /// <summary>Encontros de combate multi-round (Fase 16.4, REALISM-16) — AD-010 via
    /// <c>combat.engage:</c>.</summary>
    [Canonical] public IReadOnlyList<CombatEncounter> CombatEncounters => _combatEncounters;
    [Canonical] public long NextCombatEncounterId => _nextCombatEncounterId;

    /// <summary>Teto de rounds e limiar de fuga (cenário).</summary>
    [Canonical] public CombatRules CombatRules { get; }

    /// <summary>Nome escolhido pelo usuário na criação (Fase 15.1, T42/ADR-0017) — cosmético,
    /// nenhuma decisão de sistema lê nome de mundo (ADR-0014), por isso volátil.</summary>
    [Volatile] public string Name { get; private set; }

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

    [Volatile, Ephemeral] public AliveNpcIndex AliveNpcIndex { get; private set; }

    [Volatile, Ephemeral] public HistoryIndex HistoryIndex { get; private set; }

    private readonly List<Npc> _npcWakeBatch = [];
    private readonly Dictionary<long, long> _npcWakeEventIdByNpc = [];

    /// <summary>NPCs que acordam neste tick (PERF-08) — derivado, fora do hash canônico.</summary>
    [Volatile, Ephemeral] public IReadOnlyList<Npc> NpcWakeBatch => _npcWakeBatch;

    [Volatile] public ColdTierArchive ColdArchive { get; private set; }

    /// <summary>Rastro de decisão por NPC (Fase 28, COG-01) — side-store não-canônico.</summary>
    [Volatile, Ephemeral] public NpcCognitionLog CognitionLog { get; } = new();

    /// <summary>União dos escopos de observação ativos (Fase 28, LOD-04).</summary>
    [Volatile, Ephemeral] public ObservationRegistry ObservationRegistry { get; } = new();

    /// <summary>Camada cosmética lazy/exata por observação (Fase 28, T5).</summary>
    [Volatile, Ephemeral] public CosmeticDetailSystem CosmeticDetail { get; private set; } = null!;

    [Volatile] internal CanonicalHashCache CanonicalHashCache { get; } = new();

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

    /// <summary>Cidades que ainda participam de decisões e projeções. Tombstones continuam em
    /// <see cref="Cities"/> para preservar referências históricas.</summary>
    public IEnumerable<City> ActiveCities() => _cities.Where(city => city.MergedIntoCityId is null);

    /// <summary>Edifício concluído (Fase 8, T5) — mesmo molde de <see cref="Cities"/>.</summary>
    [Canonical] public IReadOnlyList<Building> Buildings => _buildings;

    /// <summary><see cref="BuildingId"/> é monotônico como <see cref="WorkplaceId"/> — só este
    /// contador precisa sobreviver ao snapshot. <see cref="CityId"/>/<see cref="LocationId"/> não
    /// têm contador: nascem do stream de RNG dedicado (<see cref="NextCityId"/>), já coberto pelo
    /// snapshot de <see cref="RngStreams"/>.</summary>
    [Canonical] public long NextBuildingId => _nextBuildingId;

    private readonly List<SpatialPortal> _portals;

    /// <summary>Entradas/saídas nomeadas de espaço (Fase 15.1, T21, OQ-2) — dado descritivo, sem
    /// contador próprio: <see cref="SpatialPortal.Id"/> é autorado pelo cenário, mesmo molde de
    /// <see cref="SettlementAnchor.Id"/>. Nenhum sistema de simulação lê esta coleção nesta fase
    /// (fronteira estrita de spec.md); só <c>ScenarioLoaderV2</c> autora e a projeção da API lê.</summary>
    [Canonical] public IReadOnlyList<SpatialPortal> Portals => _portals;

    private readonly List<RestPlace> _restPlaces;
    private long _nextRestPlaceId;

    /// <summary>Camas/móveis de descanso no mundo (Fase 15.1, Stage 4, T12). Chão e moradia não
    /// entram aqui: são derivados do NPC sem household e de <see cref="Household.Location"/>.</summary>
    [Canonical] public IReadOnlyList<RestPlace> RestPlaces => _restPlaces;

    [Canonical] public long NextRestPlaceId => _nextRestPlaceId;

    [Canonical] public ResourceCatalog ResourceCatalog { get; }
    [Canonical] public IReadOnlyList<ProcessRecipe> ProcessRecipes { get; }

    private readonly List<ResourceProcess> _resourceProcesses;
    private long _nextResourceProcessId;

    [Canonical] public IReadOnlyList<ResourceProcess> ResourceProcesses => _resourceProcesses;
    [Canonical] public long NextResourceProcessId => _nextResourceProcessId;

    private readonly List<CropBatch> _cropBatches;
    private long _nextCropBatchId;

    [Canonical] public IReadOnlyList<CropBatch> CropBatches => _cropBatches;
    [Canonical] public long NextCropBatchId => _nextCropBatchId;

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
        BodyRules? bodyRules = null,
        CityRules? cityRules = null, CityCatalog? cityCatalog = null, PerfRules? perfRules = null,
        HistoryRules? historyRules = null, string name = "", IReadOnlyList<SpatialPortal>? portals = null,
        RestPlaceCatalog? restPlaceCatalog = null, IReadOnlyList<RestPlace>? restPlaces = null,
        ResourceCatalog? resourceCatalog = null, IReadOnlyList<ProcessRecipe>? processRecipes = null,
        IReadOnlyList<ResourceProcess>? resourceProcesses = null, IReadOnlyList<CropBatch>? cropBatches = null,
        ExtraordinaryScenarioData? extraordinary = null,
        IReadOnlyList<ExtraordinaryCarrierState>? extraordinaryCarriers = null,
        IReadOnlyList<ExtraordinaryConstruct>? extraordinaryConstructs = null,
        long nextExtraordinaryConstructId = 0,
        IReadOnlyList<Animal>? fauna = null,
        long nextAnimalId = 0,
        IReadOnlyList<Plant>? flora = null,
        long nextPlantId = 0,
        IReadOnlyList<EnvironmentTemperatureAdjustment>? environmentTemperatureAdjustments = null,
        IReadOnlyList<AnimalSpeciesRules>? animalSpeciesRules = null,
        IReadOnlyList<PlantSpeciesRules>? plantSpeciesRules = null,
        IReadOnlyList<BiomeSeasonTemperatureRules>? biomeSeasonTemperatureRules = null,
        IReadOnlyList<CombatEncounter>? combatEncounters = null,
        long nextCombatEncounterId = 0,
        CombatRules? combatRules = null)
    {
        Calendar = calendar;
        CurrentDate = WorldDate.Epoch(calendar);
        Seed = seed;
        Map = map;
        PopulationCatalog = populationCatalog;
        PopulationRules = populationRules;
        NeedsRules = needsRules;
        RestPlaceCatalog = restPlaceCatalog ?? RestPlaceCatalog.FromGround(needsRules.HomelessSleepEfficiency);
        ActionCatalog = actionCatalog;
        LifeStageRules = lifeStageRules;
        BranchId = branchId;
        EconomyRules = economyRules ?? EconomyRules.Disabled;
        EconomyCatalog = economyCatalog ?? EconomyCatalog.Empty;
        FamilyRules = familyRules ?? FamilyRules.Disabled;
        BodyRules = bodyRules ?? BodyRules.Default;
        CityRules = cityRules ?? CityRules.Disabled;
        CityCatalog = cityCatalog ?? CityCatalog.Empty;
        PerfRules = perfRules ?? PerfRules.Default;
        HistoryRules = historyRules ?? HistoryRules.Disabled;
        Extraordinary = extraordinary ?? ExtraordinaryScenarioData.Disabled;
        _extraordinaryCarriers = (extraordinaryCarriers ?? []).OrderBy(carrier => carrier.CarrierId.Value).ToList();
        _extraordinaryConstructs = (extraordinaryConstructs ?? []).OrderBy(construct => construct.Id).ToList();
        _nextExtraordinaryConstructId = nextExtraordinaryConstructId;
        _fauna = (fauna ?? []).OrderBy(animal => animal.Id.Value).ToList();
        _faunaById = ToLookup(_fauna, animal => animal.Id);
        _nextAnimalId = nextAnimalId;
        _flora = (flora ?? []).OrderBy(plant => plant.Id.Value).ToList();
        _floraById = ToLookup(_flora, plant => plant.Id);
        _nextPlantId = nextPlantId;
        _environmentTemperatureAdjustments = (environmentTemperatureAdjustments ?? []).ToList();
        AnimalSpeciesRules = animalSpeciesRules ?? [];
        PlantSpeciesRules = plantSpeciesRules ?? [];
        BiomeSeasonTemperatureRules = biomeSeasonTemperatureRules ?? [];
        _combatEncounters = (combatEncounters ?? []).OrderBy(e => e.Id.Value).ToList();
        _combatEncountersById = ToLookup(_combatEncounters, e => e.Id);
        _nextCombatEncounterId = nextCombatEncounterId;
        CombatRules = combatRules ?? CombatRules.Default;
        Name = name;
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
        _portals = (portals ?? []).ToList();
        _restPlaces = (restPlaces ?? []).ToList();
        _nextRestPlaceId = _restPlaces.Count == 0 ? 0 : _restPlaces.Max(place => place.Id.Value) + 1;
        ResourceCatalog = resourceCatalog ?? ResourceCatalog.Empty;
        ProcessRecipes = processRecipes ?? [];
        _resourceProcesses = (resourceProcesses ?? []).ToList();
        _nextResourceProcessId = _resourceProcesses.Count == 0 ? 0 : _resourceProcesses.Max(process => process.Id.Value) + 1;
        _cropBatches = (cropBatches ?? []).ToList();
        _nextCropBatchId = _cropBatches.Count == 0 ? 0 : _cropBatches.Max(crop => crop.Id.Value) + 1;
        AliveNpcIndex = AliveNpcIndex.RebuildFrom(this);
        HistoryIndex = HistoryIndex.RebuildFrom(this);
        ColdArchive = new ColdTierArchive();
        CosmeticDetail = new CosmeticDetailSystem(ObservationRegistry);
        BindNpcCanonicalNotifiers();
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
        BodyRules? bodyRules = null,
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
        long nextMemoryId = 0,
        string name = "",
        IReadOnlyList<SpatialPortal>? portals = null,
        RestPlaceCatalog? restPlaceCatalog = null,
        IReadOnlyList<RestPlace>? restPlaces = null,
        long nextRestPlaceId = 0,
        ResourceCatalog? resourceCatalog = null,
        IReadOnlyList<ProcessRecipe>? processRecipes = null,
        IReadOnlyList<ResourceProcess>? resourceProcesses = null,
        long nextResourceProcessId = 0,
        IReadOnlyList<CropBatch>? cropBatches = null,
        long nextCropBatchId = 0,
        ExtraordinaryScenarioData? extraordinary = null,
        IReadOnlyList<ExtraordinaryCarrierState>? extraordinaryCarriers = null,
        IReadOnlyList<ExtraordinaryConstruct>? extraordinaryConstructs = null,
        long nextExtraordinaryConstructId = 0,
        IReadOnlyList<Animal>? fauna = null,
        long nextAnimalId = 0,
        IReadOnlyList<Plant>? flora = null,
        long nextPlantId = 0,
        IReadOnlyList<EnvironmentTemperatureAdjustment>? environmentTemperatureAdjustments = null,
        IReadOnlyList<AnimalSpeciesRules>? animalSpeciesRules = null,
        IReadOnlyList<PlantSpeciesRules>? plantSpeciesRules = null,
        IReadOnlyList<BiomeSeasonTemperatureRules>? biomeSeasonTemperatureRules = null,
        long nextHistoryEventId = 0,
        IReadOnlyList<CombatEncounter>? combatEncounters = null,
        long nextCombatEncounterId = 0,
        CombatRules? combatRules = null)
    {
        Calendar = calendar;
        CurrentDate = currentDate;
        Seed = seed;
        Map = map;
        PopulationCatalog = populationCatalog;
        PopulationRules = populationRules;
        NeedsRules = needsRules;
        RestPlaceCatalog = restPlaceCatalog ?? RestPlaceCatalog.FromGround(needsRules.HomelessSleepEfficiency);
        ActionCatalog = actionCatalog;
        LifeStageRules = lifeStageRules;
        BranchId = branchId;
        EconomyRules = economyRules ?? EconomyRules.Disabled;
        EconomyCatalog = economyCatalog ?? EconomyCatalog.Empty;
        Name = name;
        _rng = new WorldRngRegistry(seed, rngStreams);
        _scheduler = new EventScheduler(pendingEvents);
        _nextEventId = nextEventId;
        _nextHistoryEventId = nextHistoryEventId;
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
        BodyRules = bodyRules ?? BodyRules.Default;
        _relationships = relationships is null ? [] : new Dictionary<RelationshipKey, Relationship>(relationships);
        _cities = (cities ?? []).ToList();
        _cityById = ToLookup(_cities, c => c.Id);
        _buildings = (buildings ?? []).ToList();
        _buildingById = ToLookup(_buildings, b => b.Id);
        _nextBuildingId = nextBuildingId;
        _portals = (portals ?? []).ToList();
        _restPlaces = (restPlaces ?? []).ToList();
        _nextRestPlaceId = nextRestPlaceId;
        ResourceCatalog = resourceCatalog ?? ResourceCatalog.Empty;
        ProcessRecipes = processRecipes ?? [];
        _resourceProcesses = (resourceProcesses ?? []).ToList();
        _nextResourceProcessId = nextResourceProcessId;
        _cropBatches = (cropBatches ?? []).ToList();
        _nextCropBatchId = nextCropBatchId;
        CityRules = cityRules ?? CityRules.Disabled;
        CityCatalog = cityCatalog ?? CityCatalog.Empty;
        PerfRules = perfRules ?? PerfRules.Default;
        HistoryRules = historyRules ?? HistoryRules.Disabled;
        Extraordinary = extraordinary ?? ExtraordinaryScenarioData.Disabled;
        _extraordinaryCarriers = (extraordinaryCarriers ?? []).OrderBy(carrier => carrier.CarrierId.Value).ToList();
        _extraordinaryConstructs = (extraordinaryConstructs ?? []).OrderBy(construct => construct.Id).ToList();
        _nextExtraordinaryConstructId = nextExtraordinaryConstructId;
        _fauna = (fauna ?? []).OrderBy(animal => animal.Id.Value).ToList();
        _faunaById = ToLookup(_fauna, animal => animal.Id);
        _nextAnimalId = nextAnimalId;
        _flora = (flora ?? []).OrderBy(plant => plant.Id.Value).ToList();
        _floraById = ToLookup(_flora, plant => plant.Id);
        _nextPlantId = nextPlantId;
        _environmentTemperatureAdjustments = (environmentTemperatureAdjustments ?? []).ToList();
        AnimalSpeciesRules = animalSpeciesRules ?? [];
        PlantSpeciesRules = plantSpeciesRules ?? [];
        BiomeSeasonTemperatureRules = biomeSeasonTemperatureRules ?? [];
        _combatEncounters = (combatEncounters ?? []).OrderBy(e => e.Id.Value).ToList();
        _combatEncountersById = ToLookup(_combatEncounters, e => e.Id);
        _nextCombatEncounterId = nextCombatEncounterId;
        CombatRules = combatRules ?? CombatRules.Default;
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
        CosmeticDetail = new CosmeticDetailSystem(ObservationRegistry);
        BindNpcCanonicalNotifiers();
    }
    internal WorldRngRegistry Rng => _rng;
    internal EventScheduler Scheduler => _scheduler;

    /// <summary>Único ponto de mutação de <see cref="Name"/> (T42) — separado do construtor
    /// porque <see cref="ScenarioLoaderV2.LoadWorld"/> monta o mundo a partir do cenário e não
    /// conhece o nome escolhido na borda; quem chama (o endpoint de create) atribui depois.</summary>
    public void Rename(string name) => Name = name;

    internal long NextEventIdAndAdvance() => _nextEventId++;

    /// <summary>Único ponto de mint de <see cref="WorldEvent.EventId"/> — monotônico e
    /// determinístico entre processos (AD-013).</summary>
    internal long NextHistoryEventIdAndAdvance() => _nextHistoryEventId++;

    internal void IncrementExampleCount(TickFrequency frequency) => _exampleTickCounts[frequency]++;

    internal void ClearNpcWakeBatch() => _npcWakeBatch.Clear();

    internal void AddNpcWake(Npc npc) => _npcWakeBatch.Add(npc);

    internal void ReplaceNpcWake(TickContext ctx, long npcId, long targetTick)
    {
        if (_npcWakeEventIdByNpc.TryGetValue(npcId, out var oldId))
            _scheduler.Cancel(oldId);

        var evt = ctx.ScheduleEvent(targetTick, NpcWakeScheduler.SystemName, npcId.ToString());
        _npcWakeEventIdByNpc[npcId] = evt.Id;
    }

    internal void ClearNpcWakeEvent(long npcId) => _npcWakeEventIdByNpc.Remove(npcId);

    internal NpcId NextNpcIdAndAdvance() => new(_nextNpcId++);
    internal HouseholdId NextHouseholdIdAndAdvance() => new(_nextHouseholdId++);

    /// <summary>Reserva <paramref name="count"/> ids em lote (T50) — usado só na carga de
    /// cenário, pra dar a cada membro do <see cref="AggregatePopulationPool"/> autorado um
    /// <see cref="NpcId"/> estável antes de qualquer materialização. Mesmo contador de
    /// <see cref="NextNpcIdAndAdvance"/>, só avançado de uma vez em vez de um por vez.</summary>
    internal IReadOnlyList<NpcId> ReserveNpcIdBlock(long count)
    {
        var ids = new List<NpcId>();
        for (long i = 0; i < count; i++)
            ids.Add(NextNpcIdAndAdvance());
        return ids;
    }

    /// <summary>Sincroniza o contador depois de um lote gerado fora do tick (seed inicial),
    /// que consome ids diretamente do <see cref="PopulationGenerator"/> em vez de um por vez.</summary>
    internal void AdvanceNpcIdTo(long value)
    {
        _nextNpcId = Math.Max(_nextNpcId, value);
        MarkCanonicalPropertyDirty(nameof(NextNpcId));
    }

    internal void AdvanceHouseholdIdTo(long value)
    {
        _nextHouseholdId = Math.Max(_nextHouseholdId, value);
        MarkCanonicalPropertyDirty(nameof(NextHouseholdId));
    }

    internal void AddNpc(Npc npc)
    {
        npc.CanonicalMutationNotifier = MarkNpcCanonicalDirty;
        _npcs.Add(npc);
        _npcById[npc.Id] = npc;
        AliveNpcIndex.OnBorn(npc);
        CanonicalHashCache.MarkNpcsStructureDirty();
    }

    internal void AddHousehold(Household household)
    {
        _households.Add(household);
        _householdById[household.Id] = household;
        MarkCanonicalPropertyDirty(nameof(Households));
    }

    /// <summary>Household sem membros é dissolvido (task 3) — sai da lista canônica.</summary>
    internal void RemoveHousehold(HouseholdId id)
    {
        _households.RemoveAll(h => h.Id == id);
        _householdById.Remove(id);
        MarkCanonicalPropertyDirty(nameof(Households));
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
        CanonicalHashCache.MarkNpcsStructureDirty();
    }

    private void BindNpcCanonicalNotifiers()
    {
        foreach (var npc in _npcs)
            npc.CanonicalMutationNotifier = MarkNpcCanonicalDirty;
    }

    private void MarkNpcCanonicalDirty(NpcId id) => CanonicalHashCache.MarkNpcDirty(id.Value);

    internal void MarkCanonicalPropertyDirty(string propertyName) =>
        CanonicalHashCache.MarkPropertyDirty(propertyName);

    internal Npc? FindNpc(NpcId id) => _npcById.GetValueOrDefault(id);
    internal Household? FindHousehold(HouseholdId id) => _householdById.GetValueOrDefault(id);

    internal void UpsertExtraordinaryCarrier(ExtraordinaryCarrierState carrier)
    {
        _extraordinaryCarriers.RemoveAll(existing => existing.CarrierId == carrier.CarrierId);
        _extraordinaryCarriers.Add(carrier);
        _extraordinaryCarriers.Sort((left, right) => left.CarrierId.Value.CompareTo(right.CarrierId.Value));
    }

    internal bool RemoveExtraordinaryCarrier(NpcId carrierId) =>
        _extraordinaryCarriers.RemoveAll(existing => existing.CarrierId == carrierId) > 0;

    internal int RemoveRelationshipsBetween(NpcId first, NpcId second)
    {
        int removed = 0;
        if (_relationships.Remove(new RelationshipKey(first, second))) removed++;
        if (_relationships.Remove(new RelationshipKey(second, first))) removed++;
        return removed;
    }

    internal long NextExtraordinaryConstructIdAndAdvance() => _nextExtraordinaryConstructId++;

    internal void AddExtraordinaryConstruct(ExtraordinaryConstruct construct)
    {
        _extraordinaryConstructs.Add(construct);
        _extraordinaryConstructs.Sort((left, right) => left.Id.CompareTo(right.Id));
    }

    internal bool RemoveExtraordinaryConstruct(long id) =>
        _extraordinaryConstructs.RemoveAll(construct => construct.Id == id) > 0;

    internal void AddEnvironmentTemperatureAdjustment(EnvironmentTemperatureAdjustment adjustment)
    {
        _environmentTemperatureAdjustments.Add(adjustment);
        CanonicalHashCache.MarkPropertyDirty(nameof(EnvironmentTemperatureAdjustments));
    }

    internal void ReplaceSeasonalEnvironmentTemperatureAdjustments(
        IReadOnlyList<EnvironmentTemperatureAdjustment> replacements)
    {
        _environmentTemperatureAdjustments.RemoveAll(
            adjustment => adjustment.UntilTick == Geography.TemperatureSeasonSystem.SeasonalUntilTick);
        foreach (var adjustment in replacements)
            _environmentTemperatureAdjustments.Add(adjustment);
        CanonicalHashCache.MarkPropertyDirty(nameof(EnvironmentTemperatureAdjustments));
    }

    internal void ReplaceExtraordinaryConstruct(ExtraordinaryConstruct construct)
    {
        RemoveExtraordinaryConstruct(construct.Id);
        AddExtraordinaryConstruct(construct);
    }

    internal AnimalId NextAnimalIdAndAdvance() => new(_nextAnimalId++);

    public void AddAnimal(Animal animal)
    {
        _fauna.Add(animal);
        _faunaById[animal.Id] = animal;
        _fauna.Sort((left, right) => left.Id.Value.CompareTo(right.Id.Value));
    }

    public Animal? FindAnimal(AnimalId id) => _faunaById.GetValueOrDefault(id);

    internal void ReplaceAnimal(Animal animal)
    {
        _fauna.RemoveAll(existing => existing.Id == animal.Id);
        _faunaById.Remove(animal.Id);
        AddAnimal(animal);
    }

    internal void RemoveAnimal(AnimalId id)
    {
        _fauna.RemoveAll(existing => existing.Id == id);
        _faunaById.Remove(id);
    }

    internal PlantId NextPlantIdAndAdvance() => new(_nextPlantId++);

    public void AddPlant(Plant plant)
    {
        _flora.Add(plant);
        _floraById[plant.Id] = plant;
        _flora.Sort((left, right) => left.Id.Value.CompareTo(right.Id.Value));
    }

    public Plant? FindPlant(PlantId id) => _floraById.GetValueOrDefault(id);

    internal void ReplacePlant(Plant plant)
    {
        _flora.RemoveAll(existing => existing.Id == plant.Id);
        _floraById.Remove(plant.Id);
        AddPlant(plant);
    }

    internal void RemovePlant(PlantId id)
    {
        _flora.RemoveAll(existing => existing.Id == id);
        _floraById.Remove(id);
    }

    internal CombatEncounterId NextCombatEncounterIdAndAdvance() => new(_nextCombatEncounterId++);

    public void AddCombatEncounter(CombatEncounter encounter)
    {
        _combatEncounters.Add(encounter);
        _combatEncountersById[encounter.Id] = encounter;
        _combatEncounters.Sort((left, right) => left.Id.Value.CompareTo(right.Id.Value));
        CanonicalHashCache.MarkPropertyDirty(nameof(CombatEncounters));
        CanonicalHashCache.MarkPropertyDirty(nameof(NextCombatEncounterId));
    }

    public CombatEncounter? FindCombatEncounter(CombatEncounterId id) =>
        _combatEncountersById.GetValueOrDefault(id);

    internal void ReplaceCombatEncounter(CombatEncounter encounter)
    {
        _combatEncounters.RemoveAll(existing => existing.Id == encounter.Id);
        _combatEncountersById.Remove(encounter.Id);
        AddCombatEncounter(encounter);
    }

    public bool IsExtraordinaryConstructCell(CellCoord cell) =>
        _extraordinaryConstructs.Any(construct => construct.Footprint.Contains(cell));

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
    public City? FindActiveCity(CityId id)
    {
        var city = FindCity(id);
        var visited = new HashSet<CityId>();
        while (city?.MergedIntoCityId is { } target && visited.Add(city.Id))
            city = FindCity(target);
        return city is { MergedIntoCityId: null } ? city : null;
    }

    internal void MergeCityInto(City daughter, City mother)
    {
        foreach (var building in _buildings.Where(building => building.City == daughter.Id).OrderBy(building => building.Id.Value))
            building.JoinCity(mother.Id);
        foreach (var workplace in _workplaces.Where(workplace => workplace.City == daughter.Id).OrderBy(workplace => workplace.Id.Value))
            workplace.JoinCity(mother.Id);
        foreach (var household in _households.OrderBy(household => household.Id.Value))
            household.ReplaceCityReference(daughter.Id, mother.Id);
        foreach (var npc in _npcs.Where(npc => npc.City == daughter.Id).OrderBy(npc => npc.Id.Value))
            npc.JoinCity(mother.Id);

        foreach (var stock in daughter.ExtractEntireStock().OrderBy(entry => entry.Key.Id))
            mother.DepositStock(stock.Key, stock.Value);

        foreach (var project in daughter.ConstructionQueue.ToList())
        {
            daughter.RemoveConstructionProject(project);
            project.JoinCity(mother.Id);
            mother.EnqueueConstruction(project);
        }

        var (pool, poolNpcIds) = daughter.ExtractEntirePool();
        mother.AbsorbPool(pool, poolNpcIds);
        daughter.MarkMergedInto(mother.Id);
    }
    public Building? FindBuilding(BuildingId id) => _buildingById.GetValueOrDefault(id);

    /// <summary>Único ponto de autoria de <see cref="SpatialPortal"/> (Fase 15.1, T21) — só
    /// <c>ScenarioLoaderV2</c> chama, no mesmo momento em que autora <see cref="City"/>/
    /// <see cref="Building"/>. Sem <c>FindPortal</c>/remoção: portal é dado descritivo estático do
    /// cenário, nenhum sistema desta fase o edita depois de carregado.</summary>
    public void AddPortal(SpatialPortal portal)
    {
        _portals.Add(portal);
        // Portals is cold (not HotProperties): must invalidate fragment cache after mutation
        // or CanonicalHash after AddPortal stays stale (PERF-12 IncrementalHasher).
        CanonicalHashCache.MarkPropertyDirty(nameof(Portals));
    }

    internal RestPlaceId NextRestPlaceIdAndAdvance() => new(_nextRestPlaceId++);

    public void AddRestPlace(RestPlace place)
    {
        _restPlaces.Add(place);
        _nextRestPlaceId = Math.Max(_nextRestPlaceId, place.Id.Value + 1);
    }

    internal ResourceProcessId NextResourceProcessIdAndAdvance() => new(_nextResourceProcessId++);

    public void AddResourceProcess(ResourceProcess process)
    {
        _resourceProcesses.Add(process);
        _nextResourceProcessId = Math.Max(_nextResourceProcessId, process.Id.Value + 1);
    }

    internal CropBatchId NextCropBatchIdAndAdvance() => new(_nextCropBatchId++);

    public void AddCropBatch(CropBatch crop)
    {
        _cropBatches.Add(crop);
        _nextCropBatchId = Math.Max(_nextCropBatchId, crop.Id.Value + 1);
    }

    public CropBatch? FindCropAt(CellCoord plot) =>
        _cropBatches.FirstOrDefault(crop => crop.Plot == plot && crop.Status != CropStatus.Harvested);

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

    /// <summary>Único ponto de mutação de <see cref="VolatileMemories"/> por compactação (Fase 11,
    /// roadmap item 10, LLM-17..19, <c>MemoryCompactionJob</c>) — troca um grupo de memórias
    /// voláteis por um resumo. Não passa por <see cref="AddNpcMemory"/>/<c>_nextMemoryId</c> de
    /// propósito: <see cref="NextMemoryId"/> é <see cref="CanonicalAttribute"/> e compactação
    /// precisa deixar o hash canônico intacto (spec.md, "hash canônico permanece idêntico"), por
    /// isso <paramref name="replacement"/> reaproveita o <see cref="NpcMemory.Id"/> de uma das
    /// memórias removidas em vez de tirar um id novo do contador compartilhado.</summary>
    internal void ReplaceVolatileMemories(IReadOnlyList<long> idsToRemove, NpcMemory replacement)
    {
        var idSet = idsToRemove.ToHashSet();
        _volatileMemories.RemoveAll(m => idSet.Contains(m.Id));
        _volatileMemories.Add(replacement);
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
        ctx.LogEvent(WorldEventKind.Minted, $"{amount.Amount}|{reason}", sourceSystem: "WorldState");
    }

    /// <summary>Retira massa da circulação (ex.: arquivo frio) — incrementa
    /// <see cref="MoneyDestroyed"/> sem o teto de <see cref="Destroy"/> (que só desfaz cunhagem).</summary>
    internal void BurnCirculatingMoney(TickContext ctx, Money amount, string reason)
    {
        if (amount.Amount <= 0) return;
        _moneyDestroyed += amount;
        ctx.LogEvent(WorldEventKind.Destroyed, $"{amount.Amount}|{reason}", sourceSystem: "WorldState");
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
        ctx.LogEvent(WorldEventKind.Destroyed, $"{amount.Amount}|{reason}", sourceSystem: "WorldState");
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
