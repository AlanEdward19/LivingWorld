using LivingWorld.Domain.Shared;
using LivingWorld.Infrastructure.EfCore;
using LivingWorld.Infrastructure.Records;

namespace LivingWorld.Infrastructure.Repositories;

/// <summary>Store de auditoria de admin, separado do event log (ADR-0015): ações de leitura, pausa e velocidade entram aqui,
/// nunca no mundo, então olhar não muda o hash. Só acrescenta e lista.</summary>
public sealed class AdminAuditStore(WorldDbContext context)
{
    private static readonly object Gate = new();

    public AdminAuditRecord Append(BranchId branch, long tick, string actor, string action, string target, string detail)
    {
        lock (Gate)
        {
            long next = context.AdminAudit.Where(a => a.BranchId == branch.Value).Select(a => (long?)a.Sequence).Max() + 1 ?? 0;
            var record = new AdminAuditRecord
            {
                BranchId = branch.Value,
                Sequence = next,
                Tick = tick,
                Actor = actor,
                Action = action,
                Target = target,
                Detail = detail,
            };
            context.AdminAudit.Add(record);
            context.SaveChanges();
            return record;
        }
    }

    public IReadOnlyList<AdminAuditRecord> Read(BranchId branch, long fromTick) =>
        context.AdminAudit
            .Where(a => a.BranchId == branch.Value && a.Tick >= fromTick)
            .OrderBy(a => a.Sequence)
            .ToList();
}
