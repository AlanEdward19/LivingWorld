using LivingWorld.Domain.Extraordinary;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Extraordinary.Systems;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Extraordinary.Inheritance;

/// <summary>Do genótipo ao poder (AD-156). Quem tem o genótipo manifesta com a penetrância do poder (sorteio fixo da pessoa),
/// na idade de início (curva da puberdade, sorteio fixo), com intensidade contínua (expressividade). O poder manifesto custa
/// fome e sede mesmo sem uso e pode reduzir a fertilidade e a vitalidade: é por aí que a seleção natural opera.
/// Consumidores: <c>NatalitySystem</c> (genótipo do filho, fertilidade), <c>MortalitySystem</c> (vitalidade), a fome e a sede
/// (<c>Lactation</c>), a rolagem do poder (<c>ExtraordinaryInvocationEngine</c>) e o início (<c>ExtraordinaryStateSystem</c>).</summary>
public static class PowerGeneticsMechanic
{
    public const string OnsetPrefix = "onset";
    private const string PhenotypePurpose = "power-phenotype";
    private const double Sqrt3 = 1.7320508075688772;

    /// <summary>Sorteios fixos da pessoa para um poder: penetrância, início, expressividade e ambiente (poligênico).</summary>
    private readonly record struct Phenotype(double PenetranceRoll, double OnsetZ, double ExpressivityZ, double EnvironmentZ);

    private static Phenotype Draw(WorldState world, NpcId id, PowerLocus locus)
    {
        ulong s = world.Rng.StreamStateFor(PhenotypePurpose, id.Value * PowerGenome.MaxLoci + locus.Ordinal);
        return new Phenotype(WorldRng.NextDouble(ref s), Normal(ref s), Normal(ref s), Normal(ref s));
    }

    /// <summary>N(0,1) aproximada (Irwin-Hall de quatro uniformes, ±3,5 dp): só soma e multiplicação.</summary>
    private static double Normal(ref ulong s) =>
        (WorldRng.NextDouble(ref s) + WorldRng.NextDouble(ref s) + WorldRng.NextDouble(ref s) + WorldRng.NextDouble(ref s) - 2.0) * Sqrt3;

    public static double PenetranceRoll(WorldState world, NpcId id, PowerLocus locus) => Draw(world, id, locus).PenetranceRoll;

    public static bool HasAffectedGenotype(WorldState world, Npc npc, PowerLocus locus) =>
        PowerGenome.IsAffected(locus, PowerGenome.Of(world, npc), npc.Sex, Draw(world, npc.Id, locus).EnvironmentZ);

    /// <summary>Genótipo que dá o poder e penetrância que o deixa aparecer.</summary>
    public static bool Expresses(WorldState world, Npc npc, PowerLocus locus)
    {
        var phenotype = Draw(world, npc.Id, locus);
        return phenotype.PenetranceRoll < locus.Genetics.Penetrance
            && PowerGenome.IsAffected(locus, PowerGenome.Of(world, npc), npc.Sex, phenotype.EnvironmentZ);
    }

    public static double OnsetAgeYears(WorldState world, Npc npc, PowerLocus locus) =>
        Math.Max(0, locus.Genetics.OnsetAgeMeanYears + locus.Genetics.OnsetAgeStdDevYears * Draw(world, npc.Id, locus).OnsetZ);

    public static long OnsetTick(WorldState world, Npc npc, PowerLocus locus) =>
        npc.BirthDate.TotalHours + (long)(OnsetAgeYears(world, npc, locus) * world.Calendar.HoursPerYear);

    /// <summary>Intensidade do poder herdado (1 = típica): dose a mais no dominante, escore acima do limiar no poligênico,
    /// e o desvio da expressividade. Poder sem genótipo (evento, autoria) fica em 1.</summary>
    public static double Intensity(WorldState world, Npc npc, string powerId)
    {
        if (!world.Extraordinary.Enabled || PowerGenome.Find(world, powerId) is not { } locus) return 1.0;
        var phenotype = Draw(world, npc.Id, locus);
        ulong genome = PowerGenome.Of(world, npc);
        if (!PowerGenome.IsAffected(locus, genome, npc.Sex, phenotype.EnvironmentZ)) return 1.0;
        int dose = PowerGenome.Dose(locus, genome);
        double baseline = locus.Genetics.Mode switch
        {
            PowerInheritanceMode.Polygenic => 1 + 0.25 * (dose + locus.Genetics.PolygenicEnvironmentStdDev * phenotype.EnvironmentZ
                - locus.Genetics.PolygenicThreshold),
            PowerInheritanceMode.Dominant or PowerInheritanceMode.XLinkedDominant when dose == 2 => 1.25,
            _ => 1.0,
        };
        return Math.Clamp(baseline + locus.Genetics.ExpressivityStdDev * phenotype.ExpressivityZ, 0.25, 2.0);
    }

