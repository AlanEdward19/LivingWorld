using System.Globalization;
using System.Text.Json.Nodes;
using LivingWorld.Api.Periods;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.History;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Observation;

namespace LivingWorld.Tests.Integration.Cities;

/// <summary>AD-190: os mundos do menu ano a ano (população, mortes, fome, sede, colheita por lavoura, estoque, preço da comida),
/// sem asserção. <c>LW_MENU_YEARS_OUT=arquivo bash scripts/test.sh --project integration --filter MenuWorldsYearsReport</c>;
/// <c>LW_MENU_YEARS</c> (padrão 20), <c>LW_MENU_SEEDS</c> (1,2,7), <c>LW_MENU_PERIODS</c> (os três do menu), <c>LW_MENU_PAR</c>
/// (mundos em paralelo, padrão 2) e <c>LW_MENU_PATCH</c> (objeto JSON fundido no payload, para ablação: ex.
/// <c>{"Climate":{"Weather":{"Enabled":false}}}</c>).</summary>
[Trait("Category", "Report")]
public class MenuWorldsYearsReport
{
    [Fact]
    public void MenuWorldsYearsReport_writes_the_years()
    {
        var path = Environment.GetEnvironmentVariable("LW_MENU_YEARS_OUT");
        if (string.IsNullOrEmpty(path)) return;
        int years = int.TryParse(Environment.GetEnvironmentVariable("LW_MENU_YEARS"), out int y) ? y : 20;
        int par = int.TryParse(Environment.GetEnvironmentVariable("LW_MENU_PAR"), out int p) ? p : 2;
        var seeds = (Environment.GetEnvironmentVariable("LW_MENU_SEEDS") ?? "1,2,7").Split(',').Select(ulong.Parse).ToList();
        var periods = (Environment.GetEnvironmentVariable("LW_MENU_PERIODS") ?? string.Join(',', DefaultPeriodSeeder.PresetIds)).Split(',');
        string? patch = Environment.GetEnvironmentVariable("LW_MENU_PATCH");
        var blocks = new List<string>[periods.Length * seeds.Count];
        using var output = new StreamWriter(path) { AutoFlush = true };
        // o maior mundo primeiro (termina por último)
        var order = Enumerable.Range(0, blocks.Length).OrderByDescending(i => i / seeds.Count).ToArray();
        Parallel.ForEach(order, new ParallelOptions { MaxDegreeOfParallelism = par }, i =>
        {
            string period = periods[i / seeds.Count];
            ulong seed = seeds[i % seeds.Count];
            var lines = Run(period, seed, years, patch);
            lock (output) foreach (var l in lines) output.WriteLine(l);
        });
    }

