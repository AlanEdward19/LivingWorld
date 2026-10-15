using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Cities;
using LivingWorld.Domain.Cities.Buildings;
using LivingWorld.Domain.Climate;
using LivingWorld.Domain.Cognition;
using LivingWorld.Domain.Crime;
using LivingWorld.Domain.Ecology;
using LivingWorld.Domain.Economy;
using LivingWorld.Domain.Emergence;
using LivingWorld.Domain.Extraordinary;
using LivingWorld.Domain.Fauna;
using LivingWorld.Domain.Flora;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Geography.Map;
using LivingWorld.Domain.Geography.Spatial;
using LivingWorld.Domain.Health;
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

/// <summary>Estado do mundo — tudo que precisa sobreviver a um snapshot. Controles de
/// host (pausa, velocidade) ficam fora de propósito: são estado do hospedeiro, não do mundo
/// (ver <see cref="SimulationHost"/>).</summary>
public sealed partial class WorldState
{
    private readonly WorldRngRegistry _rng;
    private readonly EventScheduler _scheduler;
    private long _nextEventId;
    private long _nextHistoryEventId;

    [Canonical] public WorldCalendar Calendar { get; }
    [Canonical] public WorldDate CurrentDate { get; internal set; }
    [Canonical] public long NextEventId => _nextEventId;

    /// <summary>Contador monotônico de <see cref="WorldEvent.EventId"/> —
    /// irmão de <see cref="NextEventId"/>; nunca reaproveita o contador de <c>ScheduledEvent</c>.</summary>
    [Canonical] public long NextHistoryEventId => _nextHistoryEventId;

    /// <summary>Linha temporal deste mundo. O hash canônico é por branch: dois
    /// branches com conteúdo idêntico têm hashes distintos.</summary>
    [Canonical] public BranchId BranchId { get; }

    /// <summary>Seed raiz do mundo. Precisa sobreviver ao snapshot: sem ela, um stream de RNG
    /// pedido pela primeira vez depois de uma rehidratação derivaria de uma raiz diferente.</summary>
    [Canonical] public ulong Seed { get; }

    [Canonical]
    public IReadOnlyList<RngStreamState> RngStreams => _rng.Snapshot();

    [Canonical]
    public IReadOnlyList<ScheduledEvent> PendingEvents => _scheduler.Snapshot();

    /// <summary>Geografia do mundo — grid, regiões, custo de deslocamento. Entra no
    /// hash canônico: mudar o mapa muda o mundo.</summary>
    [Canonical] public WorldMap Map { get; }

    /// <summary>Ids de cultura/profissão/tipo-de-local válidos para este cenário
    /// — decide quem pode nascer com qual cultura e barra literal de conteúdo em C#.</summary>
    [Canonical] public PopulationCatalog PopulationCatalog { get; }

    /// <summary>Tabela de vida e janela de fertilidade do cenário — alimenta
    /// toda decisão de nascimento e morte.</summary>
    [Canonical] public PopulationRules PopulationRules { get; }

    /// <summary>Parâmetros cenário-driven do utility AI/necessidades —
    /// decaimento, limiar de urgência, histerese. Entra no hash canônico: desligar/mudar o
    /// utility AI muda o mundo.</summary>
    [Canonical] public NeedsRules NeedsRules { get; }

    /// <summary>Eficiência de recuperação por tipo de lugar de descanso.
    /// <see cref="RestPlaceCatalog.GroundEfficiency"/> é o destino in-place de
    /// <see cref="NeedsRules.HomelessSleepEfficiency"/>.</summary>
    [Canonical] public RestPlaceCatalog RestPlaceCatalog { get; }

    /// <summary>Catálogo de ações e rotina diária do cenário.</summary>
    [Canonical] public ActionCatalog ActionCatalog { get; }

    /// <summary>Limiares de idade que resolvem <see cref="LifeStage"/> do cenário
    /// — nunca hardcoded (R3). Necessário para a rotina diária (<see cref="BehaviorDecisionSystem"/>)
    /// resolver `(Profession, LifeStage, hora)`.</summary>
    [Canonical] public LifeStageRules LifeStageRules { get; }

