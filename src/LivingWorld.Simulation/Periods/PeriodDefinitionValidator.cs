using LivingWorld.Domain;

namespace LivingWorld.Simulation.Periods;

/// <summary>Definição completa de período validada (Fase 13, T2): agrega o resultado de todos os
/// loaders de cenário existentes + <see cref="PeriodDynamicsData"/>. É o que
/// <c>ScenarioLoaderV2</c> (T3) e a rota de cadastro (T5) consomem — nunca constrói mundo
/// parcial: ou tudo valida, ou <see cref="PeriodDefinitionValidator.Validate"/> falha.</summary>
public sealed record PeriodDefinition(
    WorldMap Map,
    PopulationScenarioData Population,
    BehaviorScenarioData Behavior,
    EconomyScenarioData Economy,
    CityScenarioData City,
    PeriodDynamicsData Dynamics);

/// <summary>Orquestra a validação de um <c>periodDefinition</c> (Fase 13, T2): encadeia
/// <see cref="MapScenarioLoader"/>, <see cref="PopulationScenarioLoader"/>,
/// <see cref="BehaviorScenarioLoader"/>, <see cref="EconomyScenarioLoader"/>,
/// <see cref="CityScenarioLoader"/> e <see cref="PeriodDynamicsLoader"/> — cada um já valida sua
/// própria forma — e então checa referências cruzadas entre <see cref="PeriodDynamicsData"/> e o
/// <see cref="PopulationCatalog"/> resolvido (PERIOD-07..10: id de profissão citado numa regra ou
/// viés precisa existir no catálogo do período, senão erro determinístico nomeia o campo).
/// Falha em qualquer etapa interrompe a cadeia — nunca produz uma <see cref="PeriodDefinition"/>
/// parcial.</summary>
public static class PeriodDefinitionValidator
{
    public static Result<PeriodDefinition> Validate(string json)
    {
        var mapResult = MapScenarioLoader.Load(json);
        if (!mapResult.IsSuccess)
            return Result<PeriodDefinition>.Fail(mapResult.Error!);

        var populationResult = PopulationScenarioLoader.Load(json);
        if (!populationResult.IsSuccess)
            return Result<PeriodDefinition>.Fail(populationResult.Error!);

        var behaviorResult = BehaviorScenarioLoader.Load(json);
        if (!behaviorResult.IsSuccess)
            return Result<PeriodDefinition>.Fail(behaviorResult.Error!);

        var economyResult = EconomyScenarioLoader.Load(json);
        if (!economyResult.IsSuccess)
            return Result<PeriodDefinition>.Fail(economyResult.Error!);

        var cityResult = CityScenarioLoader.Load(json);
        if (!cityResult.IsSuccess)
            return Result<PeriodDefinition>.Fail(cityResult.Error!);

        var dynamicsResult = PeriodDynamicsLoader.Load(json);
        if (!dynamicsResult.IsSuccess)
            return Result<PeriodDefinition>.Fail(dynamicsResult.Error!);

        var population = populationResult.Value!;
        var dynamics = dynamicsResult.Value!;

        var referenceError = ValidateProfessionReferences(population.Catalog, dynamics);
        if (referenceError is not null)
            return Result<PeriodDefinition>.Fail(referenceError);

        return Result<PeriodDefinition>.Ok(new PeriodDefinition(
            mapResult.Value!, population, behaviorResult.Value!, economyResult.Value!, cityResult.Value!, dynamics));
    }

    private static string? ValidateProfessionReferences(PopulationCatalog catalog, PeriodDynamicsData dynamics)
    {
        foreach (var bias in dynamics.ProfessionBiases)
            if (!catalog.IsValidProfession(new ProfessionType(bias.ProfessionId)))
                return $"Dynamics.ProfessionBiases[]: ProfessionId {bias.ProfessionId} não existe em ProfessionIds";

        foreach (var rule in dynamics.TransformationRules)
            foreach (var professionId in rule.SourceProfessionIds.Concat(rule.TargetProfessionIds))
                if (!catalog.IsValidProfession(new ProfessionType(professionId)))
                    return $"Dynamics.TransformationRules[]: ProfessionId {professionId} não existe em ProfessionIds";

        return null;
    }
}