    /// <summary>Genótipo do filho na concepção e agenda do início de cada poder que ele vai manifestar.</summary>
    public static void OnBirth(WorldState world, TickContext ctx, Npc baby, Npc mother, Npc? father, long causeEventId)
    {
        var layout = PowerGenome.Layout(world.Extraordinary);
        if (layout.Count == 0) return;
        ulong? fatherGenome = father is null ? null : PowerGenome.Of(world, father);
        baby.SetPowerGenome(PowerGenome.Inherit(layout, PowerGenome.Of(world, mother), fatherGenome, baby.Sex,
            ctx.StreamFor(PowerGenome.InheritPurpose, baby.Id.Value)));
        var expressed = new List<string>();
        foreach (var locus in layout)
            if (Expresses(world, baby, locus))
                expressed.Add(locus.PowerId);
        if (expressed.Count == 0) return;
        long inherited = ctx.LogEvent(WorldEventKind.PowerInherited,
            $"{baby.Id.Value}|{mother.Id.Value}|{father?.Id.Value ?? 0}|genetic|{string.Join(',', expressed)}",
            sourceSystem: "NatalitySystem", causeEventId: causeEventId);
        foreach (var locus in layout)
            if (expressed.Contains(locus.PowerId))
                ScheduleOnset(world, ctx, baby, locus, inherited);
    }

    /// <summary>Início no futuro vira evento agendado; início já passado manifesta agora.</summary>
    public static void ScheduleOnset(WorldState world, TickContext ctx, Npc npc, PowerLocus locus, long? causeEventId)
    {
        long onset = OnsetTick(world, npc, locus);
        if (onset > ctx.CurrentTick)
            ctx.ScheduleEvent(onset, ExtraordinaryStateSystem.SystemName, $"{OnsetPrefix}|{npc.Id.Value}|{locus.PowerId}");
        else
            ExtraordinaryStateSystem.AcquireGenetic(world, ctx, npc, locus.PowerId, causeEventId);
    }

    public static double HungerMultiplier(WorldState world, Npc npc) => Product(world, npc, static g => g.HungerMultiplier);
    public static double ThirstMultiplier(WorldState world, Npc npc) => Product(world, npc, static g => g.ThirstMultiplier);
    public static double FertilityMultiplier(WorldState world, Npc npc) => Product(world, npc, static g => g.FertilityMultiplier);
    public static double HazardMultiplier(WorldState world, Npc npc) => Product(world, npc, static g => g.HazardMultiplier);

    /// <summary>Produto do efeito de cada poder que a pessoa tem. Sem poderes no mundo, ou sem poder, exatamente 1.</summary>
    private static double Product(WorldState world, Npc npc, Func<PowerGenetics, double> effect)
    {
        if (!world.Extraordinary.Enabled || world.CarrierOf(npc.Id) is not { PowerIds.Count: > 0 } carrier) return 1.0;
        var rules = PowerInheritanceRules.Resolve(world.Extraordinary.InheritanceRules);
        double m = 1.0;
        foreach (string id in carrier.PowerIds) m *= effect(rules.For(id));
        return m;
    }

    /// <summary>Recalcula o gasto de fome e sede depois que os poderes da pessoa mudam (a fome da mulher passa pela lactação).</summary>
    public static void RefreshMetabolism(WorldState world, Npc npc, long tick)
    {
        if (!npc.IsAlive) return;
        if (npc.Sex == Sex.Female) LivingWorld.Simulation.Health.Lactation.RefreshHungerCost(world, npc, tick);
        else npc.SetHungerDecayRate(world.NeedsRules.HungerDecayPerHour * HungerMultiplier(world, npc), tick);
        npc.SetThirstDecayRate(world.NeedsRules.ThirstDecayPerHour * ThirstMultiplier(world, npc), tick);
    }
}
