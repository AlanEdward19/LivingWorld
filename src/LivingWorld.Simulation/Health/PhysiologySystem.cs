using LivingWorld.Domain.Health;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Behavior.Needs;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Population.Lifecycle;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Health;

/// <summary>Saúde dinâmica (AD-134), <c>Daily</c>: o estado alimentar do dia alimenta a desnutrição acumulada; a saúde cai com
/// fome e sede em zero, doença e acima do teto da idade (velhice, desnutrição) e sobe com sono e comida até o teto, mais devagar
/// no velho. Consumidores: risco mensal de morte (<see cref="MortalitySystem.StateMultiplier"/>), corpo
/// (<c>GrowthSystem</c>: peso e nanismo), trabalho e passo (<see cref="BodyMechanic"/>), concepção e parto (piso de saúde).</summary>
[LivingCapability("NEEDS", Consumers = ["inspector.npc.needs"])]
[RunsAfter(typeof(NeedsDecaySystem))]
public sealed class PhysiologySystem : ISimulationSystem
{
    public const string SystemName = "health-physiology";

    public string Name => SystemName;
    public TickFrequency Frequency => TickFrequency.Daily;

    public void Tick(WorldState world, TickContext ctx)
    {
        var rules = world.NeedsRules.Physiology;
        if (!rules.Enabled) return;
        long now = ctx.CurrentTick;
        var alive = world.AliveNpcIndex.Alive; // em ordem de id; nada aqui mata
        for (int i = 0; i < alive.Count; i++)
            DailyUpdate(world, rules, alive[i], now);
    }

    /// <summary>Um dia de fisiologia de um NPC.</summary>
    public static void DailyUpdate(WorldState world, PhysiologyRules rules, Npc npc, long now)
    {
        double age = Math.Max(0, now - npc.BirthDate.TotalHours) / (double)world.Calendar.HoursPerYear;
        bool starving = npc.HungerZeroSinceTick is not null || npc.HungerAt(now) <= 0;
        bool parched = npc.ThirstAt(now) <= 0;
        double deficit = starving ? 1.0 : npc.HungerAt(now) < rules.MildHungerThreshold ? rules.MildHungerDeficit : 0.0;
        double undernutrition = npc.Undernutrition + (deficit - npc.Undernutrition) / rules.UndernutritionTimeConstantDays;
        npc.SetUndernutrition(undernutrition < 1e-4 ? 0.0 : undernutrition);

        int previous = npc.Health;
        double ceiling = rules.HealthCeilingByAge.At(age) * (1 - rules.UndernutritionHealthPenalty * npc.Undernutrition)
            - InfectionHealthPenalty(world, npc, age);
        int target = (int)Math.Round(Math.Clamp(ceiling, 0, 100));
        int health = previous;
        if (starving) health -= rules.StarvationHealthLossPerDay;
        if (parched) health -= rules.DehydrationHealthLossPerDay;
        if (!starving && !parched)
        {
            if (health < target && npc.SleepAt(now) > 0)
                health = Math.Min(target, health + Math.Max(1, (int)Math.Round(rules.RecoveryPerDayByAge.At(age))));
            else if (health > target)
                health = Math.Max(target, health - rules.DeclinePerDay);
        }

        health = Math.Max(health, Math.Min(previous, rules.HealthFloor));
        if (health != previous) npc.SetHealth(health);
    }

    /// <summary>Perda de teto pela doença em curso (gancho do <see cref="OutbreakSystem"/>).</summary>
    public static double InfectionHealthPenalty(WorldState world, Npc npc, double ageYears) =>
        OutbreakSystem.HealthPenalty(world, npc, ageYears);
}
