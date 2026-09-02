using LivingWorld.Domain.Cities;
using LivingWorld.Domain.Population.Body;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Cities.Queries;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Economy.Labor;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Cities.Construction;

/// <summary>Abre obras quando a cidade tem déficit de moradia ou de vagas de emprego e insumo
/// suficiente (Fase 15.1, Stage 4, T10, LWV-04.1) — demanda vira fila canônica, nunca spawn
/// instantâneo nem trabalho fingido sem workplace real.</summary>
public sealed class ConstructionDemandSystem : ISimulationSystem
{
    public const string SystemName = "cities-construction-demand";

    public string Name => SystemName;
    public TickFrequency Frequency => TickFrequency.Daily;

    public void Tick(WorldState world, TickContext ctx)
    {
        if (!world.CityRules.Enabled) return;

        var vacancyIndex = VacancyIndex.BuildForTick(world);

        foreach (var city in world.ActiveCities().OrderBy(c => c.Id.Value))
        {
            TryRequestHousing(world, city);
            TryRequestWorkplace(world, city, vacancyIndex);
        }
    }

    private static void TryRequestHousing(WorldState world, City city)
    {
        long population = CityPopulationQuery.Population(world, city.Id);
        long housing = CityPopulationQuery.Housing(world, city.Id);
        if (population <= 0 || housing >= population) return;
        if (HasPendingProject(world, city, recipe => recipe.HousingCapacityProvided > 0)) return;

        int? buildingTypeId = PickBuildingType(world, recipe => recipe.HousingCapacityProvided > 0);
        if (buildingTypeId is null) return;

        ConstructionSystem.StartConstruction(world, city.Id, buildingTypeId.Value);
    }

    private static void TryRequestWorkplace(WorldState world, City city, VacancyIndex vacancyIndex)
    {
        if (!world.EconomyRules.Enabled) return;

        foreach (var locationTypeId in LocationTypesNeedingWorkplace(world, city.Id, vacancyIndex).OrderBy(id => id))
        {
            if (HasPendingProject(world, city, recipe => recipe.Workplace?.LocationTypeId == locationTypeId)) continue;

            int? buildingTypeId = PickBuildingType(world, recipe => recipe.Workplace?.LocationTypeId == locationTypeId);
            if (buildingTypeId is null) continue;

            if (ConstructionSystem.StartConstruction(world, city.Id, buildingTypeId.Value).IsSuccess)
                return; // uma obra de workplace por tick — FIFO de demanda, determinístico
        }
    }

    private static IEnumerable<int> LocationTypesNeedingWorkplace(WorldState world, CityId cityId, VacancyIndex vacancyIndex)
    {
        var needed = new HashSet<int>();
        foreach (var npc in world.Npcs)
        {
            if (!npc.IsAlive || npc.City != cityId || npc.Employer is not null) continue;
            if (world.LifeStageRules.LifeStageOf(npc.AgeYears(world.CurrentDate)) != LifeStage.Adult) continue;
            if (!world.EconomyCatalog.LocationTypeByProfession.TryGetValue(npc.Profession.Id, out var locationTypeId)) continue;
            if (vacancyIndex.FirstWorkplaceWithVacancy(locationTypeId) is not null) continue;
            needed.Add(locationTypeId);
        }

        return needed;
    }

    private static bool HasPendingProject(WorldState world, City city, Func<BuildingRecipe, bool> matches) =>
        city.ConstructionQueue.Any(project =>
            world.CityCatalog.BuildingRecipes.TryGetValue(project.BuildingTypeId, out var recipe) && matches(recipe));

    private static int? PickBuildingType(WorldState world, Func<BuildingRecipe, bool> matches) =>
        world.CityCatalog.BuildingRecipes
            .Where(kv => matches(kv.Value))
            .OrderBy(kv => kv.Key)
            .Select(kv => (int?)kv.Key)
            .FirstOrDefault();
}