    public static List<string> Run(string period, ulong seed, int years, string? patch)
    {
        var inv = CultureInfo.InvariantCulture;
        var (world, baseClock) = Load(period, seed, patch);
        var tally = new FoodTally(world);
        var deaths = new DeathCount();
        var traced = new List<string>();
        IWorldEventSink[] sinks = Environment.GetEnvironmentVariable("LW_MENU_TRACE") == "1"
            ? [tally, deaths, new StarvationTrace(world, seed, traced)] : [tally, deaths];
        var clock = new WorldClock(baseClock.Systems, baseClock.MaxIterationsPerTick, new Fan(sinks));
        var food = new ResourceType(world.EconomyRules.FoodResourceId);
        int pop0 = world.AliveNpcIndex.Alive.Count;
        var lines = new List<string>();
        long personYears = 0, starvTotal = 0, thirstTotal = 0;
        bool monthly = Environment.GetEnvironmentVariable("LW_MENU_MONTHLY") == "1";
        bool farmsOn = Environment.GetEnvironmentVariable("LW_MENU_FARMS") == "1";
        for (int yr = 1; yr <= years; yr++)
        {
            int alive0 = world.AliveNpcIndex.Alive.Count;
            long starv = 0, thirst = 0;
            var farmDiag = new SortedDictionary<long, FarmYear>();
            for (int m = 0; m < world.Calendar.MonthsPerYear; m++)
            {
                if (farmsOn)
                {
                    for (int d = 0; d < world.Calendar.DaysPerMonth; d++)
                    {
                        clock.Run(world, 10);
                        foreach (var f in world.Workplaces.Where(w => w.LocationType.Id == 1 && !w.Closed))
                        {
                            var fy = farmDiag.TryGetValue(f.Id.Value, out var x) ? x : farmDiag[f.Id.Value] = new FarmYear();
                            int present = 0;
                            foreach (var e in f.Employees)
                                if (world.FindNpc(e) is { IsAlive: true } n && n.CurrentLocation == f.Location) present++;
                            fy.EmpSum += f.Employees.Count; fy.PresentSum += present; fy.Days++;
                            fy.TempSum[m] += LivingWorld.Simulation.Economy.Production.CropSystem.ReadCellTemperature(world, f.Location, world.CurrentDate.TotalHours);
                            if (world.FindCropAt(f.Location) is { } c)
                            {
                                if (c.Status == LivingWorld.Domain.Economy.CropStatus.Mature)
                                {
                                    if (fy.RipeMonth < 0) { fy.RipeMonth = m; fy.Standing0 = c.Standing; }
                                    fy.MatureDays++; fy.MaturePresent += present;
                                }
                                if (m == 3) fy.DevApr = c.Development;
                                if (m == 6) fy.DevJul = c.Development;
                            }
                        }
                        clock.Run(world, world.Calendar.HoursPerDay - 10);
                    }
                }
                else clock.Run(world, world.Calendar.HoursPerMonth);
                tally.SampleMonth();
                starv += tally.StarvationByMonth[^1];
                thirst += tally.DehydrationByMonth[^1];
                if (monthly)
                {
                    var fs = world.Workplaces.Where(w => w.LocationType.Id == 1 && !w.Closed).ToList();
                    lines.Add(string.Create(inv,
                        $"MES {period} seed={seed} ano={yr} mes={m} pop={world.AliveNpcIndex.Alive.Count} fome={tally.StarvationByMonth[^1]} " +
                        $"celeiro={world.CropBatches.Sum(c => c.Barn)} semente={world.CropBatches.Sum(c => c.SeedReserve)} venda={fs.Sum(f => f.Stock.GetValueOrDefault(food))} " +
                        $"casas={world.Households.Sum(h => h.Stock.GetValueOrDefault(food))} preco={(fs.Count == 0 ? 0 : fs.Average(f => (double)f.Prices.GetValueOrDefault(food))):F1} " +
                        $"prod={tally.ProducedByMonth[^1]} cons={tally.ConsumedByMonth[^1]} lavr={fs.Sum(f => f.Employees.Count)} " +
                        $"campos={string.Join(',', world.CropBatches.OrderBy(c => c.Id.Value).Select(c => string.Create(inv, $"{c.Status.ToString()[0]}{c.Development:F0}/{c.LastSeedSown}/{c.Standing}")))}"));
                }
            }
            int alive = world.AliveNpcIndex.Alive.Count;
            personYears += (alive0 + alive) / 2;
            starvTotal += starv; thirstTotal += thirst;
            var farms = world.Workplaces.Where(w => w.LocationType.Id == 1 && !w.Closed).ToList();
            double price = farms.Count == 0 ? 0 : farms.Average(f => (double)f.Prices.GetValueOrDefault(food));
            long barn = farms.Sum(f => f.Stock.GetValueOrDefault(food)) + world.CropBatches.Sum(c => c.Barn);
            long homes = world.Households.Sum(h => h.Stock.GetValueOrDefault(food));
            int workers = farms.Sum(f => f.Employees.Count);
            string crops = string.Join(' ', world.CropBatches.OrderBy(c => c.Id.Value)
                .Select(c => string.Create(inv, $"{c.Plot.X}:{c.Plot.Y}={c.LastHarvest}/{c.CropSeed}/{c.Development:F0}{(c.Status.ToString()[0])}")));
            lines.Add(string.Create(inv,
                $"{period} seed={seed} ano={yr} pop={alive} mortes={deaths.Deaths} fome={starv} sede={thirst} colheita={tally.HarvestByYear[^1]} " +
                $"semeado={tally.SeedSownByYear[^1]} celeiro={barn} casas={homes} estoque={tally.StockByMonth[^1]} preco={price:F1} " +
                $"lavradores={workers} fazendas={farms.Count} clima={tally.ClimateByYear[^1]} | {crops}"));
            foreach (var (id, fy) in farmDiag)
            {
                var f = world.Workplaces.First(w => w.Id.Value == id);
                lines.Add(string.Create(inv,
                    $"FAZENDA {period} seed={seed} ano={yr} {f.Location.X}:{f.Location.Y} emp={fy.EmpSum / (double)Math.Max(1, fy.Days):F1} presente10h={fy.PresentSum / (double)Math.Max(1, fy.Days):F1} " +
                    $"madura mes={fy.RipeMonth} dias={fy.MatureDays} presenteCeifa={fy.MaturePresent / (double)Math.Max(1, fy.MatureDays):F1} emPe={fy.Standing0} devAbr={fy.DevApr:F0} devJul={fy.DevJul:F0} " +
                    $"temp=[{string.Join(',', fy.TempSum.Select(t => (t / world.Calendar.DaysPerMonth).ToString("F1", inv)))}]"));
            }
            deaths.Deaths = 0;
            lock (traced) { foreach (var t in traced) lines.Add($"MORTE {period} {t}"); traced.Clear(); }
            if (alive == 0) break;
        }
        int popEnd = world.AliveNpcIndex.Alive.Count;
        lines.Add(string.Create(inv,
            $"RESUMO {period} seed={seed} anos={years} pop0={pop0} popFim={popEnd} razao={popEnd / (double)Math.Max(1, pop0):F2} " +
            $"fome={starvTotal} sede={thirstTotal} fomePorMilPessoaAno={starvTotal * 1000.0 / Math.Max(1, personYears):F2}"));
        return lines;
    }

