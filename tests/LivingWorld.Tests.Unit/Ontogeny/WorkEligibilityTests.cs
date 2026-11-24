using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Ontogeny;

namespace LivingWorld.Tests.Unit.Ontogeny;

/// <summary>AD-148: trabalhar pede usar ferramenta, não a mão hábil completa. Com o limiar em 1,0, o adulto que perdeu a janela da
/// mão hábil na infância (teto permanente abaixo de 1) nunca trabalhava: 55% dos adultos da seed 7 aos 30 anos.</summary>
public class WorkEligibilityTests
{
    private static DevelopmentState Adult(double fineMotor) =>
        new(1.0, fineMotor, 1.0, 1.0, 1.0, 1.0, FineMotorCeiling: fineMotor);

    [Fact]
    public void An_adult_whose_skilled_hands_window_closed_short_can_still_work()
    {
        Assert.True(OntogenyDefaults.Human.IsEligible(Adult(0.85), ActionType.Work)); // antes: falso (limiar 1,0)
        Assert.True(OntogenyDefaults.Human.IsEligible(Adult(0.70), ActionType.Work));
    }

    [Fact]
    public void Who_does_not_yet_use_tools_does_not_work()
    {
        Assert.False(OntogenyDefaults.Human.IsEligible(Adult(0.5), ActionType.Work));
    }

    /// <summary>AD-163 (08 item 50): a mão ainda inábil rende e pratica menos, entre o marco de trabalhar (0,7) e a mão hábil (1,0).</summary>
    [Fact]
    public void Fine_motor_scales_work_between_the_tools_milestone_and_skilled_hands()
    {
        var on = OntogenyDefaults.Human with { FineMotorScalesWork = true };
        Assert.Equal(0.85, on.FineMotorWorkFactor(Adult(0.85)), 9); // antes: 1,0 (rendia como a mão hábil)
        Assert.Equal(0.70, on.FineMotorWorkFactor(Adult(0.5)), 9);
        Assert.Equal(1.0, on.FineMotorWorkFactor(null)); // desenvolvimento completo
        Assert.Equal(1.0, (OntogenyDefaults.Human with { FineMotorScalesWork = false }).FineMotorWorkFactor(Adult(0.85)));
    }

    [Fact]
    public void Fine_motor_switch_survives_the_snapshot_and_stays_out_of_it_when_off()
    {
        var options = new System.Text.Json.JsonSerializerOptions { Converters = { new OntogenyRulesJsonConverter() } };
        var on = OntogenyDefaults.Human with { FineMotorScalesWork = true };
        var back = System.Text.Json.JsonSerializer.Deserialize<OntogenyRules>(System.Text.Json.JsonSerializer.Serialize(on, options), options)!;
        Assert.True(back.FineMotorScalesWork);
        var off = OntogenyDefaults.Human with { FineMotorScalesWork = false };
        Assert.DoesNotContain("FineMotorScalesWork", System.Text.Json.JsonSerializer.Serialize(off, options));
    }
}
