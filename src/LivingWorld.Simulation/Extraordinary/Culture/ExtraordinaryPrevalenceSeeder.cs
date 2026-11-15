using LivingWorld.Domain.Extraordinary;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Extraordinary.Inheritance;
using LivingWorld.Simulation.Extraordinary.Systems;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Extraordinary.Culture;

/// <summary>Portadores fundadores pela genética (AD-156): cada fundador tem os alelos sorteados pela frequência do cenário
/// (Hardy-Weinberg, ver <see cref="PowerGenome"/>); quem tem o genótipo e passa na penetrância manifesta se já tem a idade de
/// início, ou recebe o início agendado. Membro do pool agregado (sem pessoa materializada, sem sexo nem idade) recebe os poderes
/// autossômicos como portador dormente, sem materializar ninguém.</summary>
public static class ExtraordinaryPrevalenceSeeder
{
    public static int Seed(WorldState world)
    {
        var scenario = world.Extraordinary;
        if (!scenario.Enabled || scenario.Descriptors.Count == 0) return 0;
        var layout = PowerGenome.Layout(scenario);
        if (layout.All(locus => locus.FounderFrequency <= 0)) return 0;

        var ctx = new TickContext(world, world.Rng, world.Scheduler);
        int created = 0;
        foreach (var npc in world.Npcs.Where(n => n.IsAlive).OrderBy(n => n.Id.Value).ToList())
        {
            if (world.CarrierOf(npc.Id) is not null) continue;
            var now = new List<string>();
            foreach (var locus in layout)
            {
                if (!PowerGeneticsMechanic.Expresses(world, npc, locus)) continue;
                if (PowerGeneticsMechanic.OnsetTick(world, npc, locus) <= ctx.CurrentTick) now.Add(locus.PowerId);
                else ctx.ScheduleEvent(PowerGeneticsMechanic.OnsetTick(world, npc, locus), ExtraordinaryStateSystem.SystemName,
                    $"{PowerGeneticsMechanic.OnsetPrefix}|{npc.Id.Value}|{locus.PowerId}");
            }
            if (now.Count == 0) continue;
            world.UpsertExtraordinaryCarrier(ExtraordinaryStateSystem.Resolve(world, npc, now));
            PowerGeneticsMechanic.RefreshMetabolism(world, npc, ctx.CurrentTick);
            created++;
        }

        foreach (var city in world.ActiveCities().OrderBy(item => item.Id.Value))
            foreach (var id in city.PoolNpcIds.OrderBy(id => id.Value))
            {
                if (world.CarrierOf(id) is not null || world.FindNpc(id) is not null) continue;
                var powers = PooledPowers(world, id, layout);
                if (powers.Count == 0) continue;
                world.UpsertExtraordinaryCarrier(DormantAggregate(id, powers));
                created++;
            }
        return created;
    }

    private static List<string> PooledPowers(WorldState world, NpcId id, IReadOnlyList<PowerLocus> layout)
    {
        ulong genome = PowerGenome.Founder(world, id, sex: null);
        var powers = new List<string>();
        foreach (var locus in layout)
        {
            if (locus.Genetics.IsXLinked) continue;
            var probe = ProbePenetrance(world, id, locus);
            if (probe && PowerGenome.IsAffected(locus, genome, Domain.Population.Sex.Female, environment: 0)) powers.Add(locus.PowerId);
        }
        return powers;
    }

    /// <summary>Penetrância do membro do pool: o mesmo sorteio fixo que a pessoa materializada terá.</summary>
    private static bool ProbePenetrance(WorldState world, NpcId id, PowerLocus locus) =>
        PowerGeneticsMechanic.PenetranceRoll(world, id, locus) < locus.Genetics.Penetrance;

    private static ExtraordinaryCarrierState DormantAggregate(NpcId id, List<string> powers) => new(
        id, powers.OrderBy(p => p, StringComparer.Ordinal).ToList(), false, "dormant",
        new ExtraordinaryAppearanceState(1, "", ""), null, 1);
}
