using LivingWorld.Domain.Geography;
using LivingWorld.Simulation.Extraordinary.Engine;
using LivingWorld.Simulation.Extraordinary.Inheritance;
using LivingWorld.Simulation.Extraordinary.Mechanics;

namespace LivingWorld.Tests.Unit.Extraordinary.Engine;

/// <summary>
/// ADR-0040, F3: o valor tipado de cada parser e as regras de borda (sinal, limites, chave, segmentos).
/// O diferencial carga x invocação não as pega, porque os dois lados usam o mesmo parser: aqui cada regra
/// é afirmada pelo valor, e a carga é checada pelo mesmo token.
/// </summary>
public sealed class PrimitiveParserValueTests
{
    private static bool Loads(string token, ExtraordinaryMechanicKind kind = ExtraordinaryMechanicKind.Effect) =>
        ExtraordinaryPrimitiveCatalog.Validate(ExtraordinaryMechanicRegistry.Default, token, kind).IsSuccess;

    [Fact]
    public void Construct_reads_all_five_segments_and_refuses_extra_segments_wide_footprints_and_blank_looks()
    {
        var spec = ConstructMechanic.Create.Parse("construct.create:2x3:10:5:stone").Value;
        Assert.Equal((2, 3, 10, 5L, "stone"), (spec.Width, spec.Height, spec.Durability, spec.DurationHours, spec.Appearance));

        Assert.False(ConstructMechanic.Create.Parse("construct.create:1x1:10:5:stone:extra").IsSuccess);
        Assert.False(ConstructMechanic.Create.Parse("construct.create:9x1:10:5:stone").IsSuccess);
        Assert.False(ConstructMechanic.Create.Parse("construct.create:1x9:10:5:stone").IsSuccess);
        Assert.False(ConstructMechanic.Create.Parse("construct.create:1x1:10:5: ").IsSuccess);
        Assert.False(Loads("construct.create:1x1:10:5:stone:extra"));
        Assert.False(Loads("construct.create:9x1:10:5:stone"));
        Assert.False(Loads("construct.create:1x1:10:5: "));
    }

    [Fact]
    public void Matter_transmute_reads_origin_destination_and_rate_and_refuses_zero_rate_look_alike_head_and_negative_origin()
    {
        Assert.Equal((1, 2, 3), ExtraordinaryTuple(MatterTransmuteMechanic.Transmute.Parse("matter.transmute:1:2:3").Value));

        Assert.False(MatterTransmuteMechanic.Transmute.Parse("matter.transmute:1:2:0").IsSuccess);
        Assert.False(MatterTransmuteMechanic.Transmute.Parse("matter.transmutex:1:2:3").IsSuccess);
        Assert.False(MatterTransmuteMechanic.Transmute.Parse("matter.transmute:-1:2:3").IsSuccess);
        Assert.False(MatterTransmuteMechanic.Transmute.Parse("matter.transmute:1:-2:3").IsSuccess);
        Assert.False(Loads("matter.transmute:1:2:0"));
        Assert.False(Loads("matter.transmutex:1:2:3"));
        Assert.False(Loads("matter.transmute:-1:2:3"));
        Assert.True(Loads("matter.transmute :1:2:3"));
    }

    private static (int, int, int) ExtraordinaryTuple((int Origin, int Destination, int Rate) value) =>
        (value.Origin, value.Destination, value.Rate);

    [Fact]
    public void Skill_copy_reads_the_skill_id_and_refuses_a_negative_id()
    {
        Assert.Equal(7, SkillMechanic.Copy.Parse("skill.copy:7").Value);
        Assert.Equal(0, SkillMechanic.Copy.Parse("skill.copy:0").Value);
        Assert.False(SkillMechanic.Copy.Parse("skill.copy:-1").IsSuccess);
        Assert.False(Loads("skill.copy:-1"));
    }

    [Fact]
    public void Transfer_from_target_refuses_a_negative_amount_just_like_the_plain_form()
    {
        var plain = TransferMechanic.Token.Parse("transfer.health:5").Value;
        var inverted = TransferMechanic.Token.Parse("transfer.health:5:from-target").Value;
        Assert.Equal((5, false, 5, true), (plain.Amount, plain.FromTarget, inverted.Amount, inverted.FromTarget));

        Assert.False(TransferMechanic.Token.Parse("transfer.health:-5:from-target").IsSuccess);
        Assert.False(TransferMechanic.Token.Parse("transfer.health:-5").IsSuccess);
        Assert.False(Loads("transfer.health:-5:from-target"));
        Assert.False(Loads("transfer.lifespan-years:-2:from-target"));
    }

