using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Cities;
using LivingWorld.Domain.Emergence;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Behavior.Decision;
using LivingWorld.Simulation.Cities.Streets;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Emergence;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Tests.Unit.Cities;

/// <summary>AD-179: a obra ocupa o tempo de quem trabalha nela. O diarista aceita pela utilidade, vai e volta pela rota, e a obra só
/// anda pelas horas de quem está no trecho, pagas por hora; carga a pé alonga o passo.</summary>
public class SiteWorkTests
{
    // AD-197o2: a obra de rua poupa tempo de caminhada e a vila de 1,5 m por célula se atravessa em um minuto; as obras são testadas num
    // mapa de região (célula de ~250 m), onde calçar o trecho passado poupa horas de verdade
    /// <summary>Vila de código com as oficinas fechadas (ninguém tem emprego: todo adulto pode ser diarista), a técnica de calçar
    /// adotada e uma célula de terra muito passada no largo, longe de casa (paga o tesouro). Abre a obra.</summary>
    private static (WorldState World, WorldClock Clock, PavingWork Work, int Index) WorldWithOpenWork(ulong seed = 5, StreetRules? streets = null, long wallet = 0, long? hours = null)
    {
        var (world, clock) = ScenarioRunner.Create(seed, costWeights: ScenarioRunner.RegionalCostWeights, cityLayout: streets is null ? null : CityLayoutRules.Default with { Streets = streets });
        foreach (var shop in world.Workplaces)
        {
            shop.SetClosed(true);
            shop.SetPayrollFailed(true);
            foreach (var id in shop.Employees.ToList()) { shop.Fire(id); world.FindNpc(id)?.Fire(); }
        }
        foreach (var npc in world.Npcs)
        {
            if (npc.Wallet.Amount > 0) npc.TryDebitWallet(npc.Wallet);
            if (wallet > 0) npc.CreditWallet(new Money(wallet));
        }
        var city = world.ActiveCities().First();
        var ground = world.Ground!;
        world.AddTechnology(new Technology(world.Npcs.First().Culture, PavingTechnique.Category, new TechnologyVector([3, 0, 3]), 0, city.Id, null, [city.Id]));
        int index = ground.IndexOf(city.Location);
        ground.SetSurface(index, GroundSurface.Dirt);
        ground.AddPasses(index, 5000);
        city.CreditTreasury(new Money(1_000_000));
        PavingDecision.Monthly(world, new TickContext(world, world.Rng, world.Scheduler));
        var work = Assert.Single(ground.Works);
        if (hours is { } h) { work.HoursTotal = h; work.HoursLeft = h; } // uma obra longa (vários dias de turma)
        return (world, clock, work, index);
    }

    [Fact]
    public void The_stretch_is_paved_only_by_the_hours_of_laborers_present_and_each_hour_is_paid_to_who_worked_it()
    {
        var (world, clock, work, index) = WorldWithOpenWork();
        var site = SiteWork.SiteOf(world, work);
        long total = work.HoursLeft;
        var seenOnSite = new HashSet<long>();
        var seenOnRoute = new HashSet<long>();
        long worked = 0;
        for (int h = 0; h < 24 * 60 && world.Ground!.Works.Count > 0; h++)
        {
            var crewBefore = work.Crew.ToList();
            clock.Run(world, 1);
            foreach (long id in work.Crew)
            {
                var npc = world.FindNpc(new NpcId(id))!;
                if (npc.CurrentLocation == site && npc.CurrentAction == ActionType.Work) seenOnSite.Add(id);
                if (npc.CurrentAction == ActionType.Travel && npc.TravelPurpose == ActionType.Work) seenOnRoute.Add(id);
            }
            // a obra só anda quando alguém da turma acabou a jornada NO trecho
            if (work.HoursWorked > worked)
                Assert.Contains(crewBefore.Concat(work.Crew), id => world.FindNpc(new NpcId(id))!.CurrentLocation == site);
            worked = work.HoursWorked;
        }
        Assert.Empty(world.Ground!.Works);
        Assert.Equal(GroundSurface.Gravel, world.Ground.PavingAt(index));
        Assert.True(work.HoursWorked >= total, $"horas feitas {work.HoursWorked} de {total}");
        // cascalho: sem pedra comprada, tudo é jornal. AD-184: cada jornada paga o jornal do dia pela oferta da semana (que sobe sem
        // braço) e pelas horas, arredondado: entre metade do jornal base e o teto da oferta, com 1 de arredondamento por jornada
        double hourBase = PavingDecision.DayWage(world) / (double)world.StreetRules.CrewHoursPerDay;
        Assert.InRange(work.Paid, (long)(work.HoursWorked * hourBase / 2), (long)(work.HoursWorked * hourBase * world.StreetRules.OfferMaxPm / 1000) + work.HoursWorked);
        Assert.NotEmpty(seenOnSite);
        Assert.NotEmpty(seenOnRoute); // foram a pé, pela rota, nas horas de trabalho
        Assert.True(work.Crew.Count <= world.StreetRules.CrewMax);
    }

