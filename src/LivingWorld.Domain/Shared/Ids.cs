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

/// <summary>Móvel/lugar de descanso no mundo (Fase 15.1, Stage 4, T12) — id monotônico, mesmo
/// molde de <see cref="BuildingId"/>.</summary>
public readonly record struct RestPlaceId(long Value)
{
    public override string ToString() => $"rest-{Value}";
}

/// <summary>Processo material estagiado (Fase 15.1, Stage 4, T14) — id monotônico.</summary>
public readonly record struct ResourceProcessId(long Value)
{
    public override string ToString() => $"process-{Value}";
}

/// <summary>Lote de cultivo (Fase 15.1, Stage 4, T17) — id monotônico.</summary>
public readonly record struct CropBatchId(long Value)
{
    public override string ToString() => $"crop-{Value}";
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

/// <summary>Relato histórico canônico (Fase 10, HIST-01 AC4) — id monotônico, mesmo molde de
/// <see cref="FactId"/>.</summary>
public readonly record struct ReportId(long Value)
{
    public override string ToString() => $"report-{Value}";
}

/// <summary>Instância física de um relato em meio Livro (Fase 10, HIST-09) — id monotônico,
/// mesmo molde de <see cref="ReportId"/>.</summary>
public readonly record struct BookId(long Value)
{
    public override string ToString() => $"book-{Value}";
}

/// <summary>Documento narrativo publicado — crônica/biografia/relato (Fase 12, NARR-01) — id
/// monotônico, mesmo molde de <see cref="ReportId"/>.</summary>
public readonly record struct NarrativeId(long Value)
{
    public override string ToString() => $"narrative-{Value}";
}

/// <summary>Rota cosmética pendente de um NPC (Fase 28, LOD-10) — id monotônico, mesmo molde de
/// <see cref="NpcId"/>; resolvida por <see cref="ILazyPositionWorld"/> em
/// <see cref="LazyPosition.ValueAt"/>.</summary>
public readonly record struct RouteId(long Value)
{
    public override string ToString() => $"route-{Value}";
}
