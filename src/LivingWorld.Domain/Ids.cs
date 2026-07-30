namespace LivingWorld.Domain;

/// <summary>Base para IDs tipados: um wrapper que impede trocar um NpcId por um CityId por
/// engano na assinatura de um método.</summary>
/// <remarks><see cref="NpcId"/> e <see cref="HouseholdId"/> vêm de contador monotônico do
/// <c>WorldState</c> (Fase 3) — <c>Guid.NewGuid()</c> é banido em Domain/Simulation
/// (rules/simulation-determinism.md), então o id precisa nascer determinístico.</remarks>
public readonly record struct NpcId(long Value)
{
    public override string ToString() => $"npc-{Value}";
}

public readonly record struct HouseholdId(long Value)
{
    public override string ToString() => $"household-{Value}";
}

/// <summary>Local econômico (produção/estoque/emprego/mercado) da Fase 5 — id determinístico
/// novo (AD-039), mesmo molde de <see cref="NpcId"/>/<see cref="HouseholdId"/>; não reusa
/// <see cref="LocationId"/> (Guid, reservado à Fase 8).</summary>
public readonly record struct WorkplaceId(long Value)
{
    public override string ToString() => $"workplace-{Value}";
}

public readonly record struct CityId(Guid Value)
{
    public override string ToString() => Value.ToString();
}

public readonly record struct LocationId(Guid Value)
{
    public override string ToString() => Value.ToString();
}

/// <summary>Edifício de uma <see cref="City"/> (Fase 8, T3) — id monotônico, mesmo molde de
/// <see cref="WorkplaceId"/> (nasce de contador do <c>WorldState</c>, nunca de Guid).</summary>
public readonly record struct BuildingId(long Value)
{
    public override string ToString() => $"building-{Value}";
}

/// <summary>Linha temporal a que uma entidade/evento pertence (ADR-0009). Até a fase temporal
/// existe só <see cref="Root"/> — nada ramifica ainda, mas todo esquema e toda consulta já
/// recebem o valor como parâmetro explícito, nunca implícito.</summary>
public readonly record struct BranchId(long Value)
{
    public static readonly BranchId Root = new(0);

    public override string ToString() => $"branch-{Value}";
}

/// <summary>Fato histórico imutável (Fase 10, HIST-01) — id monotônico, mesmo molde de
/// <see cref="NpcId"/>/<see cref="WorkplaceId"/>.</summary>
public readonly record struct FactId(long Value)
{
    public override string ToString() => $"fact-{Value}";
}
