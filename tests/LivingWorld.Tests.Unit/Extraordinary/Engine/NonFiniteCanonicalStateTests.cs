using LivingWorld.Domain.Extraordinary;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Extraordinary.Engine;
using LivingWorld.Simulation.Extraordinary.Mechanics;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Tests.Unit.Extraordinary.Engine;

/// <summary>ADR-0040: nenhum valor não finito entra em estado canônico. JSON não representa infinito nem NaN,
/// então um valor desses derruba <see cref="WorldSnapshot.CanonicalHash"/> dali em diante.</summary>
public sealed class NonFiniteCanonicalStateTests
{
    [Theory]
    [InlineData("gravity.target:Infinity")]
    [InlineData("gravity.target:1e999")]
    [InlineData("environment.temperature:0:Infinity:10")]
    [InlineData("environment.temperature:0:-Infinity:10")]
    [InlineData("environment.temperature:0:NaN:10")]
    [InlineData("environment.temperature:0:1e39:10")]
    [InlineData("skill.learn-rate:Infinity")]
    [InlineData("skill.learn-rate:1e999")]
    public void Non_finite_value_is_refused_on_load_and_invocation_and_the_world_keeps_hashing(string token)
    {
        var (world, carrier, target) = WorldWith(token);

        var result = Invoke(world, carrier, target);

        Assert.False(ExtraordinaryPrimitiveCatalog.Validate(
            ExtraordinaryMechanicRegistry.Default, token, ExtraordinaryMechanicKind.Effect).IsSuccess);
        Assert.False(result.IsSuccess);
        Assert.NotEmpty(WorldSnapshot.CanonicalHash(world));
    }

    [Theory]
    [InlineData("gravity.target:1e308")]
    [InlineData("gravity.target:0")]
    [InlineData("environment.temperature:0:-5.5:10")]
    [InlineData("skill.learn-rate:1e308")]
    public void Finite_values_keep_loading_applying_and_hashing(string token)
    {
        var (world, carrier, target) = WorldWith(token);

        var result = Invoke(world, carrier, target);

        Assert.True(ExtraordinaryPrimitiveCatalog.Validate(
            ExtraordinaryMechanicRegistry.Default, token, ExtraordinaryMechanicKind.Effect).IsSuccess);
        Assert.True(result.IsSuccess, result.Error);
        Assert.NotEmpty(WorldSnapshot.CanonicalHash(world));
    }

    [Theory]
    [InlineData("movement.flight:Infinity")]
    [InlineData("movement.speed-multiplier:Infinity")]
    [InlineData("gravity.self:Infinity")]
    public void Infinity_kept_on_purpose_only_feeds_the_volatile_profile_and_never_the_canonical_state(string token)
    {
        var (world, carrier, _) = WorldWith(token);
        string before = WorldSnapshot.CanonicalHash(world);

        var profile = ExtraordinaryLocomotion.Resolve(world, carrier);

        Assert.True(ExtraordinaryPrimitiveCatalog.Validate(
            ExtraordinaryMechanicRegistry.Default, token, ExtraordinaryMechanicKind.Effect).IsSuccess);
        Assert.True(profile.HasModifier);
        Assert.Equal(before, WorldSnapshot.CanonicalHash(world));
    }

    private static Result<ExtraordinaryInvocationResult> Invoke(WorldState world, Npc carrier, Npc target) =>
        ExtraordinaryInvocationEngine.Invoke(
            world, new TickContext(world, world.Rng, world.Scheduler),
            new ExtraordinaryInvocation(1, carrier.Id, "p", target.Id));

    private static (WorldState World, Npc Carrier, Npc Target) WorldWith(string effect)
    {
        var descriptor = new PowerDescriptor("p", "s", [effect], "Active", [], "Guaranteed", [], [], [], []);
        var state = new ExtraordinaryCarrierState(
            new NpcId(1), [descriptor.Id], true, "manifested",
            new ExtraordinaryAppearanceState(1, "", ""), null, 1);
        var world = new WorldState(
            ScenarioRunner.DefaultCalendar, 42, ScenarioRunner.DefaultMap(42),
            ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
            ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog,
            ScenarioRunner.DefaultLifeStageRules,
            extraordinary: new ExtraordinaryScenarioData(true, [descriptor]), extraordinaryCarriers: [state]);
        var carrier = NewNpc(new NpcId(1));
        var target = NewNpc(new NpcId(2));
        world.AddNpc(carrier);
        world.AddNpc(target);
        return (world, carrier, target);
    }

    private static Npc NewNpc(NpcId id) => new(
        id, "npc" + id.Value, Sex.Male, WorldDate.Epoch(ScenarioRunner.DefaultCalendar),
        ScenarioRunner.DefaultCulture, new CellCoord(0, 0), null, null, null, 90,
        Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!,
        ProfessionType.None, currentLocation: new CellCoord(0, 0));
}
