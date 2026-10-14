using LivingWorld.Domain.Ontogeny;
using LivingWorld.Domain.Population;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Ontogeny;

/// <summary>Estado de desenvolvimento por idade: a única fonte da curva (nascimento, população inicial, materialização e o
/// sistema mensal usam a mesma). Idade efetiva = idade real × <c>RateGene</c> (predisposição, mesmo multiplicador de taxa da Fase 6).</summary>
public static class OntogenyMechanic
{
    public static double EffectiveAgeYears(WorldState world, Npc npc)
    {
        var end = npc.DeathDate ?? world.CurrentDate;
        double years = (double)(end.TotalHours - npc.BirthDate.TotalHours) / npc.BirthDate.Calendar.HoursPerYear;
        return Math.Max(0, years) * npc.RateGene.Value;
    }

    /// <summary><c>null</c> = desenvolvimento completo (ou ontogenia desligada).</summary>
    public static DevelopmentState? StateFor(WorldState world, Npc npc)
    {
        var rules = world.OntogenyRules;
        if (!rules.Enabled) return null;
        double age = EffectiveAgeYears(world, npc);
        return age >= rules.CompleteAtYears ? null : rules.StateAt(age);
    }

    /// <summary>Um mês de desenvolvimento: exposição do mês (cuidador no domicílio) decide se a curva da idade rende. Quem passou da
    /// idade em que tudo se completa e alcançou todos os eixos vira <c>null</c>; quem ficou com teto reduzido permanece com o estado
    /// final (déficit permanente), sem mais avanço.</summary>
    public static DevelopmentState? AdvanceOneMonth(WorldState world, Npc npc, DevelopmentState state)
    {
        var rules = world.OntogenyRules;
        double now = EffectiveAgeYears(world, npc);
        if (now >= rules.CompleteAtYears && !state.IsComplete) return state;
        double monthYears = (double)npc.BirthDate.Calendar.HoursPerMonth / npc.BirthDate.Calendar.HoursPerYear * npc.RateGene.Value;
        var next = rules.Advance(state, Math.Max(0, now - monthYears), now, ExposureAccumulator.HasCaregiver(world, npc));
        if (now < rules.CompleteAtYears) return next;
        return next.IsComplete ? null : next;
    }

    /// <summary>O que o NPC já pode fazer, para a escolha por utility; <c>default</c> (nada restrito) para adulto ou ontogenia desligada.</summary>
    public static Behavior.Decision.ActionEligibility EligibilityFor(OntogenyRules rules, DevelopmentState? state) =>
        !rules.Enabled || state is null
            ? default
            : new Behavior.Decision.ActionEligibility(rules.BlockedMaskFor(state), true, state.Language);

    /// <summary>Inicializa quem já existe ao montar o mundo (população inicial, materialização).</summary>
    public static void Initialize(WorldState world, Npc npc) => npc.SetDevelopment(StateFor(world, npc));
}
