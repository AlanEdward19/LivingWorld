using LivingWorld.Domain.Cities;
using LivingWorld.Domain.Cities.Buildings;
using LivingWorld.Domain.Geography;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Cities.Streets;

/// <summary>#258: o chão que o cenário entrega pronto (<see cref="StreetRules.Pavement"/>): a rua do traçado de cada cidade com a superfície declarada e a
/// calçada em volta dela e entre a porta de cada prédio e a rua. Roda uma vez, ao criar o mundo (depois dos prédios e do chão da história); o calçamento é
/// o mesmo do chão (condição, desgaste, conserto), então nada aqui é regra à parte. Função do mapa, dos prédios e do cenário: determinística, sem sorteio.</summary>
public static class StreetPavement
{
    public static void Seed(WorldState world)
    {
        if (!world.StreetsOn || world.StreetRules.Pavement is not { } seed) return;
        var ground = world.Ground!;
        var street = SurfaceOf(seed.Street);
        var sidewalk = seed.Sidewalk is null ? GroundSurface.None : SurfaceOf(seed.Sidewalk);
        if (street == GroundSurface.None) return;

        var occupied = new HashSet<CellCoord>();
        foreach (var building in world.Buildings)
            if (building.Position is { } origin)
                foreach (var cell in BuildingFootprintGenerator.Generate(building))
                    occupied.Add(new CellCoord(origin.X + cell.Cell.X, origin.Y + cell.Cell.Y));

        bool Open(CellCoord cell) =>
            world.Map.TryGetCell(cell, out var mapCell) && !mapCell.HasWater && !occupied.Contains(cell);

        void Pave(CellCoord cell, GroundSurface surface)
        {
            if (!Open(cell)) return;
            int i = ground.IndexOf(cell);
            if (i < 0) return;
            ground.SetPaving(i, surface, 1000);
            ground.SetSurface(i, surface);
        }

        foreach (var city in world.ActiveCities().OrderBy(c => c.Id.Value))
        {
            var network = CityStreets.Of(world, city);
            var streetCells = new HashSet<CellCoord>(network.Cells);
            foreach (var cell in network.Cells) Pave(cell, street);
            if (sidewalk == GroundSurface.None) continue;
            // a calçada: as células ao lado da rua, até a largura declarada (de 4 em 4 vizinhos), fora prédio e água
            var ring = new HashSet<CellCoord>(streetCells);
            for (int step = 0; step < Math.Clamp(seed.SidewalkWidth, 0, 4); step++)
            {
                var next = new List<CellCoord>();
                foreach (var cell in ring.OrderBy(c => c.Y).ThenBy(c => c.X))
                    foreach (var near in (ReadOnlySpan<CellCoord>)[new(cell.X, cell.Y + 1), new(cell.X + 1, cell.Y), new(cell.X, cell.Y - 1), new(cell.X - 1, cell.Y)])
                        if (!ring.Contains(near) && !streetCells.Contains(near) && Open(near)) next.Add(near);
                foreach (var cell in next)
                {
                    ring.Add(cell);
                    Pave(cell, sidewalk);
                }
            }
            // e o caminho da porta até a rua de cada prédio
            foreach (var (building, origin, croft) in CityStreets.Placed(world, city))
                foreach (var cell in CityStreets.LotOf(world, city, building, origin, croft).FrontPath)
                    Pave(cell, sidewalk);
        }

        ground.SurfaceVersion++;
        world.CanonicalHashCache.MarkPropertyDirty(nameof(WorldState.Ground));
    }

    private static GroundSurface SurfaceOf(string name) => name.Trim().ToLowerInvariant() switch
    {
        "gravel" or "cascalho" => GroundSurface.Gravel,
        "stone" or "pedra" => GroundSurface.Stone,
        _ => GroundSurface.None,
    };
}
