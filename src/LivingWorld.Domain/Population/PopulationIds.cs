namespace LivingWorld.Domain;

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

/// <summary>Profissão, identificada por id vindo do cenário (task 7). Não usada por
/// <see cref="Npc"/> na Fase 3 (out of scope — ver Fase 5/6): existe desde já para o catálogo
/// e o teste de arquitetura não retrofitarem depois, mesmo padrão de <c>BranchId</c> (ADR-0009).</summary>
public readonly record struct ProfessionType(int Id);

/// <summary>Tipo de local (casa, oficina...), identificado por id vindo do cenário (task 7).
/// Mesma razão de existir cedo que <see cref="ProfessionType"/>.</summary>
public readonly record struct LocationType(int Id);
