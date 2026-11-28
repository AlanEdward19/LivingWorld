using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Behavior.Resolvers;

/// <summary>Resolve qual recurso comestível um NPC consumiria ao comer
/// — mesma regra de <see cref="BehaviorDecisionSystem.ApplyEat"/>.</summary>
public static class FoodResolver
{
    public static ResourceType ResolveMeal(WorldState world, Household household)
    {
        var food = new ResourceType(world.EconomyRules.FoodResourceId);
        // #258: a refeição pronta do catálogo (a que sacia mais que a comida crua) vem antes, se a despensa a tem
        if (world.ObjectCatalog is { } objects && Objects.HomeTending.Applies(world))
        {
            int rawLevel = Objects.ObjectUse.ConsumeLevel(world, food, LivingWorld.Domain.Objects.ObjectEffectTargets.Hunger);
            ResourceType? best = null;
            int bestLevel = rawLevel;
            foreach (var descriptor in objects.Objects)
            {
                if (descriptor.ResourceId is not int id || id == food.Id || household.Stock.GetValueOrDefault(new ResourceType(id)) < 1) continue;
                int level = Objects.ObjectUse.ConsumeLevel(world, new ResourceType(id), LivingWorld.Domain.Objects.ObjectEffectTargets.Hunger);
                if (descriptor.Effects?.Any(e => e.Target == LivingWorld.Domain.Objects.ObjectEffectTargets.Hunger) != true || level <= bestLevel) continue;
                best = new ResourceType(id);
                bestLevel = level;
            }
            if (best is { } cooked) return cooked;
        }
        return world.ResourceCatalog.IsEdible(food)
            ? food
            : household.Stock.Keys.Where(world.ResourceCatalog.IsEdible).OrderBy(item => item.Id).FirstOrDefault();
    }
}