    /// <summary>Parâmetros cenário-driven da economia — capacidade, perda, salário,
    /// preço. <see cref="EconomyRules.Enabled"/> falso equivale a "economia não existe"
    /// (desligar muda o hash porque as coleções abaixo ficam sempre vazias nesse cenário).</summary>
    [Canonical] public EconomyRules EconomyRules { get; }

    /// <summary>Recipe de produção por local e vínculo profissão→local do cenário.</summary>
    [Canonical] public EconomyCatalog EconomyCatalog { get; }

    /// <summary>Todo peso/limiar/duração/flag de relações e famílias — mesmo grupo de
    /// <see cref="NeedsRules"/>/<see cref="EconomyRules"/>: cenário-driven, entra no hash canônico
    /// porque decide todo evento de relação/cortejo/concepção.</summary>
    [Canonical] public FamilyRules FamilyRules { get; }

    /// <summary>Parâmetros cenário-driven do corpo mínimo causal —
    /// distribuição de Height/Weight/MuscleMass. <see cref="BodyRules.Enabled"/> falso
    /// mantém os campos gerados mas multiplicadores de BodyMechanic neutros 1.0.</summary>
    [Canonical] public BodyRules BodyRules { get; }

    // SPEC_DEVIATION: design.md/tasks.md pressupõem `world.CityRules`/
    // `world.CityCatalog` (todo sistema de cidades lê threshold/receita por eles), mas o trabalho
    // de fundação nunca os wireou em WorldState — só criaram os tipos e o loader. Sem isso,
    // nenhum sistema de cidade teria como ler CityRules.Enabled/limiares. Mesmo padrão de
    // EconomyRules/EconomyCatalog acima.

    /// <summary>Todo limiar/peso/duração de crescimento/migração/fundação/materialização de cidade
    /// — cenário-driven, mesmo grupo de <see cref="FamilyRules"/>.</summary>
    [Canonical] public CityRules CityRules { get; }

    /// <summary>Receita de construção por tipo de edifício.</summary>
    [Canonical] public CityCatalog CityCatalog { get; }

    /// <summary>Tetos de custo e arquivamento frio — cenário-driven.</summary>
    [Canonical] public PerfRules PerfRules { get; }

    /// <summary>Parâmetros cenário-driven da história degradável — limiar,
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

    /// <summary>Animais simulados mínimos. Não herdam IA de <see cref="Npc"/>.</summary>
    [Canonical] public IReadOnlyList<Animal> Fauna => _fauna;
    [Canonical] public long NextAnimalId => _nextAnimalId;

    private readonly List<Plant> _flora;
    private readonly Dictionary<PlantId, Plant> _floraById;
    private long _nextPlantId;

    /// <summary>Plantas individuais. Distintas do estoque de <c>CropSystem</c>.</summary>
    [Canonical] public IReadOnlyList<Plant> Flora { get { SyncFlora(); return _flora; } }

    // Avanço de estágio adiado (FloraLifecycleSystem): enquanto nenhum evento de planta é possível, o
    // sistema só conta ticks; quem lê ou muda a flora materializa o pendente antes. Estado derivado.
    internal Action? FloraPendingFlush;

    /// <summary>Muda a cada inclusão/troca/remoção de planta feita de fora do avanço adiado.</summary>
    internal long FloraMutationVersion;

    /// <summary>Quantidade de plantas, sem forçar a materialização do estágio.</summary>
    internal int FloraCount => _flora.Count;

    private readonly List<EnvironmentTemperatureAdjustment> _environmentTemperatureAdjustments;

    /// <summary>Ajustes regionais de temperatura. Overlay de duração; a base mora em
    /// <see cref="MapCell.Temperature"/>.</summary>
    [Canonical]
    public IReadOnlyList<EnvironmentTemperatureAdjustment> EnvironmentTemperatureAdjustments =>
        _environmentTemperatureAdjustments;

    /// <summary>Regras de ciclo de vida por espécie animal — config de cenário.</summary>
    [Canonical] public IReadOnlyList<AnimalSpeciesRules> AnimalSpeciesRules { get; }

    /// <summary>Regras de ciclo de vida por espécie vegetal — config de cenário.</summary>
    [Canonical] public IReadOnlyList<PlantSpeciesRules> PlantSpeciesRules { get; }

