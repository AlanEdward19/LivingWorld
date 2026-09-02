using LivingWorld.Domain.Population.Skills;

namespace LivingWorld.Tests.Unit.Population.Skills;

/// <summary>Fase 6, task 4 (SKILL-01, SKILL-12) + Fase 13, T11b: habilidades de um <c>Npc</c>,
/// imutáveis, clampadas em <c>[0,cap]</c>, id aberto (não mais enum fechado de 13 valores) — uma
/// habilidade nunca ganhada vale 0, sem precisar de lista prévia de ids conhecidos.</summary>
public class SkillSetTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    [InlineData(41)]
    [InlineData(999)]
    public void Empty_returns_zero_for_any_skill_id(int id)
    {
        Assert.Equal(0.0, SkillSet.Empty.Get(new SkillType(id)));
    }

    [Fact]
    public void WithGain_increases_only_the_targeted_skill_others_unchanged()
    {
        var set = SkillSet.Empty
            .WithGain(new SkillType(0), 10.0, cap: 100.0)
            .WithGain(new SkillType(7), 10.0, cap: 100.0);

        var afterGain = set.WithGain(new SkillType(7), 5.0, cap: 100.0);

        Assert.Equal(15.0, afterGain.Get(new SkillType(7)));
        Assert.Equal(10.0, afterGain.Get(new SkillType(0)));
    }

    [Fact]
    public void WithGain_never_exceeds_cap_gain_at_cap_is_absorbed()
    {
        var set = SkillSet.Empty.WithGain(new SkillType(12), 100.0, cap: 100.0);

        var afterGain = set.WithGain(new SkillType(12), 50.0, cap: 100.0);

        Assert.Equal(100.0, afterGain.Get(new SkillType(12)));
    }

    [Fact]
    public void WithGain_never_goes_below_zero_with_negative_delta()
    {
        var set = SkillSet.Empty.WithGain(new SkillType(5), 1.0, cap: 100.0);

        var afterLoss = set.WithGain(new SkillType(5), -10.0, cap: 100.0);

        Assert.Equal(0.0, afterLoss.Get(new SkillType(5)));
    }

    [Fact]
    public void WithGain_returns_a_new_instance_original_is_unmodified()
    {
        var set = SkillSet.Empty;

        var afterGain = set.WithGain(new SkillType(10), 3.0, cap: 100.0);

        Assert.Equal(0.0, set.Get(new SkillType(10)));
        Assert.Equal(3.0, afterGain.Get(new SkillType(10)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(6)]
    [InlineData(12)]
    [InlineData(500)]
    public void Get_and_WithGain_round_trip_for_any_skill_id(int id)
    {
        var set = SkillSet.Empty;

        var afterGain = set.WithGain(new SkillType(id), 7.0, cap: 100.0);

        Assert.Equal(7.0, afterGain.Get(new SkillType(id)));
    }
}