    [Fact]
    public void Without_anyone_who_needs_the_wage_the_work_stands_still()
    {
        // todo mundo com moeda de sobra e a despensa cheia: a oferta não paga a caminhada, ninguém aceita
        var (world, clock, work, _) = WorldWithOpenWork(wallet: 1_000_000);
        foreach (var home in world.Households)
        {
            home.Deposit(new ResourceType(world.EconomyRules.FoodResourceId), 10_000);
            home.Deposit(new ResourceType(world.EconomyRules.WaterResourceId), 10_000);
        }
        long left = work.HoursLeft;
        clock.Run(world, 24 * 10);
        Assert.Empty(work.Crew);
        Assert.Equal(left, work.HoursLeft);
        Assert.Equal(0, work.HoursWorked);
    }

    [Fact]
    public void A_work_without_any_hand_for_months_is_undone()
    {
        var (world, clock, work, index) = WorldWithOpenWork(wallet: 1_000_000);
        foreach (var home in world.Households)
        {
            home.Deposit(new ResourceType(world.EconomyRules.FoodResourceId), 10_000);
            home.Deposit(new ResourceType(world.EconomyRules.WaterResourceId), 10_000);
        }
        clock.Run(world, (world.StreetRules.SiteStaleMonths + 1) * world.Calendar.HoursPerMonth);
        Assert.DoesNotContain(work, world.Ground!.Works);
        Assert.True(world.Ground.WorksAbandoned >= 1);
        Assert.Equal(GroundSurface.None, world.Ground.PavingAt(index));
    }

    [Fact]
    public void The_street_card_lists_the_work_and_its_laborers()
    {
        var (world, clock, work, index) = WorldWithOpenWork(hours: 5000);
        clock.Run(world, 24 * 2);
        Assert.NotEmpty(work.Crew);
        var cell = world.Ground!.CellOf(index);
        var view = LivingWorld.Simulation.Cities.Streets.StreetViews.ByCity(world).Values.SelectMany(v => v).Single(v => v.Cell == cell);
        Assert.Equal(work.Id, view.Work!.WorkId);
        Assert.Equal(work.Crew, view.Work.WorkerIds);
    }

    [Fact]
    public void More_hands_finish_the_stretch_sooner_up_to_the_crew_cap()
    {
        int DaysToFinish(int crew)
        {
            var (world, clock, _, _) = WorldWithOpenWork(streets: StreetRules.Default with { CrewMax = crew });
            for (int d = 1; d <= 120; d++)
            {
                clock.Run(world, 24);
                if (world.Ground!.Works.Count == 0) return d;
            }
            return int.MaxValue;
        }
        int one = DaysToFinish(1), six = DaysToFinish(6);
        Assert.True(six < one, $"6 braços {six} dias, 1 braço {one} dias");
    }

    [Fact]
    public void The_site_offer_is_a_named_term_of_working_in_the_brain_reading()
    {
        var (world, clock, work, _) = WorldWithOpenWork(hours: 5000);
        clock.Run(world, 24 * 2);
        Assert.Contains(work, world.Ground!.Works);
        Assert.NotEmpty(work.Crew);
        var npc = world.FindNpc(new NpcId(work.Crew[0]))!;
        var reading = BrainReadout.Read(world, npc.Id).Value!;
        var term = reading.Synapses.Single(s => s.Action == ActionType.Work && s.Term == UtilityTerm.SiteWork);
        Assert.True(term.Value > 0);
        Assert.Equal(UtilityGroup.Pressure, term.Group);
        Assert.Equal(PressureModel.EarnIncome, PressureModel.KindOf(UtilityTerm.SiteWork));
    }

    [Fact]
    public void Reopened_world_keeps_the_crew_and_works_the_same_hours()
    {
        var (world, clock, _, _) = WorldWithOpenWork(hours: 5000);
        clock.Run(world, 24 * 2 + 9);
        Assert.NotEmpty(world.Ground!.Works[0].Crew);
        var copy = WorldSnapshot.Deserialize(WorldSnapshot.Serialize(world));
        Assert.Equal(world.Ground.Works[0].Crew, copy.Ground!.Works[0].Crew);
        Assert.Equal(world.Ground.Works[0].HoursWorked, copy.Ground.Works[0].HoursWorked);
        var copyClock = new WorldClock(ScenarioRunner.DefaultSystems());
        clock.Run(world, 24 * 3);
        copyClock.Run(copy, 24 * 3);
        Assert.Equal(WorldSnapshot.CanonicalHash(world), WorldSnapshot.CanonicalHash(copy));
    }

