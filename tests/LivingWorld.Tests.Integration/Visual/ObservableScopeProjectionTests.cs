using LivingWorld.Api.Visual.Observable;
using LivingWorld.Api.Visual.Scope;
using LivingWorld.Domain.Cognition;
using LivingWorld.Domain.Economy;
using LivingWorld.Domain.Fauna;
using LivingWorld.Domain.Health;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Invention;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Body;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Tests.Integration.Visual;

/// <summary>AD-170 (ADR-0047 etapa 2): a projeção do observável por escopo. Cada teste monta o mundo canônico, mexe só no que
/// quer ver e confere o snapshot do escopo e o delta.</summary>
public class ObservableScopeProjectionTests
{
    private static (WorldState World, VisualScope City, Npc Npc) CityWorld(ulong seed = 42)
    {
        var (world, _) = ScenarioRunner.Create(seed, initialPopulation: 20);
        var city = world.ActiveCities().First();
        var scope = new VisualScope(VisualScopeKind.City, city.Id.ToString());
        var inside = LivingScopeProjector.Build(world, scope).Npcs.First();
        return (world, scope, world.FindNpc(inside.Id)!);
    }

    private static NpcObservable ObservableOf(WorldState world, VisualScope scope, NpcId id) =>
        LivingScopeProjector.Build(world, scope).Npcs.Single(n => n.Id == id).Observable!;

    [Fact]
    public void Npc_in_city_scope_carries_name_sex_age_stage_and_profession()
    {
        var (world, scope, npc) = CityWorld();

        var seen = ObservableOf(world, scope, npc.Id);

        Assert.Equal(
            (npc.Name, npc.Sex.ToString(), npc.AgeYears(world.CurrentDate), world.LifeStageRules.LifeStageOf(npc.AgeYears(world.CurrentDate)).ToString(), npc.Profession.Id),
            (seen.Name, seen.Sex, seen.AgeYears, seen.LifeStage, seen.ProfessionId));
    }

    [Fact]
    public void Height_is_projected_in_centimetres_from_the_body()
    {
        var (world, scope, npc) = CityWorld();

        Assert.Equal((int)Math.Round(npc.Height * 100), ObservableOf(world, scope, npc.Id).HeightCm);
    }

    [Fact]
    public void Pregnancy_shows_only_after_the_village_would_notice_it()
    {
        var (world, scope, npc) = CityWorld();
        long tick = world.CurrentDate.TotalHours;
        int hpd = world.Calendar.HoursPerDay;
        int shows = world.FamilyRules.Unions.Intimacy.ShowsAfterDays;
        npc.SetReproduction(new ReproductiveState(Pregnancy: new PregnancyState(tick - (shows - 1L) * hpd, tick + 200L * hpd, 1, "F", 270, false)));
        var hidden = ObservableOf(world, scope, npc.Id).Pregnancy;

        npc.SetReproduction(new ReproductiveState(Pregnancy: new PregnancyState(tick - (shows + 1L) * hpd, tick + 100L * hpd, 1, "FM", 270, false)));
        var visible = ObservableOf(world, scope, npc.Id).Pregnancy;

        Assert.Equal((null, 2), (hidden, visible?.Babies));
    }

    [Fact]
    public void Active_infection_marks_the_npc_sick_and_bedridden()
    {
        var (world, scope, npc) = CityWorld();
        world.AddInfection(new Infection(npc.Id, 3, world.CurrentDate.TotalHours + 100) { Symptomatic = true, Bedridden = true });

        Assert.Equal(new IllnessVisual(3, true, true), ObservableOf(world, scope, npc.Id).Illness);
    }

    [Fact]
    public void Combat_reaction_in_course_is_projected_and_omitted_when_over_or_absent()
    {
        var (world, scope, npc) = CityWorld();
        long now = world.CurrentDate.TotalMilliseconds;
        Assert.Null(ObservableOf(world, scope, npc.Id).Combat);

        npc.SetCombat(new CombatState { Reaction = CombatReaction.Flee, ReactionUntilMs = now + 2000 });
        Assert.Equal("flee", ObservableOf(world, scope, npc.Id).Combat);

        npc.SetCombat(new CombatState { Reaction = CombatReaction.CallHelp, ReactionUntilMs = now + 2000 });
        Assert.Equal("callHelp", ObservableOf(world, scope, npc.Id).Combat);

        npc.SetCombat(new CombatState { Reaction = CombatReaction.Retaliate, ReactionUntilMs = now - 1 });
        Assert.Null(ObservableOf(world, scope, npc.Id).Combat); // a reação passou: omitida (campo opcional)
    }

