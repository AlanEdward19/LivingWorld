namespace LivingWorld.Api.Visual.Layers;

/// <summary>Fase 15, T1 (VTT-04): catálogo fechado de camadas derivadas renderizáveis sobre
/// o mesmo grid base. Cada camada exige projector + renderer registrados (VTT-05).</summary>
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
