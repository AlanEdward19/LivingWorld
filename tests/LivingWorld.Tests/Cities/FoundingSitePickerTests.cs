using LivingWorld.Domain;
using LivingWorld.Simulation;

namespace LivingWorld.Tests.Cities;

/// <summary>Post-ship fix (user-reported, 2026-08-23, "cidades coladas"): <see
/// cref="FoundingSitePicker"/> agora exige o mesmo espaçamento mínimo (<see
/// cref="CityRules.AbsorptionRingCells"/>, distância de Chebyshev) que <c>dynamic-city-growth</c>
/// já garante pro sistema de overflow — não só a checagem de célula exata que existia antes.</summary>
public class FoundingSitePickerTests
{
    private static CityRules MakeRules() => CityRules.Create(
        enabled: true, foodShortageThreshold: 20, housingShortageThreshold: 20, securityShortageThreshold: 20,
        emigrationRatePerDeficitUnit: 0.1, migrationEmploymentWeight: 1, migrationFoodWeight: 1,
        migrationSecurityWeight: 1, migrationFamilyTiesWeight: 1, foundingConcentrationThreshold: 0.5,
        foundingResourceThreshold: 0, foundingRouteThreshold: 0, foundingDefensibilityThreshold: 0,
        foundingLeadershipThreshold: 0, organizationTicks: 10, materializationIdleTicksBeforeEligible: 5)
        .Value!;

    private static WorldState MakeWorld(CityRules rules) => new(
        ScenarioRunner.DefaultCalendar, seed: 23, ScenarioRunner.DefaultMap(23),
        ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
        ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog, ScenarioRunner.DefaultLifeStageRules,
        cityRules: rules);

    private static City MakeCity(WorldState world, CellCoord location) =>
        new(world.NextCityId(), location, foundedAtTick: 0, foundedFromCityId: null,
            aggregatePool: new AggregatePopulationPool(0, 0, 0));

    /// <summary>Mesma métrica que <c>CityBoundsResolver</c>/<c>OverflowClusterFinder</c> já usam —
    /// duplicada aqui (função pura de 6 linhas) só pra o teste poder verificar a garantia sem
    /// depender de nenhum tipo internal.</summary>
    private static int ChebyshevGap(CityBounds a, CityBounds b)
    {
        int aRight = a.Origin.X + a.Width - 1, aBottom = a.Origin.Y + a.Height - 1;
        int bRight = b.Origin.X + b.Width - 1, bBottom = b.Origin.Y + b.Height - 1;
        int dx = Math.Max(0, Math.Max(a.Origin.X - bRight, b.Origin.X - aRight));
        int dy = Math.Max(0, Math.Max(a.Origin.Y - bBottom, b.Origin.Y - aBottom));
        return Math.Max(dx, dy);
    }

    [Fact]
    public void Pick_never_lands_within_AbsorptionRingCells_of_either_of_two_existing_cities()
    {
        var rules = MakeRules();
        var world = MakeWorld(rules);
        var mother = MakeCity(world, new CellCoord(1, 1));
        var cityB = MakeCity(world, new CellCoord(5, 5));
        var cityC = MakeCity(world, new CellCoord(6, 5)); // perto de B, mesmo espírito do bug relatado
        world.AddCity(mother);
        world.AddCity(cityB);
        world.AddCity(cityC);

        var site = FoundingSitePicker.Pick(world, mother.Id);

        Assert.NotNull(site);
        var siteBox = new CityBounds(site!.Value, 1, 1);
        var (boundsB, _) = CityBoundsResolver.Resolve(cityB, population: 0, world.Map.Width, world.Map.Height);
        var (boundsC, _) = CityBoundsResolver.Resolve(cityC, population: 0, world.Map.Width, world.Map.Height);
        Assert.True(ChebyshevGap(siteBox, boundsB) > rules.AbsorptionRingCells);
        Assert.True(ChebyshevGap(siteBox, boundsC) > rules.AbsorptionRingCells);
    }

    [Fact]
    public void Pick_rejects_the_cell_the_old_exact_cell_only_check_would_have_accepted()
    {
        var rules = MakeRules();
        var world = MakeWorld(rules);
        var mother = MakeCity(world, new CellCoord(2, 2));
        var other = MakeCity(world, new CellCoord(5, 2));
        world.AddCity(mother);
        world.AddCity(other);

        // Sob a lógica ANTIGA (só occupied.Contains(candidate)), o primeiro candidato do anel 1
        // não-ocupado — (3, 2) — seria aceito de cara: é adjacente aos bounds de `other`.
        var oldLogicPick = new CellCoord(3, 2);
        var (otherBounds, _) = CityBoundsResolver.Resolve(other, population: 0, world.Map.Width, world.Map.Height);
        Assert.True(ChebyshevGap(new CityBounds(oldLogicPick, 1, 1), otherBounds) <= rules.AbsorptionRingCells);

        var site = FoundingSitePicker.Pick(world, mother.Id);

        Assert.NotNull(site);
        Assert.NotEqual(oldLogicPick, site!.Value);
        // Post-ship fix (round 2, population-box cross-city clamp): `Pick` checks distance against
        // `CityOccupancy.ResolveGrownBounds` (the same box `IsWithinAbsorptionRangeOfAnyOtherCity`
        // uses internally) -- with `mother` sitting close to `other`, `other`'s OWN resolved bounds
        // now correctly shrink too (round 2 of this fix), so the real check must use that box, not
        // the raw/unclamped one above (which stays useful only for the adjacency illustration).
        var (otherGrownBounds, _) = CityOccupancy.ResolveGrownBounds(world, other, population: 0);
        Assert.True(ChebyshevGap(new CityBounds(site.Value, 1, 1), otherGrownBounds) > rules.AbsorptionRingCells);
    }

