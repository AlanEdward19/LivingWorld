using Microsoft.EntityFrameworkCore;

namespace LivingWorld.Infrastructure;

/// <summary>Mapeamento EF Core do mundo (task 8): só snapshot + event log (ADR-0006) — o mundo
/// não é salvo NPC a NPC. Nenhum recurso exclusivo de SQLite (ADR-0002): tipos simples, sem
/// <c>datetime()</c> nem autoincremento composto.</summary>
public sealed class WorldDbContext(DbContextOptions<WorldDbContext> options) : DbContext(options)
{
    public DbSet<WorldSnapshotRecord> Snapshots => Set<WorldSnapshotRecord>();
    public DbSet<EventLogRecord> EventLog => Set<EventLogRecord>();
    public DbSet<FactLogRecord> FactLog => Set<FactLogRecord>();
    public DbSet<PeriodTemplateRecord> PeriodTemplates => Set<PeriodTemplateRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<WorldSnapshotRecord>(e =>
        {
            e.HasKey(s => new { s.BranchId, s.Tick });
            e.HasIndex(s => s.BranchId);
        });

        modelBuilder.Entity<EventLogRecord>(e =>
        {
            e.HasKey(l => new { l.BranchId, l.Tick, l.Sequence });
            e.HasIndex(l => l.BranchId);
        });

        modelBuilder.Entity<FactLogRecord>(e =>
        {
            e.HasKey(f => new { f.BranchId, f.FactId });
            e.HasIndex(f => f.BranchId);
        });

        modelBuilder.Entity<PeriodTemplateRecord>(e =>
        {
            e.HasKey(t => new { t.PeriodId, t.Version });
            e.HasIndex(t => t.PeriodId);
        });
    }
}
