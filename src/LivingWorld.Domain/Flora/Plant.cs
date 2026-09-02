using LivingWorld.Domain.Ecology;
using LivingWorld.Domain.Geography;

namespace LivingWorld.Domain.Flora;

/// <summary>Organismo vegetal individual (PWR-101). Não substitui o estoque econômico de cultivo.</summary>
public readonly record struct PlantId(long Value)
{
    public override string ToString() => $"plant-{Value}";
}

/// <summary>Planta individual. Fase 16.4 (T7/REALISM-07): <see cref="GrowthStage"/> já cobre o
/// ciclo (broto→crescimento→produção→senescência→morte) — nenhum campo novo no record.
/// Parâmetros por espécie (tolerância térmica, maturidade, yield, reprodução) vivem em
/// <see cref="PlantSpeciesRules"/>. Produção deposita no <c>CropBatch</c>/workplace existente,
/// nunca num segundo estoque keyed por <see cref="Plant"/>.</summary>
public sealed record Plant(
    PlantId Id,
    string Species,
    CellCoord Position,
    int GrowthStage);
