using LivingWorld.Domain.Economy;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Simulation.Serialization;

namespace LivingWorld.Tests.LongRunning.Population;

/// <summary>Corrida compartilhada (ADR-0044): a vila padrão da seed 42 (<c>ScenarioRunner.Create(42)</c>, sistemas padrão),
/// simulada uma vez por processo, hora a hora nos 10 primeiros anos e depois em passos de 5 anos até o ano 60 (100 se a
/// tabela de vida ainda não chegou ao teto). Cada leitor antes rodava o seu prefixo do mesmo mundo; agora a corrida faz a
/// mesma observação, na mesma cadência, e guarda só o resultado (mensagens, contagens, cópias imutáveis), nunca o mundo.
/// Leitores: invariantes de <see cref="PopulationScenarioTests"/> (1 e 10 anos), <see cref="EveryNpcHasACityTests"/>,
/// <see cref="Serialization.ReferentialIntegritySweepTests"/> (1 e 10 anos), o braço com natalidade de 1 ano,
/// <see cref="Economy.TradeLedgerLongRunTests"/>, <see cref="LifeTable100YearScenarioTests"/> e
/// <see cref="VillageSurvivesGenerationsTests"/> (seed 42) e, no ano 10, os quatro testes da seed 42 de
/// <see cref="FamilyPairedScenarioTests"/> (Century). Equivalência: <see cref="DefaultVillageSeed42Tests"/>.</summary>
public static class DefaultVillageSeed42
{
    public const ulong Seed = 42;
    public const long Year = 12 * 30 * 24;
    public const int GenerationsYears = 60;
    private const int LifeTableHorizonYears = 100, LifeTableStepYears = 5;

    public sealed record Observed(
        string? InvariantFailureYear1, string? InvariantFailureYear10, string? CityFailureYear1,
        IReadOnlyList<string> ReferentialYear1, IReadOnlyList<string> ReferentialYear10,
        string HashYear1, string HashYear10,
        long LedgerMaxCount, int LedgerMarkets, IReadOnlyList<TradeAggregate> LedgerYear3,
        string? FirstDegreeMarriageFailure, string? BirthParentsFailure, int NpcBirthsYear10);

    /// <summary>Do ano 10 em diante: a tabela de vida e o ano 60.</summary>
    public sealed record ObservedLong(int MaxAgeReached, int LifeTableThreshold, int AliveYear60, int ChildrenYear60, int EmployedYear60);

    /// <summary>Duas etapas: quem só lê os 10 primeiros anos não espera os outros 50. O mundo fica aqui dentro entre as
    /// etapas (a segunda continua a primeira) e nunca sai para um teste.</summary>
    private sealed record Decade(Observed Observed, WorldState World, WorldClock Clock);

    private static readonly Lazy<Decade> First = new(Simulate);
    private static readonly Lazy<ObservedLong> Rest = new(() => Continue(First.Value));

    /// <summary>Observações dos anos 0 a 10.</summary>
    public static Observed Value => First.Value.Observed;

    /// <summary>Observações do ano 10 ao 60 (até 100 para a tabela de vida).</summary>
    public static ObservedLong Long => Rest.Value;