    [Fact]
    public void Strong_grief_is_mourning_and_the_dominant_emotion()
    {
        var (world, scope, npc) = CityWorld();
        long tick = world.CurrentDate.TotalHours;
        npc.SetPsyche(new PsycheState([], AffectTick: tick, CalmFromTick: tick + 100_000, Grief: 800, Fear: 100));

        var seen = ObservableOf(world, scope, npc.Id);

        Assert.Equal((true, "grief"), (seen.Mourning, seen.DominantEmotion));
    }

    [Fact]
    public void Personal_load_and_held_container_are_projected()
    {
        var (world, scope, npc) = CityWorld();
        if (world.Invention is null) world.UseInvention(InventionRules.Default);
        world.Invention!.Carried.Add(new CarriedLoad(npc.Id.Value, 1, 3));
        world.Invention.Items.Add(new CraftedItem(9, 77, npc.Household!.Value.Value, npc.City, 0, long.MaxValue, 900));
        world.Invention.Held = [new HeldItem(npc.Id.Value, 9)];

        var seen = ObservableOf(world, scope, npc.Id);

        Assert.Equal((1, 3, 77), (seen.Carried.Single().ResourceId, seen.Carried.Single().Quantity, seen.Held!.DesignId));
    }

    [Fact]
    public void Unchanged_world_yields_no_npc_upserts_in_the_delta()
    {
        var (world, scope, _) = CityWorld();
        var before = LivingScopeProjector.Build(world, scope);

        var delta = ScopeDeltaBuilder.Diff(1, before, LivingScopeProjector.Build(world, scope));

        Assert.Empty(delta.NpcUpserts);
    }

    [Fact]
    public void A_visible_change_upserts_only_that_npc_with_its_new_observable()
    {
        var (world, scope, npc) = CityWorld();
        var before = LivingScopeProjector.Build(world, scope);
        world.AddInfection(new Infection(npc.Id, 3, world.CurrentDate.TotalHours + 100) { Bedridden = true });

        var delta = ScopeDeltaBuilder.Diff(1, before, LivingScopeProjector.Build(world, scope));

        Assert.True(Assert.Single(delta.NpcUpserts).Observable!.Illness!.Bedridden);
    }

    [Fact]
    public void World_scope_carries_the_density_of_each_city_without_its_residents()
    {
        var (world, cityScope, _) = CityWorld();
        int inside = LivingScopeProjector.Build(world, cityScope).Npcs.Count;

        var city = LivingScopeProjector.Build(world, new VisualScope(VisualScopeKind.World, "")).Cities.First();

        Assert.Equal(inside, city.Density!.Inside);
    }

    [Fact]
    public void City_buildings_come_with_full_plan_and_floors()
    {
        var (world, scope, _) = CityWorld();

        var buildings = LivingScopeProjector.Build(world, scope).Buildings;

        Assert.All(buildings, b => Assert.True(b.Floors >= 1 && b.GroundFootprint.Count > 0));
    }

    [Fact]
    public void Building_state_lists_the_members_of_households_living_there()
    {
        var (world, scope, _) = CityWorld();
        var state = LivingScopeProjector.Build(world, scope);
        var expected = world.Households.Where(h => h.Dwelling is { } d && state.Buildings.Any(b => b.Id == d))
            .SelectMany(h => h.Members).Select(m => m.Value).OrderBy(v => v);

        Assert.Equal(expected, state.BuildingStates.SelectMany(b => b.ResidentIds).OrderBy(v => v));
    }

    [Fact]
    public void Crop_in_scope_shows_stage_and_a_ripened_field_upserts_as_mature()
    {
        var (world, scope, _) = CityWorld();
        var city = world.ActiveCities().First();
        var crop = new CropBatch(new CropBatchId(world.NextCropBatchIdAndAdvance().Value), 1, city.Location,
            world.CurrentDate.TotalHours, long.MaxValue, 0, lastSeedSown: 100, effectiveSeedMilli: 100_000, development: 200);
        world.AddCropBatch(crop);
        var before = LivingScopeProjector.Build(world, scope);

        crop.Ripen(world.CurrentDate.TotalHours, 380);
        var delta = ScopeDeltaBuilder.Diff(1, before, LivingScopeProjector.Build(world, scope));

        Assert.Equal(("mature", 380L), (Assert.Single(delta.CropUpserts, c => c.Id == crop.Id.Value).Stage, delta.CropUpserts.Single(c => c.Id == crop.Id.Value).ExpectedHarvest));
    }

