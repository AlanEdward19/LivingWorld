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
    PeriodDynamicsData Dynamics,
    PeriodDescriptors Descriptors,
    ResourceCatalog ResourceCatalog,
    IReadOnlyList<ProcessRecipe> ProcessRecipes,
    ExtraordinaryScenarioData Extraordinary);

/// <summary>Orquestra a validação de um <c>periodDefinition</c> (Fase 13, T2): encadeia
/// <see cref="MapScenarioLoader"/>, <see cref="PopulationScenarioLoader"/>,
/// <see cref="BehaviorScenarioLoader"/>, <see cref="EconomyScenarioLoader"/>,
/// <see cref="CityScenarioLoader"/>, <see cref="PeriodDynamicsLoader"/> e <see
/// cref="PeriodDescriptorsLoader"/> — cada um já valida sua própria forma — e então checa
/// referências cruzadas entre <see cref="PeriodDynamicsData"/> e o
/// <see cref="PopulationCatalog"/> resolvido (PERIOD-07..10: id de profissão citado num viés, ou
/// toda <c>SourceProfessionIds</c> de uma regra de transformação, precisa existir no catálogo do
/// período — <c>TargetProfessionIds</c> fica de fora dessa checagem de propósito, Fase 13 T13:
/// o alvo de Emerge/Merge/Split pode ser justamente uma profissão que ainda não existe).
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

        var cityResult = CityScenarioLoader.Load(json, mapResult.Value!.Width, mapResult.Value!.Height);
        if (!cityResult.IsSuccess)
            return Result<PeriodDefinition>.Fail(cityResult.Error!);

        var dynamicsResult = PeriodDynamicsLoader.Load(json);
        if (!dynamicsResult.IsSuccess)
            return Result<PeriodDefinition>.Fail(dynamicsResult.Error!);

        var descriptorsResult = PeriodDescriptorsLoader.Load(json);
        if (!descriptorsResult.IsSuccess)
            return Result<PeriodDefinition>.Fail(descriptorsResult.Error!);

        var resourcesResult = ResourceProcessCatalogLoader.Load(json);
        if (!resourcesResult.IsSuccess)
            return Result<PeriodDefinition>.Fail(resourcesResult.Error!);

        var extraordinaryResult = ExtraordinaryScenarioLoader.Load(json);
        if (!extraordinaryResult.IsSuccess)
            return Result<PeriodDefinition>.Fail(extraordinaryResult.Error!);

        var population = populationResult.Value!;
        var dynamics = dynamicsResult.Value!;

        var referenceError = ValidateProfessionReferences(population.Catalog, dynamics);
        if (referenceError is not null)
            return Result<PeriodDefinition>.Fail(referenceError);

        return Result<PeriodDefinition>.Ok(new PeriodDefinition(
            mapResult.Value!, population, behaviorResult.Value!, economyResult.Value!, cityResult.Value!, dynamics,
            descriptorsResult.Value!, resourcesResult.Value!.Catalog, resourcesResult.Value!.Recipes,
            extraordinaryResult.Value!));
    }

    private static string? ValidateProfessionReferences(PopulationCatalog catalog, PeriodDynamicsData dynamics)
    {
        foreach (var bias in dynamics.ProfessionBiases)
            if (!catalog.IsValidProfession(new ProfessionType(bias.ProfessionId)))
                return $"Dynamics.ProfessionBiases[]: ProfessionId {bias.ProfessionId} não existe em ProfessionIds";

        // Fase 13, T13: só SourceProfessionIds precisa já existir no catálogo — o destino de
        // Emerge/Merge/Split é o ponto de virar profissão nova (ou reaproveitar uma já existente),
        // exigir que já exista tornaria "Emerge" sem sentido (o alvo já estaria disponível pro
        // sorteio antes da regra disparar).
        foreach (var rule in dynamics.TransformationRules)
            foreach (var professionId in rule.SourceProfessionIds)
                if (!catalog.IsValidProfession(new ProfessionType(professionId)))
                    return $"Dynamics.TransformationRules[]: ProfessionId {professionId} não existe em ProfessionIds";

        return null;
    }
}
