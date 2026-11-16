using LivingWorld.Domain.Cognition;

namespace LivingWorld.Simulation.Observation;

/// <summary>Sonda do cérebro (AD-160): em cada decisão urgente com o cérebro ligado, a escolha com os ganhos do NPC contra a
/// escolha da utilidade pura no mesmo estado. Só a sonda <c>brain-trace</c> liga (<see cref="Diagnose"/>); nenhum sistema decide por
/// ela e o hash não a vê. Uma por thread: cada mundo roda numa thread só.</summary>
public sealed class BrainDecisionTally
{
    public static bool Diagnose;

    [ThreadStatic] private static BrainDecisionTally? _current;

    public static BrainDecisionTally Current => _current ??= new BrainDecisionTally();

    public static void Reset() => _current = new BrainDecisionTally();

    /// <summary>O cenário padrão com o cérebro ligado nas regras dadas (a sonda vive fora do assembly da simulação).</summary>
    public static (Core.WorldState World, Core.WorldClock Clock) CreateWorld(ulong seed, BrainRules rules)
    {
        var (world, clock) = Scenarios.ScenarioRunner.Create(seed);
        world.UseBrain(rules);
        return (world, clock);
    }

    /// <summary>Linhas: escolha da utilidade pura; colunas: escolha com ganhos (índice de <c>ActionType</c>).</summary>
    public long[,] Matrix { get; } = new long[BrainState.Actions + 6, BrainState.Actions + 6];

    public long Decisions, Changed;

    /// <summary>Fome ou sede crítica (déficit ≥ 80): quantas decisões, quantas o cérebro trocou e quantas trocou para fora de
    /// comer, beber ou comprar.</summary>
    public long Critical, CriticalChanged, CriticalAwayFromSurvival;

    /// <summary>Margem: |nota com ganho − nota pura| da vencedora pura, somada (para a média).</summary>
    public double MarginSum;

    public List<string> Samples { get; } = [];

    public int MaxSamples { get; set; } = 40;
}
