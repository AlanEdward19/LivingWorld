using LivingWorld.Domain.Geography;

namespace LivingWorld.Domain.Population;

/// <summary>Sexo biológico do NPC (task 1). Não é dado de cenário — ao contrário de cultura,
/// profissão etc., não varia por conteúdo medieval vs. sci-fi.</summary>
public enum Sex
{
    Female,
    Male,
}

/// <summary>Cultura, identificada por id vindo do cenário (task 7) — o motor nunca conhece o
/// nome, só o id, mesmo padrão de <see cref="TerrainType"/>.</summary>
public readonly record struct CultureId(int Id);

/// <summary>Profissão, identificada por id vindo do cenário (task 7). Usada por
/// <see cref="Npc"/> desde a Fase 4 (task 6/7) como atributo estático, sem emprego real ainda
/// (Fase 5).</summary>
public readonly record struct ProfessionType(int Id)
{
    /// <summary>Sentinela "sem profissão" (task 7) — usada quando
    /// <c>PopulationCatalog.ProfessionIds</c> está vazio no momento do sorteio. Nunca colide
    /// com um id de catálogo real (ids de cenário são sempre &gt;= 0).</summary>
    public static readonly ProfessionType None = new(-1);
}

/// <summary>Tipo de local (casa, oficina...), identificado por id vindo do cenário (task 7).
/// Mesma razão de existir cedo que <see cref="ProfessionType"/>.</summary>
public readonly record struct LocationType(int Id);
