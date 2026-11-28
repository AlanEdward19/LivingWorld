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
        return BuildingLayoutGenerator.Generate(building).Floors;
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

    /// <summary>Célula de casa do domicílio no prédio (#226): a de piso logo à frente da porta, por dentro (em linha reta com a abertura), onde
    /// a pessoa entra; sem ela (planta atípica), a primeira de piso por linha e coluna. A parede é aresta, então toda célula é caminhável.</summary>
    public static CellCoord InteriorCellOf(Building building, CellCoord position)
    {
        var footprint = BuildingFootprintGenerator.Generate(building);
        var (door, facing) = BuildingFootprintGenerator.DoorOf(footprint);
        var straight = new CellCoord(door.X - facing.X, door.Y - facing.Y);
        var cell = footprint.Any(c => c.Cell == straight && c.Material == BuildingMaterial.Floor)
            ? straight
            : footprint.Where(c => c.Material == BuildingMaterial.Floor).OrderBy(c => c.Cell.Y).ThenBy(c => c.Cell.X).Select(c => c.Cell).First();
        return new CellCoord(position.X + cell.X, position.Y + cell.Y);
    }
}
