using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace LivingWorld.Infrastructure.EfCore;

/// <summary>Fábrica de design-time só para <c>dotnet ef migrations add</c> — runtime real monta
/// as options via DI (Workers/Api), sempre com o caminho do arquivo `.db` do mundo (ADR-0002).</summary>
public sealed class WorldDbContextFactory : IDesignTimeDbContextFactory<WorldDbContext>
{
    public WorldDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<WorldDbContext>()
            .UseSqlite("Data Source=design-time.db")
            .Options;
        return new WorldDbContext(options);
    }
}
