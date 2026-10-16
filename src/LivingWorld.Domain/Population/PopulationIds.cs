using LivingWorld.Domain.Geography;

namespace LivingWorld.Domain.Population;

/// <summary>Sexo biológico do NPC. Não é dado de cenário — ao contrário de cultura,
/// profissão etc., não varia por conteúdo medieval vs. sci-fi.</summary>
public enum Sex
{
    Female,
    Male,
}

/// <summary>Cultura, identificada por id vindo do cenário — o motor nunca conhece o
/// nome, só o id, mesmo padrão de <see cref="TerrainType"/>.</summary>
public readonly record struct CultureId(int Id);

/// <summary>Profissão, identificada por id vindo do cenário. Usada por
/// <see cref="Npc"/> como atributo estático, sem emprego real ainda.</summary>
public readonly record struct ProfessionType(int Id)
{
    /// <summary>Sentinela "sem profissão" — usada quando
    /// <c>PopulationCatalog.ProfessionIds</c> está vazio no momento do sorteio. Nunca colide
    /// com um id de catálogo real (ids de cenário são sempre &gt;= 0).</summary>
    public static readonly ProfessionType None = new(-1);
}

/// <summary>Tipo de local (casa, oficina...), identificado por id vindo do cenário.
/// Mesma razão de existir cedo que <see cref="ProfessionType"/>.</summary>
public readonly record struct LocationType(int Id);
