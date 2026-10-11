using LivingWorld.Api.Visual.Catalogs;
using LivingWorld.Api.Visual.Scope;
using LivingWorld.Domain.History;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Tests.Integration.Stage4;

/// <summary>T26 / LWV-07.3: timeline labels for the birth/death family stay audience-safe
/// and match the web animation catalog a11y strings.</summary>
public sealed class LifecycleEventLabelContractTests
{
    private static readonly (WorldEventKind Kind, string Label)[] Lifecycle =
    [
        (WorldEventKind.Birth, "Um novo habitante nasceu"),
        (WorldEventKind.Death, "Um habitante faleceu"),
        (WorldEventKind.Starvation, "A fome causou uma morte"),
        (WorldEventKind.MaternalDeath, "Uma mãe faleceu durante o parto"),
        (WorldEventKind.StillBirth, "Uma gestação terminou sem nascimento vivo"),
    ];

    [Fact]
    public void Lifecycle_kinds_keep_the_audience_safe_timeline_labels()
    {
        foreach (var (kind, label) in Lifecycle)
        {
            Assert.Equal(label, LivingEventPresentationCatalog.Describe(kind));
            Assert.DoesNotContain("sangue", label, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("gore", label, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("cadáver", label, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("blood", label, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void Birth_and_death_visuals_carry_the_actor_cell()
    {
        var (world, _) = ScenarioRunner.Create(seed: 74, initialPopulation: 1);
        var npc = Assert.Single(world.Npcs);
        var cell = npc.CurrentLocation;
        var events = new[]
        {
            new WorldEvent(3, WorldEventKind.Death, npc.Id.Value.ToString()),
            new WorldEvent(4, WorldEventKind.Birth, $"{npc.Id.Value}|0|0|0"),
        };

        var visuals = LivingScopeProjector.Build(world, new VisualScope(VisualScopeKind.World, ""), events).Events;

        Assert.Equal(cell, visuals.Single(evt => evt.Kind == WorldEventKind.Death).Location);
        Assert.Equal(cell, visuals.Single(evt => evt.Kind == WorldEventKind.Birth).Location);
    }
}
