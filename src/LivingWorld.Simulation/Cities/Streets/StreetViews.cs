using System.Runtime.CompilerServices;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Cities.Queries;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Cities.Streets;

/// <summary>As ruas que o chão tem (AD-176), por cidade, para o contrato e o diagnóstico: cada célula de trilha, terra batida,
/// cascalho ou pedra (ou em obra) vai para a cidade de limite crescido mais perto. Guardado até o chão, as obras ou as cidades
/// mudarem (o chão muda no mês, a obra no dia).</summary>
public static class StreetViews
{
    private sealed class Memo
    {
        public (int, int, int, long, int, long) Key = (-1, -1, -1, -1, -1, -1);
        public Dictionary<CityId, IReadOnlyList<StreetCellView>> ByCity = [];
    }

    private static readonly ConditionalWeakTable<WorldState, Memo> Memos = new();

    public static IReadOnlyDictionary<CityId, IReadOnlyList<StreetCellView>> ByCity(WorldState world)
    {
        if (!world.StreetsOn) return new Dictionary<CityId, IReadOnlyList<StreetCellView>>();
        var ground = world.Ground!;
        var cities = world.ActiveCities().OrderBy(c => c.Id.Value).ToList();
        long hoursLeft = 0;
        foreach (var open in ground.Works) hoursLeft = hoursLeft * 31 + open.HoursLeft + open.Id;
        var key = (ground.Months, ground.SurfaceVersion, ground.WorksDone, hoursLeft, cities.Count, LivingWorld.Domain.Cities.Buildings.Building.LayoutVersion);
        var memo = Memos.GetOrCreateValue(world);
        if (memo.Key == key) return memo.ByCity;

        var bounds = cities.Select(c => CityOccupancy.ResolveGrownBounds(world, c, CityPopulationQuery.Population(world, c.Id)).Bounds).ToList();
        var planned = cities.Select(c => CityStreets.Of(world, c)).ToList();
        var work = new Dictionary<int, PavingWork>();
        foreach (var w in ground.Works.OrderBy(w => w.Id))
            foreach (int c in w.Cells) work.TryAdd(c, w);
        var lists = cities.Select(_ => new List<StreetCellView>()).ToList();
        for (int i = 0; i < ground.Count; i++)
        {
            var surface = ground.SurfaceAt(i);
            work.TryGetValue(i, out var job);
            if (surface == GroundSurface.None && job is null) continue;
            var cell = ground.CellOf(i);
            int best = -1, gap = int.MaxValue;
            for (int k = 0; k < cities.Count; k++)
            {
                var b = bounds[k];
                int dx = Math.Max(0, Math.Max(b.Origin.X - cell.X, cell.X - (b.Origin.X + b.Width - 1)));
                int dy = Math.Max(0, Math.Max(b.Origin.Y - cell.Y, cell.Y - (b.Origin.Y + b.Height - 1)));
                int d = Math.Max(dx, dy);
                if (d < gap) { gap = d; best = k; }
            }
            if (best < 0) continue;
            var network = planned[best];
            int index = network.IndexOf(cell);
            int passes = ground.PassesAt(i);
            lists[best].Add(new StreetCellView(
                cities[best].Id, cell, ViewOf(surface == GroundSurface.None ? GroundSurface.Trail : surface), passes,
                index >= 0 && network.IsMain[index], index >= 0 && network.InSquare(index))
            {
                Wear = ground.WearAt(i),
                UseBand = passes < 10 ? 0 : passes < 30 ? 1 : passes < 100 ? 2 : passes < 300 ? 3 : 4,
                Planned = index >= 0,
                Condition = ground.PavingAt(i) != GroundSurface.None ? ground.ConditionAt(i) : null,
                Work = job is null ? null : new StreetWorkView(ViewOf(job.Surface),
                    (int)Math.Clamp(100 * (job.HoursTotal - Math.Max(0, job.HoursLeft)) / Math.Max(1, job.HoursTotal), 0, 100),
                    job.Repair, job.Payers.Count == 0 ? "treasury" : "frontage"),
            });
        }
        var byCity = new Dictionary<CityId, IReadOnlyList<StreetCellView>>();
        for (int k = 0; k < cities.Count; k++) byCity[cities[k].Id] = lists[k];
        memo.ByCity = byCity;
        memo.Key = key;
        return byCity;
    }

    internal static StreetSurface ViewOf(GroundSurface surface) => surface switch
    {
        GroundSurface.Dirt => StreetSurface.Dirt,
        GroundSurface.Gravel => StreetSurface.Gravel,
        GroundSurface.Stone => StreetSurface.Paved,
        _ => StreetSurface.Trail,
    };
}
