using LivingWorld.Api.Visual.Observable;
using LivingWorld.Api.Visual.Scope;
using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Cities.Buildings;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Cities.Works;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Tests.Integration.Visual;

/// <summary>AD-193: o cliente mostra a obra de verdade. O escopo Cidade projeta cada obra em andamento (canteiro, fração, turma,
/// quem está no lote agora, o que falta, por que parou), o delta só manda a obra que mudou, quem trabalha no lote tem a obra como
/// alvo e a ficha da obra diz quem ergue e para quem — sem mexer no mundo.</summary>
public class WorkSiteProjectionTests
{
    /// <summary>Uma casa despejada abre a obra num lote de rua dentro dos limites da cidade (o escopo Cidade desenha só o que cabe
    /// nele), com as horas e os ajudantes que o motor dá.</summary>
    private static (WorldState World, WorldClock Clock, VisualScope Scope, BuildWork Work) WorldWithHouseWork()
    {
        var (world, clock) = ScenarioRunner.Create(3);
        clock.Run(world, 24);
        var city = world.ActiveCities().First();
        var home = world.Households.First(h => !h.IsEmpty && h.Dwelling is not null);
        var members = home.Members.Select(world.FindNpc).Where(n => n is { IsAlive: true }).Cast<Npc>().ToList();
        home.Evict(world.CurrentDate.TotalHours);
        var site = FreeCellInScope(world, new VisualScope(VisualScopeKind.City, city.Id.ToString()), city.Id);
        var ctx = new TickContext(world, world.Rng, world.Scheduler, null);
        var work = BuildWorks.Open(world, ctx, city, BuildKind.House, -1, site, 0, 0, home.Id.Value, home.Head.Value,
            480, 0, $"{home.Id.Value}|480|0|0");
        Assert.NotEmpty(work.Helpers); // os adultos da casa ajudam sempre
        return (world, clock, new VisualScope(VisualScopeKind.City, city.Id.ToString()), work);
    }

    /// <summary>Uma origem dentro dos limites do escopo onde a planta da casa cabe sem tocar prédio nenhum que o escopo desenha.</summary>
    private static CellCoord FreeCellInScope(WorldState world, VisualScope scope, CityId city)
    {
        var bounds = new ScopeProjectionFrame(world).CityBounds[city];
        var taken = new HashSet<CellCoord>();
        foreach (var b in LivingScopeProjector.Build(world, scope).Buildings)
            foreach (var cell in b.GroundFootprint)
                for (int dx = -1; dx <= 1; dx++)
                    for (int dy = -1; dy <= 1; dy++)
                        taken.Add(new CellCoord(b.Location.X + cell.Cell.X + dx, b.Location.Y + cell.Cell.Y + dy));
        var shape = BuildingFootprintGenerator.Generate(new BuildingId(world.NextBuildingId), -1, 0);
        for (int y = bounds.Origin.Y; y < bounds.Origin.Y + bounds.Height; y++)
            for (int x = bounds.Origin.X; x < bounds.Origin.X + bounds.Width; x++)
                if (shape.All(c => bounds.Contains(new CellCoord(x + c.Cell.X, y + c.Cell.Y)) && !taken.Contains(new CellCoord(x + c.Cell.X, y + c.Cell.Y))))
                    return new CellCoord(x, y);
        throw new InvalidOperationException("sem célula livre no escopo");
    }

    [Fact]
    public void City_scope_carries_the_house_work_with_its_site_crew_and_what_is_left()
    {
        var (world, _, scope, work) = WorldWithHouseWork();

        var state = LivingScopeProjector.Build(world, scope);

        var seen = state.Works.Single(w => w.Id == work.Id);
        Assert.Equal(("casa", new CellCoord(work.X, work.Y), work.Building), (seen.Kind, seen.Location, seen.BuildingId!.Value));
        Assert.Equal(work.Household, seen.HouseholdId);
        Assert.Equal(work.Owner, seen.OwnerId);
        Assert.Equal("casa", seen.Payer);
        Assert.Equal(work.HoursTotal, seen.HoursTotal);
        Assert.Equal(work.HoursLeft, seen.HoursLeft);
        Assert.Equal(work.Helpers, seen.Helpers);
        Assert.Equal(0, seen.Progress);
        // o canteiro está no escopo como prédio com obra (o cliente desenha a obra nele, não a casa pronta)
        Assert.Contains(state.Buildings, b => b.Id.Value == work.Building && b.Work?.Id == work.Id);
        Assert.Empty(LivingScopeProjector.Build(world, new VisualScope(VisualScopeKind.World, "")).Works);
    }

