using LivingWorld.Domain.Economy;
using LivingWorld.Domain.Extraordinary;
using LivingWorld.Domain.Geography.Map;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Behavior;
using LivingWorld.Simulation.Cities;
using LivingWorld.Simulation.Economy;
using LivingWorld.Simulation.Economy.Production;
using LivingWorld.Simulation.Extraordinary.Culture;
using LivingWorld.Simulation.Geography;
using LivingWorld.Simulation.Population;

namespace LivingWorld.Simulation.Periods;

/// <summary>Definição completa de período validada: agrega o resultado de todos os
/// loaders de cenário existentes + <see cref="PeriodDynamicsData"/>. É o que
/// <c>ScenarioLoaderV2</c> e a rota de cadastro consomem — nunca constrói mundo
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
    ExtraordinaryScenarioData Extraordinary,
    LivingWorld.Domain.Emergence.EmergenceRules Emergence,
    LivingWorld.Domain.Ontogeny.OntogenyRules Ontogeny,
    LivingWorld.Domain.Population.AppearanceRules? Appearance = null);

/// <summary>Orquestra a validação de um <c>periodDefinition</c>: encadeia
/// <see cref="MapScenarioLoader"/>, <see cref="PopulationScenarioLoader"/>,
/// <see cref="BehaviorScenarioLoader"/>, <see cref="EconomyScenarioLoader"/>,
/// <see cref="CityScenarioLoader"/>, <see cref="PeriodDynamicsLoader"/> e <see
/// cref="PeriodDescriptorsLoader"/> — cada um já valida sua própria forma — e então checa
/// referências cruzadas entre <see cref="PeriodDynamicsData"/> e o
/// <see cref="PopulationCatalog"/> resolvido (id de profissão citado num viés, ou
/// toda <c>SourceProfessionIds</c> de uma regra de transformação, precisa existir no catálogo do
/// período — <c>TargetProfessionIds</c> fica de fora dessa checagem de propósito:
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

        var emergenceResult = LivingWorld.Simulation.Emergence.EmergenceScenarioLoader.Load(json);
        if (!emergenceResult.IsSuccess)
            return Result<PeriodDefinition>.Fail(emergenceResult.Error!);

        var ontogenyResult = LivingWorld.Simulation.Ontogeny.OntogenyScenarioLoader.Load(json);
        if (!ontogenyResult.IsSuccess)
            return Result<PeriodDefinition>.Fail(ontogenyResult.Error!);

        var appearanceResult = ParseAppearance(json);
        if (!appearanceResult.IsSuccess)
            return Result<PeriodDefinition>.Fail(appearanceResult.Error!);

        var population = populationResult.Value!;
        var dynamics = dynamicsResult.Value!;

        var referenceError = ValidateProfessionReferences(population.Catalog, dynamics);
        if (referenceError is not null)
            return Result<PeriodDefinition>.Fail(referenceError);

        return Result<PeriodDefinition>.Ok(new PeriodDefinition(
            mapResult.Value!, population, behaviorResult.Value!, economyResult.Value!, cityResult.Value!, dynamics,
            descriptorsResult.Value!, resourcesResult.Value!.Catalog, resourcesResult.Value!.Recipes,
            extraordinaryResult.Value!, emergenceResult.Value!, ontogenyResult.Value!, appearanceResult.Value));
    }

    private static Result<LivingWorld.Domain.Population.AppearanceRules?> ParseAppearance(string json) =>
        LivingWorld.Simulation.Population.AppearanceScenarioLoader.Load(System.Text.Json.Nodes.JsonNode.Parse(json)!.AsObject());

    private static string? ValidateProfessionReferences(PopulationCatalog catalog, PeriodDynamicsData dynamics)
    {
        foreach (var bias in dynamics.ProfessionBiases)
            if (!catalog.IsValidProfession(new ProfessionType(bias.ProfessionId)))
                return $"Dynamics.ProfessionBiases[]: ProfessionId {bias.ProfessionId} não existe em ProfessionIds";

        // Só SourceProfessionIds precisa já existir no catálogo — o destino de
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
