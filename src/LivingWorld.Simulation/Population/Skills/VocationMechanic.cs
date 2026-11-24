using LivingWorld.Domain.History;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Body;
using LivingWorld.Domain.Population.Skills;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Economy.Labor;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Population.Skills;

/// <summary>Vocação (AD-136): o gosto próprio por ofício (herdado em parte, sorteado no resto), a escolha do ofício na
/// juventude e o quanto o ofício exercido combina com a pessoa. Quem escolhe é <see cref="SkillTeachingSystem"/>; quem
/// consome o ajuste é a produção (<c>ProductionSystem</c>), a prática (<see cref="SkillPracticeSystem"/>) e a troca de
/// emprego (<c>BehaviorDecisionSystem</c>).</summary>
public static class VocationMechanic
{
    public const string SourceSystem = "VocationMechanic";

    /// <summary>Normal aproximada de média 0 e variância 1 (soma de três uniformes), só com aritmética.</summary>
    private static double Normal(WorldRng rng) => (rng.NextDouble() + rng.NextDouble() + rng.NextDouble() - 1.5) * 2.0;

    /// <summary>Gosto por ofício de quem ainda não tem: herdado na média dos pais que se conhecem (fração
    /// <see cref="VocationRules.TasteHeritability"/>) e sorteado no resto, com variância 1 na população.</summary>
    public static VocationState NewTaste(WorldState world, TickContext ctx, Npc npc, VocationRules rules, bool chosen)
    {
        var mother = npc.MotherId is { } m ? world.FindNpc(m)?.Vocation : null;
        var father = npc.FatherId is { } f ? world.FindNpc(f)?.Vocation : null;
        var rng = ctx.StreamFor("vocation-taste", npc.Id.Value);
        double h = rules.TasteHeritability;
        var taste = new Dictionary<int, double>();
        foreach (int id in world.PopulationCatalog.ProfessionIds.OrderBy(i => i))
        {
            double noise = Normal(rng);
            taste[id] = (mother, father) switch
            {
                ({ } a, { } b) => h * (a.TasteFor(id) + b.TasteFor(id)) / 2 + Math.Sqrt(1 - h * h / 2) * noise,
                ({ } a, null) => h / 2 * a.TasteFor(id) + Math.Sqrt(1 - h * h / 4) * noise,
                (null, { } b) => h / 2 * b.TasteFor(id) + Math.Sqrt(1 - h * h / 4) * noise,
                _ => noise,
            };
        }
        return new VocationState(taste, Chosen: chosen);
    }

    public static double Aptitude(VocationRules rules, Npc npc, int profession) =>
        rules.Aptitude(profession, npc.Personality, npc.MuscleMass, npc.Vocation);

    /// <summary>Rendimento de quem exerce o próprio ofício: 1 + efeito × aptidão (1 sem vocação ou sem ofício).</summary>
    public static double OutputFit(SkillsRules rules, Npc npc) => Fit(rules, npc, npc.Profession, rules.Vocation.FitOutput);

    public static double LearningFit(SkillsRules rules, Npc npc) => Fit(rules, npc, npc.Profession, rules.Vocation.FitLearning);

    /// <summary>O quanto a pessoa valoriza trabalhar em <paramref name="profession"/>: combina com ela, vale mais.</summary>
    public static double JobValueFit(SkillsRules rules, Npc npc, ProfessionType profession) =>
        Fit(rules, npc, profession, rules.Vocation.FitJobValue);

    private static double Fit(SkillsRules rules, Npc npc, ProfessionType profession, double effect)
    {
        var vocation = rules.Vocation;
        if (!vocation.Enabled || effect == 0 || profession == ProfessionType.None || npc.Vocation is null) return 1.0;
        return VocationRules.FitMultiplier(effect, Aptitude(vocation, npc, profession.Id));
    }

