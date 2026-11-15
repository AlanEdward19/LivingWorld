using LivingWorld.Domain.Extraordinary;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Body;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Extraordinary.Inheritance;
using LivingWorld.Simulation.Extraordinary.Systems;
using LivingWorld.Simulation.Population.Lifecycle;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Tests.Unit.Extraordinary;

/// <summary>AD-156: o nascimento herda alelos (não poderes prontos), o poder chega na idade de início e custa fome e sede.</summary>
public sealed class PowerInheritanceNatalityTests
{
    private static readonly WorldCalendar Calendar = new(24, 30, 12);
    private static readonly PowerGenetics Certain = new(Penetrance: 1, MutationRate: 0);

    [Fact]
    public void Birth_from_a_homozygous_dominant_mother_inherits_the_allele_and_schedules_the_onset_without_granting_the_power()
    {
        var scene = Couple(motherGenome: 2, fatherGenome: 0);

        var baby = TriggerBirth(scene);

        var inherited = Assert.Single(scene.Sink.Events, e => e.Kind == WorldEventKind.PowerInherited);
        var onset = Assert.Single(scene.World.Scheduler.Snapshot(), e => e.Payload == $"onset|{baby.Id.Value}|power-a");
        double onsetYears = (onset.TargetTick - baby.BirthDate.TotalHours) / (double)Calendar.HoursPerYear;
        Assert.Equal((1, $"{baby.Id.Value}|1|2|genetic|power-a", false),
            (PowerGenome.Copies(baby.PowerGenome, 0), inherited.Payload, scene.World.CarrierOf(baby.Id) is not null));
        Assert.InRange(onsetYears, 13 - 3.5 * 1.5, 13 + 3.5 * 1.5);
    }

    [Fact]
    public void Onset_event_grants_the_power_as_a_genetic_acquisition_and_raises_hunger_and_thirst()
    {
        var scene = Couple(motherGenome: 2, fatherGenome: 0);
        var baby = TriggerBirth(scene);
        var onset = Assert.Single(scene.World.Scheduler.Snapshot(), e => e.Payload!.StartsWith("onset|"));

        new ExtraordinaryStateSystem().HandleEvent(scene.World, scene.Ctx, onset);

        var acquired = Assert.Single(scene.Sink.Events, e => e.Kind == WorldEventKind.ExtraordinaryAcquired);
        var needs = scene.World.NeedsRules;
        Assert.Equal(($"{baby.Id.Value}|power-a|genetic:onset", needs.ThirstDecayPerHour * Certain.ThirstMultiplier),
            (acquired.Payload, baby.ThirstNeed.DecayRatePerTick));
        Assert.Equal(needs.HungerDecayPerHour * Certain.HungerMultiplier, baby.HungerNeed.DecayRatePerTick, 12);
    }

    [Fact]
    public void Powers_acquired_by_event_are_not_inherited_only_alleles_are()
    {
        var scene = Couple(motherGenome: 0, fatherGenome: 0);
        scene.World.UpsertExtraordinaryCarrier(Carrier(scene.Mother.Id));
        scene.World.UpsertExtraordinaryCarrier(Carrier(scene.Father.Id));

        var baby = TriggerBirth(scene);

        Assert.Equal((0UL, false), (baby.PowerGenome, scene.Sink.Events.Any(e => e.Kind == WorldEventKind.PowerInherited)));
    }

    [Fact]
    public void Same_seed_gives_the_same_genotype_and_record()
    {
        var first = Couple(motherGenome: 1, fatherGenome: 1, seed: 77);
        var second = Couple(motherGenome: 1, fatherGenome: 1, seed: 77);

        var a = TriggerBirth(first);
        var b = TriggerBirth(second);

        Assert.Equal(a.PowerGenome, b.PowerGenome);
        Assert.Equal(WorldSnapshot.CanonicalHash(first.World), WorldSnapshot.CanonicalHash(second.World));
    }

    [Fact]
    public void Manifested_power_lowers_fertility_by_the_declared_factor()
    {
        var scene = Couple(motherGenome: 0, fatherGenome: 0);
        scene.World.UpsertExtraordinaryCarrier(Carrier(scene.Mother.Id));

        Assert.Equal((Certain.FertilityMultiplier, 1.0),
            (PowerGeneticsMechanic.FertilityMultiplier(scene.World, scene.Mother), PowerGeneticsMechanic.FertilityMultiplier(scene.World, scene.Father)));
    }

    [Fact]
    public void Birth_without_powers_in_the_world_draws_no_genotype()
    {
        var scene = Couple(motherGenome: 2, fatherGenome: 2, enabled: false);

        var baby = TriggerBirth(scene);

        Assert.Equal((0UL, 0), (baby.PowerGenome, scene.World.Scheduler.Snapshot().Count(e => e.Payload?.StartsWith("onset|") == true)));
    }

