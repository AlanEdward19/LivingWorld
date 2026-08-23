using LivingWorld.Domain;

namespace LivingWorld.Simulation.Economy;

/// <summary>Cadeia de água <c>travel→collect→carry→deliver</c> (Fase 15.1, Stage 4, T15).
/// Fonte é célula com <see cref="MapCell.HasWater"/>; quantidade só entra no estoque na entrega.</summary>
public static class WaterLogistics
{
    public static Result<CellCoord> NearestSource(WorldMap map, CellCoord from)
    {
        CellCoord? best = null;
        double bestCost = double.PositiveInfinity;
        foreach (var cell in map.Cells.OrderBy(item => item.Coord.Y).ThenBy(item => item.Coord.X))
        {
            if (!cell.HasWater) continue;
            var path = MapPathfinder.ShortestCost(map, from, cell.Coord);
            if (!path.IsSuccess) continue;
            if (path.Value < bestCost)
            {
                bestCost = path.Value;
                best = cell.Coord;
            }
        }

        return best is { } found
            ? Result<CellCoord>.Ok(found)
            : Result<CellCoord>.Fail("source: nenhuma fonte de água alcançável");
    }

    public static Result<ResourceProcess> Collect(WorldState world, Npc npc, long now, long quantity = 1)
    {
        var water = new ResourceType(world.EconomyRules.WaterResourceId);
        var recipe = world.ProcessRecipes.FirstOrDefault(item => item.Kind == ProcessKind.CollectWater)
            ?? ProcessRecipe.Create(ProcessKind.CollectWater, new Dictionary<int, long>(), water.Id, quantity, null, 1).Value!;
        if (recipe.OutputQuantity != quantity)
            recipe = recipe with { OutputQuantity = quantity };
        return ResourceProcessSystem.Start(world, npc, recipe, now);
    }

    public static Result<ResourceProcess> Deliver(WorldState world, Npc npc, long now)
    {
        var water = new ResourceType(world.EconomyRules.WaterResourceId);
        long quantity = Math.Max(1, npc.CarriedQuantity);
        var recipe = world.ProcessRecipes.FirstOrDefault(item => item.Kind == ProcessKind.DeliverWater)
            ?? ProcessRecipe.Create(ProcessKind.DeliverWater, new Dictionary<int, long>(), water.Id, quantity, null, 1).Value!;
        if (recipe.OutputQuantity != quantity)
            recipe = recipe with { OutputQuantity = quantity };
        return ResourceProcessSystem.Start(world, npc, recipe, now);
    }
}
