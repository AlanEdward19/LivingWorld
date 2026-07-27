using LivingWorld.Domain;

namespace LivingWorld.Tests.Population;

/// <summary>Fase 6, task 4 (SKILL-01, SKILL-12): as 13 habilidades de um <c>Npc</c>, imutáveis,
/// clampadas em <c>[0,cap]</c>.</summary>
public class SkillSetTests
{
    private static readonly SkillType[] AllSkillTypes = Enum.GetValues<SkillType>();

    [Fact]
    public void Initial_sets_all_13_skills_to_the_same_starting_value()
    {
        var set = SkillSet.Initial(5.0);

        foreach (var type in AllSkillTypes)
            Assert.Equal(5.0, set.Get(type));
    }

    [Fact]
    public void WithGain_increases_only_the_targeted_skill_others_unchanged()
    {
        var set = SkillSet.Initial(10.0);

        var afterGain = set.WithGain(SkillType.Craft, 5.0, cap: 100.0);

        Assert.Equal(15.0, afterGain.Get(SkillType.Craft));
        foreach (var type in AllSkillTypes.Where(t => t != SkillType.Craft))
            Assert.Equal(10.0, afterGain.Get(type));
    }

    [Fact]
    public void WithGain_never_exceeds_cap_gain_at_cap_is_absorbed()
    {
        var set = SkillSet.Initial(100.0);

        var afterGain = set.WithGain(SkillType.Magic, 50.0, cap: 100.0);

        Assert.Equal(100.0, afterGain.Get(SkillType.Magic));
    }

    [Fact]
    public void WithGain_never_goes_below_zero_with_negative_delta()
    {
        var set = SkillSet.Initial(1.0);

        var afterLoss = set.WithGain(SkillType.Combat, -10.0, cap: 100.0);

        Assert.Equal(0.0, afterLoss.Get(SkillType.Combat));
    }

    [Fact]
    public void WithGain_returns_a_new_instance_original_is_unmodified()
    {
        var set = SkillSet.Initial(0.0);

        var afterGain = set.WithGain(SkillType.Research, 3.0, cap: 100.0);

        Assert.Equal(0.0, set.Get(SkillType.Research));
        Assert.Equal(3.0, afterGain.Get(SkillType.Research));
    }

    [Theory]
    [InlineData(SkillType.Agriculture)]
    [InlineData(SkillType.Hunting)]
    [InlineData(SkillType.Trade)]
    [InlineData(SkillType.Construction)]
    [InlineData(SkillType.Medicine)]
    [InlineData(SkillType.Combat)]
    [InlineData(SkillType.Teaching)]
    [InlineData(SkillType.Craft)]
    [InlineData(SkillType.Politics)]
    [InlineData(SkillType.Leadership)]
    [InlineData(SkillType.Research)]
    [InlineData(SkillType.Technology)]
    [InlineData(SkillType.Magic)]
    public void Get_and_WithGain_round_trip_for_every_skill_type(SkillType type)
    {
        var set = SkillSet.Initial(0.0);

        var afterGain = set.WithGain(type, 7.0, cap: 100.0);

        Assert.Equal(7.0, afterGain.Get(type));
    }
}
