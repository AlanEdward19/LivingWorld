using System.Linq.Expressions;

namespace LivingWorld.Infrastructure.Repositories;

/// <summary>Formato "latest por chave composta" / "lookup exato por versão" compartilhado por
/// <see cref="SqliteWorldRepository"/> (branch+tick) e <see cref="SqlitePeriodTemplateRepository"/>
/// (periodId+version) — mesma consulta, chave diferente. Expressões (não <c>Func</c>) pra
/// continuar traduzindo pra SQL via EF Core, não avaliar em memória.</summary>
internal static class VersionedQuery
{
    public static TRecord? Latest<TRecord, TVersion>(
        IQueryable<TRecord> source, Expression<Func<TRecord, bool>> partition, Expression<Func<TRecord, TVersion>> version) =>
        source.Where(partition).OrderByDescending(version).FirstOrDefault();

    public static TRecord? Exact<TRecord>(IQueryable<TRecord> source, Expression<Func<TRecord, bool>> match) =>
        source.SingleOrDefault(match);
}
