using Xunit;

namespace LivingWorld.Tests;

/// <summary>Uma <see cref="LivingWorldApiFactory"/> compartilhada para endpoints de <b>leitura</b>
/// (ou leituras que só tocam estado efêmero do gateway sem <c>WorldHost.Replace</c>).
/// Estar na mesma collection já serializa as classes entre si — protege o
/// <c>WorldHost</c>/<c>WorldState</c> singleton da factory.
/// <b>Não</b> use <c>DisableParallelization=true</c> aqui: isso serializa a collection contra
/// <i>toda</i> a assembly (over-serialization). Classes que mutam mundo
/// (<c>Replace</c>, tick, create/start, FOW seed, move, periods, conversa, materialize,
/// narrative seed) usam <c>IClassFixture&lt;LivingWorldApiFactory&gt;</c> própria.</summary>
[CollectionDefinition(Name)]
public sealed class ApiEndpointCollection : ICollectionFixture<LivingWorldApiFactory>
{
    public const string Name = "API endpoints";
}
