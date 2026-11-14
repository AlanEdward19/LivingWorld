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
}
