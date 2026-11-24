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
        var deaths = new DeathCount { World = world, CountKinds = Environment.GetEnvironmentVariable("LW_MENU_EVENTS") == "1" };
        var traced = new List<string>();
        IWorldEventSink[] sinks = Environment.GetEnvironmentVariable("LW_MENU_TRACE") == "1"
            ? [tally, deaths, new StarvationTrace(world, seed, traced)] : [tally, deaths];
        var clock = new WorldClock(baseClock.Systems, baseClock.MaxIterationsPerTick, new Fan(sinks));
        var food = new ResourceType(world.EconomyRules.FoodResourceId);
        int pop0 = world.AliveNpcIndex.Alive.Count;
        var lines = new List<string>();
        long personYears = 0, starvTotal = 0, thirstTotal = 0;
        int built1 = 0; // AD-184: prédios prontos no fim do ano 1 (para prédios por ano) e casas sem moradia no fim
        bool monthly = Environment.GetEnvironmentVariable("LW_MENU_MONTHLY") == "1";
        bool farmsOn = Environment.GetEnvironmentVariable("LW_MENU_FARMS") == "1";
        // AD-191: LW_MENU_PROBE="ano:mes0-mes1:cada_h:casa,casa,..." liga a sonda de casa (anos de 1 em diante, meses de 0)
        (int Year, int M0, int M1, int Every, long[] Houses)? probe = null;
        if (Environment.GetEnvironmentVariable("LW_MENU_PROBE") is { Length: > 0 } pspec && pspec.Split(':') is [var py, var pm, var pe, var ph])
            probe = (int.Parse(py, inv), int.Parse(pm.Split('-')[0], inv), int.Parse(pm.Split('-')[^1], inv), int.Parse(pe, inv),
                ph.Split(',').Select(x => long.Parse(x, inv)).ToArray());
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
                            bool ripe = world.FindCropAt(f.Location) is { Status: LivingWorld.Domain.Economy.CropStatus.Mature };
                            foreach (var e in f.Employees)
                                if (world.FindNpc(e) is { IsAlive: true } n)
                                {
                                    if (n.CurrentLocation == f.Location) present++;
                                    else if (ripe) { string k = $"{n.CurrentAction}/{n.TravelPurpose?.ToString() ?? "-"}"; fy.Absent[k] = fy.Absent.GetValueOrDefault(k) + 1; }
                                }
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
                else if (probe is { } pr && pr.Year == yr && m >= pr.M0 && m <= pr.M1)
                {
                    // AD-191: a sonda de casa, de hora em hora, a cada pr.Every horas
                    for (int h = 0; h < world.Calendar.HoursPerMonth; h++)
                    {
                        clock.Run(world, 1);
                        if (world.CurrentDate.TotalHours % pr.Every == 0)
                            lines.AddRange(LivingWorld.Simulation.Observation.HouseProbe.Lines(world, pr.Houses, explain: true).Select(l => $"SONDA {period} seed={seed} {l}"));
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
                $"{period} seed={seed} ano={yr} pop={alive} pool={world.ActiveCities().Sum(c => c.AggregatePool.Count)} cidades={world.ActiveCities().Count()} emigr={deaths.Emigrated} mortes={deaths.Deaths} fome={starv} sede={thirst} colheita={tally.HarvestByYear[^1]} " +
                $"semeado={tally.SeedSownByYear[^1]} celeiro={barn} casas={homes} estoque={tally.StockByMonth[^1]} preco={price:F1} " +
                $"lavradores={workers} fazendas={farms.Count} clima={tally.ClimateByYear[^1]} fora={OutsideTheirCity(world)} | {crops}"));
            foreach (var (id, fy) in farmDiag)
            {
                var f = world.Workplaces.First(w => w.Id.Value == id);
                lines.Add(string.Create(inv,
                    $"FAZENDA {period} seed={seed} ano={yr} {f.Location.X}:{f.Location.Y} caixa={f.Treasury.Amount} empFim={f.Employees.Count} emp={fy.EmpSum / (double)Math.Max(1, fy.Days):F1} presente10h={fy.PresentSum / (double)Math.Max(1, fy.Days):F1} " +
                    $"madura mes={fy.RipeMonth} dias={fy.MatureDays} presenteCeifa={fy.MaturePresent / (double)Math.Max(1, fy.MatureDays):F1} emPe={fy.Standing0} devAbr={fy.DevApr:F0} devJul={fy.DevJul:F0} " +
                    $"temp=[{string.Join(',', fy.TempSum.Select(t => (t / world.Calendar.DaysPerMonth).ToString("F1", inv)))}] " +
                    $"ausentesCeifa={string.Join(',', fy.Absent.OrderByDescending(a => a.Value).Take(6).Select(a => a.Key + "=" + a.Value))}"));
            }
            if (deaths.CountKinds) { lines.Add($"EVENTOS {period} seed={seed} ano={yr} " + string.Join(' ', deaths.Kinds.Select(kv => kv.Key + "=" + kv.Value))); deaths.Kinds.Clear(); }
            deaths.Deaths = 0; deaths.Emigrated = 0;
            lock (traced) { foreach (var t in traced) lines.Add($"MORTE {period} {t}"); traced.Clear(); }
            if (yr == 1) built1 = world.Buildings.Count(b => b.UnderWork is null);
            if (alive == 0) break;
        }
        int popEnd = world.AliveNpcIndex.Alive.Count;
        lines.Add(string.Create(inv,
            $"RESUMO {period} seed={seed} anos={years} pop0={pop0} popFim={popEnd} razao={popEnd / (double)Math.Max(1, pop0):F2} " +
            $"fome={starvTotal} sede={thirstTotal} fomePorMilPessoaAno={starvTotal * 1000.0 / Math.Max(1, personYears):F2} " +
            $"predios1={built1} prediosFim={world.Buildings.Count(b => b.UnderWork is null)} semCasa={world.Households.Count(h => !h.IsEmpty && h.Dwelling is null)}"));
        return lines;
    }

    /// <summary>AD-192: prédios e canteiros de cada cidade com alguma célula fora do limite crescido dela (o escopo Cidade não os mostra).</summary>
    private static int OutsideTheirCity(WorldState world)
    {
        int outside = 0;
        foreach (var city in world.ActiveCities())
        {
            var bounds = LivingWorld.Simulation.Cities.CityOccupancy.ResolveGrownBounds(world, city,
                LivingWorld.Simulation.Cities.Queries.CityPopulationQuery.Population(world, city.Id)).Bounds;
            foreach (var b in world.Buildings)
                if (b.City == city.Id && b.Position is { } at
                    && LivingWorld.Domain.Cities.Buildings.BuildingFootprintGenerator.Generate(b).Any(c => !bounds.Contains(new CellCoord(at.X + c.Cell.X, at.Y + c.Cell.Y))))
                    outside++;
        }
        return outside;
    }

    public static (WorldState, WorldClock) Load(string period, ulong seed, string? patch)
    {
        var root = JsonNode.Parse(DefaultPeriodSeeder.PayloadFor(period))!.AsObject();
        root["Seed"] = seed;
        if (Environment.GetEnvironmentVariable("LW_AD194") == "off") // AD-194: material em horas, sem eventos de dia perdido
            root["CityLayout"] = System.Text.Json.JsonSerializer.SerializeToNode(LivingWorld.Domain.Cities.CityLayoutRules.Default with
            { Streets = LivingWorld.Domain.Cities.StreetRules.Default.WithoutAd194() });
        if (Environment.GetEnvironmentVariable("LW_AD195") is { } ad195) // AD-195: off|nowalk|nohome
            root["CityLayout"] = System.Text.Json.JsonSerializer.SerializeToNode(LivingWorld.Domain.Cities.CityLayoutRules.Default with
            { Streets = LivingWorld.Domain.Cities.StreetRules.Default.WithAd195Mode(ad195) });
        if (!string.IsNullOrEmpty(patch)) Merge(root, JsonNode.Parse(patch)!.AsObject());
        var loaded = LivingWorld.Simulation.Scenarios.ScenarioLoaderV2.LoadWorld(root.ToJsonString());
        if (!loaded.IsSuccess) throw new InvalidOperationException($"{period}: {loaded.Error}");
        Ablate(loaded.Value.Item1);
        return loaded.Value;
    }

    /// <summary>AD-191: <c>LW_MENU_OFF</c> (vírgulas) desliga as regras novas para medir antes × depois no mesmo binário.</summary>
    private static void Ablate(WorldState world)
    {
        var off = (Environment.GetEnvironmentVariable("LW_MENU_OFF") ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var ag = world.AgricultureRules;
        if (off.Contains("barnprice"))
        {
            // a reserva de fundação (AD-191) sai do celeiro como se o mundo tivesse sido semeado sem ela
            long reserve = LivingWorld.Simulation.Economy.Production.CropSystem.FoundingReservePerPerson(ag);
            foreach (var group in world.Workplaces.Where(w => w.LocationType.Id == 1).GroupBy(w => w.City))
            {
                long residents = world.Npcs.Count(n => n.IsAlive && n.City == group.Key);
                foreach (var farm in group)
                    world.FindCropAt(farm.Location)?.TakeBarn(residents / group.Count() * reserve);
            }
        }
        foreach (var key in off)
            ag = key switch
            {
                "fieldvalue" => ag with { FieldLaborValue = false },
                "barnprice" => ag with { BarnReserveYears = 0 },
                _ => ag,
            };
        world.AgricultureRules = ag;
        if (off.Contains("water"))
            world.NaturalWaterRules = world.NaturalWaterRules with { Fetch = world.NaturalWaterRules.Fetch with { NoEmptyTrips = false } };
        if (off.Contains("customary") && world.CrimeRules is { } crime)
            world.CrimeRules = crime with { Justice = crime.Justice with { CustomaryValue = false } };
        if (off.Contains("canearn"))
            world.NeedsRules = world.NeedsRules with { Psyche = world.NeedsRules.Psyche with { IncomeNeedNeedsWork = false } };
        if (off.Contains("anystall"))
            world.NeedsRules = world.NeedsRules with { Provisioning = world.NeedsRules.Provisioning with { FoodFromAnyStall = false } };
        if (off.Contains("purse") && world.FamilyRules.Pressure is { } p)
            world.FamilyRules = world.FamilyRules with { Pressure = p with { PurseToShopper = false } };
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
        public readonly SortedDictionary<string, long> Absent = new(StringComparer.Ordinal);
    }

    private sealed class DeathCount : IWorldEventSink
    {
        public long Deaths, Emigrated;
        public readonly SortedDictionary<string, long> Kinds = new(StringComparer.Ordinal);
        public bool CountKinds;
        public WorldState? World;

        public void Record(WorldEvent evt)
        {
            if (CountKinds)
            {
                string k = evt.Kind + "@" + evt.SourceSystem;
                Kinds[k] = Kinds.GetValueOrDefault(k) + 1;
                // AD-191: para que tipo de oficina vai quem a alocação de trabalho muda
                if (evt.Kind == WorldEventKind.Hired && World is not null && evt.Payload.Split('|') is [_, var to] && long.TryParse(to, out long wid)
                    && World.Workplaces.FirstOrDefault(w => w.Id.Value == wid) is { } dest)
                {
                    string kt = k + ":tipo" + dest.LocationType.Id;
                    Kinds[kt] = Kinds.GetValueOrDefault(kt) + 1;
                }
            }
            if (evt.Kind == WorldEventKind.Emigrated) Emigrated++;
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
