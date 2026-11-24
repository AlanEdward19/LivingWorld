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

    /// <summary>AD-184: uma linha por morte de fome ou sede num mundo do menu (<c>StarvationTrace</c>: casa, despensa, emprego, onde
    /// mora, se largou o ofício, se está em obra). <c>LW_MENU_STARVATION_OUT=arquivo</c>, <c>LW_MENU_PERIODS</c>, <c>LW_MENU_SEEDS</c>,
    /// <c>LW_MENU_YEARS</c> (padrão 20).</summary>
    [Fact]
    public void MenuWorldsStarvation_writes_one_line_per_death()
    {
        var path = Environment.GetEnvironmentVariable("LW_MENU_STARVATION_OUT");
        if (string.IsNullOrEmpty(path)) return;
        int years = int.Parse(Environment.GetEnvironmentVariable("LW_MENU_YEARS") ?? "20");
        var seeds = (Environment.GetEnvironmentVariable("LW_MENU_SEEDS") ?? "2").Split(',').Select(ulong.Parse).ToList();
        var periods = (Environment.GetEnvironmentVariable("LW_MENU_PERIODS") ?? "grande-metropole").Split(',');
        var lines = new List<string>();
        Parallel.For(0, periods.Length * seeds.Count, new ParallelOptions { MaxDegreeOfParallelism = 2 }, i =>
        {
            var (world, clock) = MenuWorlds.Load(periods[i / seeds.Count], seeds[i % seeds.Count]);
            var mine = new List<string>();
            var traced = new WorldClock(clock.Systems, clock.MaxIterationsPerTick,
                new LivingWorld.Simulation.Observation.StarvationTrace(world, seeds[i % seeds.Count], mine));
            for (int y = 0; y < years; y++)
            {
                traced.Run(world, world.Calendar.HoursPerYear);
                mine.Add($"{seeds[i % seeds.Count]} ano={y + 1} pop={world.AliveNpcIndex.Alive.Count} empregados={world.AliveNpcIndex.Alive.Count(n => n.Employer is not null)} locais={world.Workplaces.Count(w => !w.Closed)} obras={world.Ground?.Builds.Count}");
            }
            lock (lines) lines.AddRange(mine.Select(l => periods[i / seeds.Count] + " " + l));
        });
        File.WriteAllLines(path, lines);
    }
}
