using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Behavior.Resolvers;

/// <summary>Resolve qual recurso comestível um NPC consumiria ao comer (Fase 15.1, Stage 4,
/// LWV-03.2) — mesma regra de <see cref="BehaviorDecisionSystem.ApplyEat"/>.</summary>
public static class FoodResolver
{
    public static ResourceType ResolveMeal(WorldState world, Household household)
    {
        var food = new ResourceType(world.EconomyRules.FoodResourceId);
        return world.ResourceCatalog.IsEdible(food)
            ? food
            : household.Stock.Keys.Where(world.ResourceCatalog.IsEdible).OrderBy(item => item.Id).FirstOrDefault();
    }
}
