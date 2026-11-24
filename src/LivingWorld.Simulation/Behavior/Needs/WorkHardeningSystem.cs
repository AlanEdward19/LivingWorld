using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Population.Skills;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Behavior.Needs;

/// <summary>Crescimento lento de <see cref="Npc.MuscleMass"/> sob trabalho físico pesado
/// sustentado — categoria SLOW (<c>Daily</c>), mesmo espírito de
/// <see cref="SkillPracticeSystem"/>. Delega a mutação a
/// <see cref="BodyMechanic.ApplyWorkHardening"/>.</summary>
[LivingCapability("SKILLS")]
public sealed class WorkHardeningSystem : ISimulationSystem
{
    public const string SystemName = "behavior-work-hardening";

    public string Name => SystemName;
    public TickFrequency Frequency => TickFrequency.Midshift;

    /// <summary>NPC empregado, em ação Work, presente no próprio Workplace — ganha um delta
    /// diário de MuscleMass até o teto do cenário. Sem BodyRules.Enabled — no-op.</summary>
    public void Tick(WorldState world, TickContext ctx)
    {
        if (!world.BodyRules.Enabled) return;

        bool growth = world.BodyRules.Growth.Enabled;
        var alive = world.AliveNpcIndex.Alive; // em ordem de id, só vivos
        for (int i = 0; i < alive.Count; i++)
        {
            var npc = alive[i];
            if (npc.Employer is not { } employerId) continue;
            if (npc.CurrentAction != ActionType.Work) continue;
            if (world.FindWorkplace(employerId) is not { } workplace) continue;
            if (npc.CurrentLocation != workplace.Location) continue;

            // AD-134: o dia de trabalho braçal conta para o músculo do mês (GrowthSystem); sem crescimento, o ganho antigo
            if (growth) npc.AddPhysicalWorkDay();
            else BodyMechanic.ApplyWorkHardening(world, npc);
        }
    }
}