    /// <summary>Overlap de área — mesma checagem que <c>FoundingSitePicker.Overlaps</c> faz
    /// internamente, duplicada aqui (4 linhas) só pra o teste poder verificar a garantia sem
    /// depender de tipos internal.</summary>
    private static bool Overlaps(CityBounds a, CityBounds b) =>
        a.Origin.X < b.Origin.X + b.Width && b.Origin.X < a.Origin.X + a.Width &&
        a.Origin.Y < b.Origin.Y + b.Height && b.Origin.Y < a.Origin.Y + a.Height;

    /// <summary>ROOT-CAUSE regression test (user-reported, 2026-08-23, "cidades coladas" round 2):
    /// reproduz o cenário exato confirmado — mãe sozinha no mapa, sem nenhuma outra cidade por
    /// perto pra disparar o espaçamento mínimo existente — e prova que a filha ainda assim nunca
    /// acaba com bounds sobrepostos aos da mãe. Antes deste fix, a mãe era excluída de QUALQUER
    /// checagem e o anel 1 aceitava o primeiro candidato, produzindo mãe Origin(4,4) 3x3 / filha
    /// Origin(3,3) 3x3 (4 células compartilhadas) para uma mãe em (5,5).</summary>
    [Fact]
    public void Pick_never_overlaps_the_mothers_own_bounds_even_with_no_other_city_on_the_map()
    {
        var rules = MakeRules();
        var world = MakeWorld(rules);
        var mother = MakeCity(world, new CellCoord(5, 5));
        world.AddCity(mother);

        var site = FoundingSitePicker.Pick(world, mother.Id);

        Assert.NotNull(site);
        var (motherBounds, _) = CityOccupancy.ResolveGrownBounds(world, mother, population: 0);
        int daughterSide = CityBoundsResolver.SideFor(0, world.Map.Width, world.Map.Height);
        var daughterBounds = new CityBounds(
            new CellCoord(site!.Value.X - daughterSide / 2, site.Value.Y - daughterSide / 2), daughterSide, daughterSide);
        Assert.False(Overlaps(daughterBounds, motherBounds));
    }

    /// <summary>The other half of "close but never overlapping": avoiding overlap must NOT push
    /// the daughter out to the full <c>AbsorptionRingCells</c>-scale gap that unrelated cities
    /// require — she should land just outside the mother's edge.</summary>
    [Fact]
    public void Pick_still_lands_close_to_the_mother_not_pushed_out_to_AbsorptionRingCells_scale()
    {
        var rules = MakeRules();
        var world = MakeWorld(rules);
        var mother = MakeCity(world, new CellCoord(5, 5));
        world.AddCity(mother);

        var site = FoundingSitePicker.Pick(world, mother.Id);

        Assert.NotNull(site);
        int chebyshevFromMother = Math.Max(Math.Abs(site!.Value.X - mother.Location.X), Math.Abs(site.Value.Y - mother.Location.Y));
        // Não-overlap de dois lados-3 exige só 3 células de distância entre origens (mesma
        // magnitude de MinSize) -- bem abaixo do que o AbsorptionRingCells (3) exigiria SOMADO ao
        // próprio lado das duas caixas (~6+) se a mãe fosse tratada como qualquer outra cidade.
        Assert.True(chebyshevFromMother > 0);
        Assert.True(chebyshevFromMother <= 4);
    }

    [Fact]
    public void Pick_returns_null_when_no_cell_on_the_map_clears_the_minimum_distance_from_every_other_city()
    {
        var rules = MakeRules();
        var world = MakeWorld(rules);
        var mother = MakeCity(world, new CellCoord(5, 5));
        // Quatro cidades nos cantos do mapa 10x10: com AbsorptionRingCells=3 e bounds mínimos
        // (side 3, população 0), o halo de cada canto cobre um quadrado de 9x9 células ao seu
        // redor -- as quatro juntas cobrem o mapa inteiro, então nenhuma célula sobra livre.
        world.AddCity(mother);
        world.AddCity(MakeCity(world, new CellCoord(0, 0)));
        world.AddCity(MakeCity(world, new CellCoord(9, 0)));
        world.AddCity(MakeCity(world, new CellCoord(0, 9)));
        world.AddCity(MakeCity(world, new CellCoord(9, 9)));

        var site = FoundingSitePicker.Pick(world, mother.Id);

        Assert.Null(site); // falha honesta -- mapa cheio, nenhuma cidade colada é forçada
    }
}
