using Xunit;

namespace LivingWorld.Tests;

/// <summary>Uma <see cref="LivingWorldApiFactory"/> para todas as classes de endpoint que só
/// leem/mutam o mundo de forma acumulativa (cidades/facts/periods). <see
/// cref="DisableParallelization"/> serializa essas classes entre si — evita corrida no
/// <c>WorldHost</c> singleton da factory — e corta dezenas de boots ASP.NET+Migrate por run.
/// Classes que fazem <c>WorldHost.Replace</c> / <c>POST /worlds/create</c> ficam fora
/// (<c>IClassFixture</c> própria).</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ApiEndpointCollection : ICollectionFixture<LivingWorldApiFactory>
{
    public const string Name = "API endpoints";
}
