using LivingWorld.Domain.Invention;

namespace LivingWorld.Tests.Unit.Invention;

/// <summary>AD-153: a sonda de terra decide por leis (atrito de arrasto e de patim, braço de rolamento e atrito do eixo, força humana
/// sustentada, viga, pino e disco), nunca por uma entrada "carroça". As peças saem dos materiais do cenário.</summary>
public class LandProbeTests
{
    private static readonly InventionRules Rules = InventionRules.Default;

    private static MaterialSpec Wood => Rules.Materials.First(m => !m.CoatingOnly && m.NaturalShape == PartShape.Cylinder && m.TensileMPa >= 30);

    private static MaterialSpec Dense => Rules.Materials.OrderByDescending(m => m.DensityKgM3).First(m => !m.CoatingOnly);

    private static DesignPart Deck(MaterialSpec m, short thick = 40) =>
        new(PartShape.Box, 1000, 600, thick, 0, (short)m.Id, 0, JointKind.Fixed, -1, 0, 0, 0);

    /// <summary>Duas peças iguais por baixo do tabuleiro, nas pontas do comprimento (redondas) ou dos lados (esguias).</summary>
    private static List<DesignPart> WithPair(DesignPart deck, DesignPart under, int parent = 0)
    {
        var parts = new List<DesignPart> { deck };
        var placed = under with { Parent = (sbyte)parent, OffsetZMm = (short)(deck.OffsetZMm - under.SpanZ) };
        bool round = under.Shape != PartShape.Box;
        short shift = (short)(round ? deck.LengthMm / 2 - under.LengthMm / 2 : deck.WidthMm / 2 - under.WidthMm / 2);
        parts.Add(round ? placed with { OffsetXMm = shift } : placed with { OffsetYMm = shift });
        parts.Add(round ? placed with { OffsetXMm = (short)-shift } : placed with { OffsetYMm = (short)-shift });
        return parts;
    }

    private static DesignPart Runner(MaterialSpec m) => new(PartShape.Box, 1000, 40, 40, 0, (short)m.Id, 0, JointKind.Fixed, 0, 0, 0, 0);

    private static DesignPart Wheel(MaterialSpec m, JointKind joint = JointKind.Axle, PartShape shape = PartShape.Cylinder) =>
        new(shape, 60, 300, 300, 0, (short)m.Id, 0, joint, 0, 0, 0, 0);

    [Fact]
    public void For_the_same_deck_dragging_carries_less_than_runners_and_runners_less_than_round_parts_on_axles()
    {
        var drag = PhysicsEvaluator.Haul([Deck(Wood)], Rules);
        var skid = PhysicsEvaluator.Haul(WithPair(Deck(Wood), Runner(Wood)), Rules);
        var roll = PhysicsEvaluator.Haul(WithPair(Deck(Wood), Wheel(Wood)), Rules);

        Assert.True(drag.Works && skid.Works && roll.Works, $"{drag} | {skid} | {roll}");
        Assert.Equal((GroundMode.Drag, GroundMode.Skid, GroundMode.Roll), (drag.Mode, skid.Mode, roll.Mode));
        Assert.True(drag.CapacityG < skid.CapacityG && skid.CapacityG < roll.CapacityG, $"{drag.CapacityG} < {skid.CapacityG} < {roll.CapacityG}");
        Assert.True(drag.ResistancePm > skid.ResistancePm && skid.ResistancePm > roll.ResistancePm);
        Assert.True(roll.CapacityG > Rules.FootCarryKg * 1000L, "rolar leva mais que os braços");
    }

    [Fact]
    public void A_runner_with_the_smoothest_coating_still_rubs_like_wood_on_earth()
    {
        // AD-162: 0,10 é madeira encerada em neve; em terra o patim mais liso fica perto de madeira sobre madeira (~0,22)
        var coat = Rules.Materials.Where(m => m.CoatingOnly).MaxBy(m => m.FinishPm)!;
        var skid = PhysicsEvaluator.Haul(WithPair(Deck(Wood), Runner(Wood) with { Coating = (short)coat.Id }), Rules);

        Assert.Equal(GroundMode.Skid, skid.Mode);
        Assert.InRange(skid.ResistancePm, 220, 250);
    }

