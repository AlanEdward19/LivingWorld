using System.Text.Json.Serialization;

namespace LivingWorld.Domain.Cities;

/// <summary>#258: o chão que o cenário já entrega pronto à vila: a rua do traçado pavimentada e a calçada em volta dela (e entre a porta de cada prédio e a rua),
/// com a superfície escolhida pelo cenário (<c>Gravel</c> ou <c>Stone</c>). Sem isto o chão nasce só do pisoteio (AD-176). Nada no código sabe o que é rua
/// de subúrbio: o cenário diz a superfície e a largura da calçada.</summary>
/// <param name="Street">Superfície da rua do traçado (<c>Gravel</c> ou <c>Stone</c>).</param>
/// <param name="Sidewalk">Superfície da calçada (<c>Gravel</c> ou <c>Stone</c>); nulo, sem calçada.</param>
/// <param name="SidewalkWidth">Largura da calçada, em células, de cada lado da rua.</param>
public sealed record PavementSeed(string Street = "Stone", string? Sidewalk = "Gravel", int SidewalkWidth = 1);

// Parte de StreetRules — o entorno do lote como parâmetro de cenário (#258).
public sealed partial record StreetRules
{
    /// <summary>#258: o chão pavimentado de partida (rua e calçada). Nulo = o chão nasce do uso (sem mudança para quem não declara).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public PavementSeed? Pavement { get; init; }

    /// <summary>#258: quintal limpo: não há árvore a até esta distância (em células) de qualquer prédio, nem sobre a rua e a calçada. 0 = a regra de antes
    /// (o sorteio de árvores por célula, só fora das cidades).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int ClearAroundBuildingsCells { get; init; }
}