    [Fact]
    public void Walking_loaded_is_slower_and_the_site_haul_counts_the_loaded_leg()
    {
        var rules = StreetRules.Default;
        Assert.Equal(1.0, Mobility.LoadFactorOfKg(rules, 0));
        Assert.True(Mobility.LoadFactorOfKg(rules, 20) > 1.0);
        Assert.Equal(Mobility.LoadFactorOfKg(rules, rules.CarryMaxKg), Mobility.LoadFactorOfKg(rules, rules.CarryMaxKg * 3));

        var (world, _) = ScenarioRunner.Create(4, costWeights: ScenarioRunner.RegionalCostWeights);
        var npc = world.Npcs.First(n => n.IsAlive && n.Household is not null);
        var home = world.FindHousehold(npc.Household!.Value)!;
        var far = world.Workplaces.OrderByDescending(w => Math.Abs(w.Location.X - home.Location.X) + Math.Abs(w.Location.Y - home.Location.Y)).First().Location;
        long empty = Mobility.TravelTicks(world, npc, home.Location, far, 1.0);
        world.Invention!.Carry(npc.Id.Value, world.EconomyRules.WaterResourceId, 30); // 30 unidades de água às costas
        Assert.True(Mobility.LoadFactor(world, npc) > 1.0);
        long loaded = Mobility.TravelTicks(world, npc, home.Location, far, 1.0);
        Assert.True(loaded >= empty, $"carregado {loaded} h, vazio {empty} h");

        // o material da obra: horas da célula com a ida carregada passam as de ir e voltar vazio
        var noLoad = ScenarioRunner.Create(4, costWeights: ScenarioRunner.RegionalCostWeights, cityLayout: CityLayoutRules.Default with { Streets = rules with { SiteLoadKg = 0 } }).World;
        var cell = world.ActiveCities().First().Location;
        Assert.True(PavingDecision.HoursFor(world, cell, GroundSurface.Gravel) >= PavingDecision.HoursFor(noLoad, cell, GroundSurface.Gravel));
    }
    /// <summary>A casa da fila da cidade (cenário <c>default.json</c>, com receita de casa): sem turma no lote não anda; com diaristas
    /// (as oficinas fechadas deixam todo adulto sem emprego), o material vai do largo ao lote pela rota e a casa sobe pelas horas
    /// deles, pagas pelo tesouro.</summary>
    [Fact]
    public void A_house_of_the_city_queue_rises_only_by_the_hours_of_its_crew_on_the_lot()
    {
        var root = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(Path.Combine(FindRepoRoot(), "scenarios", "default.json")))!;
        root["CityLayout"] = new System.Text.Json.Nodes.JsonObject { ["Streets"] = new System.Text.Json.Nodes.JsonObject { ["BuildNeedsCrew"] = true } };
        var (world, clock) = LivingWorld.Simulation.Scenarios.ScenarioLoaderV2.LoadWorld(root.ToJsonString()).Value;
        Assert.True(world.StreetRules.BuildNeedsCrew);
        var city = world.ActiveCities().First();
        foreach (var shop in world.Workplaces)
        {
            shop.SetClosed(true);
            shop.SetPayrollFailed(true);
            foreach (var id in shop.Employees.ToList()) { shop.Fire(id); world.FindNpc(id)?.Fire(); }
        }
        foreach (var npc in world.Npcs) if (npc.Wallet.Amount > 0) npc.TryDebitWallet(npc.Wallet);
        city.DepositStock(new ResourceType(3), 20);
        city.DepositStock(new ResourceType(5), 10);
        city.CreditTreasury(new Money(1_000_000));
        // AD-184: o canteiro ocupa o lote desde a escolha; conta só a casa pronta
        int Ready() => world.Buildings.Count(b => b.BuildingTypeId == 10 && b.UnderWork is null);
        int before = Ready();
        Assert.True(LivingWorld.Simulation.Cities.Construction.ConstructionSystem.StartConstruction(world, city.Id, 10).IsSuccess);
        var project = city.ConstructionQueue[0];
        long ticks = project.TicksRemaining;
        bool sawOnLot = false;
        for (int h = 0; h < 24 * 30 && Ready() == before; h++)
        {
            clock.Run(world, 1);
            if (project.SiteX >= 0 && project.Crew.Any(id => world.FindNpc(new NpcId(id))!.CurrentLocation == new CellCoord(project.SiteX, project.SiteY)))
                sawOnLot = true;
            // nunca anda mais do que as horas trabalhadas no lote (as primeiras vão ao transporte do material)
            Assert.True(ticks - project.TicksRemaining <= project.HoursWorked * 2, $"passos {ticks - project.TicksRemaining}, horas {project.HoursWorked}");
        }
        Assert.True(sawOnLot);
        Assert.Equal(before + 1, Ready());
        Assert.True(project.HoursWorked >= ticks, $"{project.HoursWorked} horas para {ticks} passos");
        double hourBase = PavingDecision.DayWage(world) / (double)world.StreetRules.CrewHoursPerDay; // AD-184: jornal pela oferta
        Assert.InRange(project.Paid, (long)(project.HoursWorked * hourBase / 2), (long)(project.HoursWorked * hourBase * world.StreetRules.OfferMaxPm / 1000) + project.HoursWorked);
    }

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "LivingWorld.sln")))
            dir = Directory.GetParent(dir)?.FullName;
        return dir ?? throw new InvalidOperationException("LivingWorld.sln não encontrado a partir de " + AppContext.BaseDirectory);
    }
}
