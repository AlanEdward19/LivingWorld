using LivingWorld.Domain;
using Microsoft.EntityFrameworkCore;

namespace LivingWorld.Infrastructure;

/// <summary>Implementação EF Core de <see cref="IPeriodTemplateRepository"/> (Fase 13, T4). Mesmo
/// molde de <see cref="SqliteWorldRepository"/> — nenhum recurso exclusivo de SQLite entra no
/// mapeamento (ADR-0002).</summary>
public sealed class SqlitePeriodTemplateRepository(WorldDbContext context) : IPeriodTemplateRepository
{
    public Result<Unit> Save(PeriodTemplateRecord template)
    {
        bool exists = context.PeriodTemplates
            .Any(t => t.PeriodId == template.PeriodId && t.Version == template.Version);
        if (exists)
            return Result<Unit>.Fail($"PeriodId {template.PeriodId} versão {template.Version} já está registrado");

        context.PeriodTemplates.Add(template);
        context.SaveChanges();
        return Result<Unit>.Ok(Unit.Value);
    }

    public PeriodTemplateRecord? FindLatestVersion(string periodId) =>
        context.PeriodTemplates
            .Where(t => t.PeriodId == periodId)
            .OrderByDescending(t => t.Version)
            .FirstOrDefault();

    public PeriodTemplateRecord? Find(string periodId, int version) =>
        context.PeriodTemplates
            .SingleOrDefault(t => t.PeriodId == periodId && t.Version == version);

    public IReadOnlyList<PeriodTemplateRecord> ListLatestPerPeriod() =>
        context.PeriodTemplates
            .AsEnumerable() // agrupar em memória — EF Core não traduz bem GroupBy+First aninhado, catálogo é pequeno
            .GroupBy(t => t.PeriodId)
            .Select(g => g.OrderByDescending(t => t.Version).First())
            .OrderBy(t => t.PeriodId)
            .ToList();
}
