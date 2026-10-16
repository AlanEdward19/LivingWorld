using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Llm;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Behavior.Decision;

namespace LivingWorld.Tests.Unit.Behavior.Decision;

/// <summary>Fase 16.3 T31 (COH-51/52): <see cref="PressureModel.DerivePressures"/>.</summary>
public class PressureModelTests
{
    private static readonly Personality Neutral =
        Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!;

    private static readonly Personality Loyal =
        Personality.Create(50, 50, 50, 50, 50, 50, loyalty: 80, altruism: 70, impulsivity: 50, riskAversion: 50).Value!;

    private static DecisionContext Ctx(
        int hunger = 100,
        int thirst = 100,
        int sleep = 100,
        int social = 100,
        HouseholdSnapshot? household = null,
        IReadOnlyList<NpcMemory>? memories = null,
        IReadOnlyList<string>? beliefs = null,
        IReadOnlyList<RelationshipFact>? relationships = null,
        Personality? personality = null,
        BodySnapshot? body = null) =>
        new(
            new NpcId(1),
            Tick: 10,
            new NeedsSnapshot(hunger, thirst, sleep, social),
            body ?? new BodySnapshot(1.75, 70, 30, 1.0, 1.0),
            household,
            memories ?? [],
            beliefs ?? [],
            relationships ?? [],
            PowerOpportunities: [],
            personality ?? Neutral,
            CurrentAction: null);

    [Fact]
    public void High_hunger_derives_AcquireFood_pressure()
    {
        var pressures = PressureModel.DerivePressures(Ctx(hunger: 20));

        var food = Assert.Single(pressures, p => p.Kind == PressureModel.AcquireFood);
        Assert.Equal(80, food.Intensity);
        Assert.Contains("Hunger", food.Factors);
    }

    [Fact]
    public void ProtectHousehold_combines_at_least_three_factors()
    {
        var household = new HouseholdSnapshot(
            new HouseholdId(1),
            new Dictionary<ResourceType, long>(),
            [new NpcId(1), new NpcId(2), new NpcId(3)]);

        var memory = new NpcMemory(
            1, new NpcId(1), MemoryCategory.Social,
            "perigo na estrada perto de casa", 80, 1,
            [new NpcId(1)], new CellCoord(0, 0));

        var pressures = PressureModel.DerivePressures(Ctx(
            hunger: 100,
            household: household,
            memories: [memory],
            relationships: [new RelationshipFact(new NpcId(2), Trust: 70, Affection: 65, Respect: 60, Familiarity: 40)],
            personality: Loyal,
            body: new BodySnapshot(1.7, 65, 18, 0.9, 1.0)));

        var protect = Assert.Single(pressures, p => p.Kind == PressureModel.ProtectHousehold);
        Assert.True(protect.Factors.Count >= 3,
            $"expected ≥3 factors, got [{string.Join(", ", protect.Factors)}]");
        Assert.Contains("Dependents", protect.Factors);
        Assert.Contains("RelationshipStrength", protect.Factors);
        Assert.Contains("Threat", protect.Factors);
        Assert.True(protect.Intensity > 0);
    }

    [Fact]
    public void No_household_omits_ProtectHousehold()
    {
        var pressures = PressureModel.DerivePressures(Ctx(hunger: 50, household: null));

        Assert.DoesNotContain(pressures, p => p.Kind == PressureModel.ProtectHousehold);
        Assert.Contains(pressures, p => p.Kind == PressureModel.AcquireFood);
    }

    [Fact]
    public void Ambition_and_empty_stock_derive_EarnIncome()
    {
        var ambitious = Personality.Create(50, 50, 50, 50, 50, ambition: 80, 50, 50, 50, 50).Value!;
        var household = new HouseholdSnapshot(
            new HouseholdId(1),
            new Dictionary<ResourceType, long>(),
            [new NpcId(1)]);

        var pressures = PressureModel.DerivePressures(Ctx(
            hunger: 100, household: household, personality: ambitious));

        var earn = Assert.Single(pressures, p => p.Kind == PressureModel.EarnIncome);
        Assert.Contains("Ambition", earn.Factors);
        Assert.Contains("EmptyHouseholdStock", earn.Factors);
    }

    [Fact]
    public void DerivePressures_is_deterministic()
    {
        var household = new HouseholdSnapshot(
            new HouseholdId(1),
            new Dictionary<ResourceType, long> { [new ResourceType(1)] = 2 },
            [new NpcId(1), new NpcId(2)]);
        var ctx = Ctx(hunger: 40, sleep: 30, social: 50, household: household, personality: Loyal);

        var a = PressureModel.DerivePressures(ctx);
        var b = PressureModel.DerivePressures(ctx);

        Assert.Equal(a.Count, b.Count);
        for (int i = 0; i < a.Count; i++)
        {
            Assert.Equal(a[i].Kind, b[i].Kind);
            Assert.Equal(a[i].Intensity, b[i].Intensity);
            Assert.Equal(a[i].Factors, b[i].Factors);
        }
    }
}
