using LivingWorld.Api.Periods;
using LivingWorld.Simulation.Core;
using LivingWorld.Tests.Shared.Cities;

namespace LivingWorld.Tests.Integration.Cities;

/// <summary>AD-179: diagnóstico dos mundos do menu ao lado da vila de código, sem asserção. Seeds 1, 2 e 7; meses 1, 3 e 12
/// (<c>LW_MENU_MONTHS</c> muda). Rode com <c>LW_MENU_WORLDS_OUT=arquivo bash scripts/test.sh --project integration --filter
/// MenuWorldsReport</c>. <c>LW_MENU_PERIODS</c> (lista separada por vírgula) e <c>LW_MENU_SEEDS</c> restringem.</summary>
[Trait("Category", "Report")]
public class MenuWorldsReport
{
    [Fact]
    public void MenuWorldsReport_writes_the_numbers()
    {
        var path = Environment.GetEnvironmentVariable("LW_MENU_WORLDS_OUT");
        if (string.IsNullOrEmpty(path)) return;
        var months = (Environment.GetEnvironmentVariable("LW_MENU_MONTHS") ?? "1,3,12").Split(',').Select(int.Parse).ToList();
        var seeds = (Environment.GetEnvironmentVariable("LW_MENU_SEEDS") ?? "1,2,7").Split(',').Select(ulong.Parse).ToList();
        var periods = (Environment.GetEnvironmentVariable("LW_MENU_PERIODS") ?? "padrao," + string.Join(',', DefaultPeriodSeeder.PresetIds)).Split(',');
        var lines = new string[periods.Length * seeds.Count][];
        // um mundo por seed e período, em paralelo (só leitura do payload; cada mundo é dele)
        Parallel.For(0, lines.Length, new ParallelOptions { MaxDegreeOfParallelism = 2 }, i =>
        {
            string period = periods[i / seeds.Count];
            ulong seed = seeds[i % seeds.Count];
            var (world, clock) = MenuWorlds.Load(period, seed);
            var probe = new MenuWorldProbe(world);
            var sampled = new WorldClock(clock.Systems, clock.MaxIterationsPerTick, probe);
            var mine = new List<string>();
            int done = 0;
            foreach (int mark in months)
            {
                long hours = (mark - done) * world.Calendar.HoursPerMonth;
                for (long h = 0; h < hours; h++)
                {
                    sampled.Run(world, 1);
                    probe.SampleHour();
                }
                done = mark;
                mine.Add(probe.Snapshot($"{period} seed={seed} mes={mark}"));
            }
            lines[i] = [.. mine];
        });
        using var output = new StreamWriter(path);
        foreach (var block in lines)
            foreach (var line in block) output.WriteLine(line);
    }
}
