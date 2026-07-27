using LivingWorld.Domain;
using LivingWorld.Simulation;

namespace LivingWorld.Tests.Economy;

/// <summary>Fase 5, T24 — segundo critério mais importante da fase: para cada recurso,
/// <c>inicial + produzido == consumido + estocado + perdido</c>, exato, checado a cada tick
/// (ECON-15). "Perdido" soma todo <see cref="WorldEventKind.ResourceLost"/> (excesso de
/// capacidade e spoilage, mesmo evento pros dois — <see cref="ProductionSystem"/>). "Estocado"
/// soma <see cref="Workplace.Stock"/> + <see cref="Household.Stock"/> de todo mundo.
/// <c>Buy</c> só transfere estoque entre os dois, nunca consome (T23/instrumentação em
/// <see cref="BehaviorDecisionSystem.ApplyEat"/>), então não perturba a conta.</summary>
public class ResourceConservationTests
{
    private const long TenYearsInHours = 10 * 12 * 30 * 24;

    private sealed class LossTrackingSink : IWorldEventSink
    {
        public Dictionary<int, long> LostByResource { get; } = [];

        public void Record(WorldEvent evt)
        {
            if (evt.Kind != WorldEventKind.ResourceLost) return;
            var parts = evt.Payload!.Split('|');
            int resourceId = int.Parse(parts[1]);
            long amount = long.Parse(parts[2]);
            LostByResource[resourceId] = LostByResource.GetValueOrDefault(resourceId) + amount;
        }
    }

    private static long TotalStocked(WorldState world, ResourceType resource) =>
        world.Workplaces.Sum(w => w.Stock.GetValueOrDefault(resource)) +
        world.Households.Sum(h => h.Stock.GetValueOrDefault(resource));

    [Fact]
    public void Produced_equals_consumed_plus_stocked_plus_lost_every_tick_over_10_years_for_every_resource()
    {
        var sink = new LossTrackingSink();
        var (world, clock) = ScenarioRunner.Create(seed: 42, initialPopulation: 20);
        clock = new WorldClock(ScenarioRunner.DefaultSystems(), clock.MaxIterationsPerTick, sink);

        var resources = new[] { new ResourceType(1), new ResourceType(2), new ResourceType(4) };
        var initialStock = resources.ToDictionary(r => r, r => TotalStocked(world, r));

        for (long tick = 0; tick < TenYearsInHours; tick++)
        {
            clock.Tick(world);

            foreach (var resource in resources)
            {
                long produced = world.ResourceProduced.GetValueOrDefault(resource);
                long consumed = world.ResourceConsumed.GetValueOrDefault(resource);
                long stocked = TotalStocked(world, resource);
                long lost = sink.LostByResource.GetValueOrDefault(resource.Id);

                long left = initialStock[resource] + produced;
                long right = consumed + stocked + lost;
                Assert.True(left == right,
                    $"tick {world.CurrentDate.TotalHours} resource {resource.Id}: inicial({initialStock[resource]})+produzido({produced}) = {left} != consumido({consumed})+estocado({stocked})+perdido({lost}) = {right}");
            }
        }
    }
}