    private static Decade Simulate()
    {
        var (world, _) = ScenarioRunner.Create(seed: Seed);
        // Casamentos e nascimentos dos 10 primeiros anos, para os testes de FamilyPairedScenarioTests (só observa).
        var events = new FamilyPairedScenarioTests.RecordingSink { OnlyMarriagesAndBirths = true };
        var clock = new WorldClock(ScenarioRunner.DefaultSystems(), sink: events);
        string? invariant1 = null, invariant10 = null, city1 = null;
        IReadOnlyList<string> referential1 = [];
        string hash1 = "";
        long ledgerMax = 0;
        int markets = 0;
        IReadOnlyList<TradeAggregate> ledger3 = [];
        for (long tick = 1; tick <= 10 * Year; tick++)
        {
            clock.Tick(world);
            // PopulationScenarioTests: amostra por dia e no último tick do horizonte (1 ano e 10 anos caem em fim de dia).
            if (tick % PopulationScenarioTests.SampleEveryHours == 0 && invariant10 is null)
            {
                invariant10 = PopulationScenarioTests.InvariantFailure(world, tick);
                if (tick <= Year) invariant1 = invariant10;
            }
            if (tick <= Year && tick % 24 == 0 && city1 is null) city1 = EveryNpcHasACityTests.CityFailure(world, tick);
            if (tick % Year != 0) continue;
            long year = tick / Year;
            if (year <= 3) ledgerMax = Math.Max(ledgerMax, world.TradeLedger.Count);
            if (year == 1)
            {
                referential1 = ReferentialIntegritySweep.Check(world);
                hash1 = WorldSnapshot.CanonicalHash(world);
            }
            if (year == 3)
            {
                markets = world.Workplaces.Count(w => world.EconomyCatalog.MarketLocationTypeIds.Contains(w.LocationType.Id));
                ledger3 = [.. world.TradeLedger];
            }
        }
        var referential10 = ReferentialIntegritySweep.Check(world);
        string hash10 = WorldSnapshot.CanonicalHash(world);
        events.Recording = false;
        string? marriages = Failure(() => FamilyPairedScenarioTests.AssertNoFirstDegreeMarriages(world, events));
        string? births = Failure(() => FamilyPairedScenarioTests.AssertBirthParents(world, events));
        int npcBirths = events.Events.Count(FamilyPairedScenarioTests.IsNpcBirth);
        return new Decade(new Observed(invariant1, invariant10, city1, referential1, referential10, hash1, hash10, ledgerMax, markets, ledger3,
            marriages, births, npcBirths), world, clock);
    }

    /// <summary>A checagem de um teste como valor observado: nulo se passa, senão a mensagem da falha.</summary>
    private static string? Failure(Action check)
    {
        try { check(); return null; }
        catch (Exception ex) { return $"{ex.GetType().Name}: {ex.Message}"; }
    }

    private static ObservedLong Continue(Decade decade)
    {
        var (world, clock) = (decade.World, decade.Clock);
        int threshold = (int)(ScenarioRunner.DefaultPopulationRules.LifeTable.MaxLongevityYears * 0.9);
        int maxAge = world.Npcs.Max(n => n.AgeYears(world.CurrentDate)); // ano 10: amostras dos anos 5 e 10 do teste antigo
        int alive60 = 0, children60 = 0, employed60 = 0;
        for (int year = 10; year < LifeTableHorizonYears && (year < GenerationsYears || maxAge < threshold); year += LifeTableStepYears)
        {
            clock.Run(world, LifeTableStepYears * Year);
            if (maxAge < threshold) maxAge = world.Npcs.Max(n => n.AgeYears(world.CurrentDate));
            if (year + LifeTableStepYears != GenerationsYears) continue;
            var alive = world.Npcs.Where(n => n.IsAlive).ToList();
            alive60 = alive.Count;
            children60 = alive.Count(n => n.AgeYears(world.CurrentDate) < 15);
            employed60 = alive.Count(n => n.Employer is not null);
        }
        return new ObservedLong(maxAge, threshold, alive60, children60, employed60);
    }
}

/// <summary>A corrida compartilhada é o mesmo mundo da corrida isolada (ADR-0044): mesmo hash canônico no ano 1, depois de um
/// ano inteiro com todas as observações por hora (como <see cref="Population.DefaultVillageCenturyTests"/>, que compara um prefixo).</summary>
public class DefaultVillageSeed42Tests
{
    [Fact, Trait("Category", "Scenario")]
    public void Shared_run_matches_an_isolated_run_of_seed_42_after_one_year()
    {
        var (world, clock) = ScenarioRunner.Create(seed: DefaultVillageSeed42.Seed);
        clock.Run(world, DefaultVillageSeed42.Year);

        Assert.Equal(WorldSnapshot.CanonicalHash(world), DefaultVillageSeed42.Value.HashYear1);
    }
}
