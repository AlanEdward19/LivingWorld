using System.Globalization;
using LivingWorld.Domain.Geography;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Cities.Streets;

/// <summary>Números da rua de verdade (AD-176) para o diagnóstico: células por superfície, quanto do traçado reservado virou rua
/// usada, trilhas fora dele, horas médias casa→trabalho e casa→água pela rota, casas sem rota, acessos e obras.</summary>
public static class StreetDiagnostics
{
    public static string Line(WorldState world)
    {
        var inv = CultureInfo.InvariantCulture;
        if (!world.StreetsOn) return "rua=desligada";
        var ground = world.Ground!;
        var counts = new int[5];
        long passes = 0; int maxWear = 0;
        for (int i = 0; i < ground.Count; i++)
        {
            counts[(int)ground.SurfaceAt(i)]++;
            passes += ground.PassesAt(i);
            maxWear = Math.Max(maxWear, ground.WearAt(i));
        }
        int surfaced = counts[1] + counts[2] + counts[3] + counts[4];
        var planned = CityStreets.AllStreetCells(world);
        int plannedUsed = planned.Count(c => ground.SurfaceAt(c) != GroundSurface.None);
        int offPlan = 0;
        for (int i = 0; i < ground.Count; i++)
            if (ground.SurfaceAt(i) != GroundSurface.None && !planned.Contains(ground.CellOf(i))) offPlan++;

        double work = 0; int workN = 0;
        foreach (var npc in world.AliveNpcIndex.Alive)
        {
            if (npc.Household is not { } h || world.FindHousehold(h) is not { } home || npc.Employer is not { } e
                || world.FindWorkplace(e) is not { } place) continue;
            work += StreetRoutes.CostOf(world, home.Location, place.Location).Cost;
            workN++;
        }
        double water = 0; int waterN = 0;
        foreach (var home in world.Households)
        {
            if (home.IsEmpty) continue;
            long ticks = LivingWorld.Simulation.Economy.Supply.NaturalWaterSystem.WalkToWater(world, home.Location, 30);
            if (ticks == long.MaxValue) continue;
            water += ticks;
            waterN++;
        }
        var access = new SortedDictionary<string, int>(StringComparer.Ordinal);
        int noRoute = 0;
        foreach (var building in world.Buildings)
        {
            if (building.Position is null) continue;
            var kind = StreetRoutes.AccessOf(world, building, planned) ?? "novo";
            access[kind] = access.GetValueOrDefault(kind) + 1;
            if (kind == "none") noRoute++;
            else if (world.FindActiveCity(building.City) is { } city)
            {
                var cell = building.Position.Value;
                if (!double.IsFinite(StreetRoutes.CostOf(world, cell, city.Location).Cost)) noRoute++;
            }
        }
        string Share(int n) => surfaced == 0 ? "0" : (n / (double)surfaced).ToString("F2", inv);
        return string.Create(inv,
            $"chaoTrilha={counts[1]} chaoTerra={counts[2]} chaoCascalho={counts[3]} chaoPedra={counts[4]} " +
            $"fracTrilha/Terra/Cascalho/Pedra={Share(counts[1])}/{Share(counts[2])}/{Share(counts[3])}/{Share(counts[4])} " +
            $"tracado={planned.Count} tracadoUsado={plannedUsed} ({(planned.Count == 0 ? 0 : plannedUsed / (double)planned.Count):F2}) " +
            $"trilhasForaDoTracado={offPlan} hCasaTrabalho={(workN == 0 ? 0 : work / workN):F2} hCasaAgua={(waterN == 0 ? 0 : water / waterN):F2} " +
            $"casasSemRota={noRoute} acessos[{string.Join(' ', access.Select(kv => kv.Key + "=" + kv.Value))}] " +
            $"obrasFeitas={ground.WorksDone} obrasAbertas={ground.Works.Count} passagensMes={passes} pisoteioMax={maxWear} paresPendentes={ground.PendingTripPairs}");
    }
}
