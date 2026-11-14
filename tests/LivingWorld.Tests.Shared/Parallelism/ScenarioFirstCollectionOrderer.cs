using Xunit.Abstractions;

namespace LivingWorld.Tests.Shared.Parallelism;

/// <summary>Ordem de partida das coleções do xUnit: as que têm teste <c>Category=Scenario</c> (vários anos de simulação) saem
/// primeiro, o resto depois, cada grupo em ordem de nome. Só muda quando cada coleção começa, nunca o que ela roda nem o
/// resultado. Sem isso o xUnit começava cenários de minutos no fim da suíte (medido em 2026-11-13: Orphan_care aos 298 s,
/// Climate aos 411 s de 487 s) e a cauda ficava com poucos testes longos; com cenário primeiro, os rápidos enchem a cauda.</summary>
public sealed class ScenarioFirstCollectionOrderer : ITestCollectionOrderer
{
    private const string DefaultPrefix = "Test collection for ";

    public IEnumerable<ITestCollection> OrderTestCollections(IEnumerable<ITestCollection> testCollections) =>
        testCollections
            .Select(collection => (Collection: collection, Heavy: IsScenario(collection)))
            .OrderBy(entry => entry.Heavy ? 0 : 1)
            .ThenBy(entry => entry.Collection.DisplayName, StringComparer.Ordinal)
            .Select(entry => entry.Collection)
            .ToList();

    private static bool IsScenario(ITestCollection collection)
    {
        // Coleção nomeada ([Collection]) junta várias classes que o orderer não enxerga: fica no grupo comum.
        if (!collection.DisplayName.StartsWith(DefaultPrefix, StringComparison.Ordinal)) return false;
        var type = collection.TestAssembly.Assembly.GetType(collection.DisplayName[DefaultPrefix.Length..]);
        if (type is null) return false;
        return HasScenarioTrait(type.GetCustomAttributes(typeof(TraitAttribute)))
            || type.GetMethods(false).Any(method => HasScenarioTrait(method.GetCustomAttributes(typeof(TraitAttribute))));
    }

    private static bool HasScenarioTrait(IEnumerable<IAttributeInfo> traits) =>
        traits.Any(trait => trait.GetConstructorArguments().ToArray() is [string name, string value]
            && name == "Category" && value == "Scenario");
}
