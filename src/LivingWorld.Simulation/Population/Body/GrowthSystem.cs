using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Behavior.Needs;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Health;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Population.Body;

/// <summary>Corpo que cresce (AD-134), <c>Monthly</c>: altura e peso pela curva da idade e do sexo, nanismo pela desnutrição
/// de quem cresce, músculo pelo trabalho braçal do mês, pela idade e pela fome; e o custo de comida de quem amamenta (o leite
/// cai no desmame). Consumidores: capacidade de trabalho e custo de andar (<see cref="BodyMechanic"/>), combate.</summary>
[LivingCapability("NEEDS", Consumers = ["inspector.npc.needs"])]
[RunsAfter(typeof(PhysiologySystem))]
public sealed class GrowthSystem : ISimulationSystem
{
    public const string SystemName = "body-growth";

    public string Name => SystemName;
    public TickFrequency Frequency => TickFrequency.Monthly;

    public void Tick(WorldState world, TickContext ctx)
    {
        long now = ctx.CurrentTick;
        bool growth = BodyGrowth.Enabled(world);
        bool lactation = Lactation.Enabled(world);
        bool hormones = Endocrine.Enabled(world);
        if (!growth && !lactation && !hormones) return;
        long window = (Lactation.Rules(world).WeaningEndDays + world.Calendar.DaysPerMonth) * world.Calendar.HoursPerDay;
        var alive = world.AliveNpcIndex.Alive; // em ordem de id
        for (int i = 0; i < alive.Count; i++)
        {
            var npc = alive[i];
            if (growth) BodyGrowth.MonthlyUpdate(world, npc, now);
            if (hormones) Endocrine.MonthlyUpkeep(world, npc, now); // AD-139: puberdade e anemia da menstruação
            if (lactation && npc.Sex == Sex.Female && npc.Reproduction?.LastBirthTick is { } birth && now - birth < window)
                Lactation.RefreshHungerCost(world, npc, now);
        }
    }
}
