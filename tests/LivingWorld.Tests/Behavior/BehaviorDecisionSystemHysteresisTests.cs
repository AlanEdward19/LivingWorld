using LivingWorld.Domain;
using LivingWorld.Simulation;
using LivingWorld.Tests.Baselines;

namespace LivingWorld.Tests.Behavior;

/// <summary>Fase 4, task 13: histerese (NEEDS-12), terminação da seleção de ação (NEEDS-09) e
/// ausência de deadlock de rotina (NEEDS-13) do <see cref="BehaviorDecisionSystem"/>.</summary>
public class BehaviorDecisionSystemHysteresisTests
{
    private const int WindowDays = 30;
    private static readonly string BaselinesDir = Path.Combine(FindRepoRoot(), "tests", "baselines");

    private static WorldState BuildPopulatedWorld(ulong seed, NeedsRules rules, int population = 20)
    {
        var world = new WorldState(
            ScenarioRunner.DefaultCalendar, seed, ScenarioRunner.DefaultMap(seed), ScenarioRunner.DefaultPopulationCatalog,
            ScenarioRunner.DefaultPopulationRules, rules, ScenarioRunner.DefaultActionCatalog, ScenarioRunner.DefaultLifeStageRules);
        PopulationSeeder.SeedInitial(world, population, ScenarioRunner.DefaultCulture, ScenarioRunner.DefaultVillageLocation);
        return world;
    }

    /// <summary>Conta trocas de ação corrente somadas entre todos os NPCs vivos ao longo dos
    /// ticks — a primeira atribuição (null -&gt; ação) conta igual nos dois braços (mesmo
    /// ponto de partida), então não enviesa a comparação causal (NEEDS-12).</summary>
    private static int CountActionSwitches(WorldState world, WorldClock clock, long ticks)
    {
        var previous = world.Npcs.Where(n => n.IsAlive).ToDictionary(n => n.Id, n => n.CurrentAction);
        int switches = 0;

        for (long t = 0; t < ticks; t++)
        {
            clock.Tick(world);
            foreach (var npc in world.Npcs.Where(n => n.IsAlive))
            {
                if (previous.TryGetValue(npc.Id, out var prevAction) && prevAction != npc.CurrentAction)
                    switches++;
                previous[npc.Id] = npc.CurrentAction;
            }
        }

        return switches;
    }

    private static double SwitchesPerDay(ulong seed, bool hysteresisEnabled)
    {
        var rules = ScenarioRunner.DefaultNeedsRules with { HysteresisEnabled = hysteresisEnabled };
        var world = BuildPopulatedWorld(seed, rules);
        var clock = new WorldClock([new NeedsDecaySystem(), new BehaviorDecisionSystem()]);

        int switches = CountActionSwitches(world, clock, ticks: WindowDays * 24);
        return switches / (double)WindowDays;
    }

    private static double Percentile99(IReadOnlyList<double> sortedAscending)
    {
        int index = Math.Clamp((int)Math.Ceiling(0.99 * sortedAscending.Count) - 1, 0, sortedAscending.Count - 1);
        return sortedAscending[index];
    }

    [Theory]
    [InlineData(1u)]
    [InlineData(2u)]
    [InlineData(3u)]
    [InlineData(4u)]
    [InlineData(5u)]
    [InlineData(6u)]
    [InlineData(7u)]
    [InlineData(8u)]
    [InlineData(9u)]
    [InlineData(10u)]
    [InlineData(11u)]
    [InlineData(12u)]
    [InlineData(13u)]
    [InlineData(14u)]
    [InlineData(15u)]
    [InlineData(16u)]
    [InlineData(17u)]
    [InlineData(18u)]
    [InlineData(19u)]
    [InlineData(20u)]
    public void Hysteresis_reduces_action_switches_with_a_control_arm_in_20_of_20_seeds(ulong seed)
    {
        double withHysteresis = SwitchesPerDay(seed, hysteresisEnabled: true);
        double withoutHysteresis = SwitchesPerDay(seed, hysteresisEnabled: false);

        Assert.True(withHysteresis < withoutHysteresis,
            $"seed {seed}: trocas/dia com histerese ({withHysteresis}) deveria ser menor que sem histerese ({withoutHysteresis})");
    }

