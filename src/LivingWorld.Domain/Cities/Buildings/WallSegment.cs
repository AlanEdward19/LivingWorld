using LivingWorld.Domain.Geography;

namespace LivingWorld.Domain.Cities.Buildings;

/// <summary>#226: a aresta entre duas células vizinhas (coordenadas locais da planta; <c>A</c> vem antes de <c>B</c> por linha e coluna). A parede
/// não ocupa célula: fica na aresta, e a célula de cada lado é piso inteiro. Uma das células pode ser de fora da planta (parede externa).</summary>
public readonly record struct WallEdge(CellCoord A, CellCoord B)
{
    /// <summary>A aresta entre <paramref name="a"/> e <paramref name="b"/> (vizinhas de 4), na forma canônica (a ordem não importa).</summary>
    public static WallEdge Between(CellCoord a, CellCoord b) =>
        a.Y < b.Y || (a.Y == b.Y && a.X < b.X) ? new WallEdge(a, b) : new WallEdge(b, a);
}

/// <summary>Um trecho de parede (ou, com <see cref="BuildingMaterial.Door"/>, a abertura da porta) numa aresta, com o material para o cliente desenhar.
/// Contrato aditivo: o cliente desenha parede onde o motor diz.</summary>
public sealed record WallSegment(CellCoord A, CellCoord B, BuildingMaterial Material)
{
    [System.Text.Json.Serialization.JsonIgnore]
    public WallEdge Edge => WallEdge.Between(A, B);
}
