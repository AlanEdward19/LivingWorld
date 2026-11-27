using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Body;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Players;

/// <summary>O próximo passo de uma meta, sempre algo que os comandos de hoje fazem (#235). <c>Command</c>: <c>act</c> (POST action,
/// <c>Verb</c> é a ação), <c>interact</c> (POST interact com <c>Kind</c>, <c>TargetId</c>, <c>Verb</c>, <c>ResourceId</c>: a oferta já
/// está ao alcance) ou <c>go</c> (andar até a célula <c>X</c>,<c>Y</c>; <c>Distance</c> em células). <c>Here</c>: já dá para executar.</summary>
public sealed record GoalStep(string Command, string Kind, long TargetId, string Verb, string Label, int X, int Y, int Distance, bool Here,
    int? ResourceId = null);

/// <summary>Uma meta do corpo controlado, derivada do estado do motor: <c>Kind</c> estável (<c>need.hunger</c>, <c>family.child-hungry</c>…),
/// o motivo, a urgência (0 a 100) e o próximo passo (nulo só quando nada no mundo permite um).</summary>
public sealed record PlayerGoal(string Kind, string Reason, int Urgency, GoalStep? Step);

/// <summary>Metas que nascem do mundo (#235): nenhuma missão escrita à mão. Lê fome, sede, sono e companhia do corpo, os filhos que
/// dependem dele, a família doente, o emprego, o aluguel atrasado, a carteira, o teto e o ofício, e aponta para a oferta ou o lugar
/// reais que o jogador pode usar. Só lê, sem sorteio; roda sob pedido (nunca por tick). Ordem: urgência, depois <c>Kind</c>.</summary>
public static partial class PlayerGoals
{
    /// <summary>Falta mínima (100 − necessidade) para a necessidade virar meta; acima do <see cref="NeedsRules.UrgencyThreshold"/> é urgente.</summary>
    public const int MinNeedDeficit = 50;
    /// <summary>Saúde abaixo da qual um familiar vira meta.</summary>
    public const int SickBelow = 50;
    public const int MaxGoals = 8;

    public static IReadOnlyList<PlayerGoal> Of(WorldState world, Npc npc)
    {
        var c = new Scene(world, npc);
        var goals = new List<PlayerGoal>();
        long now = world.CurrentDate.TotalHours;

        AddNeed(goals, "need.hunger", "Você está com fome", npc.HungerAt(now), () => FoodStep(c));
        AddNeed(goals, "need.thirst", "Você está com sede", npc.ThirstAt(now), () => WaterStep(c));
        AddNeed(goals, "need.sleep", "Você está exausto", npc.SleepAt(now), () => SleepStep(c));
        AddNeed(goals, "need.social", "Você está sozinho há muito tempo", npc.SocialAt(now), () => TalkStep(c, null));

        FamilyGoals(goals, c, now);
        LivelihoodGoals(goals, c);

        return goals.GroupBy(g => g.Kind).Select(g => g.First())
            .OrderByDescending(g => g.Urgency).ThenBy(g => g.Kind, StringComparer.Ordinal).Take(MaxGoals).ToList();
    }

    private static void AddNeed(List<PlayerGoal> goals, string kind, string reason, int need, Func<GoalStep?> step)
    {
        int deficit = Deficit(need);
        if (deficit >= MinNeedDeficit) goals.Add(new PlayerGoal(kind, reason, deficit, step()));
    }

    /// <summary>Falta de 0 a 100; necessidade zerada é urgência total (a mesma linha do motor, <c>need == 0</c>).</summary>
    internal static int Deficit(int need) => need <= 0 ? 100 : Math.Clamp(100 - need, 0, 100);

