using LivingWorld.Domain.Economy;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Body;
using LivingWorld.Domain.Population.Skills;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Economy.Labor;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Economy.Venture;

/// <summary>Decisão de empreender (AD-152), <c>Monthly</c>, depois da folha e da retirada do lucro. Ninguém tem lista de quem
/// abre o quê: o adulto que sabe o ofício (<see cref="VentureRules.MinSkill"/>) olha a cidade dele, faz a conta do negócio
/// (<see cref="VenturePlan"/>: quanto venderia ao preço que cobraria contra a folha que pagaria) e, com essa oportunidade, decide
/// pelo caráter: a chance do mês cresce com a ambição e cai com a aversão ao risco; sem trabalho no ofício, a necessidade dobra
/// a oportunidade. Decidido, ele junta o capital (<see cref="VentureCapital"/>: poupança do casal, herança, sócio) e, se fecha
/// a conta, abre (<see cref="VentureFounding"/>): paga foro, obra, equipamento e estoque com moeda que existe e põe o resto no
/// caixa da oficina, que nasce dele. Quem é dono de uma oficina do ofício parada reabre a dele (só o capital de giro). Uma
/// abertura por ofício por cidade por mês. Consumidores: a oficina nova contrata, vende
/// (a fazenda compra a ferramenta dela), paga salário e rende o lucro ao dono e aos sócios (<see cref="OwnerDrawSystem"/>).</summary>
[LivingCapability("VENTURE", Events = [WorldEventKind.VentureFounded, WorldEventKind.Hired], Consumers = ["timeline.wages", "inspector.employment"])]
[RunsAfter(typeof(OwnerDrawSystem))]
public sealed class VentureSystem(SkillsRules skillsRules) : ISimulationSystem
{
    public const string SystemName = "economy-venture";

    public string Name => SystemName;
    public TickFrequency Frequency => TickFrequency.Monthly;

    public void Tick(WorldState world, TickContext ctx)
    {
        var economy = world.EconomyRules;
        if (!economy.Enabled || economy.Venture is not { LocationTypeIds.Count: > 0 } rules) return;

        var calendar = world.CurrentDate.Calendar;
        long month = ctx.CurrentTick / ((long)calendar.HoursPerDay * calendar.DaysPerMonth);
        List<Npc>? candidates = null;
        foreach (var city in world.ActiveCities().OrderBy(c => c.Id.Value))
            foreach (int type in rules.LocationTypeIds.Distinct().OrderBy(t => t))
            {
                if (VentureTrade.Of(world, skillsRules, type) is not { } trade) continue;
                var plan = VenturePlan.For(world, rules, trade, city.Id);
                if (plan.Margin <= 0) continue;

                candidates ??= Grown(world);
                foreach (var npc in candidates)
                {
                    if (npc.City != city.Id || npc.Skills.Get(trade.Skill) < rules.MinSkill) continue;
                    var owned = OwnedBy(world, npc);
                    // quem já tem oficina trabalhando não abre outra; a dele parada (folha que não fechou, ninguém nela) ele reabre
                    if (owned is not null && (owned.City != city.Id || owned.LocationType.Id != type || VenturePlan.Operating(world, owned))) continue;
                    double chance = ChanceOf(world, rules, trade, plan, npc);
                    if (chance <= 0 || ctx.StreamFor("venture", npc.Id.Value * 1_000_003L + month).NextDouble() >= chance) continue;
                    var opened = owned is null
                        ? VentureFounding.TryFound(world, ctx, rules, trade, plan, npc)
                        : VentureFounding.TryReopen(world, ctx, rules, trade, plan, npc, owned);
                    if (opened is not null) break; // um por ofício e cidade no mês
                }
            }
    }

    /// <summary>Chance do mês: <see cref="VentureRules.MonthlyChance"/> × oportunidade × ambição × (1 − aversão ao risco). Quem não
    /// acha trabalho pago no ofício sente a oportunidade em dobro (até 1).</summary>
    internal static double ChanceOf(WorldState world, VentureRules rules, VentureTrade trade, VenturePlan plan, Npc npc)
    {
        bool needsWork = npc.Employer is not { } employer || world.FindWorkplace(employer) is not { } job
            || job.PayrollFailed || job.LocationType.Id != trade.LocationTypeId;
        double opportunity = Math.Min(1.0, plan.Margin * (needsWork && !VenturePlan.HasPaidVacancy(world, trade, npc.City) ? 2 : 1));
        var p = npc.Personality;
        return rules.MonthlyChance * opportunity * (p.Ambition / 100.0) * (1 - p.RiskAversion / 100.0);
    }

    /// <summary>Adultos vivos com a idade de mestre, em ordem de id (a ordem decide quem abre primeiro no mês).</summary>
    private static List<Npc> Grown(WorldState world)
    {
        int minAge = world.EconomyRules.Venture!.MinAgeYears;
        var grown = new List<Npc>();
        foreach (var npc in world.AliveNpcIndex.Alive)
        {
            int age = npc.AgeYears(world.CurrentDate);
            if (age >= minAge && world.LifeStageRules.LifeStageOf(age) == LifeStage.Adult) grown.Add(npc);
        }

        grown.Sort((a, b) => a.Id.Value.CompareTo(b.Id.Value));
        return grown;
    }

    /// <summary>A oficina de que <paramref name="npc"/> é dono (a de menor id), ou nula.</summary>
    private static Workplace? OwnedBy(WorldState world, Npc npc)
    {
        Workplace? found = null;
        foreach (var workplace in world.Workplaces)
            if (workplace.Owner == npc.Id && (found is null || workplace.Id.Value < found.Id.Value)) found = workplace;
        return found;
    }
}

/// <summary>Um ofício que se pode abrir: o tipo de local, a receita, o ofício (profissão) que trabalha nele, a habilidade dele e o
/// salário da tabela.</summary>
internal sealed record VentureTrade(int LocationTypeId, ProductionRecipe Recipe, ProfessionType Profession, SkillType Skill, long Wage)
{
    public static VentureTrade? Of(WorldState world, SkillsRules skills, int locationTypeId)
    {
        if (!world.EconomyCatalog.Recipes.TryGetValue(locationTypeId, out var recipe)) return null;
        int? profession = null;
        foreach (var (p, type) in world.EconomyCatalog.LocationTypeByProfession)
            if (type == locationTypeId && (profession is null || p < profession)) profession = p;
        if (profession is not { } id || !skills.SkillByProfession.TryGetValue(id, out var skill)) return null;
        return new VentureTrade(locationTypeId, recipe, new ProfessionType(id), skill,
            world.EconomyRules.WageByProfession.GetValueOrDefault(id));
    }
}
