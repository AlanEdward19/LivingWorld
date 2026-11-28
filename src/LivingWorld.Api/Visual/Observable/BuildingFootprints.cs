using System.Runtime.CompilerServices;
using LivingWorld.Domain.Cities.Buildings;
using LivingWorld.Domain.Geography;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Api.Visual.Observable;

/// <summary>#242: as células do mundo que a planta do térreo de algum prédio cobre (quem está nelas está dentro de um prédio; fora delas, na rua ou
/// no terreno). Guardado por mundo e refeito quando muda o conjunto de prédios (a planta é função pura do prédio).</summary>
internal static class BuildingFootprints
{
    private sealed record Index(int Count, long IdSum, long Edits, HashSet<CellCoord> Cells);

    private static readonly ConditionalWeakTable<WorldState, Index> Cache = new();

    public static HashSet<CellCoord> Of(WorldState world)
    {
        var buildings = world.Buildings;
        long idSum = 0, edits = 0;
        for (int i = 0; i < buildings.Count; i++) { idSum += buildings[i].Id.Value; edits += buildings[i].Edit?.Version + 1 ?? 0; }
        if (Cache.TryGetValue(world, out var known) && known.Count == buildings.Count && known.IdSum == idSum && known.Edits == edits) return known.Cells;

        var cells = new HashSet<CellCoord>();
        foreach (var building in buildings)
        {
            if (building.Position is not { } origin) continue;
            foreach (var cell in BuildingLayoutGenerator.Generate(building).FloorLayouts[0].Cells)
                cells.Add(new CellCoord(origin.X + cell.Cell.X, origin.Y + cell.Cell.Y));
        }

        Cache.AddOrUpdate(world, new Index(buildings.Count, idSum, edits, cells));
        return cells;
    }
}
