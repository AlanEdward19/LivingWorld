using LivingWorld.Domain.Cities.Buildings;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Cities;

/// <summary>Vagas de moradia de um prédio e o jeito de ocupá-las (lodging-and-rent): uma receita com
/// <c>HousingCapacityProvided</c> abriga esse número de domicílios (quartos, andares de cortiço, estalagem); sem
/// receita de habitação, o prédio abriga um domicílio por andar (a casa semeada). Nada por época no código.</summary>
public static class DwellingCapacity
{
    public static int Of(WorldState world, Building building)
    {
        if (world.CityCatalog.BuildingRecipes.TryGetValue(building.BuildingTypeId, out var recipe) && recipe.HousingCapacityProvided > 0)
            return (int)Math.Min(recipe.HousingCapacityProvided, int.MaxValue);
        return BuildingLayoutGenerator.FloorsOf(building.Id, building.BuildingTypeId);
    }

    /// <summary>Domicílios por prédio em que moram agora, de uma só passada (O(domicílios)).</summary>
    public static Dictionary<BuildingId, int> TenantsByBuilding(WorldState world)
    {
        var tenants = new Dictionary<BuildingId, int>();
        foreach (var household in world.Households)
            if (household.Dwelling is { } dwelling)
                tenants[dwelling] = tenants.GetValueOrDefault(dwelling) + 1;
        return tenants;
    }

    /// <summary>Célula de dormir do domicílio no prédio: a primeira célula de piso (por linha e coluna), rotacionada
    /// com a planta. Casas 3x3 têm exatamente uma; a origem do footprint é parede e o NPC nunca entraria.</summary>
    public static CellCoord InteriorCellOf(Building building, CellCoord position) =>
        BuildingFootprintGenerator.Generate(building)
            .Where(cell => cell.Material == BuildingMaterial.Floor)
            .OrderBy(cell => cell.Cell.Y)
            .ThenBy(cell => cell.Cell.X)
            .Select(cell => new CellCoord(position.X + cell.Cell.X, position.Y + cell.Cell.Y))
            .First();
}