    [Canonical] public IReadOnlyList<BiomeSeasonTemperatureRules> BiomeSeasonTemperatureRules { get; }

    private readonly List<CombatEncounter> _combatEncounters;
    private readonly Dictionary<CombatEncounterId, CombatEncounter> _combatEncountersById;
    private long _nextCombatEncounterId;

    /// <summary>Encontros de combate multi-round — via
    /// <c>combat.engage:</c>.</summary>
    [Canonical] public IReadOnlyList<CombatEncounter> CombatEncounters => _combatEncounters;

    /// <summary>Livro de trocas das oficinas (ver <see cref="TradeAggregate"/>), canônico e limitado: dia a dia na
    /// janela de detalhe, mês a mês depois dela.</summary>
    [Canonical] public IReadOnlyList<TradeAggregate> TradeLedger => _tradeLedger;
    private readonly List<TradeAggregate> _tradeLedger;

    /// <summary>Livro do tesouro das cidades (receita e gasto por mês, ver <see cref="TreasuryAggregate"/>), canônico e
    /// limitado a um registro por cidade e mês.</summary>
    [Canonical] public IReadOnlyList<TreasuryAggregate> TreasuryLedger => _treasuryLedger;
    private readonly List<TreasuryAggregate> _treasuryLedger;

    /// <summary>Regras de doença do cenário (disease-epidemics); desligadas por padrão.</summary>
    [Canonical] public DiseaseRules DiseaseRules { get; }

    /// <summary>Regras de furto do cenário (crime-and-theft); desligado por padrão: ninguém furta.</summary>
    [Canonical] public TheftRules TheftRules { get; }

    /// <summary>Regras da emergência aberta (emergence-first-slice); desligadas por padrão: nenhuma tecnologia emerge.</summary>
    [Canonical] public EmergenceRules EmergenceRules { get; }

    /// <summary>Marcos de desenvolvimento do cenário (ontogeny-model, Fase 21); desligadas por padrão no construtor, ligadas pelo cenário.</summary>
    [Canonical] public LivingWorld.Domain.Ontogeny.OntogenyRules OntogenyRules { get; }