    private static void FamilyGoals(List<PlayerGoal> goals, Scene c, long now)
    {
        var (world, npc) = (c.World, c.Npc);
        Npc? hungriest = null;
        int worst = 0;
        string what = "fome";
        foreach (var member in c.Household?.Members.OrderBy(m => m.Value).Select(world.FindNpc).OfType<Npc>() ?? [])
        {
            if (member.Id == npc.Id || !member.IsAlive || !PlayerOrders.DependentChild(world, npc, member)) continue;
            int hunger = Deficit(member.HungerAt(now)), thirst = Deficit(member.ThirstAt(now));
            if (Math.Max(hunger, thirst) > worst) (hungriest, worst, what) = (member, Math.Max(hunger, thirst), thirst > hunger ? "sede" : "fome");
        }
        if (hungriest is not null && worst >= MinNeedDeficit)
            goals.Add(new PlayerGoal("family.child-hungry", $"{hungriest.Name}, que depende de você, está com {what}",
                Math.Min(100, worst + 5), what == "fome" ? FoodStep(c) : WaterStep(c)));

        Npc? sick = null;
        bool spouse = false;
        foreach (var member in FamilyOf(c))
        {
            if (member.Health >= SickBelow || (sick is not null && member.Health >= sick.Health)) continue;
            (sick, spouse) = (member, npc.Spouse == member.Id);
        }
        if (sick is not null)
            goals.Add(new PlayerGoal(spouse ? "family.spouse-sick" : "family.member-sick",
                $"{sick.Name}, {(spouse ? "seu cônjuge" : "da sua casa")}, está doente (saúde {sick.Health})",
                Math.Min(100, 100 - sick.Health + (spouse || PlayerOrders.DependentChild(world, npc, sick) ? 10 : 0)), TalkStep(c, sick)));
    }

    /// <summary>O cônjuge e quem mora com o corpo, vivos, em ordem de id.</summary>
    private static IEnumerable<Npc> FamilyOf(Scene c)
    {
        var ids = new SortedSet<long>(c.Household?.Members.Select(m => m.Value) ?? []);
        if (c.Npc.Spouse is { } spouse) ids.Add(spouse.Value);
        ids.Remove(c.Npc.Id.Value);
        return ids.Select(id => c.World.FindNpc(new NpcId(id))).OfType<Npc>().Where(n => n.IsAlive);
    }

    private static void LivelihoodGoals(List<PlayerGoal> goals, Scene c)
    {
        var (world, npc) = (c.World, c.Npc);
        bool adult = world.LifeStageRules.LifeStageOf(npc.AgeYears(world.CurrentDate)) == LifeStage.Adult;
        bool working = npc.CurrentAction == ActionType.Work && c.Workplace is { } w && npc.CurrentLocation == w.Location; // onde o SkillPracticeSystem exige o corpo

        if (c.Household is { } home && home.RentArrearsMonths > 0)
            goals.Add(new PlayerGoal("money.rent-arrears",
                $"O aluguel está atrasado há {home.RentArrearsMonths} {(home.RentArrearsMonths == 1 ? "mês" : "meses")}",
                Math.Min(95, 50 + 15 * home.RentArrearsMonths), WorkStep(c)));
        if (c.Household is null || c.Household.IsHomeless)
            goals.Add(new PlayerGoal("home.homeless", "Você está sem teto", 70, WorkStep(c)));
        if (adult && npc.Wallet.Amount == 0 && world.EconomyRules.Enabled)
            goals.Add(new PlayerGoal("money.broke", "Você está sem dinheiro", 45, WorkStep(c)));
        if (adult && !working && npc.Employer is not null && c.Workplace is not null)
            goals.Add(new PlayerGoal("work.report", "Seu posto de trabalho espera por você", 40, WorkStep(c)));
        if (npc.Employer is not null && c.Workplace is not null && PlayerSkills.Practiced(world, npc) is { } skill && skill.Level < skill.MaxLevel)
            goals.Add(new PlayerGoal("skill.grow",
                $"Praticar o ofício: nível {skill.Level}, faltam {skill.ToNext:0.#} pontos para o {skill.Level + 1}", 20, WorkStep(c)));
    }
}
