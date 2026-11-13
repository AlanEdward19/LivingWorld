using LivingWorld.Domain.History;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Population.Lifecycle;

/// <summary>Morte por idade como risco contínuo (AD-130): uma vez por mês cada vivo sorteia o risco do mês seguinte pela
/// tábua de vida, modulado pelo estado atual (saúde, vitalidade, prematuridade no 1º ano, senescência de poder). Quem
/// sorteia a morte recebe um único evento numa hora qualquer do mês — nenhuma varredura por tick e nenhuma data decidida
/// no nascimento.</summary>
[LivingCapability("DEATH", Events = [WorldEventKind.Death, WorldEventKind.Starvation, WorldEventKind.Dehydration, WorldEventKind.Exhaustion, WorldEventKind.EstateSettled, WorldEventKind.Disease], Consumers = ["timeline.death"])]
public sealed class MortalitySystem : ISimulationSystem
{
    public const string SystemName = "population-mortality";

    /// <summary>Prefixo do evento de morte agendado. Evento sem ele (morte planejada de snapshot anterior ao AD-130) é
    /// ignorado: o risco contínuo já cobre o NPC.</summary>
    public const string DeathPayloadPrefix = "age|";

    public string Name => SystemName;
    public TickFrequency Frequency => TickFrequency.Monthly;

    public void Tick(WorldState world, TickContext ctx)
    {
        long now = ctx.CurrentTick;
        long window = world.Calendar.HoursPerMonth;
        var alive = world.AliveNpcIndex.Alive; // em ordem de id; o sorteio só agenda, não muda a lista
        for (int i = 0; i < alive.Count; i++)
            RollWindow(world, ctx, alive[i], now, window);
    }

    public void HandleEvent(WorldState world, TickContext ctx, ScheduledEvent evt)
    {
        if (evt.Payload is not { } payload || !payload.StartsWith(DeathPayloadPrefix, StringComparison.Ordinal)) return;
        var npc = world.FindNpc(new NpcId(long.Parse(payload.AsSpan(DeathPayloadPrefix.Length))));
        if (npc is null || !npc.IsAlive) return; // já morreu de outra causa: sem-op

        NpcDeath.Apply(world, ctx, npc, WorldEventKind.Death);
    }

    /// <summary>Cobre o NPC recém-criado (nascimento, população inicial, clone) até a próxima varredura mensal e, se nasceu
    /// prematuro, sorteia a morte neonatal da faixa.</summary>
    public static void CoverUntilNextSweep(WorldState world, TickContext ctx, Npc npc)
    {
        long now = ctx.CurrentTick;
        long month = world.Calendar.HoursPerMonth;
        long untilSweep = month - (((now % month) + month) % month);
        if (npc.GestationAtBirthDays is { } days
            && world.PopulationRules.Fertility.PretermBandOf(days) is { } band
            && ctx.StreamFor("mortality-neonatal", npc.Id.Value) is var rng
            && rng.NextDouble() < band.NeonatalDeathRisk)
        {
            long neonatalHours = world.Calendar.HoursPerDay * (long)world.Calendar.DaysPerMonth;
            ScheduleDeath(ctx, npc, now + 1 + (long)(rng.NextDouble() * neonatalHours));
            return;
        }

        RollWindow(world, ctx, npc, now, untilSweep);
    }

    /// <summary>Sorteia a morte no intervalo [início, início + horas) e, se sair, agenda numa hora uniforme dele.</summary>
    public static void RollWindow(WorldState world, TickContext ctx, Npc npc, long startTick, long hours)
    {
        if (hours <= 0) return;
        double risk = WindowRisk(world, npc, startTick, hours);
        if (risk <= 0) return;
        var rng = ctx.StreamFor("mortality", unchecked(npc.Id.Value * 1_000_003L + startTick));
        if (rng.NextDouble() >= risk) return;
        ScheduleDeath(ctx, npc, startTick + 1 + Math.Min(hours - 1, (long)(rng.NextDouble() * hours)));
    }

