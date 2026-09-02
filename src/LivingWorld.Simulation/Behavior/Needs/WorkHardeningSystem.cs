using LivingWorld.Domain;

namespace LivingWorld.Simulation;

/// <summary>Crescimento lento de <see cref="Npc.MuscleMass"/> sob trabalho físico pesado
/// sustentado (Fase 16.3, COH-24) — categoria SLOW (<c>Daily</c>), mesmo espírito de
/// <see cref="SkillPracticeSystem"/>. Delega a mutação a
/// <see cref="BodyMechanic.ApplyWorkHardening"/>.</summary>
public sealed class WorkHardeningSystem : ISimulationSystem
{
    public const string SystemName = "behavior-work-hardening";

    public string Name => SystemName;
    public TickFrequency Frequency => TickFrequency.Daily;

    /// <summary>NPC empregado, em ação Work, presente no próprio Workplace — ganha um delta
    /// diário de MuscleMass até o teto do cenário. Sem BodyRules.Enabled — no-op.</summary>
    public void Tick(WorldState world, TickContext ctx)
    {
        if (!world.BodyRules.Enabled) return;

        foreach (var npc in world.Npcs.OrderBy(n => n.Id.Value))
        {
            if (!npc.IsAlive) continue;
            if (npc.Employer is not { } employerId) continue;
            if (npc.CurrentAction != ActionType.Work) continue;
            if (world.FindWorkplace(employerId) is not { } workplace) continue;
            if (npc.CurrentLocation != workplace.Location) continue;

            BodyMechanic.ApplyWorkHardening(world, npc);
        }
    }
}
