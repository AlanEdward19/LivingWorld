using LivingWorld.Domain;

namespace LivingWorld.Simulation;

// SPEC_DEVIATION (dynamic-city-growth, T6): design.md declara este tipo em
// src/LivingWorld.Domain/Cities/ — mesmo motivo já documentado em CityOccupancy.cs/
// OverflowPlacer.cs/BuildingPlacementResolver.cs: agrupar prédios de overflow exige as posições
// reais deles (via CityOccupancy, que precisa de WorldState) e a população real (Npcs vivos em
// WorldState.Npcs), e WorldState só existe em LivingWorld.Simulation.

/// <summary>Agrupa os prédios de overflow de uma cidade (dynamic-city-growth, T6, CITYGROW-04) —
/// "overflow" aqui significa: não absorvido pelos bounds crescidos da própria cidade
/// (<see cref="CityOccupancy.ResolveGrownBounds"/>) e fora do alcance de absorção de toda cidade
/// existente (nunca um cluster que já deveria só estender uma cidade vizinha, spec Edge Cases).
/// Membresia de cluster é por distância mútua (encadeada/transitiva) — dois prédios de overflow no
/// mesmo cluster se existir uma cadeia de vizinhos a distância <see cref="CityRules.AbsorptionRingCells"/>
/// entre eles, mesmo que não estejam diretamente próximos um do outro.</summary>
public static class OverflowClusterFinder
{
    /// <summary>Um grupo de prédios de overflow mutuamente próximos, com a caixa que os contém e
    /// a população real (nunca do <see cref="AggregatePopulationPool"/>) que mora dentro dela.</summary>
    public sealed record Cluster(IReadOnlyList<Building> Buildings, CityBounds Bounds, long Population);

    public static IReadOnlyList<Cluster> FindClusters(WorldState world, City city)
    {
        int ring = world.CityRules.AbsorptionRingCells;

        long ownPopulation = CityPopulationQuery.Population(world, city.Id);
        var (ownPopulationBounds, _) = SpatialBoundsResolver.ResolveCity(city, ownPopulation, world.Map.Width, world.Map.Height);
        var (ownGrownBounds, _) = CityOccupancy.ResolveGrownBounds(world, city, ownPopulation);

        var owned = CityOccupancy.OwnedBuildingFootprintBoxesWithOwners(world, city, ownPopulationBounds);

        var overflow = owned
            .Where(p => !ContainsBox(ownGrownBounds, p.Box))
            .Where(p => !IsWithinAbsorptionRangeOfAnyOtherCity(world, city.Id, p.Box, ring))
            .ToList();

        return BuildClusters(overflow, ring, world);
    }

    /// <summary>Union-find sobre os prédios de overflow: dois entram no mesmo cluster se houver
    /// uma cadeia de vizinhos a distância &lt;= <paramref name="ring"/> entre eles — poucos prédios
    /// de overflow por cidade por construção (ver design.md Tech Decisions), então O(n^2) basta.</summary>
    private static List<Cluster> BuildClusters(
        IReadOnlyList<(Building Building, CityBounds Box)> overflow, int ring, WorldState world)
    {
        int n = overflow.Count;
        var parent = Enumerable.Range(0, n).ToArray();
        int Find(int x) => parent[x] == x ? x : (parent[x] = Find(parent[x]));
        void Union(int a, int b)
        {
            a = Find(a);
            b = Find(b);
            if (a != b) parent[a] = b;
        }

        for (int i = 0; i < n; i++)
            for (int j = i + 1; j < n; j++)
                if (ChebyshevGap(overflow[i].Box, overflow[j].Box) <= ring)
                    Union(i, j);

        return overflow
            .Select((member, index) => (member, root: Find(index)))
            .GroupBy(x => x.root)
            .Select(group =>
            {
                var members = group.Select(x => x.member).ToList();
                var bounds = UnionBounds(members.Select(m => m.Box).ToList());
                long population = world.Npcs.Count(npc => npc.IsAlive && bounds.Contains(npc.CurrentLocation));
                return new Cluster(members.Select(m => m.Building).ToList(), bounds, population);
            })
            .ToList();
    }

    // internal (era private) — dynamic-city-growth post-ship fix, T7b/CITYGROW-04: reusado por
    // SpatialSettlementFoundingSystem.HandleEvent pra reverificar, no disparo do evento (não só no
    // agendamento), que o cluster ainda está fora do alcance de absorção de toda cidade existente
    // -- mesmo motivo que já reverifica o limiar de concentração ali: outras cidades podem ter
    // crescido durante a espera de OrganizationTicks.
    internal static bool IsWithinAbsorptionRangeOfAnyOtherCity(WorldState world, CityId excludeCityId, CityBounds box, int ring)
    {
        foreach (var other in world.Cities)
        {
            if (other.Id == excludeCityId) continue;
            long otherPopulation = CityPopulationQuery.Population(world, other.Id);
            var (otherGrownBounds, _) = CityOccupancy.ResolveGrownBounds(world, other, otherPopulation);
            if (ChebyshevGap(otherGrownBounds, box) <= ring) return true;
        }
        return false;
    }

    private static bool ContainsBox(CityBounds outer, CityBounds inner) =>
        inner.Origin.X >= outer.Origin.X && inner.Origin.Y >= outer.Origin.Y &&
        inner.Origin.X + inner.Width <= outer.Origin.X + outer.Width &&
        inner.Origin.Y + inner.Height <= outer.Origin.Y + outer.Height;

    /// <summary>dynamic-city-growth, T7: exposta (não privada) porque <c>SpatialSettlementFoundingSystem</c>
    /// precisa reconstruir a mesma caixa a partir dos ids capturados no payload, na re-verificação
    /// no disparo do evento (o cluster pode ter mudado de composição entre o agendamento e o
    /// disparo).</summary>
    internal static CityBounds UnionBounds(IReadOnlyList<CityBounds> boxes)
    {
        int minX = boxes.Min(b => b.Origin.X), minY = boxes.Min(b => b.Origin.Y);
        int maxX = boxes.Max(b => b.Origin.X + b.Width - 1), maxY = boxes.Max(b => b.Origin.Y + b.Height - 1);
        return new CityBounds(new CellCoord(minX, minY), maxX - minX + 1, maxY - minY + 1);
    }

    /// <summary>Distância de Chebyshev entre as bordas de dois retângulos (mesma métrica de
    /// <c>CityBoundsResolver.ChebyshevGap</c>/<c>OverflowPlacer</c>'s anéis) — 0 quando se sobrepõem
    /// ou se tocam. Duplicada aqui (função pura de 6 linhas) em vez de expor a privada de
    /// <c>CityBoundsResolver</c> só pra este único uso cross-file.</summary>
    private static int ChebyshevGap(CityBounds a, CityBounds b)
    {
        int aRight = a.Origin.X + a.Width - 1, aBottom = a.Origin.Y + a.Height - 1;
        int bRight = b.Origin.X + b.Width - 1, bBottom = b.Origin.Y + b.Height - 1;

        int dx = Math.Max(0, Math.Max(a.Origin.X - bRight, b.Origin.X - aRight));
        int dy = Math.Max(0, Math.Max(a.Origin.Y - bBottom, b.Origin.Y - aBottom));
        return Math.Max(dx, dy);
    }
}