    /// <summary>Risco de morte por idade no intervalo, pelo estado atual do NPC. Ganchos de estado: saúde (na tábua),
    /// vitalidade, prematuridade no 1º ano, senescência de poder e deslocamento de anos de vida. Doença e desnutrição
    /// (outras fatias) entram multiplicando <see cref="StateMultiplier"/>.</summary>
    public static double WindowRisk(WorldState world, Npc npc, long startTick, long hours)
    {
        double senescence = 1.0;
        if (world.CarrierOf(npc.Id) is { IsManifested: true } carrier)
        {
            if (carrier.SenescenceRateMultiplier <= 0.0) return 0.0; // poder que não envelhece: sem morte por idade
            senescence = carrier.SenescenceRateMultiplier;
        }

        long hoursPerYear = world.Calendar.HoursPerYear;
        long calendarAgeHours = Math.Max(0, startTick - npc.BirthDate.TotalHours);
        long biologicalHours = Math.Max(0, (long)(calendarAgeHours * senescence) + npc.LifespanOffsetHours);
        int biologicalAge = (int)(biologicalHours / hoursPerYear);
        double multiplier = StateMultiplier(world, npc, calendarAgeHours < hoursPerYear, startTick);
        // a tábua já conta a senescência: a saúde entra relativa ao teto da idade (AD-134), sem contar a velhice duas vezes
        int health = world.NeedsRules.Physiology.RelativeHealth(biologicalAge, npc.Health);
        return world.PopulationRules.LifeTable.PeriodMortality(
            biologicalAge, health, multiplier, hours / (double)hoursPerYear);
    }

    /// <summary>Multiplicador do estado do NPC sobre a tábua (a saúde já entra na própria tábua).</summary>
    public static double StateMultiplier(WorldState world, Npc npc, bool firstYear) =>
        StateMultiplier(world, npc, firstYear, world.CurrentDate.TotalHours);

    /// <summary>Multiplicador do estado em <paramref name="tick"/>: vitalidade, prematuridade, desnutrição acumulada e, no
    /// lactente sem leite recente, o risco da papa pela idade (AD-134).</summary>
    public static double StateMultiplier(WorldState world, Npc npc, bool firstYear, long tick)
    {
        double multiplier = world.FamilyRules.VitalityMortalitySelectionEnabled
            ? world.FamilyRules.EffectiveVitalityMultiplier(npc.Vitality)
            : 1.0;
        if (firstYear && npc.GestationAtBirthDays is { } days
            && world.PopulationRules.Fertility.PretermBandOf(days) is { } band)
            multiplier *= band.InfantHazardMultiplier;
        var physiology = world.NeedsRules.Physiology;
        if (!physiology.Enabled) return multiplier;
        double ageYears = Math.Max(0, tick - npc.BirthDate.TotalHours) / (double)world.Calendar.HoursPerYear;
        multiplier *= physiology.MalnutritionMultiplier(ageYears, npc.Undernutrition);
        multiplier *= LivingWorld.Simulation.Population.Body.Genome.HazardMultiplier(world, npc, ageYears); // AD-139
        var lactation = physiology.Lactation;
        double ageDays = Math.Max(0, tick - npc.BirthDate.TotalHours) / (double)world.Calendar.HoursPerDay;
        // quem já existia quando o mundo começou (sem gestação registrada) não teve a amamentação simulada
        if (lactation.Enabled && npc.GestationAtBirthDays is not null && ageDays < lactation.WeaningEndDays
            && (npc.LastNursedTick is not { } nursed || tick - nursed > lactation.NursedRecentlyDays * world.Calendar.HoursPerDay))
            multiplier *= lactation.UnnursedHazardByAgeMonths.At(ageYears * world.Calendar.MonthsPerYear);
        return multiplier;
    }

    private static void ScheduleDeath(TickContext ctx, Npc npc, long tick) =>
        ctx.ScheduleEvent(tick, SystemName, DeathPayloadPrefix + npc.Id.Value);
}
