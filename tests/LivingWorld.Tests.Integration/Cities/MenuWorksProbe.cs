using System.Globalization;
using LivingWorld.Domain.History;
using LivingWorld.Simulation.Core;
using LivingWorld.Tests.Shared.Cities;

namespace LivingWorld.Tests.Integration.Cities;

/// <summary>AD-194: sonda das obras de um mundo do menu, sem asserção. Por ano: obras abertas e prontas por tipo (casa, oficina,
/// predio-N), fila da cidade, prédios, casas sem moradia e casais de favor. <c>LW_WORKS_PROBE_OUT=arquivo</c>, <c>LW_MENU_PERIODS</c>
/// (cidade-media), <c>LW_MENU_SEEDS</c> (2), <c>LW_MENU_YEARS</c> (8).</summary>
[Trait("Category", "Report")]
public class MenuWorksProbe
{
    private sealed class Sink : IWorldEventSink
    {
        public Dictionary<string, int> Counts { get; } = [];
        public void Record(WorldEvent evt)
        {
            string? key = evt.Kind switch
            {
                WorldEventKind.BuildWorkStarted => "abre:" + evt.Payload.Split('|')[2],
                WorldEventKind.BuildWorkDone => "pronta:" + evt.Payload.Split('|')[2],
                WorldEventKind.WorkAbandoned => "abandona:" + evt.Payload.Split('|')[2] + ":" + evt.Payload.Split('|')[5] + ":" + evt.Payload.Split('|')[6],
                WorldEventKind.Marriage => "casamento",
                WorldEventKind.VentureFounded => "empreende",
                _ => null,
            };
            if (key is not null) Counts[key] = Counts.GetValueOrDefault(key) + 1;
        }
    }

    [Fact]
    public void MenuWorksProbe_writes_the_years()
    {
        var path = Environment.GetEnvironmentVariable("LW_WORKS_PROBE_OUT");
        if (string.IsNullOrEmpty(path)) return;
        int years = int.TryParse(Environment.GetEnvironmentVariable("LW_MENU_YEARS"), out int y) ? y : 8;
        var seeds = (Environment.GetEnvironmentVariable("LW_MENU_SEEDS") ?? "2").Split(',').Select(ulong.Parse).ToList();
        var periods = (Environment.GetEnvironmentVariable("LW_MENU_PERIODS") ?? "cidade-media").Split(',');
        using var output = new StreamWriter(path) { AutoFlush = true };
        foreach (var period in periods)
            foreach (var seed in seeds)
            {
                var (world, baseClock) = MenuWorlds.Load(period, seed);
                var sink = new Sink();
                var clock = new WorldClock(baseClock.Systems, baseClock.MaxIterationsPerTick, sink);
                output.WriteLine($"{period} seed={seed} streets={world.StreetsOn} houseIsWork={world.StreetRules.HouseIsWork} cities={world.CityRules.Enabled} buildNeedsCrew={world.StreetRules.BuildNeedsCrew} predios={world.Buildings.Count}");
                int step = int.TryParse(Environment.GetEnvironmentVariable("LW_PROBE_MONTHS"), out int pm) && pm > 0 ? pm : 12;
                for (int year = 1; year <= years * 12 / step; year++)
                {
                    clock.Run(world, world.Calendar.HoursPerMonth * step);
                    var queue = world.ActiveCities().Sum(c => c.ConstructionQueue.Count);
                    int homeless = world.Households.Count(h => !h.IsEmpty && h.Dwelling is null);
                    string counts = string.Join(' ', sink.Counts.OrderBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => $"{kv.Key}={kv.Value}"));
                    output.WriteLine(string.Create(CultureInfo.InvariantCulture,
                        $"  ano={year} pop={world.AliveNpcIndex.Alive.Count} predios={world.Buildings.Count(b => b.UnderWork is null)} canteiros={world.Buildings.Count(b => b.UnderWork is not null)} obras={world.Ground?.Builds.Count} fila={queue} semCasa={homeless} deFavor={world.Ground?.Lodgers.Count} {counts}"));
                    foreach (var w in world.Ground?.Builds ?? [])
                        output.WriteLine($"    obra {w.Id} {w.Kind} feito={w.HoursTotal - w.HoursLeft}/{w.HoursTotal} lote={w.MaterialOnSite} posto={w.MaterialPlaced}/{w.MaterialNeeded} turma={w.Crew.Count} ajuda={w.Helpers.Count} parada={w.Stalled} falta={w.MaterialShort} ultima={world.CurrentDate.TotalHours - w.LastProgressTick}h viagens="
                            + string.Join(";", w.Hauls.Select(t => $"{t.Npc}:{t.Stage}:{t.Source}:{t.X},{t.Y}:{world.FindNpc(new LivingWorld.Domain.Shared.NpcId(t.Npc))?.CurrentLocation}:{world.FindNpc(new LivingWorld.Domain.Shared.NpcId(t.Npc))?.CurrentAction}:{t.GatherDone}/{t.GatherHours}")));
                    foreach (long lodger in world.Ground?.Lodgers ?? [])
                        if (world.FindHousehold(new LivingWorld.Domain.Shared.HouseholdId(lodger)) is { } home)
                            output.WriteLine($"    favor casa={lodger} membros={home.Members.Count} {LivingWorld.Simulation.Cities.Works.BuildWorks.WhyNoHouse(world, home)}");
                }
            }
    }
}