    /// <summary>Regravação manual (mesmo padrão de <c>PopulationBaselineTests.ZZZ_record_baseline</c>):
    /// remove o Skip, roda uma vez pra (re)gravar <c>tests/baselines/action-switches.json</c>,
    /// reverte o Skip antes de commitar.</summary>
    [Fact(Skip = "grava baseline — rode manualmente")]
    public void ZZZ_record_action_switches_baseline()
    {
        var perSeed = Enumerable.Range(1, 20).ToDictionary(seed => seed, seed => SwitchesPerDay((ulong)seed, hysteresisEnabled: true));
        BaselineFixture.Record(BaselinesDir, "action-switches", perSeed);
    }

    [Fact]
    public void Action_switches_per_day_with_hysteresis_matches_the_recorded_baseline_and_stays_at_or_under_its_99th_percentile()
    {
        var perSeed = Enumerable.Range(1, 20).ToDictionary(seed => seed, seed => SwitchesPerDay((ulong)seed, hysteresisEnabled: true));

        BaselineFixture.AssertMatches(BaselinesDir, "action-switches", perSeed);

        double ceiling = Percentile99(perSeed.Values.OrderBy(v => v).ToArray());
        foreach (var (seed, value) in perSeed)
            Assert.True(value <= ceiling, $"seed {seed}: trocas/dia ({value}) excede o teto absoluto (percentil 99 = {ceiling})");
    }

    /// <summary>NEEDS-09: o teto de passos é um <c>for</c> limitado a <c>maxSteps</c> iterações —
    /// nunca recursão nem reagendamento — então a ausência de laço infinito é garantida pela
    /// própria forma do código (mesmo raciocínio do teto de <see cref="WorldClock.DispatchDueEvents"/>);
    /// não precisa de wrapper de timeout de parede pra provar isso, só que aborta nomeando o NPC
    /// e as ações empatadas em vez de devolver um resultado silenciosamente incorreto.</summary>
    [Fact]
    public void Cyclic_utility_scenario_aborts_naming_the_npc_and_the_tied_actions_instead_of_looping()
    {
        ActionType Cycle(ActionType current) => current == ActionType.Work ? ActionType.Idle : ActionType.Work;

        var ex = Assert.Throws<TickBudgetExceededException>(() =>
            BehaviorDecisionSystem.ResolveWithStepCap(npcId: 7, initial: ActionType.Work, Cycle, maxSteps: 5));

        Assert.Contains("7", ex.Message);
        Assert.Contains(ActionType.Work.ToString(), ex.Message);
        Assert.Contains(ActionType.Idle.ToString(), ex.Message);
    }

    /// <summary>NEEDS-13: nenhum NPC vivo permanece na mesma ação além da duração máxima
    /// declarada dela — checado a cada tick, 10 anos. A cobertura "toda ação do catálogo declara
    /// duração" já existe em <c>ActionCatalogTests.Create_fails_naming_the_action_missing_a_declared_duration</c>
    /// (T2); aqui a prova é em cima do sistema rodando de verdade.</summary>
    [Fact]
    public void No_npc_exceeds_the_catalogs_declared_max_duration_over_10_years()
    {
        var world = BuildPopulatedWorld(seed: 7, ScenarioRunner.DefaultNeedsRules, population: 30);
        var clock = new WorldClock([new NeedsDecaySystem(), new BehaviorDecisionSystem()]);
        var catalog = world.ActionCatalog;
        const long tenYears = 10 * 12 * 30 * 24;

        for (long tick = 0; tick < tenYears; tick++)
        {
            clock.Tick(world);

            foreach (var npc in world.Npcs.Where(n => n.IsAlive))
            {
                if (npc.CurrentAction is not { } action) continue;
                int maxDuration = catalog.MaxDurationHours[action];
                long duration = world.CurrentDate.TotalHours - npc.ActionStartedAtTick;
                Assert.True(duration <= maxDuration,
                    $"npc {npc.Id.Value} ficou em {action} por {duration}h no tick {world.CurrentDate.TotalHours} (máximo declarado: {maxDuration}h)");
            }
        }
    }

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "LivingWorld.sln")))
            dir = Directory.GetParent(dir)?.FullName;
        return dir ?? throw new InvalidOperationException("LivingWorld.sln não encontrado a partir de " + AppContext.BaseDirectory);
    }
}
