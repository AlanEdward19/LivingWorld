using LivingWorld.Api.Visual;
using LivingWorld.Domain;
using LivingWorld.Simulation;

namespace LivingWorld.Tests.Extraordinary;

public sealed class ExtraordinaryVisualProjectionTests
{
    [Fact]
    public void Carrier_projection_exposes_each_extraordinary_axis_without_interpreting_power_names()
    {
        var (world, city, npc) = WorldWithCarrier(isManifested: true);

        var snapshotMarker = Assert.Single(CityProjector.Build(world, city.Id).Value!.Residents);
        var liveMarker = Assert.Single(LivingScopeProjector.Build(
            world, new VisualScope(VisualScopeKind.City, city.Id.ToString())).Npcs);

        Assert.Equal(
            ("power-a|power-b", true, "conditional-active", 1.4, "tint-token", "trail-token",
                "hunger", 9, 3L, 0.25),
            (string.Join('|', snapshotMarker.Extraordinary!.PowerIds), snapshotMarker.Extraordinary.IsManifested,
                snapshotMarker.Extraordinary.ManifestationState, snapshotMarker.Extraordinary.ScaleMultiplier,
                snapshotMarker.Extraordinary.SkinTint, snapshotMarker.Extraordinary.MovementTrail,
                snapshotMarker.Extraordinary.NeedSubstitution!.ReplacesNeed,
                snapshotMarker.Extraordinary.NeedSubstitution.ResourceId,
                snapshotMarker.Extraordinary.NeedSubstitution.UnitsPerUse,
                snapshotMarker.Extraordinary.SenescenceRateMultiplier));
        Assert.Equal(snapshotMarker.Extraordinary, liveMarker.Extraordinary);
        Assert.Equal(npc.Id, liveMarker.Id);
    }

    [Fact]
    public void Npc_without_carrier_keeps_the_optional_extraordinary_projection_absent()
    {
        var (world, city, _) = WorldWithCarrier(isManifested: false);
        var ordinary = NewNpc(new NpcId(2), city.Id, city.Location);
        world.AddNpc(ordinary);

        var marker = Assert.Single(
            CityProjector.Build(world, city.Id).Value!.Residents,
            resident => resident.Id == ordinary.Id);

        Assert.Null(marker.Extraordinary);
    }

    private static (WorldState World, City City, Npc Npc) WorldWithCarrier(bool isManifested)
    {
        var carrier = new ExtraordinaryCarrierState(
            new NpcId(1), ["power-a", "power-b"], isManifested, "conditional-active",
            new ExtraordinaryAppearanceState(1.4, "tint-token", "trail-token"),
            new NeedSubstitutionDescriptor("hunger", new ResourceType(9), 3), 0.25);
        var world = new WorldState(
            ScenarioRunner.DefaultCalendar, 77, ScenarioRunner.DefaultMap(77),
            ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
            ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog,
            ScenarioRunner.DefaultLifeStageRules,
            extraordinary: new ExtraordinaryScenarioData(true, []), extraordinaryCarriers: [carrier]);
        var city = new City(
            world.NextCityId(), ScenarioRunner.DefaultVillageLocation, 0, null, AggregatePopulationPool.Empty);
        world.AddCity(city);
        var npc = NewNpc(carrier.CarrierId, city.Id, city.Location);
        world.AddNpc(npc);
        return (world, city, npc);
    }

    private static Npc NewNpc(NpcId id, CityId city, CellCoord location) => new(
        id, $"npc-{id.Value}", Sex.Male,
        WorldDate.Epoch(ScenarioRunner.DefaultCalendar).AddYears(-30), ScenarioRunner.DefaultCulture,
        location, motherId: null, fatherId: null, household: null, health: 100,
        personality: Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!,
        profession: ProfessionType.None, city: city, currentLocation: location);
}
