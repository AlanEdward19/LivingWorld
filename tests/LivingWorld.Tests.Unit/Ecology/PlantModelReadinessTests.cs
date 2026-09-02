using System.Reflection;
using LivingWorld.Domain;

namespace LivingWorld.Tests.Ecology;

/// <summary>T7 — <see cref="Plant"/> já cobre o ciclo via <see cref="Plant.GrowthStage"/>;
/// parâmetros por espécie vivem em <see cref="PlantSpeciesRules"/> (REALISM-07). Nenhum campo
/// novo em <see cref="Plant"/> nem índice agregado novo em <c>WorldState</c>.</summary>
public sealed class PlantModelReadinessTests
{
    [Fact]
    public void Plant_record_has_only_id_species_position_and_growth_stage()
    {
        var fields = typeof(Plant)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Select(p => p.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            ["GrowthStage", "Id", "Position", "Species"],
            fields);
    }

    [Fact]
    public void Plant_growth_stage_covers_lifecycle_without_extra_entity_fields()
    {
        var seedling = new Plant(new PlantId(1), "wheat", new CellCoord(0, 0), GrowthStage: 0);
        var mature = seedling with { GrowthStage = 3 };

        Assert.Equal(0, seedling.GrowthStage);
        Assert.Equal(3, mature.GrowthStage);
        Assert.Equal(seedling.Id, mature.Id);
        Assert.Equal(seedling.Species, mature.Species);
        Assert.Equal(seedling.Position, mature.Position);
    }
}