    /// <summary>A escolha do ofício, uma vez, na juventude: inclinação, exposição (habilidade que já tem), oportunidade (vaga
    /// que paga na cidade) e a pressão da casa, mais um pouco de acaso. O ofício escolhido passa a ser o da pessoa; o
    /// registro guarda o ofício da casa, a força dela e se o jovem cedeu contra a própria inclinação.</summary>
    public static void Choose(WorldState world, TickContext ctx, Npc youth, SkillsRules skills, LifeStageRules stages)
    {
        var rules = skills.Vocation;
        var state = youth.Vocation ?? NewTaste(world, ctx, youth, rules, chosen: false);
        var professions = world.PopulationCatalog.ProfessionIds.OrderBy(i => i).ToList();
        if (professions.Count == 0) return;

        var (house, force) = HouseOf(world, youth, rules, stages);
        double compliance = VocationRules.Compliance(youth.Personality);
        var opportunity = Opportunities(world, youth, professions);
        double bestOpportunity = opportunity.Count == 0 ? 0 : opportunity.Values.Max();
        var rng = ctx.StreamFor("vocation-choice", youth.Id.Value);

        int chosen = professions[0], free = professions[0];
        double bestUtility = double.NegativeInfinity, bestFree = double.NegativeInfinity;
        foreach (int id in professions)
        {
            double skill = skills.SkillByProfession.TryGetValue(id, out var skillType) ? youth.Skills.Get(skillType) / skills.Cap : 0;
            double opp = bestOpportunity > 0 ? opportunity.GetValueOrDefault(id) / bestOpportunity : 0;
            double own = rules.Aptitude(id, youth.Personality, youth.MuscleMass, state)
                + rules.ExposureWeight * skill + rules.OpportunityWeight * opp + rules.ChoiceNoise * Normal(rng);
            double pushed = own + (id == house ? rules.PressureScale * force * compliance : 0);
            if (own > bestFree) (bestFree, free) = (own, id);
            if (pushed > bestUtility) (bestUtility, chosen) = (pushed, id);
        }

        bool pressured = chosen == house && free != house;
        youth.SetVocation(state with { Chosen = true, HouseProfession = house, Pressure = force, Pressured = pressured });
        if (youth.Profession.Id != chosen) youth.SwitchProfession(new ProfessionType(chosen));
        ctx.LogEvent(WorldEventKind.VocationChosen,
            $"{youth.Id.Value}|{chosen}|{house}|{(pressured ? 1 : 0)}|{force.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)}",
            sourceSystem: SourceSystem);
    }

    /// <summary>O ofício da casa e a força com que ela empurra: entre os adultos da casa com ofício, quem empurra mais
    /// (desempate pelo menor id). Sem ninguém: nenhum ofício (0) e força 0.</summary>
    internal static (int Profession, double Force) HouseOf(WorldState world, Npc youth, VocationRules rules, LifeStageRules stages)
    {
        if (youth.Household is not { } homeId || world.FindHousehold(homeId) is not { } home) return (0, 0);
        int profession = 0;
        double force = -1;
        foreach (var memberId in home.Members.OrderBy(m => m.Value))
        {
            if (memberId == youth.Id || world.FindNpc(memberId) is not { IsAlive: true } adult) continue;
            if (adult.Profession == ProfessionType.None) continue;
            if (stages.LifeStageOf(adult.AgeYears(world.CurrentDate)) is not (LifeStage.Adult or LifeStage.Elder)) continue;
            bool owns = world.Workplaces.Any(w => w.Owner == adult.Id);
            double f = rules.PressureOf(adult.Personality, owns);
            if (f > force) (force, profession) = (f, adult.Profession.Id);
        }
        return force < 0 ? (0, 0) : (profession, force);
    }

    /// <summary>Por ofício, o valor do trabalho na melhor oficina da cidade com vaga que paga (0 se não há).</summary>
    private static Dictionary<int, double> Opportunities(WorldState world, Npc youth, List<int> professions)
    {
        var result = new Dictionary<int, double>();
        var catalog = world.EconomyCatalog;
        if (catalog.LocationTypeByProfession.Count == 0) return result;
        var labor = LaborValueIndex.BuildForTick(world);
        foreach (var workplace in labor.In(youth.City))
        {
            if (workplace.Employees.Count >= workplace.MaxVacancies) continue;
            foreach (int id in professions)
            {
                if (!catalog.LocationTypeByProfession.TryGetValue(id, out int type) || type != workplace.LocationType.Id) continue;
                if (!Payroll.CanPay(world, workplace, new ProfessionType(id))) continue;
                double value = labor.ValueOf(workplace);
                if (value > result.GetValueOrDefault(id)) result[id] = value;
            }
        }
        return result;
    }
}
