namespace LivingWorld.Domain;

/// <summary>Catálogo de ids de terreno/bioma/recurso válidos para um cenário (task 2). O motor
/// só conhece ids — nome e apresentação são dado de cliente (Fase 14), nunca literal aqui.
/// <see cref="TerrainType.Unset"/> nunca é um id válido do catálogo.</summary>
public sealed record GeographyCatalog(
    HashSet<int> TerrainIds,
    HashSet<int> BiomeIds,
    HashSet<int> ResourceIds)
{
    public bool IsValidTerrain(TerrainType terrain) =>
        terrain != TerrainType.Unset && TerrainIds.Contains(terrain.Id);

    public bool IsValidBiome(BiomeType biome) => BiomeIds.Count == 0 || BiomeIds.Contains(biome.Id);

    public bool IsValidResource(ResourceType resource) =>
        ResourceIds.Count == 0 || ResourceIds.Contains(resource.Id);
}

/// <summary>Tabela de pesos de custo de deslocamento (task 3), vinda do cenário: base por
/// distância, peso por unidade de altitude subida e multiplicador por terreno.</summary>
public sealed record CostWeights(double Base, double AltitudeWeight, IReadOnlyDictionary<int, double> TerrainWeight)
{
    public double WeightOf(TerrainType terrain) => TerrainWeight.GetValueOrDefault(terrain.Id, 1.0);
}
