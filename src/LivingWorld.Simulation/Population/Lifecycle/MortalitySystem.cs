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
[LivingCapability("DEATH", Events = [WorldEventKind.Death, WorldEventKind.Starvation, WorldEventKind.Dehydration, WorldEventKind.Exhaustion, WorldEventKind.EstateSettled, WorldEventKind.Disease, WorldEventKind.Accident, WorldEventKind.OldAge], Consumers = ["timeline.death"])]
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
        var rest = payload.AsSpan(DeathPayloadPrefix.Length);
        int bar = rest.IndexOf('|');
        var npc = world.FindNpc(new NpcId(long.Parse(bar < 0 ? rest : rest[..bar])));
        if (npc is null || !npc.IsAlive) return; // já morreu de outra causa: sem-op

        // causa nomeada da sobra da tábua (AD-137): A = acidente, O = velhice; sem marca, sem causa (snapshot antigo também)
        var kind = bar < 0 ? WorldEventKind.Death : rest[(bar + 1)..] switch
        {
            "A" => WorldEventKind.Accident,
            "O" => WorldEventKind.OldAge,
            _ => WorldEventKind.Death,
        };
        NpcDeath.Apply(world, ctx, npc, kind);
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
            ScheduleDeath(ctx, npc, now + 1 + (long)(rng.NextDouble() * neonatalHours), "");
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
        long tick = startTick + 1 + Math.Min(hours - 1, (long)(rng.NextDouble() * hours));
        ScheduleDeath(ctx, npc, tick, CauseMark(world, npc, startTick, rng.NextDouble()));
    }

    /// <summary>Nome da morte sorteada (AD-137): acidente pela taxa anual da idade e do sexo sobre o risco que sobrou, velhice
    /// pela parte da sobra que a idade nomeia; o resto fica sem causa. Só rotula: não muda quem morre nem quando.</summary>
    private static string CauseMark(WorldState world, Npc npc, long startTick, double roll)
    {
        var causes = world.NeedsRules.Physiology.DeathCauses;
        double age = Math.Max(0, startTick - npc.BirthDate.TotalHours) / (double)world.Calendar.HoursPerYear;
        double residual = world.PopulationRules.LifeTable.BaseAnnualMortality((int)age) * (1 - world.DiseaseRules.BaselineShare(age));
        double accident = causes.AccidentShare(age, npc.Sex == Sex.Male, residual);
        if (roll < accident) return "|A";
        return roll < accident + (1 - accident) * Math.Clamp(causes.OldAgeShareByAge.At(age), 0, 1) ? "|O" : "";
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
        double multiplier = StateMultiplier(world, npc, calendarAgeHours < hoursPerYear, startTick)
            // doença como causa (AD-137): a parte da tábua que as infecções explicam sai do risco-base
            * (1 - world.DiseaseRules.BaselineShare(calendarAgeHours / (double)hoursPerYear));
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

    private static void ScheduleDeath(TickContext ctx, Npc npc, long tick, string cause) =>
        ctx.ScheduleEvent(tick, SystemName, DeathPayloadPrefix + npc.Id.Value + cause);
}
