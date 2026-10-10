namespace LivingWorld.Api.Visual.Layers;

/// <summary>Catálogo fechado de camadas derivadas renderizáveis sobre
/// o mesmo grid base. Cada camada exige projector + renderer registrados.</summary>
public enum VisualLayerId
{
    Terrain,
    Biome,
    Rivers,
    Mountains,
    Resources,
    Roads,
    Borders,
    Kingdoms,
    Cities,
    Villages,
    Routes,
    Migrations,
    Conflicts,
    Climate
}
