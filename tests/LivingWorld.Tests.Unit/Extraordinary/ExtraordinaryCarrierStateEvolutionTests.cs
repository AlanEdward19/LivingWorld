using LivingWorld.Domain;

namespace LivingWorld.Tests.Extraordinary;

public sealed class ExtraordinaryCarrierStateEvolutionTests
{
    [Fact]
    public void Carrier_state_defaults_use_count_and_stage_index_to_zero()
    {
        var state = MinimalCarrierState();

        Assert.Equal(0, state.UseCount);
        Assert.Equal(0, state.CurrentStageIndex);
    }

    [Fact]
    public void Existing_carrier_construction_omitting_new_fields_keeps_defaults()
    {
        var state = new ExtraordinaryCarrierState(
            new NpcId(1),
            ["test-power"],
            true,
            "manifested",
            new ExtraordinaryAppearanceState(1, "", ""),
            null,
            1);

        Assert.Equal(0, state.UseCount);
        Assert.Equal(0, state.CurrentStageIndex);
    }

    private static ExtraordinaryCarrierState MinimalCarrierState() =>
        new(
            new NpcId(1),
            ["test-power"],
            true,
            "manifested",
            new ExtraordinaryAppearanceState(1, "", ""),
            null,
            1);
}