    [Fact]
    public void Scope_climate_carries_the_season_of_the_month()
    {
        var (world, scope, _) = CityWorld();

        var climate = LivingScopeProjector.Build(world, scope).Climate!;

        Assert.Equal(world.CurrentDate.Month / (world.Calendar.MonthsPerYear / 4), climate.SeasonIndex);
    }

    [Fact]
    public void Animal_inside_the_city_is_individual_there_and_grouped_in_the_world()
    {
        var (world, scope, _) = CityWorld();
        var city = world.ActiveCities().First();
        world.AddAnimal(new Animal(new AnimalId(world.NextAnimalId), "wolf", city.Location, true, null, LazyNeed.Initial(80, 0, 0)));

        var inCity = LivingScopeProjector.Build(world, scope).Animals;
        var groups = LivingScopeProjector.Build(world, new VisualScope(VisualScopeKind.World, "")).FaunaGroups;

        Assert.Equal((1, 1), (inCity.Count(a => a.Species == "wolf"), groups.Where(g => g.Species == "wolf").Sum(g => g.Count)));
    }

    [Fact]
    public void Leaving_on_the_ground_appears_in_the_scope_where_it_lies()
    {
        var (world, scope, npc) = CityWorld();
        if (world.Invention is null) world.UseInvention(InventionRules.Default);
        var city = world.ActiveCities().First();
        world.Invention!.PutLeaving(new Leaving(5, city.Location.X, city.Location.Y, -1, -1, npc.Id.Value, 0));
        world.Invention.LeftLoads = [new LeftLoad(5, 1, 4)];

        var leaving = Assert.Single(LivingScopeProjector.Build(world, scope).Leavings);

        Assert.Equal((5L, 4L, (long?)null), (leaving.Id, leaving.Units, leaving.HouseholdId));
    }

    [Fact]
    public void Invented_object_of_the_city_shows_shape_and_material_of_its_design()
    {
        var (world, scope, npc) = CityWorld();
        if (world.Invention is null) world.UseInvention(InventionRules.Default);
        var part = new DesignPart(PartShape.Cylinder, 300, 200, 200, 10, 4, 0, JointKind.Fixed, -1, 0, 0, 0);
        var design = Design.Create([part], npc.Id.Value, 0, 0, 0, DesignOrigin.Invented);
        world.Invention!.Designs.Add(design);
        world.Invention.Items.Add(new CraftedItem(11, design.Id, npc.Household!.Value.Value, npc.City, 0, long.MaxValue, 500));

        var item = Assert.Single(LivingScopeProjector.Build(world, scope).Objects, o => o.Id == 11);

        Assert.Equal(("cylinder", (int?)4), (item.RootShape, item.RootMaterial));
    }

    [Fact]
    public void Delta_replayed_over_the_snapshot_reaches_the_new_observable_state()
    {
        var (world, scope, npc) = CityWorld();
        var before = LivingScopeProjector.Build(world, scope);
        world.AddInfection(new Infection(npc.Id, 3, world.CurrentDate.TotalHours + 100));
        world.AddAnimal(new Animal(new AnimalId(world.NextAnimalId), "deer", world.ActiveCities().First().Location, true, null, LazyNeed.Initial(80, 0, 0)));
        var after = LivingScopeProjector.Build(world, scope);

        Assert.Equal(after, LivingDeltaReducer.Apply(before, ScopeDeltaBuilder.Diff(1, before, after)));
    }

    [Fact]
    public void Projecting_every_scope_leaves_the_canonical_hash_untouched()
    {
        var (world, scope, _) = CityWorld();
        string hash = WorldSnapshot.CanonicalHash(world);

        LivingScopeProjector.Build(world, scope);
        LivingScopeProjector.Build(world, new VisualScope(VisualScopeKind.World, ""));

        Assert.Equal(hash, WorldSnapshot.CanonicalHash(world));
    }
}
