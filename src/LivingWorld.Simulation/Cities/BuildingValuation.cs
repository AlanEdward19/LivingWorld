using LivingWorld.Domain.Cities;
using LivingWorld.Domain.Cities.Buildings;
using LivingWorld.Domain.Cities.Spatial;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Cities.Construction;
using LivingWorld.Simulation.Cities.Queries;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Cities;

/// <summary>Fotografia da cidade para precificar imóveis (lodging-and-rent): bounds, população e as duas pressões de demanda,
/// calculadas sob demanda e uma vez só. Uma por consulta (mês, projeção), nunca guardada entre ticks.</summary>
public sealed class ValuationContext
{
    private readonly Lazy<double> _lodgingPressure;
    private readonly Lazy<double> _labourPressure;

    public ValuationContext(WorldState world, City city)
    {
        World = world;
        City = city;
        Bounds = CityOccupancy.ResolveGrownBounds(world, city, CityPopulationQuery.Population(world, city.Id)).Bounds;
        _lodgingPressure = new Lazy<double>(() => ComputeLodgingPressure(world, city));
        _labourPressure = new Lazy<double>(() => ComputeLabourPressure(world, city));
    }

    public WorldState World { get; }
    public City City { get; }
    public CityBounds Bounds { get; }

    /// <summary>Quem precisa de teto (inquilinos + sem teto) por vaga de hospedagem da cidade; 1 sem hospedagem.</summary>
    public double LodgingPressure => _lodgingPressure.Value;

    /// <summary>População por vaga de trabalho da cidade; 1 sem vagas.</summary>
    public double LabourPressure => _labourPressure.Value;

    private static double ComputeLodgingPressure(WorldState world, City city)
    {
        var recipes = world.CityCatalog.BuildingRecipes;
        var lodgingIds = new HashSet<BuildingId>();
        long units = 0;
        foreach (var building in world.Buildings)
        {
            if (building.City != city.Id || !recipes.TryGetValue(building.BuildingTypeId, out var recipe) || recipe.Lodging is null) continue;
            lodgingIds.Add(building.Id);
            units += DwellingCapacity.Of(world, building);
        }
        if (units == 0) return 1.0;

        long need = world.Households.Count(h => h.City == city.Id && (h.Dwelling is null || lodgingIds.Contains(h.Dwelling.Value)));
        return need / (double)units;
    }

    private static double ComputeLabourPressure(WorldState world, City city)
    {
        long vacancies = world.Workplaces.Where(w => w.City == city.Id).Sum(w => (long)w.MaxVacancies);
        return vacancies == 0 ? 1.0 : CityPopulationQuery.Population(world, city.Id) / (double)vacancies;
    }
}

/// <summary>Preço de imóvel que reage ao lugar e à demanda: valor-base do tipo (cenário) × fator de local × fator de demanda,
/// dentro de [Min, Max]. Local = centralidade na cidade, comércio e oficinas por perto, beira d'água. Demanda = pressão
/// por hospedagem (prédio de aluguel) ou por vagas de trabalho (estabelecimento). Função pura do mundo: sem sorteio.</summary>
public static class BuildingValuation
{
    private const int AmenityCap = 5;
    private const int WaterfrontReach = 2;

    public static double MultiplierOf(ValuationContext context, Building building, BuildingRecipe recipe)
    {
        if (recipe.Pricing is not { } pricing) return 1.0;

        var world = context.World;
        double location = 1.0;
        var position = building.Position ?? BuildingPlacementResolver.Resolve(building, context.City, world, context.Bounds)?.Position;
        if (position is { } at)
        {
            location += pricing.CentralityWeight * Centrality(context, at)
                + pricing.AmenityWeight * Amenities(world, building, pricing.AmenityRadius, at) / AmenityCap
                + pricing.WaterfrontWeight * (Waterfront(world, at) ? 1 : 0);
        }

        double pressure = recipe.Lodging is not null ? context.LodgingPressure : context.LabourPressure;
        double demand = Math.Max(0.0, 1.0 + pricing.DemandSensitivity * (pressure - 1.0));
        return Math.Clamp(location * demand, pricing.MinMultiplier, pricing.MaxMultiplier);
    }

    /// <summary>Valor do imóvel; 0 para tipo sem <c>Pricing</c>.</summary>
    public static long ValueOf(ValuationContext context, Building building, BuildingRecipe recipe) =>
        recipe.Pricing is { } pricing ? (long)Math.Round(pricing.BaseValue * MultiplierOf(context, building, recipe)) : 0;

    /// <summary>Aluguel mensal por vaga: com <c>Pricing</c> é o valor vezes o rendimento; sem, o aluguel fixo do tipo.</summary>
    public static long RentOf(ValuationContext context, Building building, BuildingRecipe recipe)
    {
        if (recipe.Lodging is not { } lodging) return 0;
        if (recipe.Pricing is not { RentYieldPerMonth: > 0 } pricing) return lodging.RentPerMonth;
        return Math.Max(1, (long)Math.Round(ValueOf(context, building, recipe) * pricing.RentYieldPerMonth));
    }

    /// <summary>Diária por vaga: a tarifa-base do tipo vezes o mesmo multiplicador de local e demanda; 0 se não há diária.</summary>
    public static long NightlyOf(ValuationContext context, Building building, BuildingRecipe recipe)
    {
        if (recipe.Lodging is not { NightlyRate: > 0 } lodging) return 0;
        return Math.Max(1, (long)Math.Round(lodging.NightlyRate * MultiplierOf(context, building, recipe)));
    }

    /// <summary>1 no centro da cidade, 0 na borda dos bounds (ou além).</summary>
    private static double Centrality(ValuationContext context, CellCoord at)
    {
        var center = context.City.Location;
        double reach = Math.Max(1, Math.Max(context.Bounds.Width, context.Bounds.Height) / 2.0);
        double distance = Math.Max(Math.Abs(at.X - center.X), Math.Abs(at.Y - center.Y));
        return 1.0 - Math.Min(1.0, distance / reach);
    }

    /// <summary>Oficinas de outros prédios dentro do raio (Chebyshev), no máximo <see cref="AmenityCap"/>.</summary>
    private static int Amenities(WorldState world, Building own, int radius, CellCoord at)
    {
        if (radius <= 0) return 0;
        int count = 0;
        foreach (var workplace in world.Workplaces)
        {
            if (workplace.Building == own.Id) continue;
            if (Math.Max(Math.Abs(workplace.Location.X - at.X), Math.Abs(workplace.Location.Y - at.Y)) <= radius && ++count >= AmenityCap)
                break;
        }
        return count;
    }

    private static bool Waterfront(WorldState world, CellCoord at)
    {
        for (int dx = -WaterfrontReach; dx <= WaterfrontReach; dx++)
            for (int dy = -WaterfrontReach; dy <= WaterfrontReach; dy++)
                if (world.Map.TryGetCell(new CellCoord(at.X + dx, at.Y + dy), out var cell) && cell.HasWater)
                    return true;
        return false;
    }
}
