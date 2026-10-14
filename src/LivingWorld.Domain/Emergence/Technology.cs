using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;

namespace LivingWorld.Domain.Emergence;

/// <summary>Identidade de uma tecnologia: só o vetor de eixos inteiros, nunca um nome (ADR-0013). Dois vetores iguais são a
/// mesma tecnologia; a distância de Manhattan entre eles decide variação ou entidade nova.</summary>
public sealed record TechnologyVector(int[] Axes)
{
    public string Key => string.Join('.', Axes);

    public int DistanceTo(TechnologyVector other)
    {
        if (other.Axes.Length != Axes.Length) return int.MaxValue;
        int sum = 0;
        for (int i = 0; i < Axes.Length; i++) sum += Math.Abs(Axes[i] - other.Axes[i]);
        return sum;
    }

    public bool Equals(TechnologyVector? other) => other is not null && Axes.AsSpan().SequenceEqual(other.Axes);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (int axis in Axes) hash.Add(axis);
        return hash.ToHashCode();
    }
}

/// <summary>Tecnologia conhecida por uma cultura. <see cref="VariationOfKey"/> preenchido = variação de uma tecnologia
/// existente (distância abaixo do limiar de novidade), não entidade nova. <see cref="AdoptedBy"/> são as cidades da
/// cultura que a usam, em ordem de adoção.</summary>
public sealed record Technology(
    CultureId Culture,
    string Category,
    TechnologyVector Vector,
    long DiscoveredAtTick,
    CityId OriginCity,
    string? VariationOfKey,
    IReadOnlyList<CityId> AdoptedBy)
{
    public bool IsVariation => VariationOfKey is not null;
}
