using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Cities.Buildings;
using LivingWorld.Domain.Geography;

namespace LivingWorld.Api.Visual.Observable;

// Projeção do observável (ADR-0047 etapa 2, AD-170). Só leitura do estado do motor; nada aqui é guardado no mundo.
// Valores contínuos vêm arredondados (altura em cm, emoção em décimos, fração em centésimos) para o delta só mandar o que
// mudou de um jeito que se vê.

/// <summary>Sinais visíveis de um NPC no escopo que mudam devagar: nome, sexo, idade e estágio, ofício, corpo, gravidez já aparente,
/// doença, emoções e o que leva consigo. A ação com alvo, o início e a intenção ficam no próprio <c>NpcVisual</c> (mudam junto com a
/// posição).</summary>
public sealed record NpcObservable(
    string Name,
    string Sex,
    int AgeYears,
    string LifeStage,
    int ProfessionId,
    string? ProfessionName,
    int HeightCm,
    double GrowthFraction,
    PregnancyVisual? Pregnancy,
    IllnessVisual? Illness,
    EmotionsVisual Emotions,
    string? DominantEmotion,
    bool Mourning,
    IReadOnlyList<CarriedVisual> Carried,
    HeldObjectVisual? Held);

/// <summary>Gravidez só depois de aparecer (<c>Intimacy.ShowsAfterDays</c>, o mesmo marco em que a vila reage); fração da gestação
/// em vigésimos.</summary>
public sealed record PregnancyVisual(double Fraction, int Babies);

/// <summary>Infecção ativa: doença (id do cenário), sintomática e de cama, como o motor já decide.</summary>
public sealed record IllnessVisual(int DiseaseId, bool Symptomatic, bool Bedridden);

/// <summary>As seis emoções do <c>PsycheState</c> já decaídas para o tick, em décimos (0..1).</summary>
public sealed record EmotionsVisual(double Grief, double Fear, double Anger, double Joy, double Shame, double Loneliness);

/// <summary>Carga pessoal (AD-147): recurso e unidades.</summary>
public sealed record CarriedVisual(int ResourceId, int Quantity, string? ResourceName = null);

/// <summary>Recipiente que a pessoa levou de casa (AD-157): a peça e o design (forma e material em <c>/designs/{id}</c>).</summary>
public sealed record HeldObjectVisual(long ItemId, int DesignId);

/// <summary>Densidade de uma cidade no escopo Mundo: quantos materializados estão dentro dos limites, quantos ainda são pool, as
/// ações mais comuns e a temperatura do lugar. O mapa-múndi vê a cidade viva sem receber cada morador.</summary>
public sealed record CityDensityVisual(int Inside, long Pooled, IReadOnlyList<ActionCount> Actions, double TemperatureC);

public sealed record ActionCount(ActionType? Action, int Count);

/// <summary>Função legível e ocupação de um prédio (só o que muda; planta e andares ficam em <c>BuildingVisual</c>).</summary>
public sealed record BuildingStateVisual(
    long BuildingId,
    int BuildingTypeId,
    string? TypeName,
    string Function,
    long? WorkplaceId,
    int? LocationTypeId,
    string? LocationTypeName,
    long? OwnerId,
    IReadOnlyList<long> HouseholdIds,
    IReadOnlyList<long> ResidentIds,
    IReadOnlyList<long> WorkerIds,
    IReadOnlyList<StockLine> Stock,
    long PantryUnits,
    bool Closed);

/// <summary>Uma linha de estoque resumido: até 10 unidades exato, acima disso dois algarismos significativos.</summary>
public sealed record StockLine(int ResourceId, long Quantity, string? ResourceName = null);

/// <summary>Lavoura (<c>CropBatch</c>): estágio visual e fração, célula e área (hectares semeados e aráveis), cultura, fazenda e dono
/// (a oficina na célula da lavoura) e colheita esperada (o grão em pé quando madura; senão semente efetiva × rendimento × clima do
/// ano, a mesma conta do amadurecer).</summary>
public sealed record CropVisual(
    long Id, CellCoord Plot, int ResourceId, string Status, string Stage, double Fraction, bool Thirsty,
    double SownHectares, double ArableHectares, long Standing, string? ResourceName, long? FarmId, long? OwnerId, long ExpectedHarvest);

