using LivingWorld.Domain.Shared;
using LivingWorld.Infrastructure.Records;

namespace LivingWorld.Infrastructure.Repositories;

/// <summary>Persistência de templates oficiais de período (Fase 13, T4). <see
/// cref="Save"/> nunca sobrescreve uma versão já registrada — conflito de versão vira
/// <see cref="Result{T}.Fail"/> determinístico (PERIOD-07..10), quem chama decide o código HTTP
/// (409 na rota de cadastro, T5).</summary>
public interface IPeriodTemplateRepository
{
    Result<Unit> Save(PeriodTemplateRecord template);

    PeriodTemplateRecord? FindLatestVersion(string periodId);

    PeriodTemplateRecord? Find(string periodId, int version);

    /// <summary>Catálogo de períodos registrados (T5, <c>GET /periods</c>): a versão mais recente
    /// de cada <see cref="PeriodTemplateRecord.PeriodId"/> distinto.</summary>
    IReadOnlyList<PeriodTemplateRecord> ListLatestPerPeriod();
}
