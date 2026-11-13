using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Population.Body;

namespace LivingWorld.Simulation.Population.Family;

/// <summary>Meios de uma casa nova e a prudência de quem decide (AD-145): o freio preventivo da sociedade pré-industrial.
/// Quem não tem com o que montar casa adia o casamento e, já casado, o primeiro filho, no tanto em que é prudente. A prudência
/// sai dos termos que o NPC já tem: consciência e aversão a risco contra impulsividade (com o impulso da adolescência e a
/// antevisão descontada dela) e a lembrança de fome, que deixa mais cauteloso. Leitura pura: nada é escrito no mundo.</summary>
public static class HouseholdMeans
{
    /// <summary>Prudência (0..1): ≈ 0,5 no temperamento médio adulto; menor no pico da adolescência; maior em quem passou fome.</summary>
    public static double Prudence(WorldState world, Npc npc, long tick)
    {
        var rules = Endocrine.Rules(world);
        double drive = Endocrine.AdolescentDrive(world, npc, tick);
        double care = (npc.Personality.Conscientiousness + Endocrine.EffectiveRiskAversion(rules, npc.Personality, drive)) / 200.0;
        double restraint = 1.5 - Endocrine.EffectiveImpulsivity(rules, npc.Personality, drive) / 100.0;
        double foresight = 1 - drive * rules.ForesightDiscount;
        double famine = LivingWorld.Simulation.Cognition.PsycheMechanic.HoardFactor(world, npc, tick);
        return Math.Clamp(care * restraint * foresight * famine, 0, 1);
    }

    /// <summary>Meios do casal (0..1): alguém dos dois com trabalho (<see cref="PopulationPressureRules.MarriageEarnerShare"/>) e as economias
    /// dos dois contra os meses de gasto de uma casa de dois (<see cref="PopulationPressureRules.MarriageSavingsMonths"/>).</summary>
    public static double OfCouple(WorldState world, Npc a, Npc b)
    {
        var rules = world.FamilyRules.Pressure ?? PopulationPressureRules.Measured;
        double earner = a.Employer is not null || b.Employer is not null ? 1 : 0;
        double needed = rules.MarriageSavingsMonths * MonthlyCostPerPerson(world) * 2;
        double savings = Math.Clamp((a.Wallet.Amount + b.Wallet.Amount) / needed, 0, 1);
        return rules.MarriageEarnerShare * earner + (1 - rules.MarriageEarnerShare) * savings;
    }

    /// <summary>Quanto da vontade de casar com <paramref name="partner"/> sobra a <paramref name="evaluator"/> depois de pesar os
    /// meios do casal (1 = nada tira).</summary>
    public static double MarriageReadiness(WorldState world, Npc evaluator, Npc partner)
    {
        double weight = world.FamilyRules.Pressure?.MarriageMeansWeight ?? 0;
        if (weight <= 0 || !world.EconomyRules.Enabled) return 1;
        double means = OfCouple(world, evaluator, partner);
        if (means >= 1) return 1;
        return 1 - weight * Prudence(world, evaluator, world.CurrentDate.TotalHours) * (1 - means);
    }

    /// <summary>Multiplicador da concepção do primeiro filho do casal: sem ninguém trabalhando e com a despensa curta, o casal
    /// prudente adia (os dois decidem: vale a média da prudência). Depois do primeiro parto, 1 (fecundidade natural).</summary>
    public static double FirstBirthReadiness(WorldState world, Npc mother, Npc father, Household? household)
    {
        double weight = world.FamilyRules.Pressure?.FirstBirthMeansWeight ?? 0;
        if (weight <= 0 || !world.EconomyRules.Enabled || (mother.Reproduction?.Parity ?? 0) > 0) return 1;
        if (mother.Employer is not null || father.Employer is not null) return 1;
        double means = OfCouple(world, mother, father);
        long food = household?.Stock.GetValueOrDefault(new ResourceType(world.EconomyRules.FoodResourceId)) ?? 0;
        double pantry = Math.Clamp(food / (double)(Math.Max(1, household?.Members.Count ?? 1) * world.Calendar.DaysPerMonth), 0, 1);
        means = Math.Max(means, pantry);
        long tick = world.CurrentDate.TotalHours;
        double prudence = (Prudence(world, mother, tick) + Prudence(world, father, tick)) / 2;
        return 1 - weight * prudence * (1 - means);
    }

    /// <summary>Gasto de um mês de uma pessoa: uma ração e uma água por dia ao piso de preço do cenário.</summary>
    public static double MonthlyCostPerPerson(WorldState world)
    {
        var economy = world.EconomyRules;
        long food = Math.Max(1, economy.PriceFloor.GetValueOrDefault(economy.FoodResourceId, 1));
        long water = Math.Max(1, economy.PriceFloor.GetValueOrDefault(economy.WaterResourceId, 1));
        return (food + water) * (double)world.Calendar.DaysPerMonth;
    }
}
