using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Body;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Population.Body;

/// <summary>Corpo por idade (AD-134): estatura herdada (fundador sorteado, recém-nascido pela média do gene dos pais com
/// segregação), altura e peso pela curva da idade, compleição individual fixa por NPC e músculo que segue o uso. Streams por
/// NPC (<c>stature-gene</c>, <c>stature-env</c>, <c>body-frame</c>): o mesmo NPC tem o mesmo corpo em qualquer mundo reaberto.</summary>
public static class BodyGrowth
{
    /// <summary>Parte dos dias com trabalho braçal suposta para quem já existe quando o mundo começa.</summary>
    public const double FounderWorkShare = 0.5;

    public static bool Enabled(WorldState world) => world.BodyRules.Growth.Enabled;

    /// <summary>Compleição (fator do IMC) fixa por NPC, em torno de 1.</summary>
    public static double Frame(WorldState world, Npc npc) =>
        Math.Clamp(1.0 + world.BodyRules.Growth.FrameStdDev * QuantitativeGenetics.Normal(world.Rng.StreamFor("body-frame", npc.Id.Value)),
            0.75, 1.3);

    public static double AgeYears(WorldState world, Npc npc, long tick) =>
        Math.Max(0, tick - npc.BirthDate.TotalHours) / (double)world.Calendar.HoursPerYear;

    /// <summary>Fundador (população inicial ou autorada): gene da estatura sorteado com variância h², corpo da idade.</summary>
    public static void InitializeFounder(WorldState world, Npc npc)
    {
        var growth = world.BodyRules.Growth;
        if (!growth.Enabled) return;
        double gene = Math.Sqrt(Math.Clamp(growth.StatureHeritability, 0, 1))
            * QuantitativeGenetics.Normal(world.Rng.StreamFor("stature-gene", npc.Id.Value));
        SetStatureAndBody(world, npc, gene, workShare: FounderWorkShare);
    }

    /// <summary>Recém-nascido: gene = média dos pais + segregação; tamanho de recém-nascido pela gestação.</summary>
    public static void InitializeNewborn(WorldState world, Npc baby, Npc mother, Npc? father)
    {
        var body = world.BodyRules;
        if (!body.Growth.Enabled) return;
        double gene = QuantitativeGenetics.Inherit(
            mother.StatureGene, (father ?? mother).StatureGene,
            body.Genetics.Mendelian ? body.Genetics.StatureSegregationStdDev : 0.0,
            world.Rng.StreamFor("stature-gene", baby.Id.Value));
        SetStatureAndBody(world, baby, gene, workShare: 0);
    }

    private static void SetStatureAndBody(WorldState world, Npc npc, double gene, double workShare)
    {
        var growth = world.BodyRules.Growth;
        double environment = QuantitativeGenetics.Normal(world.Rng.StreamFor("stature-env", npc.Id.Value));
        double adult = Math.Max(0.5, growth.AdultHeightFrom(npc.Sex, gene, environment));
        npc.SetStature(gene, adult);
        double age = AgeYears(world, npc, world.CurrentDate.TotalHours);
        double height = growth.HeightAt(npc.Sex, age, adult, npc.GestationAtBirthDays);
        double weight = growth.WeightAt(npc.Sex, age, height, Frame(world, npc), npc.Undernutrition, npc.GestationAtBirthDays);
        npc.SetBody(height, weight, growth.MuscleTarget(npc.Sex, age, weight, workShare));
    }

    /// <summary>Um mês de corpo: nanismo pela desnutrição de quem cresce, altura e peso da idade, músculo em direção ao alvo
    /// do uso do mês (retorno decrescente, com teto do cenário). Zera os dias de trabalho do mês.</summary>
    public static void MonthlyUpdate(WorldState world, Npc npc, long tick)
    {
        var body = world.BodyRules;
        var growth = body.Growth;
        if (!growth.Enabled) return;
        double age = AgeYears(world, npc, tick);
        double monthYears = 1.0 / world.Calendar.MonthsPerYear;

        double adult = npc.AdultHeight;
        if (adult <= 0)
        {
            // snapshot anterior ao AD-134: adulto guarda a altura que tinha; criança herda a média do sexo
            adult = age >= growth.GrowthEndAgeYears ? npc.Height / Math.Max(1e-6, growth.ElderHeightFactor.At(age))
                : growth.AdultHeightMean(npc.Sex);
            npc.SetStature(npc.StatureGene, adult);
        }

        double stunting = growth.StuntingLoss(npc.Sex, age, monthYears, adult, npc.Undernutrition);
        if (stunting > 0)
        {
            adult -= stunting;
            npc.SetAdultHeight(adult);
        }

        double height = growth.HeightAt(npc.Sex, age, adult, npc.GestationAtBirthDays);
        if (age < growth.GrowthEndAgeYears) height = Math.Max(height, npc.Height); // quem cresce não encolhe
        double weight = growth.WeightAt(npc.Sex, age, height, Frame(world, npc), npc.Undernutrition, npc.GestationAtBirthDays);
        double workShare = npc.PhysicalWorkDays / (double)world.Calendar.DaysPerMonth;
        double target = growth.MuscleTarget(npc.Sex, age, weight, workShare);
        // quem cresce acompanha a curva; no adulto o treino rende devagar e a perda (fome, desuso) vem mais rápido
        double rate = age < growth.GrowthEndAgeYears ? 1.0
            : target < npc.MuscleMass ? Math.Min(1.0, 2 * growth.MuscleAdaptationPerMonth) : growth.MuscleAdaptationPerMonth;
        double muscle = npc.MuscleMass + (target - npc.MuscleMass) * rate;
        muscle = Math.Clamp(Math.Min(muscle, weight * 0.6), 0.05, body.MuscleMassMax);
        npc.SetBody(height, weight, muscle);
        npc.ResetPhysicalWorkDays();
    }
}