    [Fact]
    public void Whoever_works_on_the_lot_is_on_site_and_has_the_work_as_target()
    {
        var (world, _, scope, work) = WorldWithHouseWork();
        var helper = world.FindNpc(new NpcId(work.Helpers[0]))!;
        helper.MoveTo(new CellCoord(work.X, work.Y), world.CurrentDate.TotalHours);
        helper.SetCurrentAction(ActionType.Work, world.CurrentDate.TotalHours);

        var state = LivingScopeProjector.Build(world, scope);

        Assert.Contains(helper.Id.Value, state.Works.Single(w => w.Id == work.Id).OnSite);
        var visual = state.Npcs.Single(n => n.Id == helper.Id);
        Assert.Equal(("work", work.Id.ToString()), (visual.ActionTarget!.Kind, visual.ActionTarget.Id));
    }

    [Fact]
    public void Delta_sends_only_the_work_that_changed_and_drops_the_finished_one()
    {
        var (world, _, scope, work) = WorldWithHouseWork();
        var before = LivingScopeProjector.Build(world, scope);
        var same = LivingScopeProjector.Build(world, scope);
        Assert.Empty(ScopeDeltaBuilder.Diff(1, before, same).WorkUpserts);

        work.HoursLeft -= 10;
        var progressed = LivingScopeProjector.Build(world, scope);
        var delta = ScopeDeltaBuilder.Diff(2, same, progressed);
        Assert.Equal(work.Id, Assert.Single(delta.WorkUpserts).Id);
        Assert.Equal(progressed.Works, LivingDeltaReducer.Apply(same, delta).Works);

        world.Ground!.Builds.Remove(work);
        var done = LivingScopeProjector.Build(world, scope);
        Assert.Equal([work.Id], ScopeDeltaBuilder.Diff(3, progressed, done).WorkRemoved);
    }

    [Fact]
    public void Work_inspection_names_who_builds_for_whom_and_the_site()
    {
        var (world, _, _, work) = WorldWithHouseWork();

        var sheet = EntityInspectionQuery.Inspect(world, "obra", work.Id.ToString(), PresentationNames.None, null)!;

        Assert.Equal("work", sheet.Kind);
        Assert.StartsWith("obra da casa", sheet.Name);
        var state = Assert.IsType<WorkEntityState>(sheet.State);
        Assert.Equal(work.Id, state.Work.Id);
        Assert.Contains(sheet.Links, l => l.Relation == "household" && l.Id == work.Household.ToString());
        Assert.Contains(sheet.Links, l => l.Relation == "site" && l.Id == work.Building.ToString());
        Assert.Equal(work.Helpers.Count, state.Helpers.Count);
        var canteiro = EntityInspectionQuery.Inspect(world, "building", work.Building.ToString(), PresentationNames.None, null)!;
        Assert.Contains(canteiro.Links, l => l.Kind == "work" && l.Id == work.Id.ToString());
    }

    /// <summary>A obra da fila da cidade conta o material da receita (posto e o total) e para quando o estoque da cidade não tem o
    /// insumo do próximo passo, com horas da turma guardadas.</summary>
    [Fact]
    public void Queued_city_work_shows_its_material_and_stops_for_lack_of_it()
    {
        var timber = new ResourceType(3);
        var recipe = LivingWorld.Domain.Cities.BuildingRecipe.Create(new Dictionary<ResourceType, long> { [timber] = 20 }, 10, 4).Value!;
        var project = new ConstructionProject(new CityId(Guid.Parse("11111111-1111-1111-1111-111111111111")), 1, new Dictionary<ResourceType, long>(), 10)
        { HoursBanked = 3, HaulHoursLeft = 0 };
        project.RecordConsumption(timber, 4);

        var (materials, lacking) = WorkSiteProjector.MaterialOf(recipe, project, new Dictionary<ResourceType, long>(), PresentationNames.None);
        Assert.Equal((3, 20L, 4L), (materials[0].ResourceId, materials[0].Needed, materials[0].Placed));
        Assert.False(lacking); // o 1º passo pede 2 e já há 4 postos
        project.RecordConsumption(timber, -4);
        Assert.True(WorkSiteProjector.MaterialOf(recipe, project, new Dictionary<ResourceType, long>(), PresentationNames.None).Short);
        Assert.False(WorkSiteProjector.MaterialOf(recipe, project, new Dictionary<ResourceType, long> { [timber] = 2 }, PresentationNames.None).Short);
        project.HaulHoursLeft = 5; // ainda carregando: as horas vão ao transporte, não ao estoque
        Assert.False(WorkSiteProjector.MaterialOf(recipe, project, new Dictionary<ResourceType, long>(), PresentationNames.None).Short);
    }

    [Fact]
    public void Projecting_works_leaves_the_canonical_hash_untouched()
    {
        var (world, clock, scope, _) = WorldWithHouseWork();
        clock.Run(world, 30);
        string hash = WorldSnapshot.CanonicalHash(world);

        LivingScopeProjector.Build(world, scope);
        foreach (var work in world.Ground!.Builds) EntityInspectionQuery.Inspect(world, "work", work.Id.ToString(), PresentationNames.None, null);

        Assert.Equal(hash, WorldSnapshot.CanonicalHash(world));
    }
}
