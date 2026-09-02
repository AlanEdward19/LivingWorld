using LivingWorld.Domain;
using LivingWorld.Simulation;

namespace LivingWorld.Tests.Extraordinary;

/// <summary>T11 / EVO-10: hook de herança no NatalitySystem + auditoria PowerInherited.</summary>
public sealed class PowerInheritanceNatalityTests
{
    private static readonly WorldCalendar Calendar = new(24, 30, 12);

    [Fact]
    public void Birth_with_both_parents_carriers_logs_power_inherited_and_assigns_carrier()
    {
        var powerA = Descriptor("power-a", ["npc.health:1"]);
        var powerB = Descriptor("power-b", ["npc.health:2"]);
        var scene = CoupleWithPowers(
            powerA, powerB,
            PowerInheritanceRules.Create(1.0, bothWeight: 1, oneOfWeight: 0, mixedWeight: 0).Value!);

        var baby = TriggerBirth(scene);

        Assert.Contains(scene.Sink.Events, e => e.Kind == WorldEventKind.Birth);
        var inherited = Assert.Single(scene.Sink.Events, e => e.Kind == WorldEventKind.PowerInherited);
        Assert.Equal(
            $"{baby.Id.Value}|{scene.Mother.Id.Value}|{scene.Father.Id.Value}|Both|power-a,power-b",
            inherited.Payload);

        var carrier = Assert.Single(
            scene.World.ExtraordinaryCarriers, c => c.CarrierId == baby.Id);
        Assert.Equal(["power-a", "power-b"], carrier.PowerIds.OrderBy(id => id).ToArray());
    }

    [Fact]
    public void Birth_without_both_parents_carriers_does_not_log_power_inherited()
    {
        var powerA = Descriptor("power-a", ["npc.health:1"]);
        var scene = CoupleWithPowers(
            powerA, parentBPower: null,
            PowerInheritanceRules.Create(1.0, 1, 0, 0).Value!);

        var baby = TriggerBirth(scene);

        Assert.Contains(scene.Sink.Events, e => e.Kind == WorldEventKind.Birth);
        Assert.DoesNotContain(scene.Sink.Events, e => e.Kind == WorldEventKind.PowerInherited);
        Assert.DoesNotContain(scene.World.ExtraordinaryCarriers, c => c.CarrierId == baby.Id);
    }

    [Fact]
    public void Birth_inheritance_is_deterministic_for_same_seed()
    {
        var powerA = Descriptor("power-a", ["npc.health:1"]);
        var powerB = Descriptor("power-b", ["npc.health:2"]);
        var rules = PowerInheritanceRules.Create(1.0, 0, 1, 0).Value!;

        var scene1 = CoupleWithPowers(powerA, powerB, rules, seed: 77);
        var scene2 = CoupleWithPowers(powerA, powerB, rules, seed: 77);
        var baby1 = TriggerBirth(scene1);
        var baby2 = TriggerBirth(scene2);

        var evt1 = Assert.Single(scene1.Sink.Events, e => e.Kind == WorldEventKind.PowerInherited);
        var evt2 = Assert.Single(scene2.Sink.Events, e => e.Kind == WorldEventKind.PowerInherited);
        Assert.Equal(baby1.Id, baby2.Id);
        Assert.Equal(evt1.Payload, evt2.Payload);
    }

    private static PowerDescriptor Descriptor(string id, IReadOnlyList<string> effects) =>
        new(id, "test-source", effects, "Active", [], "Guaranteed", [], [], [], []);

