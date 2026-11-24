using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Behavior.Decision;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Crime;
using static LivingWorld.Tests.Unit.Players.PlayerAbsenceTests;

namespace LivingWorld.Tests.Unit.Players;

/// <summary>AD-197h: no passo fino a viagem dura o caminho em ms (um trajeto curto leva minutos) e quem viaja está, para quem
/// percebe, testemunha ou encontra, na célula da rota em que a pose de agora o põe. De hora em hora nada disso existe.</summary>
public class FinePoseTests
{
    private const int Minute = 60_000;

    [Fact]
    public void A_short_trip_with_a_fine_delta_takes_minutes_not_an_hour()
    {
        var arm = Village(101, initialPopulation: 60);
        long? took = null;
        Npc? walker = null;
        long departed = 0;
        for (int i = 0; i < 24 * 60 && took is null; i++)
        {
            arm.Clock.Tick(arm.World, Minute);
            long now = arm.World.CurrentDate.TotalMilliseconds;
            if (walker is null)
            {
                var markets = LivingWorld.Simulation.Economy.Market.MarketIndex.BuildForTick(arm.World);
                walker = arm.World.Npcs.FirstOrDefault(n => n.IsAlive && n.CurrentAction == ActionType.Travel && n.ActionStartedAtMs == now
                    && BehaviorDecisionSystem.TravelArrivalMs(arm.World, n, markets) is { } end && end - now < WorldMillis.PerHour / 2);
                departed = now;
            }
            else if (walker.CurrentAction != ActionType.Travel || walker.ActionStartedAtMs != departed)
            {
                took = now - departed;
            }
        }

        Assert.NotNull(took);
        Assert.InRange(took!.Value, Minute, WorldMillis.PerHour - Minute); // chegou em minutos, dentro da hora
    }

    [Fact]
    public void A_traveller_in_the_middle_of_the_route_is_counted_on_the_path_cell_and_witnesses_there()
    {
        var arm = Village(102, initialPopulation: 60);
        Npc? traveller = null;
        LivingWorld.Domain.Geography.CellCoord pose = default;
        for (int i = 0; i < 24 * 60 * 6 && traveller is null; i++)
        {
            arm.Clock.Tick(arm.World, 10_000);
            foreach (var npc in arm.World.AliveNpcIndex.Alive)
                if (arm.World.AliveNpcIndex.PoseOf(npc) is { } cell && arm.World.AliveNpcIndex.At(cell).Count == 1)
                {
                    traveller = npc;
                    pose = cell;
                    break;
                }
        }
        Assert.NotNull(traveller);
        var index = arm.World.AliveNpcIndex;
        var offender = arm.World.Npcs.First(n => n.IsAlive && n.Id != traveller!.Id && n.CurrentAction != ActionType.Travel);
        var seenAt = (index.At(pose).Contains(traveller!), index.At(traveller!.CurrentLocation).Contains(traveller!));
        offender.MoveTo(pose, arm.World.CurrentDate.TotalHours);

        var outcome = Justice.OnCrime(arm.World, arm.Ctx, CrimeKind.Assault, offender, null, -1, pose, 1, "corpo", "teste", caught: true,
            arm.Ctx.OneShot("pose-witness"));

        Assert.Equal((true, false, 1), (seenAt.Item1, seenAt.Item2, outcome.Seers));
    }

    [Fact]
    public void By_the_hour_and_with_the_ablation_there_are_no_travel_poses()
    {
        var off = PlayerRules.Default with { Time = TimeRules.Default with { FineDecisions = false } };
        var hourly = Village(103, initialPopulation: 60);
        var ablated = Village(103, rules: off, initialPopulation: 60);
        bool anyHourly = false, anyAblated = false;
        for (int i = 0; i < 48; i++)
        {
            hourly.Clock.Tick(hourly.World);
            anyHourly |= hourly.World.AliveNpcIndex.HasPoses;
        }
        for (int i = 0; i < 6 * 60; i++)
        {
            ablated.Clock.Tick(ablated.World, Minute);
            anyAblated |= ablated.World.AliveNpcIndex.HasPoses;
        }

        Assert.Equal((false, false), (anyHourly, anyAblated));
    }
}
