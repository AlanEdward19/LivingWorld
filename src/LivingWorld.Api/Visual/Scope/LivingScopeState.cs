using LivingWorld.Api.Visual.Catalogs;
using LivingWorld.Api.Visual.Observable;
using LivingWorld.Api.Visual.Projection;
using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Cities.Buildings;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Cities.Queries;

namespace LivingWorld.Api.Visual.Scope;

public sealed record NpcVisual(
    NpcId Id,
    CellCoord Location,
    ActionType? CurrentAction,
    CityId? City = null,
    CellCoord? RelocationDestination = null,
    ExtraordinaryNpcVisual? Extraordinary = null,
    // #46 fatia 2: genes que o cliente usa para vestir o boneco; nulo em mundo sem AppearanceRules
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] Appearance? Appearance = null)
{
    /// <summary>AD-170: sinais visíveis (ação com alvo, idade, ofício, corpo, doença, emoção, carga). No delta vem omitido quando não
    /// mudou desde o tick anterior (o cliente mantém o que já tinha).</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public NpcObservable? Observable { get; init; }

    /// <summary>AD-170: tick em que a ação atual começou, o alvo dela e a intenção (o que foi fazer e onde). AD-172: a projeção
    /// sempre preenche; no delta de um NPC já conhecido vem omitido (com alvo e intenção) quando os quatro não mudaram.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public long? ActionStartedAtTick { get; init; } = 0;
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public NpcActionTargetDto? ActionTarget { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public ActionType? Intent { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? IntentTarget { get; init; }

    /// <summary>AD-176: células da rota real de quem viaja (onde está, soleira, caminho, soleira do destino, chegada), em ordem;
    /// ausente = não está viajando. O cliente anda o boneco por elas até <see cref="RouteArrivalTick"/>.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<CellCoord>? Route { get; init; }

    /// <summary>AD-176: tick em que a viagem chega (o motor move o NPC para o destino nele).</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public long? RouteArrivalTick { get; init; }
}
public sealed record CityVisual(
    CityId Id, string Name, CellCoord Location, long Population, CellBounds Bounds,
    CityId? FoundedFromCityId = null)
{
    /// <summary>AD-170: densidade da cidade no escopo Mundo (quem está dentro, sem mandar cada morador).</summary>
    public CityDensityVisual? Density { get; init; }
}
public sealed record BuildingVisual(
    BuildingId Id, CityId CityId, int BuildingTypeId, CellCoord Location, int Orientation = 0)
{
    /// <summary>AD-170: planta completa do térreo e andares, para desenhar prédio novo vindo no delta sem recarregar.</summary>
    public int Floors { get; init; } = 1;
    public IReadOnlyList<FootprintCell> GroundFootprint { get; init; } = [];
    /// <summary>AD-170: valor do imóvel e hospedagem, os mesmos do marcador de cidade (só com <c>Pricing</c>/<c>Lodging</c> no cenário).</summary>
    public long Value { get; init; }
    public LodgingMarker? Lodging { get; init; }
    /// <summary>AD-173: o lote de verdade (quintal de trás, passagem da porta à rua, rua da frente). O cliente desenha só isto.</summary>
    public LivingWorld.Simulation.Cities.LotView? Lot { get; init; }
}
public sealed record ProcessVisual(
    long Id, string Kind, long TargetId, double Progress, string DescriptorKey,
    double? Quality = null, long? RemainingHours = null, CellCoord? Location = null,
    IReadOnlyList<CellCoord>? Footprint = null, string? AppearanceToken = null)
{
    /// <summary>AD-170: recurso do processo de material (saída, cultura ou carga) e o nome dele no período.</summary>
    public int? ResourceId { get; init; }
    public string? ResourceName { get; init; }
}
public sealed record IndicatorUpdate(string Key, double Value);
public sealed record NotableVisualEvent(long Tick, WorldEventKind Kind, string Label, CellCoord? Location = null)
{
    /// <summary>AD-170: envolvidos estruturados (ids de pessoa, casa, prédio, caso, espólio) lidos do payload.</summary>
    public EventInvolved? Involved { get; init; }

    /// <summary>AD-170: id do evento no log do motor (0 = evento sem id, como a fundação derivada da cidade).</summary>
    public long EventId { get; init; }
}

public sealed record LivingScopeState(
    IReadOnlyList<NpcVisual> Npcs,
    IReadOnlyList<CityVisual> Cities,
    IReadOnlyList<BuildingVisual> Buildings,
    IReadOnlyList<ProcessVisual> Processes,
    IReadOnlyList<IndicatorUpdate> Indicators,
    IReadOnlyList<NotableVisualEvent> Events)
{
    public static LivingScopeState Empty { get; } = new([], [], [], [], [], []);

    // AD-170 (ADR-0047 etapa 2): o observável do escopo, por acréscimo. Prédio: função e ocupação; lavoura; fauna (indivíduos na
    // cidade, bandos no mundo); espólio no chão; objetos inventados; estação e clima.
    public IReadOnlyList<BuildingStateVisual> BuildingStates { get; init; } = [];
    public IReadOnlyList<CropVisual> Crops { get; init; } = [];
    public IReadOnlyList<AnimalVisual> Animals { get; init; } = [];
    public IReadOnlyList<FaunaGroupVisual> FaunaGroups { get; init; } = [];
    public IReadOnlyList<LeavingVisual> Leavings { get; init; } = [];
    public IReadOnlyList<ObjectVisual> Objects { get; init; } = [];
    public ScopeClimateVisual? Climate { get; init; }

    /// <summary>AD-176: as ruas de verdade do escopo (Cidade: as dela; Mundo: todas), as mesmas da camada <c>Roads</c>.</summary>
    public IReadOnlyList<LivingWorld.Simulation.Cities.StreetCellView> Roads { get; init; } = [];

    /// <summary>AD-172: tick desta foto (fora do JSON e da igualdade). O delta e o redutor avançam os processos lineares pela
    /// diferença entre o tick do delta e este.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public long Tick { get; init; }

    public bool Equals(LivingScopeState? other) =>
        other is not null
        && Npcs.SequenceEqual(other.Npcs)
        && Cities.SequenceEqual(other.Cities)
        && Buildings.SequenceEqual(other.Buildings)
        && Processes.SequenceEqual(other.Processes)
        && Indicators.SequenceEqual(other.Indicators)
        && Events.SequenceEqual(other.Events)
        && BuildingStates.SequenceEqual(other.BuildingStates)
        && Crops.SequenceEqual(other.Crops)
        && Animals.SequenceEqual(other.Animals)
        && FaunaGroups.SequenceEqual(other.FaunaGroups)
        && Leavings.SequenceEqual(other.Leavings)
        && Objects.SequenceEqual(other.Objects)
        && Roads.SequenceEqual(other.Roads)
        && Equals(Climate, other.Climate);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        AddRange(ref hash, Npcs);
        AddRange(ref hash, Cities);
        AddRange(ref hash, Buildings);
        AddRange(ref hash, Processes);
        AddRange(ref hash, Indicators);
        AddRange(ref hash, Events);
        AddRange(ref hash, BuildingStates);
        AddRange(ref hash, Crops);
        AddRange(ref hash, Animals);
        AddRange(ref hash, FaunaGroups);
        AddRange(ref hash, Leavings);
        AddRange(ref hash, Objects);
        AddRange(ref hash, Roads);
        hash.Add(Climate);
        return hash.ToHashCode();
    }

    private static void AddRange<T>(ref HashCode hash, IEnumerable<T> values)
    {
        foreach (var value in values)
            hash.Add(value);
    }
}