    internal static Npc BornAdult(NpcId id, Sex sex, ulong genome)
    {
        var npc = new Npc(
            id, $"npc-{id.Value}", sex, WorldDate.Epoch(Calendar).AddYears(-20),
            ScenarioRunner.DefaultCulture, new CellCoord(0, 0), motherId: null, fatherId: null,
            household: new HouseholdId(1), health: 100,
            personality: Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!,
            profession: ProfessionType.None, currentLocation: new CellCoord(0, 0));
        npc.SetGestationAtBirth(280); // nascido no mundo: o genótipo guardado vale (não o sorteio de fundador)
        npc.SetPowerGenome(genome);
        return npc;
    }

    private static CoupleScene Couple(ulong motherGenome, ulong fatherGenome, ulong seed = 42, bool enabled = true)
    {
        var mother = BornAdult(new NpcId(1), Sex.Female, motherGenome);
        var father = BornAdult(new NpcId(2), Sex.Male, fatherGenome);
        mother.Marry(father.Id);
        father.Marry(mother.Id);
        var household = new Household(new HouseholdId(1), new CellCoord(0, 0), mother.Id, [mother.Id, father.Id],
            new Dictionary<ResourceType, long>());
        var table = LifeTable.Create(90, [new LifeTableBracket(0, 89, 0.01)]).Value!;
        var conception = PopulationRules.Create(table, fertilityMinAge: 16, fertilityMaxAge: 45, annualConceptionChance: 1.0,
            gestationDays: 10).Value!;
        var power = new PowerDescriptor("power-a", "genetica", ["npc.health:1"], "Active", [], "Guaranteed", [], [], [], []);
        var world = new WorldState(
            Calendar, seed, ScenarioRunner.DefaultMap(seed), ScenarioRunner.DefaultPopulationCatalog, conception,
            ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog, ScenarioRunner.DefaultLifeStageRules,
            familyRules: PermissiveFamilyRules(),
            extraordinary: new ExtraordinaryScenarioData(enabled, [power], inheritanceRules: new PowerInheritanceRules(Certain)));
        world.AddNpc(mother);
        world.AddNpc(father);
        world.AddHousehold(household);
        world.AdvanceNpcIdTo(3);
        world.AdvanceHouseholdIdTo(2);
        world.CurrentDate = WorldDate.Epoch(Calendar).AddYears(20);
        var sink = new RecordingSink();
        return new CoupleScene(world, new TickContext(world, world.Rng, world.Scheduler, sink), mother, father, household, sink);
    }

    private static Npc TriggerBirth(CoupleScene scene)
    {
        var evt = scene.Ctx.ScheduleEvent(scene.World.CurrentDate.TotalHours, NatalitySystem.SystemName,
            $"{scene.Mother.Id.Value}|{scene.Father.Id.Value}|{scene.Household.Id.Value}|100");
        new NatalitySystem().HandleEvent(scene.World, scene.Ctx, evt);
        scene.World.Scheduler.Cancel(evt.Id);
        return Assert.Single(scene.World.Npcs, n => n.Id != scene.Mother.Id && n.Id != scene.Father.Id);
    }

    private static FamilyRules PermissiveFamilyRules()
    {
        var deltas = new Dictionary<(RelationshipEventType, RelationshipAxis), double>();
        foreach (var type in Enum.GetValues<RelationshipEventType>())
            foreach (var axis in Enum.GetValues<RelationshipAxis>())
                deltas[(type, axis)] = 0;
        return FamilyRules.Create(
            relationshipDeltas: deltas, decayPerDay: 0, contactLossThresholdDays: 30, neutralAxisValue: 50,
            attractionWeights: new Dictionary<AttractionFactor, double>(), courtshipThreshold: 0.5, courtshipDurationDays: 10,
            marriageInitialStock: new Dictionary<int, long>(), conceptionHealthFloor: 0, conceptionRelationshipFloor: 0,
            conceptionResourceFloor: new Dictionary<int, long>(), maternalDeathRisk: 0, infantDeathRisk: 0,
            vitalityMotherWeight: 0.5, vitalityFatherWeight: 0.5, vitalityMutationStdDev: 0, vitalityMortalityWeight: 0,
            upbringingWealthWeight: 0.2, environmentalWealthChannelEnabled: false, neutralDriftEnabled: false,
            vitalityMortalitySelectionEnabled: true).Value!;
    }

    private static ExtraordinaryCarrierState Carrier(NpcId id) =>
        new(id, ["power-a"], true, "active", new ExtraordinaryAppearanceState(1, "", ""), null, 1);

    private sealed record CoupleScene(WorldState World, TickContext Ctx, Npc Mother, Npc Father, Household Household, RecordingSink Sink);

    private sealed class RecordingSink : IWorldEventSink
    {
        public List<WorldEvent> Events { get; } = [];
        public void Record(WorldEvent evt) => Events.Add(evt);
    }
}
