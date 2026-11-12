using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Behavior.Decision;
using LivingWorld.Simulation.Behavior.Needs;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Tests.Unit.Behavior;

/// <summary>#152: quem viaja com chegada conhecida não acorda de hora em hora só porque a duração máxima de Travel
/// venceu ou porque a próxima ação da rotina, para onde já vai, pede saída antecipada.</summary>
public class NpcWakeSchedulerTravelTests
{
    private const long Now = 10;

    /// <summary>Mundo sem decaimento e sem faixas de rotina: os únicos candidatos são o fim da ação e a chegada.</summary>
    private static (WorldState World, Npc Npc) LongTrip()
    {
        var catalog = ActionCatalog.Create(
            Enum.GetValues<ActionType>().ToDictionary(a => a, _ => 4), routineSlots: [], defaultAction: ActionType.Idle).Value!;
        var needs = NeedsRules.Create(
            hungerDecayPerHour: 0, thirstDecayPerHour: 0, sleepDecayPerHour: 0, socialDecayPerHour: 0,
            urgencyThreshold: 70, maxActionSelectionSteps: 10, hysteresisEnabled: false,
            continuityBonus: 0, homelessSleepEfficiency: 0.5).Value!;
        var world = new WorldState(
            ScenarioRunner.DefaultCalendar, 1, ScenarioRunner.DefaultMap(1),
            ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
            needs, catalog, ScenarioRunner.DefaultLifeStageRules);
        world.CurrentDate = WorldDate.Epoch(ScenarioRunner.DefaultCalendar).AddHours(Now);
        var npc = new Npc(
            new NpcId(1), "n", Sex.Male, WorldDate.Epoch(ScenarioRunner.DefaultCalendar).AddYears(-20),
            ScenarioRunner.DefaultCulture, new CellCoord(0, 0), null, null, null, 100,
            Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!,
            ProfessionType.None, new CellCoord(0, 0),
            currentAction: ActionType.Travel, actionStartedAtTick: 0); // a duração máxima (4 h) venceu no tick 4
        return (world, npc);
    }

    [Fact]
    public void Traveler_past_the_max_travel_duration_sleeps_until_the_known_arrival()
    {
        var (world, npc) = LongTrip();

        long wake = NpcWakeScheduler.ComputeNextWakeTick(
            npc, world.NeedsRules, world.ActionCatalog, Now, world, travelArrivalTick: Now + 6);

        Assert.Equal(Now + 6, wake);
    }

    [Fact]
    public void Traveler_whose_arrival_tick_already_passed_is_still_reevaluated_next_hour()
    {
        var (world, npc) = LongTrip();

        long wake = NpcWakeScheduler.ComputeNextWakeTick(
            npc, world.NeedsRules, world.ActionCatalog, Now, world, travelArrivalTick: Now);

        Assert.Equal(Now + 1, wake);
    }

    [Fact]
    public void Traveler_without_a_known_arrival_still_wakes_when_the_action_duration_is_over()
    {
        var (world, npc) = LongTrip();

        long wake = NpcWakeScheduler.ComputeNextWakeTick(npc, world.NeedsRules, world.ActionCatalog, Now, world);

        Assert.Equal(Now + 1, wake);
    }

    [Fact]
    public void Traveler_already_heading_to_the_next_routine_action_waits_for_the_boundary_instead_of_leaving_early_again()
    {
        var (world, clock) = ScenarioRunner.Create(42);
        clock.Run(world, 48); // dois dias: a contratação diária já empregou gente
        // 3 h: o adulto empregado dorme e a próxima ação da rotina é Work, a algumas horas dali.
        long now = world.CurrentDate.TotalHours - world.CurrentDate.Hour + 24 + 3;
        world.CurrentDate = WorldDate.Epoch(world.CurrentDate.Calendar).AddHours(now);
        var away = world.Households.Select(h => h.Location).Concat(world.Workplaces.Select(w => w.Location)).Distinct().ToList();
        var (npc, _) = (
            from candidate in world.Npcs
            where candidate.IsAlive && candidate.Employer is not null
                && world.LifeStageRules.LifeStageOf(candidate.AgeYears(world.CurrentDate)) == LivingWorld.Domain.Population.Body.LifeStage.Adult
                && NextRoutineIsWork(world, candidate)
            from cell in away
            where cell != candidate.CurrentLocation
            select (candidate, cell)).First(pair =>
        {
            pair.candidate.MoveTo(pair.cell, now);
            return BehaviorDecisionSystem.CommuteLeadHours(world, pair.candidate, ActionType.Work) >= 1;
        });
        long lead = BehaviorDecisionSystem.CommuteLeadHours(world, npc, ActionType.Work);
        int untilWork = RoutineBoundaries.HoursUntilChange(
            world.ActionCatalog, npc.Profession, LivingWorld.Domain.Population.Body.LifeStage.Adult, hour: 3, hoursPerDay: 24);
        Assert.True(untilWork >= 2, $"a rotina muda em {untilWork} h: o teste não distinguiria os dois casos");
        npc.SetHunger(100, now);
        npc.SetThirst(100, now);
        npc.SetSleep(100, now);
        npc.SetSocial(100, now);
        npc.SetCurrentAction(ActionType.Travel, now);

        long Wake(ActionType? purpose)
        {
            npc.SetTravelPurpose(purpose);
            return NpcWakeScheduler.ComputeNextWakeTick(
                npc, world.NeedsRules, world.ActionCatalog, now, world, travelArrivalTick: now + 50);
        }

        Assert.Equal((now + Math.Max(1, untilWork - lead), now + untilWork), (Wake(null), Wake(ActionType.Work)));
    }

    /// <summary>Às 3 h a próxima ação da rotina deste ofício é Work (o lavrador entra às 6 h; o ferreiro às 7 h, depois de
    /// uma hora de Idle): só nesse caso o trajeto até o trabalho antecipa o despertar.</summary>
    private static bool NextRoutineIsWork(WorldState world, Npc npc)
    {
        const LivingWorld.Domain.Population.Body.LifeStage adult = LivingWorld.Domain.Population.Body.LifeStage.Adult;
        int until = RoutineBoundaries.HoursUntilChange(world.ActionCatalog, npc.Profession, adult, hour: 3, hoursPerDay: 24);
        return RoutineBoundaries.RoutineAt(world.ActionCatalog, npc.Profession, adult, (3 + until) % 24, 24) == ActionType.Work;
    }
}