    [Fact]
    public void A_round_part_without_an_axle_does_not_roll()
    {
        var fixedWheel = PhysicsEvaluator.Haul(WithPair(Deck(Wood), Wheel(Wood, JointKind.Fixed)), Rules);
        var rolling = PhysicsEvaluator.Haul(WithPair(Deck(Wood), Wheel(Wood)), Rules);

        Assert.NotEqual(GroundMode.Roll, fixedWheel.Mode);
        Assert.True(fixedWheel.CapacityG < rolling.CapacityG / 2, $"{fixedWheel} vs {rolling}");
    }

    [Fact]
    public void A_part_that_is_not_round_by_shape_does_not_roll_even_on_an_axle()
    {
        var square = PhysicsEvaluator.Haul(WithPair(Deck(Wood), Wheel(Wood, JointKind.Axle, PartShape.Box)), Rules);

        Assert.NotEqual(GroundMode.Roll, square.Mode);
    }

    [Fact]
    public void A_bigger_wheel_rolls_easier()
    {
        var small = PhysicsEvaluator.Haul(WithPair(Deck(Wood), Wheel(Wood) with { WidthMm = 160, HeightMm = 160 }), Rules);
        var big = PhysicsEvaluator.Haul(WithPair(Deck(Wood), Wheel(Wood)), Rules);

        Assert.True(big.ResistancePm < small.ResistancePm, $"{big.ResistancePm} < {small.ResistancePm}");
    }

    [Fact]
    public void A_thin_axle_breaks_under_the_load_that_a_thick_one_holds()
    {
        // as rodas giram num pino de uma vara fina presa ao tabuleiro: o pino é tão grosso quanto a vara
        var stick = new DesignPart(PartShape.Cylinder, 900, 8, 8, 0, (short)Wood.Id, 0, JointKind.Fixed, 0, 0, 0, -20);
        var parts = new List<DesignPart> { Deck(Wood), stick };
        var wheel = Wheel(Wood) with { Parent = 1, OffsetZMm = -300 };
        parts.Add(wheel with { OffsetXMm = 420 });
        parts.Add(wheel with { OffsetXMm = -420 });

        var thin = PhysicsEvaluator.Haul(parts, Rules);
        parts[1] = stick with { WidthMm = 60, HeightMm = 60, OffsetZMm = -60 };
        var thick = PhysicsEvaluator.Haul(parts, Rules);

        Assert.Equal(FailureMode.Breaks, thin.Failure);
        Assert.True(thick.Works && thick.Mode == GroundMode.Roll, thick.ToString());
    }

    [Fact]
    public void A_slab_too_heavy_for_a_person_to_drag_fails_as_too_heavy()
    {
        var slab = new DesignPart(PartShape.Box, 800, 800, 300, 0, (short)Dense.Id, 0, JointKind.Fixed, -1, 0, 0, 0);

        Assert.Equal(FailureMode.TooHeavy, PhysicsEvaluator.Haul([slab], Rules).Failure);
    }

    [Fact]
    public void A_bare_log_has_no_room_for_a_load()
    {
        var log = new DesignPart(PartShape.Cylinder, 2000, 200, 200, 0, (short)Wood.Id, 0, JointKind.Fixed, -1, 0, 0, 0);

        Assert.Equal(FailureMode.TooSmall, PhysicsEvaluator.Haul([log], Rules).Failure);
    }

    [Fact]
    public void A_stronger_pull_hauls_more()
    {
        var parts = WithPair(Deck(Wood), Runner(Wood));

        Assert.True(PhysicsEvaluator.Haul(parts, Rules with { HumanPullN = 400 }).CapacityG > PhysicsEvaluator.Haul(parts, Rules).CapacityG);
    }
}
