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

    /// <summary>Criação adotiva (dívida da Fase 21): enquanto cresce, o <c>Upbringing</c> da criança caminha para a média dos cuidadores que
    /// <b>não são seus pais</b>, na proporção deles entre os cuidadores, da exposição e da fração do desenvolvimento que o mês representa. Com os pais
    /// criando, nada muda — o valor do nascimento já vem deles.</summary>
    internal static void AdoptUpbringing(WorldState world, Npc child, double exposure, double monthFraction)
    {
        if (exposure <= 0 || child.Household is not { } householdId || world.FindHousehold(householdId) is not { } household) return;

        int caregivers = 0, adoptive = 0;
        double sum = 0;
        foreach (var memberId in household.Members.OrderBy(m => m.Value))
        {
            if (memberId == child.Id || world.FindNpc(memberId) is not { IsAlive: true } member) continue;
            if (world.LifeStageRules.LifeStageOf(member.AgeYears(world.CurrentDate)) != Domain.Population.Body.LifeStage.Adult) continue;
            caregivers++;
            if (memberId == child.MotherId || memberId == child.FatherId) continue;
            adoptive++;
            sum += member.Upbringing;
        }
        if (adoptive == 0) return;

        double weight = Math.Clamp((double)adoptive / caregivers * exposure * monthFraction, 0.0, 1.0);
        child.SetUpbringing(child.Upbringing + (sum / adoptive - child.Upbringing) * weight);
    }

    /// <summary>Idioma que a pessoa fala de fato: o de maior fluência enquanto cresce, depois o gravado ao completar, senão o da própria
    /// cultura. Criança criada por outra cultura fala a de quem a criou.</summary>
    public static CultureId SpokenCulture(Npc npc) =>
        npc.Development?.Fluency?.Dominant() is { } dominant ? new CultureId(dominant) : npc.NativeLanguage ?? npc.Culture;

    /// <summary>Um mês de desenvolvimento: exposição do mês (cuidador no domicílio) decide se a curva da idade rende. Quem passou da
    /// idade em que tudo se completa e alcançou todos os eixos vira <c>null</c>; quem ficou com teto reduzido permanece com o estado
    /// final (déficit permanente), sem mais avanço.</summary>
    public static DevelopmentState? AdvanceOneMonth(WorldState world, Npc npc, DevelopmentState state)
    {
        var rules = world.OntogenyRules;
        double now = EffectiveAgeYears(world, npc);
        double monthYears = (double)npc.BirthDate.Calendar.HoursPerMonth / npc.BirthDate.Calendar.HoursPerYear * npc.RateGene.Value;
        // Já passou da idade de conclusão no mês anterior e ficou incompleto: déficit permanente, o estado final não anda mais.
        if (now - monthYears >= rules.CompleteAtYears && !state.IsComplete) return state;
        var languages = ExposureAccumulator.CaregiverLanguages(world, npc);
        var next = rules.Advance(state, Math.Max(0, now - monthYears), now, rules.ExposureFor(languages.Count), languages);
        AdoptUpbringing(world, npc, rules.ExposureFor(languages.Count), monthYears / rules.CompleteAtYears);
        if (now < rules.CompleteAtYears) return next;
        if (!next.IsComplete) return next;
        if (next.Fluency?.Dominant() is { } dominant) npc.SetNativeLanguage(new CultureId(dominant));
        return null;
    }

    /// <summary>O que o NPC já pode fazer, para a escolha por utility; <c>default</c> (nada restrito) para adulto ou ontogenia desligada.</summary>
    public static Behavior.Decision.ActionEligibility EligibilityFor(OntogenyRules rules, DevelopmentState? state) =>
        !rules.Enabled || state is null
            ? default
            : new Behavior.Decision.ActionEligibility(rules.BlockedMaskFor(state), true, state.Language);

    /// <summary>Inicializa quem já existe ao montar o mundo (população inicial, materialização).</summary>
    public static void Initialize(WorldState world, Npc npc) => npc.SetDevelopment(StateFor(world, npc));
}
