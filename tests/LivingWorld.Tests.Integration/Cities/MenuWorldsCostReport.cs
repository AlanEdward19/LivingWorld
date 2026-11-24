using System.Diagnostics;
using LivingWorld.Api.Periods;
using LivingWorld.Simulation.Scenarios;
using Microsoft.EntityFrameworkCore;

namespace LivingWorld.Tests.Integration.Cities;

/// <summary>AD-179: custo dos mundos do menu, µs por NPC-tick do relógio (sem sonda), um mundo por vez. Rode com
/// <c>LW_MENU_COST_OUT=arquivo bash scripts/test.sh --project integration --filter MenuWorldsCostReport</c>; <c>LW_MENU_COST_MONTHS</c>
/// (padrão 3) e seeds 1, 2, 7. Só lê o payload pelo seeder (o mesmo do jogo).</summary>
[Trait("Category", "Report")]
public class MenuWorldsCostReport
{
    [Fact]
    public void MenuWorldsCostReport_writes_micros_per_npc_tick()
    {
        var path = Environment.GetEnvironmentVariable("LW_MENU_COST_OUT");
        if (string.IsNullOrEmpty(path)) return;
        int months = int.TryParse(Environment.GetEnvironmentVariable("LW_MENU_COST_MONTHS"), out int m) ? m : 3;
        using var output = new StreamWriter(path);
        foreach (var period in new[] { "vila-pequena", "cidade-media", "grande-metropole" })
            foreach (ulong seed in new ulong[] { 1, 2, 7 })
            {
                using var connection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
                connection.Open();
                var options = new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<LivingWorld.Infrastructure.EfCore.WorldDbContext>()
                    .UseSqlite(connection).Options;
                using var context = new LivingWorld.Infrastructure.EfCore.WorldDbContext(options);
                Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.Migrate(context.Database);
                var repository = new LivingWorld.Infrastructure.Repositories.SqlitePeriodTemplateRepository(context);
                DefaultPeriodSeeder.SeedIfEmpty(repository);
                var root = System.Text.Json.Nodes.JsonNode.Parse(repository.FindLatestVersion(period)!.PayloadJson)!;
                root["Seed"] = seed;
                var (world, clock) = ScenarioLoaderV2.LoadWorld(root.ToJsonString()).Value;
                long hours = months * world.Calendar.HoursPerMonth;
                long npcTicks = 0;
                var watch = new Stopwatch();
                long alloc0 = GC.GetAllocatedBytesForCurrentThread();
                for (long h = 0; h < hours; h++)
                {
                    npcTicks += world.AliveNpcIndex.Alive.Count;
                    watch.Start();
                    clock.Run(world, 1);
                    watch.Stop();
                }
                long alloc = GC.GetAllocatedBytesForCurrentThread() - alloc0;
                output.WriteLine(System.FormattableString.Invariant(
                    $"custo {period} seed={seed} meses={months} pop={world.AliveNpcIndex.Alive.Count} usPorNpcTick={watch.Elapsed.TotalMilliseconds * 1000 / Math.Max(1, npcTicks):F3} bytesPorNpcTick={alloc / (double)Math.Max(1, npcTicks):F0}"));
                output.Flush();
            }
    }
}