    private static CoupleScene CoupleWithPowers(
        PowerDescriptor parentAPower,
        PowerDescriptor? parentBPower,
        PowerInheritanceRules rules,
        ulong seed = 42)
    {
        var descriptors = parentBPower is null
            ? new List<PowerDescriptor> { parentAPower }
            : [parentAPower, parentBPower];

        var carriers = new List<ExtraordinaryCarrierState>
        {
            Carrier(new NpcId(1), [parentAPower.Id]),
        };
        if (parentBPower is not null)
            carriers.Add(Carrier(new NpcId(2), [parentBPower.Id]));

        var mother = Adult(new NpcId(1), Sex.Female, "mother");
        var father = Adult(new NpcId(2), Sex.Male, "father");
        mother.Marry(father.Id);
        father.Marry(mother.Id);
        var household = new Household(
            new HouseholdId(1), new CellCoord(0, 0), mother.Id, [mother.Id, father.Id],
            new Dictionary<ResourceType, long>());

        var table = LifeTable.Create(90, [new LifeTableBracket(0, 89, 0.01)]).Value!;
        var conception = PopulationRules.Create(
            table, fertilityMinAge: 16, fertilityMaxAge: 45, annualConceptionChance: 1.0,
            gestationDays: 10).Value!;

        var world = new WorldState(
            Calendar, seed, ScenarioRunner.DefaultMap(seed),
            ScenarioRunner.DefaultPopulationCatalog, conception,
            ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog,
            ScenarioRunner.DefaultLifeStageRules,
            familyRules: PermissiveFamilyRules(),
            extraordinary: new ExtraordinaryScenarioData(
                true, descriptors, inheritanceRules: rules),
            extraordinaryCarriers: carriers);

        world.AddNpc(mother);
        world.AddNpc(father);
        world.AddHousehold(household);
        world.AdvanceNpcIdTo(3);
        world.AdvanceHouseholdIdTo(2);
        world.CurrentDate = WorldDate.Epoch(Calendar).AddYears(20);
        var sink = new RecordingSink();
        return new CoupleScene(
            world, new TickContext(world, world.Rng, world.Scheduler, sink),
            mother, father, household, sink);
    }

    private static Npc TriggerBirth(CoupleScene scene)
    {
        var evt = scene.Ctx.ScheduleEvent(
            scene.World.CurrentDate.TotalHours, NatalitySystem.SystemName,
            $"{scene.Mother.Id.Value}|{scene.Father.Id.Value}|{scene.Household.Id.Value}|100");
        new NatalitySystem().HandleEvent(scene.World, scene.Ctx, evt);
        return Assert.Single(
            scene.World.Npcs, n => n.Id != scene.Mother.Id && n.Id != scene.Father.Id);
    }

    private static ExtraordinaryCarrierState Carrier(NpcId id, IReadOnlyList<string> powerIds) =>
        new(id, powerIds, true, "active", new ExtraordinaryAppearanceState(1, "", ""), null, 1);

    private static Npc Adult(NpcId id, Sex sex, string name) => new(
        id, name, sex, WorldDate.Epoch(Calendar).AddYears(-20),
        ScenarioRunner.DefaultCulture, new CellCoord(0, 0), motherId: null, fatherId: null,
        household: new HouseholdId(1), health: 100,
        personality: Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!,
        profession: ProfessionType.None, currentLocation: new CellCoord(0, 0));

    private static FamilyRules PermissiveFamilyRules() =>
        FamilyRules.Create(
            relationshipDeltas: ZeroDeltas(),
            decayPerDay: 0,
            contactLossThresholdDays: 30,
            neutralAxisValue: 50,
            attractionWeights: new Dictionary<AttractionFactor, double>(),
            courtshipThreshold: 0.5,
            courtshipDurationDays: 10,
            marriageInitialStock: new Dictionary<int, long>(),
            conceptionHealthFloor: 0,
            conceptionRelationshipFloor: 0,
            conceptionResourceFloor: new Dictionary<int, long>(),
            maternalDeathRisk: 0,
            infantDeathRisk: 0,
            vitalityMotherWeight: 0.5,
            vitalityFatherWeight: 0.5,
            vitalityMutationStdDev: 0,
            vitalityMortalityWeight: 0,
            upbringingWealthWeight: 0.2,
            environmentalWealthChannelEnabled: false,
            neutralDriftEnabled: false,
            vitalityMortalitySelectionEnabled: true).Value!;

    private static Dictionary<(RelationshipEventType, RelationshipAxis), double> ZeroDeltas()
    {
        var deltas = new Dictionary<(RelationshipEventType, RelationshipAxis), double>();
        foreach (var type in Enum.GetValues<RelationshipEventType>())
            foreach (var axis in Enum.GetValues<RelationshipAxis>())
                deltas[(type, axis)] = 0;
        return deltas;
    }

    private sealed record CoupleScene(
        WorldState World, TickContext Ctx, Npc Mother, Npc Father, Household Household,
        RecordingSink Sink);

    private sealed class RecordingSink : IWorldEventSink
    {
        public List<WorldEvent> Events { get; } = [];
        public void Record(WorldEvent evt) => Events.Add(evt);
    }
}