/// <summary>Animal no escopo Cidade: espécie, célula, energia em dezenas, se carrega doença e de quem é predador.</summary>
public sealed record AnimalVisual(long Id, string Species, CellCoord Position, int Energy, bool CarriesDisease, string? PredatorOf);

/// <summary>Fauna agregada no escopo Mundo: espécie por região, quantos e o centro do bando.</summary>
public sealed record FaunaGroupVisual(string Species, int RegionId, int Count, CellCoord Center);

/// <summary>Espólio no chão (AD-168): onde, de quem era, de que casa, unidades, o recipiente e quem foi buscar ou leva.</summary>
public sealed record LeavingVisual(
    long Id, CellCoord Location, long DeadId, long? HouseholdId, long? HeirHouseholdId, long Tick, long Units,
    long? ItemId, int? DesignId, long? FetcherId, long? CarrierId);

/// <summary>Objeto inventado (peça feita de um design): casa dona, quem o leva agora, onde está, se está à venda; forma e material
/// da peça raiz aqui e o resto em <c>/designs/{id}</c>.</summary>
public sealed record ObjectVisual(
    long Id, int DesignId, long HouseholdId, long? HolderId, CellCoord? Location, bool ForSale, long? MarketId,
    string? RootShape, int? RootMaterial, double QualityFraction);

/// <summary>Estação e clima do escopo: mês, índice e chave da estação (quarto do ano), temperatura do lugar (meio grau), região,
/// fator de safra do ano (milésimos, 1000 = médio) e seca.</summary>
public sealed record ScopeClimateVisual(
    int Month, int SeasonIndex, string SeasonKey, double TemperatureC, int RegionId, int CropYearFactorMilli, bool Drought,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    WeatherVisual? Weather = null);

/// <summary>AD-180: o tempo do dia no lugar (ausente com o tempo desligado). <c>precipitation</c>: none, rain, snow;
/// <c>intensity</c>: none, light (&lt;2 mm), moderate (&lt;8 mm), heavy; <c>ground</c> (chão de terra): dry, wet, mud, heavy-mud, snow,
/// frozen; temperaturas em °C (meio grau), luz em horas (décimo), nascer e pôr do sol em hora solar.</summary>
public sealed record WeatherVisual(
    string Precipitation, string Intensity, double PrecipitationMm, bool Storm, bool Hail, bool Frost, bool Drought,
    bool HeatWave, bool ColdWave, double WindMs, double TemperatureNowC, double FeelsLikeC, double MinC, double MaxC,
    double DaylightHours, double SunriseHour, double SunsetHour, string Ground, int SnowCm, int GroundWetPm, int OutdoorLostDays);

/// <summary>Envolvidos de um evento, lidos do payload do motor. Crime traz autor, vítima, caso e desfecho, sem descrição; violência
/// sexual, doméstica e contra criança vêm com <see cref="RecordOnly"/> (só registro, sem cena).</summary>
public sealed record EventInvolved(
    long? SubjectId = null,
    long? OtherId = null,
    long? AuthorId = null,
    long? VictimId = null,
    IReadOnlyList<long>? RelatedIds = null,
    long? HouseholdId = null,
    long? BuildingId = null,
    long? WorkplaceId = null,
    long? CaseId = null,
    long? LeavingId = null,
    int? DesignId = null,
    string? CityId = null,
    long? Amount = null,
    string? Outcome = null,
    bool RecordOnly = false);

/// <summary>Planta e andares de um prédio (pura do id, tipo e orientação): guardada por chave para não regerar todo tick.</summary>
public sealed record BuildingPlan(int Floors, IReadOnlyList<FootprintCell> GroundFootprint);

/// <summary>Camada <c>Climate</c> (AD-170): estação do mundo e clima por região (temperatura na célula representativa, fator de safra
/// do ano e seca).</summary>
public sealed record ClimateLayerPayload(int Month, int SeasonIndex, string SeasonKey, IReadOnlyList<RegionClimateVisual> Regions);

public sealed record RegionClimateVisual(int RegionId, CellCoord Center, double TemperatureC, int CropYearFactorMilli, bool Drought,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    WeatherVisual? Weather = null);
