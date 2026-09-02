using LivingWorld.Domain;
using LivingWorld.Simulation;

namespace LivingWorld.Tests.Extraordinary;

public sealed class FertilityMechanicTests
{
    private static readonly WorldCalendar Calendar = new(24, 30, 12);

    private static readonly LifeTable Table = LifeTable.Create(90,
    [
        new LifeTableBracket(0, 89, 0.01),
    ]).Value!;

    private static readonly PopulationRules CertainConception = PopulationRules.Create(
        Table, fertilityMinAge: 16, fertilityMaxAge: 45, annualConceptionChance: 1.0, gestationDays: 10).Value!;

    private static readonly Personality SomePersonality =
        Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!;

    [Fact]
    public void Fertility_zero_never_conceives_across_yearly_ticks_for_an_otherwise_certain_couple()
    {
        var (world, ctx, mother, _) = CoupleWithFertility("attribute.fertility:0", manifested: true, onMother: true);

        for (int year = 0; year < 20; year++)
            new NatalitySystem().Tick(world, ctx);

        Assert.Null(mother.PregnantUntil);
        Assert.Empty(world.Scheduler.Snapshot());
    }

    [Fact]
    public void Control_couple_without_fertility_token_conceives_at_the_base_certain_rate()
    {
        var (world, ctx, mother, _) = CoupleWithFertility(effect: null, manifested: true, onMother: true);

        new NatalitySystem().Tick(world, ctx);

        Assert.NotNull(mother.PregnantUntil);
    }

    [Fact]
    public void Fertility_multiplier_scales_the_npc_rate_while_manifested()
    {
        var (world, mother, father) = CoupleWorld("attribute.fertility:2.5", manifested: true, onMother: true);

        Assert.Equal(2.5, AttributeMechanic.FertilityMultiplier(world, mother));
        Assert.Equal(1.0, AttributeMechanic.FertilityMultiplier(world, father));
    }

    [Fact]
    public void Father_fertility_zero_also_blocks_couple_conception()
    {
        var (world, ctx, mother, _) = CoupleWithFertility("attribute.fertility:0", manifested: true, onMother: false);

        new NatalitySystem().Tick(world, ctx);

        Assert.Null(mother.PregnantUntil);
    }

    [Fact]
    public void Ceasing_the_power_restores_the_base_conception_rate_with_no_residue()
    {
        var (world, ctx, mother, father) = CoupleWithFertility(
            "attribute.fertility:0", manifested: true, onMother: true);

        new NatalitySystem().Tick(world, ctx);
        Assert.Null(mother.PregnantUntil);

        world.UpsertExtraordinaryCarrier(Carrier(mother.Id, manifested: false));
        Assert.Equal(1.0, AttributeMechanic.FertilityMultiplier(world, mother));
        Assert.Equal(1.0, AttributeMechanic.FertilityMultiplier(world, father));

        new NatalitySystem().Tick(world, ctx);
        Assert.NotNull(mother.PregnantUntil);
    }

    [Fact]
    public void Unknown_attribute_keys_are_unsupported_on_invocation()
    {
        var mechanic = new AttributeMechanic();
        var (world, _, mother, father) = CoupleWithFertility(effect: null, manifested: true, onMother: true);
        var ctx = MechanicContext(world, mother, father);

        var prepared = mechanic.PrepareEffect(ctx, "attribute.unknown:2");

        Assert.False(prepared.IsSuccess);
        Assert.Equal("Effects: alvo não suportado 'attribute.unknown'", prepared.Error);
    }

    [Fact]
    public void Fertility_prepare_effect_skips_mutation_like_movement()
    {
        var mechanic = new AttributeMechanic();
        var (world, _, mother, father) = CoupleWithFertility(
            "attribute.fertility:0", manifested: true, onMother: true);
        var ctx = MechanicContext(world, mother, father);

        var prepared = mechanic.PrepareEffect(ctx, "attribute.fertility:0");

        Assert.True(prepared.IsSuccess);
        Assert.Null(prepared.Value);
    }

    private static ExtraordinaryMechanicContext MechanicContext(WorldState world, Npc carrier, Npc target)
    {
        var tick = new TickContext(world, world.Rng, world.Scheduler);
        var invocation = new ExtraordinaryInvocation(1, carrier.Id, "fertility", target.Id);
        return new ExtraordinaryMechanicContext(
            world, tick, invocation, carrier, target, ExtraordinaryMechanicKind.Effect);
    }

    private static (WorldState World, TickContext Ctx, Npc Mother, Npc Father) CoupleWithFertility(
        string? effect, bool manifested, bool onMother)
    {
        var (world, mother, father) = CoupleWorld(effect, manifested, onMother);
        return (world, new TickContext(world, world.Rng, world.Scheduler), mother, father);
    }

    private static (WorldState World, Npc Mother, Npc Father) CoupleWorld(
        string? effect, bool manifested, bool onMother)
    {
        var location = new CellCoord(1, 1);
        var mother = new Npc(
            new NpcId(1), "mother", Sex.Female, WorldDate.Epoch(Calendar).AddYears(-20), new CultureId(1), location,
            motherId: null, fatherId: null, household: null, health: 100,
            personality: SomePersonality, profession: ProfessionType.None, currentLocation: location,
            vitality: 60, upbringing: 40);
        var father = new Npc(
            new NpcId(2), "father", Sex.Male, WorldDate.Epoch(Calendar).AddYears(-22), new CultureId(1), location,
            motherId: null, fatherId: null, household: null, health: 100,
            personality: SomePersonality, profession: ProfessionType.None, currentLocation: location,
            vitality: 70, upbringing: 45);

        mother.Marry(father.Id);
        father.Marry(mother.Id);

        var household = new Household(new HouseholdId(1), location, mother.Id, [mother.Id, father.Id]);
        mother.JoinHousehold(household.Id);
        father.JoinHousehold(household.Id);
        household.Deposit(new ResourceType(1), 100);

        PowerDescriptor[] descriptors = [];
        ExtraordinaryCarrierState[] carriers = [];
        if (effect is not null)
        {
            var descriptor = new PowerDescriptor(
                "fertility", "test", [effect], "Passive", [], "Guaranteed", [], [], [], []);
            descriptors = [descriptor];
            var hostId = onMother ? mother.Id : father.Id;
            carriers = [Carrier(hostId, manifested)];
        }

        var world = new WorldState(
            Calendar, 42, ScenarioRunner.DefaultMap(42),
            ScenarioRunner.DefaultPopulationCatalog, CertainConception,
            ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog,
            ScenarioRunner.DefaultLifeStageRules,
            familyRules: PermissiveFamilyRules(),
            extraordinary: new ExtraordinaryScenarioData(true, descriptors),
            extraordinaryCarriers: carriers);

        world.AddNpc(mother);
        world.AddNpc(father);
        world.AddHousehold(household);
        world.AdvanceNpcIdTo(3);
        world.AdvanceHouseholdIdTo(2);
        world.CurrentDate = WorldDate.Epoch(Calendar).AddYears(20);
        return (world, mother, father);
    }

    private static ExtraordinaryCarrierState Carrier(NpcId npcId, bool manifested) =>
        new(npcId, ["fertility"], manifested, manifested ? "manifested" : "dormant",
            new ExtraordinaryAppearanceState(1, "", "trail"), null, 1);

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
}