    public static (WorldState, WorldClock) Load(string period, ulong seed, string? patch)
    {
        var root = JsonNode.Parse(DefaultPeriodSeeder.PayloadFor(period))!.AsObject();
        root["Seed"] = seed;
        if (!string.IsNullOrEmpty(patch)) Merge(root, JsonNode.Parse(patch)!.AsObject());
        var loaded = LivingWorld.Simulation.Scenarios.ScenarioLoaderV2.LoadWorld(root.ToJsonString());
        if (!loaded.IsSuccess) throw new InvalidOperationException($"{period}: {loaded.Error}");
        return loaded.Value;
    }

    private static void Merge(JsonObject into, JsonObject from)
    {
        foreach (var (k, v) in from.ToList())
        {
            if (v is JsonObject o && into[k] is JsonObject target) Merge(target, o);
            else into[k] = v?.DeepClone();
        }
    }

    private sealed class FarmYear
    {
        public long EmpSum, PresentSum, Days, MatureDays, MaturePresent, Standing0;
        public int RipeMonth = -1;
        public double DevApr, DevJul;
        public double[] TempSum = new double[12];
    }

    private sealed class DeathCount : IWorldEventSink
    {
        public long Deaths;

        public void Record(WorldEvent evt)
        {
            if (evt.SourceSystem != YearEventTally.DeathSource) return;
            if (evt.Kind is WorldEventKind.Death or WorldEventKind.Starvation or WorldEventKind.Dehydration or WorldEventKind.Exhaustion
                or WorldEventKind.Disease or WorldEventKind.MaternalDeath or WorldEventKind.Accident or WorldEventKind.OldAge
                or WorldEventKind.Homicide or WorldEventKind.Executed) Deaths++;
        }
    }

    private sealed class Fan(params IWorldEventSink[] sinks) : IWorldEventSink
    {
        public void Record(WorldEvent evt)
        {
            foreach (var s in sinks) s.Record(evt);
        }
    }
}
