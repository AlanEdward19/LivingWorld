using LivingWorld.Domain;

namespace LivingWorld.Simulation;

// SPEC_DEVIATION (Fase 8, T13): design.md lista 5 limiares de fundação (concentração, recurso,
// rota, defensabilidade, liderança), mas nenhum sistema de Foundation produz dado real pra
// recurso/rota/defensabilidade/liderança (nenhum mapa de recurso natural ligado a cidade, nenhuma
// rota, nenhum conceito de defensabilidade ou liderança formal de assentamento). Só concentração é
// mensurável (população da cidade). Os outros 4 ficam sempre satisfeitos (nível 1.0 >= qualquer
// threshold em [0,1], mesma faixa validada por CityRules.Create) — vacuamente verdadeiros, mesmo
// espírito do nível neutro de segurança em CityGrowthSystem/MigrationSystem (T11/T12), prontos
// pra quando um sinal real existir. ConcentrationLevel = population / (population + 1): função
// monotônica pura da população, sem novo limiar mágico — o limiar real continua só
// FoundingConcentrationThreshold (CityRules).

/// <summary>Checa limiares de fundação de assentamento mensalmente (Fase 8, T13, CITY-08); ao
/// bater todos, agenda um evento único em <c>now + CityRules.OrganizationTicks</c> (mesmo padrão
/// de <see cref="MortalitySystem.SchedulePlannedDeath"/>); ao disparar, funda uma <see
/// cref="City"/> nova e move o <see cref="AggregatePopulationPool"/> inteiro da cidade-mãe pra
/// ela, preservando a soma de população.</summary>
public sealed class SettlementFoundingSystem : ISimulationSystem
{
    public const string SystemName = "cities-settlement-founding";

    public string Name => SystemName;
    public TickFrequency Frequency => TickFrequency.Monthly;

    public void Tick(WorldState world, TickContext ctx)
    {
        if (!world.CityRules.Enabled) return;
        var rules = world.CityRules;

        foreach (var city in world.Cities)
        {
            if (city.FoundingScheduledAtTick is not null) continue; // já agendado, não reagenda
            if (!AllThresholdsMet(world, rules, city)) continue;

            ctx.ScheduleEvent(ctx.CurrentTick + rules.OrganizationTicks, SystemName, city.Id.Value.ToString());
            city.MarkFoundingScheduled(ctx.CurrentTick);
        }
    }

    public void HandleEvent(WorldState world, TickContext ctx, ScheduledEvent evt)
    {
        var motherCityId = new CityId(Guid.Parse(evt.Payload!));
        var motherCity = world.FindCity(motherCityId);
        if (motherCity is null) return; // referência perdida — sem-op, não exceção

        var (extractedPool, extractedPoolNpcIds) = motherCity.ExtractEntirePool();
        var newCity = new City(
            world.NextCityId(), motherCity.Location, ctx.CurrentTick, motherCityId, extractedPool,
            name: CityNameGenerator.Generate(world), poolNpcIds: extractedPoolNpcIds);
        world.AddCity(newCity);
    }

    private static bool AllThresholdsMet(WorldState world, CityRules rules, City city)
    {
        double concentration = ConcentrationLevel(world, city.Id);
        const double unmeasuredLevel = 1.0; // ver SPEC_DEVIATION acima

        return concentration >= rules.FoundingConcentrationThreshold
            && unmeasuredLevel >= rules.FoundingResourceThreshold
            && unmeasuredLevel >= rules.FoundingRouteThreshold
            && unmeasuredLevel >= rules.FoundingDefensibilityThreshold
            && unmeasuredLevel >= rules.FoundingLeadershipThreshold;
    }

    private static double ConcentrationLevel(WorldState world, CityId cityId)
    {
        long population = CityPopulationQuery.Population(world, cityId);
        return population / (population + 1.0);
    }
}