    [Theory]
    [InlineData("household.resource.9:-2")]
    [InlineData("household.resource.9:0")]
    public void Household_cost_refuses_a_non_positive_amount(string token)
    {
        Assert.False(HouseholdResourceCostMechanic.Cost.Parse(token).IsSuccess);
        Assert.False(Loads(token, ExtraordinaryMechanicKind.Cost));
        Assert.Equal((new ResourceType(9), 2), HouseholdResourceCostMechanic.Cost.Parse("household.resource.9:2").Value);
    }

    [Fact]
    public void Teleport_refuses_a_negative_range_and_reads_a_positive_one()
    {
        Assert.Equal(5, TeleportMechanic.Range.Parse("npc.teleport:5").Value.Amount);
        Assert.False(TeleportMechanic.Range.Parse("npc.teleport:-5").IsSuccess);
        Assert.False(Loads("npc.teleport:-5"));
    }

    [Fact]
    public void Combat_strike_and_engage_refuse_a_negative_magnitude_and_read_a_positive_one()
    {
        Assert.Equal(8, CombatMechanic.Strike.Parse("combat.strike:8").Value.Amount);
        Assert.Equal(3, CombatMechanic.Engage.Parse("combat.engage:3").Value.Amount);
        Assert.False(CombatMechanic.Strike.Parse("combat.strike:-8").IsSuccess);
        Assert.False(CombatMechanic.Engage.Parse("combat.engage:-3").IsSuccess);
        Assert.False(Loads("combat.strike:-8"));
        Assert.False(Loads("combat.engage:-3"));
    }

    [Fact]
    public void Clone_and_split_on_death_refuse_a_negative_count_and_read_a_positive_one()
    {
        Assert.Equal(1, NpcCloneMechanic.Clone.Parse("npc.clone:1").Value.Amount);
        Assert.Equal(2, NpcSplitOnDeathMechanic.Split.Parse("npc.split-on-death:2").Value.Amount);
        Assert.False(NpcCloneMechanic.Clone.Parse("npc.clone:-1").IsSuccess);
        Assert.False(NpcSplitOnDeathMechanic.Split.Parse("npc.split-on-death:-2").IsSuccess);
        Assert.False(Loads("npc.clone:-1"));
        Assert.False(Loads("npc.split-on-death:-2"));
    }

    [Fact]
    public void Area_selector_is_read_raw_and_refuses_the_typed_form_but_tolerates_non_selector_area_tokens()
    {
        Assert.Equal(new AreaSelector("radius", 3), AreaSelectorMechanic.Selector.Parse("area:radius:3").Value);
        Assert.Equal(new AreaSelector("region", 2), AreaSelectorMechanic.Selector.Parse("area:region:2").Value);
        Assert.False(AreaSelectorMechanic.Selector.Parse("area:radius:fire:3").IsSuccess);
        Assert.False(AreaSelectorMechanic.Selector.Parse("area:region:fire:2").IsSuccess);
        Assert.False(AreaSelectorMechanic.Selector.Parse("area:radius:-1").IsSuccess);
        Assert.False(Loads("area:radius:fire:3"));
        Assert.False(Loads("area:region:fire:2"));
        Assert.False(AreaSelectorMechanic.Selector.Parse("area:foo:3").Value.IsSelector);
        Assert.True(Loads("area:foo:3"));
        Assert.True(Loads("area:radius"));
    }

    [Fact]
    public void Movement_values_read_by_gravity_accept_infinity_and_keep_refusing_non_positive_numbers()
    {
        Assert.True(double.IsPositiveInfinity(MovementEffectMechanic.Flight.Parse("movement.flight:Infinity").Value));
        Assert.Equal(3d, MovementEffectMechanic.SpeedMultiplier.Parse("movement.speed-multiplier:3").Value);
        Assert.False(MovementEffectMechanic.Flight.Parse("movement.flight:0").IsSuccess);
        Assert.False(MovementEffectMechanic.Flight.Parse("movement.flight").IsSuccess);
        Assert.True(Loads("movement.flight:Infinity"));
        Assert.True(Loads("movement.speed-multiplier:Infinity"));
        Assert.True(Loads("movement.qualquer"));
    }

    [Fact]
    public void Blank_argument_foresight_preview_is_read_unchanged_and_an_empty_one_is_refused()
    {
        Assert.Equal(" ", ForesightMechanic.Preview.Parse("foresight.preview: ").Value);
        Assert.Equal("death", ForesightMechanic.Preview.Parse("foresight.preview:death").Value);
        Assert.False(ForesightMechanic.Preview.Parse("foresight.preview:").IsSuccess);
        Assert.True(Loads("foresight.preview: "));
        Assert.False(Loads("foresight.preview:"));
    }
}
