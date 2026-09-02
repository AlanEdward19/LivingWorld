using LivingWorld.Domain.Cities;
using LivingWorld.Simulation.Cities.Founding;
using LivingWorld.Simulation.Cities.Queries;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Tests.Integration.Cities;

/// <summary>Fase 8, T20 (CITY-08): fundação com gatilho já satisfeito, rodada como cenário vivo
/// (via <see cref="WorldClock"/>, não uma chamada direta a <c>Tick</c>/<c>HandleEvent</c> como em
/// <see cref="SettlementFoundingSystemTests"/>/T13) — funda em <c>≤ OrganizationTicks</c> e
/// preserva a soma de população no split.
///
/// SPEC_DEVIATION herdado de T13 (ver comentário em SettlementFoundingSystem.cs): dos 5
/// limiares do roadmap (concentração, recurso, rota, defensabilidade, liderança), só
/// concentração tem dado real hoje — os outros 4 ficam vacuamente satisfeitos (nível fixo 1.0)
/// porque nenhum sistema de mapa/recurso/liderança de assentamento existe ainda em Foundation.
/// Rodar "todos os limiares batidos" sem um braço de controle discriminaria nada (R1 de
/// rules/eval-criteria.md: um teste que passa não importa o que os 4 limiares vacuosamente
/// satisfeitos valham não prova nada sobre eles). Por isso este arquivo roda um par: (a) limiar
/// de concentração batido no tick 0 → funda dentro de OrganizationTicks; (b) exatamente a mesma
/// cena, só o limiar de concentração inalcançável pela população existente → nunca funda no
/// mesmo horizonte. Isso prova o único sinal real (concentração) discrimina de verdade — os
/// outros 4 permanecem documentados como não-discrimináveis nesta fase, não fingidos.</summary>
public class SettlementFoundingScenarioTests
{
    private const long OrganizationTicks = 240; // 10 dias
    // 1a checagem mensal (720h) + OrganizationTicks + folga pequena — para exatamente depois da
    // primeira fundação disparar, antes da 2a checagem mensal (1440h). A cidade nova herda o
    // pool inteiro da mãe e por isso volta a satisfazer o mesmo limiar de concentração
    // imediatamente (SPEC_DEVIATION da classe) — um horizonte maior refundaria em cascata
    // (observado rodando o teste: 4 cidades em 3 checagens mensais), o que testaria uma
    // propriedade diferente (refundação encadeada) fora do Done-when de T20 ("funda em <= K
    // ticks, nunca antes do limiar bater" — não "quantas vezes funda num horizonte longo").
    private const long HorizonHours = 720 + OrganizationTicks + 10;

    private static CityRules MakeRules(double foundingConcentrationThreshold) => CityRules.Create(
        enabled: true, foodShortageThreshold: 100, housingShortageThreshold: 100, securityShortageThreshold: 100,
        emigrationRatePerDeficitUnit: 0, migrationEmploymentWeight: 0, migrationFoodWeight: 0,
        migrationSecurityWeight: 0, migrationFamilyTiesWeight: 0, foundingConcentrationThreshold,
        // Vacuamente satisfeitos por design atual (ver SPEC_DEVIATION da classe) — 0 é o mínimo
        // válido do range [0,1] de CityRules.Create, nunca cruzado por escolha, sempre satisfeito
        // por unmeasuredLevel=1.0 fixo no sistema.
        foundingResourceThreshold: 0, foundingRouteThreshold: 0, foundingDefensibilityThreshold: 0,
        foundingLeadershipThreshold: 0, organizationTicks: OrganizationTicks, materializationIdleTicksBeforeEligible: 1)
        .Value!;

    private static (WorldState World, City City) BuildScenario(double foundingConcentrationThreshold)
    {
        var rules = MakeRules(foundingConcentrationThreshold);
        var world = new WorldState(
            ScenarioRunner.DefaultCalendar, seed: 20, ScenarioRunner.DefaultMap(20),
            ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
            ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog, ScenarioRunner.DefaultLifeStageRules,
            cityRules: rules);

        var city = new City(
            world.NextCityId(), ScenarioRunner.DefaultVillageLocation, foundedAtTick: 0, foundedFromCityId: null,
            aggregatePool: new AggregatePopulationPool(50, 500, 400)); // concentração = 50/51 ~= 0.980
        world.AddCity(city);

        return (world, city);
    }

    private static long TotalPopulation(WorldState world) =>
        world.Cities.Sum(c => CityPopulationQuery.Population(world, c.Id));

    [Fact]
    public void Founding_fires_within_organization_ticks_and_preserves_total_population_when_concentration_threshold_is_met()
    {
        var (world, city) = BuildScenario(foundingConcentrationThreshold: 0.5); // 0.980 >= 0.5, satisfeito
        long populationBefore = TotalPopulation(world);
        var clock = new WorldClock([new SettlementFoundingSystem()]);

        clock.Run(world, HorizonHours);

        Assert.Equal(2, world.Cities.Count);
        var newCity = world.Cities.Single(c => c.Id != city.Id);
        Assert.Equal(city.Id, newCity.FoundedFromCityId);
        Assert.True(newCity.FoundedAtTick <= OrganizationTicks + 720,
            $"cidade fundada em {newCity.FoundedAtTick}, deveria ser <= OrganizationTicks (720 do 1o mes + {OrganizationTicks})");
        Assert.Equal(populationBefore, TotalPopulation(world));
    }

    [Fact]
    public void Founding_never_fires_in_the_same_horizon_when_concentration_threshold_is_unreachable()
    {
        // Mesma cena, só o limiar de concentracao muda para acima do que a populacao existente
        // alcanca (0.980 < 0.999) — controle da unica variavel real (R1: prova a transicao
        // rejeitada, nao so a aceita).
        var (world, city) = BuildScenario(foundingConcentrationThreshold: 0.999);
        long populationBefore = TotalPopulation(world);
        var clock = new WorldClock([new SettlementFoundingSystem()]);

        clock.Run(world, HorizonHours);

        Assert.Single(world.Cities);
        Assert.Null(world.FindCity(city.Id)!.FoundingScheduledAtTick);
        Assert.Equal(populationBefore, TotalPopulation(world));
    }
}
