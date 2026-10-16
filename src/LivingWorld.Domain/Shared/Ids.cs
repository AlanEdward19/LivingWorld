using LivingWorld.Domain.Cities;
using LivingWorld.Domain.Population;

namespace LivingWorld.Domain.Shared;

/// <summary>Base para IDs tipados: um wrapper que impede trocar um NpcId por um CityId por
/// engano na assinatura de um método.</summary>
/// <remarks><see cref="NpcId"/> e <see cref="HouseholdId"/> vêm de contador monotônico do
/// <c>WorldState</c> — <c>Guid.NewGuid()</c> é banido em Domain/Simulation
/// (rules/simulation-determinism.md), então o id precisa nascer determinístico.</remarks>
public readonly record struct NpcId(long Value)
{
    public override string ToString() => $"npc-{Value}";
}

public readonly record struct HouseholdId(long Value)
{
    public override string ToString() => $"household-{Value}";
}

/// <summary>Local econômico (produção/estoque/emprego/mercado) — id determinístico
/// novo, mesmo molde de <see cref="NpcId"/>/<see cref="HouseholdId"/>; não reusa
/// <see cref="LocationId"/> (Guid, reservado para uso futuro).</summary>
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

/// <summary>Edifício de uma <see cref="City"/> — id monotônico, mesmo molde de
/// <see cref="WorkplaceId"/> (nasce de contador do <c>WorldState</c>, nunca de Guid).</summary>
public readonly record struct BuildingId(long Value)
{
    public override string ToString() => $"building-{Value}";
}

/// <summary>Móvel/lugar de descanso no mundo — id monotônico, mesmo
/// molde de <see cref="BuildingId"/>.</summary>
public readonly record struct RestPlaceId(long Value)
{
    public override string ToString() => $"rest-{Value}";
}

/// <summary>Processo material estagiado — id monotônico.</summary>
public readonly record struct ResourceProcessId(long Value)
{
    public override string ToString() => $"process-{Value}";
}

/// <summary>Lote de cultivo — id monotônico.</summary>
public readonly record struct CropBatchId(long Value)
{
    public override string ToString() => $"crop-{Value}";
}

/// <summary>Linha temporal a que uma entidade/evento pertence. Até a fase temporal
/// existe só <see cref="Root"/> — nada ramifica ainda, mas todo esquema e toda consulta já
/// recebem o valor como parâmetro explícito, nunca implícito.</summary>
public readonly record struct BranchId(long Value)
{
    public static readonly BranchId Root = new(0);

    public override string ToString() => $"branch-{Value}";
}

/// <summary>Fato histórico imutável — id monotônico, mesmo molde de
/// <see cref="NpcId"/>/<see cref="WorkplaceId"/>.</summary>
public readonly record struct FactId(long Value)
{
    public override string ToString() => $"fact-{Value}";
}

/// <summary>Relato histórico canônico — id monotônico, mesmo molde de
/// <see cref="FactId"/>.</summary>
public readonly record struct ReportId(long Value)
{
    public override string ToString() => $"report-{Value}";
}

/// <summary>Instância física de um relato em meio Livro — id monotônico,
/// mesmo molde de <see cref="ReportId"/>.</summary>
public readonly record struct BookId(long Value)
{
    public override string ToString() => $"book-{Value}";
}

/// <summary>Documento narrativo publicado — crônica/biografia/relato — id
/// monotônico, mesmo molde de <see cref="ReportId"/>.</summary>
public readonly record struct NarrativeId(long Value)
{
    public override string ToString() => $"narrative-{Value}";
}

/// <summary>Rota cosmética pendente de um NPC — id monotônico, mesmo molde de
/// <see cref="NpcId"/>; resolvida por <see cref="ILazyPositionWorld"/> em
/// <see cref="LazyPosition.ValueAt"/>.</summary>
public readonly record struct RouteId(long Value)
{
    public override string ToString() => $"route-{Value}";
}