    /// <summary>Regras de aparência (#46 fatia 2). Nulo = mundo sem genes de aparência: omitido do snapshot e do hash canônico,
    /// que ficam idênticos aos de antes (opt-in por período, ADR-0032).</summary>
    [Canonical, System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public LivingWorld.Domain.Population.AppearanceRules? AppearanceRules { get; }

    /// <summary>Tecnologias por cultura, na ordem de descoberta; identificadas só pelo vetor (ADR-0013).</summary>
    [Canonical] public IReadOnlyList<Technology> Technologies => _technologies;
    private readonly List<Technology> _technologies;

    /// <summary>Infecções ativas, ordenadas por NPC e doença.</summary>
    [Canonical] public IReadOnlyList<Infection> Infections => _infections;
    private readonly List<Infection> _infections;

    /// <summary>Regras de clima do cenário (weather-drought); desligado por padrão (todo ano vale 1000).</summary>
    [Canonical] public ClimateRules ClimateRules { get; }

    /// <summary>Anos de safra recentes por região, ordenados por região e ano (os antigos saem sozinhos).</summary>
    [Canonical] public IReadOnlyList<ClimateYear> ClimateYears => _climateYears;
    private readonly List<ClimateYear> _climateYears;

    /// <summary>Imunidades vigentes, ordenadas por NPC e doença.</summary>
    [Canonical] public IReadOnlyList<Immunity> Immunities => _immunities;
    private readonly List<Immunity> _immunities;

    /// <summary>Primeiros contatos de compra ainda sem evento (ver <c>TradeLedgerSystem</c>). Volátil: o sistema a esvazia
    /// a cada hora, então só uma hora de contatos se perde se o mundo for salvo no meio.</summary>
    internal List<LivingWorld.Simulation.Economy.Trade.TradeContact> PendingTradeContacts { get; } = [];
    [Canonical] public long NextCombatEncounterId => _nextCombatEncounterId;

    /// <summary>Teto de rounds e limiar de fuga (cenário).</summary>
    [Canonical] public CombatRules CombatRules { get; }

    /// <summary>Nome escolhido pelo usuário na criação — cosmético,
    /// nenhuma decisão de sistema lê nome de mundo, por isso volátil.</summary>
    [Volatile] public string Name { get; private set; }

    /// <summary>Id do período (cenário de catálogo) de que o mundo nasceu, quando a criação o informou; vazio caso contrário.
    /// Só rotula (o cliente usa para buscar os nomes do catálogo), nenhuma decisão o lê: volátil, fora do hash canônico.</summary>
    [Volatile] public string PeriodId { get; private set; } = "";

    private readonly List<Fact> _facts;
    private long _nextFactId;

    /// <summary>Fatos imutáveis do esqueleto — append-only na simulação.</summary>
    [Canonical] public IReadOnlyList<Fact> Facts => _facts;

    [Canonical] public long NextFactId => _nextFactId;

    private long _nextReportId;

    [Canonical] public long NextReportId => _nextReportId;

    private readonly List<ReportState> _reports;
    private readonly List<Book> _books;
    private long _nextBookId;

    /// <summary>Relatos registrados no mundo — inclui relatos fora do cânone vivo
    /// (ex.: referenciados por <see cref="Books"/> após despejo).</summary>
    [Canonical] public IReadOnlyList<ReportState> Reports => _reports;

    /// <summary>Livros como objetos do mundo.</summary>
    [Canonical] public IReadOnlyList<Book> Books => _books;

    [Canonical] public long NextBookId => _nextBookId;

    private readonly List<NpcMemory> _canonicalMemories;
    private readonly List<NpcMemory> _volatileMemories;
    private long _nextMemoryId;

    /// <summary>Memórias de NPC com importância >= limiar do
    /// cenário no momento em que foram registradas — alimenta <c>Recall</c> e o
    /// prompt da LLM, por isso decide.</summary>
    [Canonical] public IReadOnlyList<NpcMemory> CanonicalMemories => _canonicalMemories;

    /// <summary>Memórias abaixo do limiar canônico — compactáveis livremente (futuro) sem
    /// tocar o hash canônico.</summary>
    [Volatile] public IReadOnlyList<NpcMemory> VolatileMemories => _volatileMemories;

    [Canonical] public long NextMemoryId => _nextMemoryId;

    [Volatile, Ephemeral] public AliveNpcIndex AliveNpcIndex { get; private set; }

    [Volatile, Ephemeral] public HistoryIndex HistoryIndex { get; private set; }

    private readonly List<Npc> _npcWakeBatch = [];
    private readonly Dictionary<long, long> _npcWakeEventIdByNpc = [];

    /// <summary>NPCs que acordam neste tick — derivado, fora do hash canônico.</summary>
    [Volatile, Ephemeral] public IReadOnlyList<Npc> NpcWakeBatch => _npcWakeBatch;

    [Volatile] public ColdTierArchive ColdArchive { get; private set; }

    /// <summary>Rastro de decisão por NPC — side-store não-canônico.</summary>
    [Volatile, Ephemeral] public NpcCognitionLog CognitionLog { get; } = new();

    /// <summary>União dos escopos de observação ativos.</summary>
    [Volatile, Ephemeral] public ObservationRegistry ObservationRegistry { get; } = new();

    /// <summary>Camada cosmética lazy/exata por observação.</summary>
    [Volatile, Ephemeral] public CosmeticDetailSystem CosmeticDetail { get; private set; } = null!;

    [Volatile] internal CanonicalHashCache CanonicalHashCache { get; } = new();

    private readonly Dictionary<RelationshipKey, Relationship> _relationships;

    /// <summary>Uma <see cref="Relationship"/> por par ordenado, criada sob demanda
    /// ("quem nunca se encontra nunca se conhece"). Este dicionário **é** a
    /// coleção canônica, sem lista paralela: não há iteração ordenada por id sequencial fazendo
    /// sentido para um par (quem precisar de determinismo ordena por
    /// <c>(From.Value, To.Value)</c> na hora, ex. <c>RelationshipSystem</c>/hash).</summary>
    [Canonical] public IReadOnlyDictionary<RelationshipKey, Relationship> Relationships => _relationships;

    // Índice derivado de _relationships: alvos (To) de cada From, ordenados por To. Permite ler as
    // relações de um NPC em O(grau) em vez de varrer o dicionário inteiro (crescia ~O(n²) com os anos).
    private readonly Dictionary<NpcId, List<NpcId>> _relationshipTargetsByFrom = [];

    private readonly List<Npc> _npcs;
    private readonly Dictionary<NpcId, Npc> _npcById;
    private readonly List<Household> _households;
    private readonly Dictionary<HouseholdId, Household> _householdById;
    private long _nextNpcId;
    private long _nextHouseholdId;

    private Money _moneyMinted;
    private Money _moneyDestroyed;

    /// <summary>Massa monetária cunhada desde a origem do mundo — nunca
    /// alterada implicitamente por transação/salário, só por <see cref="Mint"/> nomeado.</summary>
    [Canonical] public Money MoneyMinted => _moneyMinted;

    /// <summary>Massa monetária destruída desde a origem do mundo — a
    /// invariante de conservação é <c>saldo_total == inicial + MoneyMinted - MoneyDestroyed</c>.</summary>
    [Canonical] public Money MoneyDestroyed => _moneyDestroyed;

    private readonly Dictionary<ResourceType, long> _resourceProduced = [];
    private readonly Dictionary<ResourceType, long> _resourceConsumed = [];

    /// <summary>Contador auditável de produção bruta por recurso desde a origem do mundo
    /// — nunca influencia decisão nenhuma (por isso <see cref="VolatileAttribute"/>,
    /// não entra no hash canônico), só sustenta a invariante
    /// <c>produzido == consumido + estocado + perdido</c> que os testes de conservação checam.
    /// Incrementado por <c>ProductionSystem</c> a cada depósito bem-sucedido.</summary>
    [Volatile] public IReadOnlyDictionary<ResourceType, long> ResourceProduced => _resourceProduced;

    /// <summary>Contador auditável de consumo (destruição real do recurso, não transferência de
    /// estoque — <c>Buy</c> move estoque entre Workplace/Household e não conta aqui) desde a
    /// origem do mundo. Incrementado por <c>BehaviorDecisionSystem.ApplyEat</c> a cada retirada
    /// bem-sucedida.</summary>
    [Volatile] public IReadOnlyDictionary<ResourceType, long> ResourceConsumed => _resourceConsumed;

    private readonly List<Workplace> _workplaces;
    private readonly Dictionary<WorkplaceId, Workplace> _workplaceById;
    private long _nextWorkplaceId;

    /// <summary>Local de produção/estoque/mercado — mesmo molde de <see
    /// cref="Households"/> (lista + dono canônico).</summary>
    [Canonical] public IReadOnlyList<Workplace> Workplaces => _workplaces;

    [Canonical] public long NextWorkplaceId => _nextWorkplaceId;

    private readonly List<City> _cities;
    private readonly Dictionary<CityId, City> _cityById;
    private readonly List<Building> _buildings;
    private readonly Dictionary<BuildingId, Building> _buildingById;
    private long _nextBuildingId;

    /// <summary>Cidade — mesmo molde de <see cref="Households"/>/<see
    /// cref="Workplaces"/> (lista + dono canônico).</summary>
    [Canonical] public IReadOnlyList<City> Cities => _cities;

    private readonly List<SpatialPortal> _portals;

    /// <summary>Entradas/saídas nomeadas de espaço — dado descritivo, sem
    /// contador próprio: <see cref="SpatialPortal.Id"/> é autorado pelo cenário, mesmo molde de
    /// <see cref="SettlementAnchor.Id"/>. Nenhum sistema de simulação lê esta coleção
    /// (fronteira estrita de spec.md); só <c>ScenarioLoaderV2</c> autora e a projeção da API lê.</summary>
    [Canonical] public IReadOnlyList<SpatialPortal> Portals => _portals;

    private readonly List<RestPlace> _restPlaces;
    private long _nextRestPlaceId;

    /// <summary>Camas/móveis de descanso no mundo. Chão e moradia não
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

    // Lotes ainda não colhidos, na ordem de inserção. Um lote colhido é terminal e não é lido por
    // ninguém (o rendimento já foi contabilizado): sai de CropBatches na colheita, para o snapshot não
    // crescer com os anos. Índice derivado, compactado de forma preguiçosa.
    private readonly List<CropBatch> _activeCropBatches = new();

    internal IReadOnlyList<CropBatch> ActiveCropBatches
    {
        get
        {
            _activeCropBatches.RemoveAll(crop => crop.Status == CropStatus.Harvested);
            return _activeCropBatches;
        }
    }
    [Canonical] public long NextCropBatchId => _nextCropBatchId;

    /// <summary>Contador do sistema de exemplo — descartável quando sistemas reais o substituírem. Nenhuma
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
        CombatRules? combatRules = null,
        IReadOnlyList<TradeAggregate>? tradeLedger = null,
        IReadOnlyList<TreasuryAggregate>? treasuryLedger = null,
        DiseaseRules? diseaseRules = null,
        IReadOnlyList<Infection>? infections = null,
        IReadOnlyList<Immunity>? immunities = null,
        ClimateRules? climateRules = null,
        IReadOnlyList<ClimateYear>? climateYears = null,
        TheftRules? theftRules = null,
        EmergenceRules? emergenceRules = null,
        LivingWorld.Domain.Ontogeny.OntogenyRules? ontogenyRules = null,
        IReadOnlyList<Technology>? technologies = null,
        LivingWorld.Domain.Population.AppearanceRules? appearanceRules = null)
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
        FamilyRules = (familyRules ?? FamilyRules.Disabled).WithAppearanceAttraction(appearanceRules?.AttractionWeight ?? 0);
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
        _tradeLedger = (tradeLedger ?? []).ToList();
        _treasuryLedger = (treasuryLedger ?? []).ToList();
        DiseaseRules = diseaseRules ?? DiseaseRules.Disabled;
        _infections = (infections ?? []).ToList();
        _immunities = (immunities ?? []).ToList();
        ClimateRules = climateRules ?? ClimateRules.Disabled;
        TheftRules = theftRules ?? TheftRules.Disabled;
        EmergenceRules = emergenceRules ?? EmergenceRules.Disabled;
        OntogenyRules = ontogenyRules ?? LivingWorld.Domain.Ontogeny.OntogenyRules.Disabled;
        AppearanceRules = appearanceRules;
        _technologies = (technologies ?? []).ToList();
        _climateYears = (climateYears ?? []).ToList();
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
        _activeCropBatches.AddRange(_cropBatches.Where(crop => crop.Status != CropStatus.Harvested));
        _nextCropBatchId = _cropBatches.Count == 0 ? 0 : _cropBatches.Max(crop => crop.Id.Value) + 1;
        AliveNpcIndex = AliveNpcIndex.RebuildFrom(this);
        HistoryIndex = HistoryIndex.RebuildFrom(this);
        ColdArchive = new ColdTierArchive();
        CosmeticDetail = new CosmeticDetailSystem(ObservationRegistry);
        BindNpcCanonicalNotifiers();
    }

    /// <summary>Reconstrói a partir de um snapshot — rehidratação.</summary>
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
        CombatRules? combatRules = null,
        IReadOnlyList<TradeAggregate>? tradeLedger = null,
        IReadOnlyList<TreasuryAggregate>? treasuryLedger = null,
        DiseaseRules? diseaseRules = null,
        IReadOnlyList<Infection>? infections = null,
        IReadOnlyList<Immunity>? immunities = null,
        ClimateRules? climateRules = null,
        IReadOnlyList<ClimateYear>? climateYears = null,
        TheftRules? theftRules = null,
        EmergenceRules? emergenceRules = null,
        LivingWorld.Domain.Ontogeny.OntogenyRules? ontogenyRules = null,
        IReadOnlyList<Technology>? technologies = null,
        LivingWorld.Domain.Population.AppearanceRules? appearanceRules = null)
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
        FamilyRules = (familyRules ?? FamilyRules.Disabled).WithAppearanceAttraction(appearanceRules?.AttractionWeight ?? 0);
        BodyRules = bodyRules ?? BodyRules.Default;
        _relationships = relationships is null ? [] : new Dictionary<RelationshipKey, Relationship>(relationships);
        foreach (var key in _relationships.Keys)
            IndexRelationshipTarget(key);
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
        _activeCropBatches.AddRange(_cropBatches.Where(crop => crop.Status != CropStatus.Harvested));
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
        _tradeLedger = (tradeLedger ?? []).ToList();
        _treasuryLedger = (treasuryLedger ?? []).ToList();
        DiseaseRules = diseaseRules ?? DiseaseRules.Disabled;
        _infections = (infections ?? []).ToList();
        _immunities = (immunities ?? []).ToList();
        ClimateRules = climateRules ?? ClimateRules.Disabled;
        TheftRules = theftRules ?? TheftRules.Disabled;
        EmergenceRules = emergenceRules ?? EmergenceRules.Disabled;
        OntogenyRules = ontogenyRules ?? LivingWorld.Domain.Ontogeny.OntogenyRules.Disabled;
        AppearanceRules = appearanceRules;
        _technologies = (technologies ?? []).ToList();
        _climateYears = (climateYears ?? []).ToList();
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

    /// <summary>Único ponto de mutação de <see cref="Name"/> — separado do construtor
    /// porque <see cref="ScenarioLoaderV2.LoadWorld"/> monta o mundo a partir do cenário e não
    /// conhece o nome escolhido na borda; quem chama (o endpoint de create) atribui depois.</summary>
    public void Rename(string name) => Name = name;

    /// <summary>Único ponto de mutação de <see cref="PeriodId"/>, chamado na criação (e ao reidratar o snapshot).</summary>
    public void AssignPeriod(string periodId) => PeriodId = periodId;

    internal long NextEventIdAndAdvance() => _nextEventId++;

    /// <summary>Único ponto de mint de <see cref="WorldEvent.EventId"/> — monotônico e
    /// determinístico entre processos.</summary>
    internal long NextHistoryEventIdAndAdvance() => _nextHistoryEventId++;

    internal void IncrementExampleCount(TickFrequency frequency) => _exampleTickCounts[frequency]++;

    /// <summary>Muda a cada alteração da lista de ajustes de temperatura — invalida o memo por região
    /// de <see cref="EnvironmentTemperatureMechanic"/>. Contador interno, fora do estado do mundo.</summary>
    internal long TemperatureAdjustmentsVersion { get; private set; }

    private static int LowerBoundById<T>(List<T> sorted, long id, Func<T, long> idOf)
    {
        int lo = 0, hi = sorted.Count;
        while (lo < hi)
        {
            int mid = (lo + hi) >>> 1;
            if (idOf(sorted[mid]) < id) lo = mid + 1; else hi = mid;
        }
        return lo;
    }

    private static void InsertSortedById<T>(List<T> sorted, T item, Func<T, long> idOf) =>
        sorted.Insert(LowerBoundById(sorted, idOf(item), idOf), item);

    /// <summary>Mesmo Id = mesma posição ordenada: troca no lugar, sem deslocar o resto da lista.</summary>
    private static void ReplaceSortedById<T>(List<T> sorted, T item, Func<T, long> idOf)
    {
        long id = idOf(item);
        int index = LowerBoundById(sorted, id, idOf);
        if (index < sorted.Count && idOf(sorted[index]) == id)
            sorted[index] = item;
        else
            sorted.Insert(index, item);
    }

    private static void RemoveSortedById<T>(List<T> sorted, long id, Func<T, long> idOf)
    {
        int index = LowerBoundById(sorted, id, idOf);
        while (index < sorted.Count && idOf(sorted[index]) == id)
            sorted.RemoveAt(index);
    }

    private readonly Dictionary<HouseholdId, RestPlace> _bedByHousehold = new();
    private int _bedsIndexedCount;

    private Guid NextGuidFromRng()
    {
        var rng = _rng.Stream("city-founding");
        var bytes = new byte[16];
        BitConverter.GetBytes(rng.NextDouble()).CopyTo(bytes, 0);
        BitConverter.GetBytes(rng.NextDouble()).CopyTo(bytes, 8);
        return new Guid(bytes);
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
