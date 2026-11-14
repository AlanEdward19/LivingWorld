using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Health;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Body;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Health;
using LivingWorld.Simulation.Population.Lifecycle;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Simulation.Scheduling;
using LivingWorld.Tests.Shared;

namespace LivingWorld.Tests.Unit.Health;

/// <summary>Doença de época (AD-137): contágio pela convivência (casa, turma, encontros), incubação, doente de cama com quem
/// cuida, desfecho no sintoma (morte agendada ou cura com imunidade), fonte ambiental, importação com surto na história e a parte
/// da tábua que a doença explica. Ligada no cenário de código.</summary>
public class DiseaseTests
{
    private static readonly WorldCalendar Calendar = new(24, 30, 12);
    private static readonly Personality Neutral = Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!;
    private static readonly CellCoord Here = new(1, 1);

    private sealed class RecordingSink : IWorldEventSink
    {
        public List<WorldEvent> Events { get; } = [];
        public void Record(WorldEvent evt) => Events.Add(evt);
    }

    private static DiseaseRules Rules(
        double contagion = 1.0, double lethality = 0.0, int durationDays = 5, int immunityDays = 60,
        int threshold = int.MaxValue, double outbreakChance = 0.0) =>
        new(true, [new DiseaseProfile(1, contagion, lethality, durationDays, immunityDays)], threshold, outbreakChance);

    private static DiseaseRules With(DiseaseProfile profile) => new(true, [profile], int.MaxValue, 0);

    private static WorldState NewWorld(DiseaseRules? rules = null, ulong seed = 1) => new(
        Calendar, seed, ScenarioRunner.DefaultMap(1), ScenarioRunner.DefaultPopulationCatalog,
        ScenarioRunner.DefaultPopulationRules, ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog,
        ScenarioRunner.DefaultLifeStageRules, familyRules: ScenarioRunner.DefaultFamilyRules, diseaseRules: rules);

    private static Npc Add(WorldState world, CellCoord at, int ageYears = 25, Sex sex = Sex.Male, NpcId? mother = null)
    {
        var npc = new Npc(
            world.NextNpcIdAndAdvance(), "npc", sex, WorldDate.Epoch(Calendar).AddYears(-ageYears), new CultureId(1),
            at, mother, null, household: null, health: 100, personality: Neutral, profession: new ProfessionType(1), currentLocation: at);
        world.AddNpc(npc);
        return npc;
    }

    private static Household House(WorldState world, params Npc[] members)
    {
        var home = new Household(world.NextHouseholdIdAndAdvance(), Here, members[0].Id, members.Select(m => m.Id).ToList());
        world.AddHousehold(home);
        foreach (var m in members) m.JoinHousehold(home.Id);
        return home;
    }

    /// <summary>Um dia pelo relógio (só a doença), para os eventos agendados (a morte) vencerem.</summary>
    private static RecordingSink Days(WorldState world, int days = 1)
    {
        var sink = new RecordingSink();
        new WorldClock([new OutbreakSystem()], sink: sink).Run(world, days * 24L);
        return sink;
    }

    private static Infection Sick(WorldState world, Npc npc, int durationDays = 10, int disease = 1)
    {
        long now = world.CurrentDate.TotalHours;
        var infection = new Infection(npc.Id, disease, now + durationDays * 24L) { InfectedTick = now, OnsetTick = now };
        world.AddInfection(infection);
        return infection;
    }

    [Fact]
    public void The_code_scenario_has_period_diseases_on_and_a_world_without_rules_has_none()
    {
        Assert.False(NewWorld().DiseaseRules.Enabled);
        var rules = ScenarioRunner.Create(1).World.DiseaseRules;
        Assert.True(rules.Enabled);
        // os padrões que mais matavam, como dado: água no verão, catarro no inverno, infância com imunidade de vida, peste rara
        Assert.Contains(rules.Diseases, d => d.Route == DiseaseRoute.WaterFood && d.Season(7) > 1.5 * d.Season(1));
        Assert.Contains(rules.Diseases, d => d.Route == DiseaseRoute.Respiratory && d.Season(0) > 1.5 * d.Season(7)
            && d.LethalityFor(0, 0) > 3 * d.LethalityFor(25, 0) && d.LethalityFor(80, 0) > 3 * d.LethalityFor(25, 0));
        Assert.Contains(rules.Diseases, d => d.ImmunityDays >= 30_000 && d.ImportAnnualChance > 0 && d.LethalityFor(1, 0) > d.LethalityFor(10, 0));
        Assert.Contains(rules.Diseases, d => d.ImportAnnualChance is > 0 and < 0.05 && d.LethalityFor(30, 0) >= 0.5);
        Assert.All(rules.Diseases, d => Assert.True(d.IncubationDays + d.IncubationJitterDays > 0, d.Name));
        Assert.True(rules.BaselineShare(2) > 0.5 && rules.BaselineShare(70) < rules.BaselineShare(2));
    }

    [Fact]
    public void A_disabled_system_allocates_nothing_per_tick_even_with_many_npcs()
    {
        var world = NewWorld();
        for (int i = 0; i < 1000; i++) Add(world, Here);
        var system = new OutbreakSystem();
        var ctx = new TickContext(world, world.Rng, world.Scheduler, new RecordingSink());
        system.Tick(world, ctx); // aquece
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++) system.Tick(world, ctx);

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    [Fact]
    public void A_nan_outbreak_chance_never_starts_an_outbreak()
    {
        var world = NewWorld(Rules(threshold: 2, outbreakChance: double.NaN));
        for (int i = 0; i < 5; i++) Add(world, Here);

        Days(world, 5);

        Assert.Empty(world.Infections);
    }

    [Fact]
    public void Contagion_follows_the_household_and_not_mere_presence_in_the_same_cell()
    {
        var world = NewWorld(Rules(contagion: 1.0, durationDays: 10));
        var sick = Add(world, Here);
        var housemate = Add(world, Here);
        var strangerHere = Add(world, Here); // mesma célula, outra casa, sem encontro (CommunityContactsPerDay = 0)
        var immune = Add(world, Here);
        House(world, sick, housemate, immune);
        House(world, strangerHere);
        Sick(world, sick);
        world.SetImmunity(immune.Id, 1, untilTick: world.CurrentDate.TotalHours + 1000);

        Days(world);

        Assert.True(world.IsInfected(housemate.Id, 1));
        Assert.False(world.IsInfected(strangerHere.Id, 1));
        Assert.False(world.IsInfected(immune.Id, 1));
    }

    [Fact]
    public void A_zero_contagion_never_spreads()
    {
        var world = NewWorld(Rules(contagion: 0.0, durationDays: 10));
        var sick = Add(world, Here);
        var housemate = Add(world, Here);
        House(world, sick, housemate);
        Sick(world, sick);

        Days(world, 5);

        Assert.False(world.IsInfected(housemate.Id, 1));
    }

    [Fact]
    public void Incubation_hides_the_disease_and_its_contagion_until_onset()
    {
        var profile = new DiseaseProfile(1, 1.0, 0.0, 5, 60) { IncubationDays = 4 };
        var world = NewWorld(With(profile));
        var first = Add(world, Here);
        var housemate = Add(world, Here);
        House(world, first, housemate);
        var rng = new WorldRng(3);
        world.AddInfection(OutbreakSystem.NewInfection(world, world.DiseaseRules, profile, first.Id, world.CurrentDate.TotalHours, rng));

        Days(world, 3);
        Assert.False(world.Infections.Single().Symptomatic);
        Assert.Equal(0, OutbreakSystem.HealthPenalty(world, first, 25));
        Assert.False(world.IsInfected(housemate.Id, 1));

        Days(world, 2);
        Assert.True(world.Infections.First(i => i.Npc == first.Id).Symptomatic);
        Assert.True(OutbreakSystem.HealthPenalty(world, first, 25) > 0);
        Assert.True(world.IsInfected(housemate.Id, 1));
    }

    [Fact]
    public void The_bedridden_do_not_work_and_their_caregiver_stays_home_on_care_days()
    {
        var world = NewWorld(Rules(contagion: 0.0, durationDays: 30));
        var mother = Add(world, Here, ageYears: 30, sex: Sex.Female);
        var father = Add(world, Here, ageYears: 32);
        var child = Add(world, Here, ageYears: 6, mother: mother.Id);
        var healthy = Add(world, Here);
        House(world, father, mother, child);
        Sick(world, child);
        Days(world);

        var infection = world.Infections.Single();
        Assert.True(infection.Bedridden);
        Assert.Equal(mother.Id, infection.Caregiver); // a mãe cuida do filho
        long now = world.CurrentDate.TotalHours;
        Assert.Equal(ActionType.Sleep, SickBehavior.Constrain(world, child, ActionType.Work, now));
        Assert.Equal(ActionType.Sleep, SickBehavior.Constrain(world, child, ActionType.Socialize, now));
        Assert.Equal(ActionType.Eat, SickBehavior.Constrain(world, child, ActionType.Eat, now));
        Assert.Equal(ActionType.Work, SickBehavior.Constrain(world, healthy, ActionType.Work, now));
        Assert.Equal(ActionType.Work, SickBehavior.Constrain(world, father, ActionType.Work, now));
        // sede urgente vence a cama e a fuga sugerida pela percepção: o doente bebe do que há em casa
        child.SetThirst(10, now);
        Assert.Equal(ActionType.Buy, SickBehavior.Constrain(world, child, ActionType.Travel, now)); // casa sem água: vai buscar
        var home = world.FindHousehold(child.Household!.Value)!;
        home.Deposit(new LivingWorld.Domain.Geography.ResourceType(world.EconomyRules.WaterResourceId), 5);
        Assert.Equal(ActionType.Eat, SickBehavior.Constrain(world, child, ActionType.Travel, now));

        int careDays = 0;
        for (int day = 0; day < 200; day++)
            if (SickBehavior.Constrain(world, mother, ActionType.Work, now + day * 24L) == ActionType.Idle) careDays++;
        Assert.InRange(careDays, 70, 130); // metade dos dias (CareWorkShare = 0,5)
    }

    [Fact]
    public void A_lethality_of_one_kills_during_the_illness_with_a_disease_event_naming_the_profile()
    {
        var world = NewWorld(Rules(contagion: 0.0, lethality: 1.0, durationDays: 4));
        var sick = Add(world, Here);
        Sick(world, sick, durationDays: 4);

        var sink = Days(world, 5);

        Assert.False(sick.IsAlive);
        Assert.Contains(sink.Events, e => e.Kind == WorldEventKind.Disease && e.Payload == $"{sick.Id.Value}|1");
        Assert.Contains(sink.Events, e => e.Kind == WorldEventKind.Disease && e.SourceSystem == "NpcDeath");
        Assert.Empty(world.Infections);
    }

    [Fact]
    public void Recovery_leaves_immunity_that_blocks_reinfection_expires_and_can_leave_a_sequela()
    {
        var profile = new DiseaseProfile(1, 1.0, 0.0, 2, 10) { SequelaChance = 1.0, SequelaFrailtyYears = 3 };
        var world = NewWorld(With(profile));
        var sick = Add(world, Here);
        var carrier = Add(world, Here);
        House(world, sick, carrier);
        Sick(world, sick, durationDays: 2);

        Days(world, 3);
        Assert.False(world.IsInfected(sick.Id, 1)); // curado
        Assert.True(world.IsImmune(sick.Id, 1));
        Assert.Equal(3 * Calendar.HoursPerYear, sick.LifespanOffsetHours);

        world.RemoveInfection(carrier.Id, 1);
        Sick(world, carrier, durationDays: 5);
        Days(world);
        Assert.False(world.IsInfected(sick.Id, 1)); // imune: não pega de novo

        Days(world, 12);
        Assert.False(world.IsImmune(sick.Id, 1)); // a imunidade venceu
    }

    [Fact]
    public void Deaths_of_a_finished_epidemic_are_within_half_to_one_and_a_half_times_the_declared_lethality()
    {
        var world = NewWorld(Rules(contagion: 0.0, lethality: 0.2, durationDays: 3, immunityDays: 360), seed: 3);
        for (int i = 0; i < 400; i++) Sick(world, Add(world, Here), durationDays: 3);

        Days(world, 6);

        double mortality = world.Npcs.Count(n => !n.IsAlive) / 400.0;
        Assert.InRange(mortality, 0.2 * 0.5, 0.2 * 1.5);
        Assert.Empty(world.Infections);
    }

    [Fact]
    public void The_environment_gives_about_the_declared_yearly_cases_and_more_in_the_peak_month()
    {
        double[] season = [0.2, 0.2, 0.2, 0.2, 0.2, 0.2, 3.0, 3.0, 3.0, 1.2, 0.2, 0.2];
        var profile = new DiseaseProfile(1, 0.0, 0.0, 1, 0)
        {
            EnvironmentalIncidenceByAge = AgeCurve.Of((0, 0.5), (60, 0.5)),
            SeasonByMonth = season,
        };
        var world = NewWorld(With(profile), seed: 5);
        for (int i = 0; i < 2000; i++) Add(world, Here);
        var byMonth = new long[12];
        for (int day = 0; day < 360; day++)
        {
            Days(world); // um dia de doença: o caso de ontem já sarou, os de hoje acabaram de pegar
            byMonth[world.CurrentDate.Month] += world.Infections.Count(i => i.InfectedTick == world.CurrentDate.TotalHours);
        }

        long total = byMonth.Sum();
        Assert.InRange(total, 0.8 * 1000, 1.2 * 1000); // 2000 pessoas × 0,5 caso/ano
        Assert.True(byMonth[7] > 5 * byMonth[1], $"pico {byMonth[7]} contra inverno {byMonth[1]}");
    }

    [Fact]
    public void An_import_opens_an_outbreak_in_history_and_its_end_closes_it()
    {
        var profile = new DiseaseProfile(1, 0.0, 0.0, 3, 1000) { ImportAnnualChance = 360.0 }; // chega no 1º dia
        var world = NewWorld(With(profile));
        var city = new LivingWorld.Domain.Cities.City(world.NextCityId(), Here, 0, null, LivingWorld.Domain.Cities.AggregatePopulationPool.Empty);
        world.AddCity(city);
        for (int i = 0; i < 5; i++) Add(world, Here).JoinCity(city.Id);

        var started = Days(world);
        Assert.Single(started.Events, e => e.Kind == WorldEventKind.OutbreakStarted);
        Assert.Single(world.Infections);

        // ninguém mais pega: o surto acaba com o caso
        foreach (var npc in world.Npcs) world.SetImmunity(npc.Id, 1, long.MaxValue);
        var ended = Days(world, 4);
        Assert.Contains(ended.Events, e => e.Kind == WorldEventKind.OutbreakEnded && e.Payload.StartsWith("1|", StringComparison.Ordinal));
    }

    [Fact]
    public void Founders_had_the_childhood_disease_already_by_the_declared_share_of_their_age()
    {
        var profile = new DiseaseProfile(1, 1.0, 0.0, 5, 36_000) { FounderImmunityByAge = AgeCurve.Of((0, 0), (20, 0.9)) };
        var world = NewWorld(With(profile));
        var adults = Enumerable.Range(0, 400).Select(_ => Add(world, Here, ageYears: 30)).ToList();
        long now = world.CurrentDate.TotalHours;

        int susceptible = adults.Count(a => OutbreakSystem.IsSusceptible(world, world.DiseaseRules.Diseases[0], a.Id, now));

        Assert.InRange(susceptible, 20, 60); // ~10% de 400
    }

    [Fact]
    public void The_life_table_loses_the_share_that_disease_explains()
    {
        var plain = NewWorld();
        var withDisease = NewWorld(new DiseaseRules(true, [new DiseaseProfile(1, 0, 0, 1, 0)], int.MaxValue, 0)
        {
            BaselineShareByAge = AgeCurve.Of((0, 0.5), (100, 0.5)),
        });
        var a = Add(plain, Here, ageYears: 2);
        var b = Add(withDisease, Here, ageYears: 2);

        double full = MortalitySystem.WindowRisk(plain, a, 0, 720);
        double rest = MortalitySystem.WindowRisk(withDisease, b, 0, 720);

        Assert.InRange(rest / full, 0.49, 0.51);
    }

    [Fact]
    public void The_rest_of_the_table_names_accident_and_old_age()
    {
        var world = NewWorld();
        var old = Add(world, Here, ageYears: 80);
        var young = Add(world, Here, ageYears: 30);
        var sink = new RecordingSink();
        var ctx = new TickContext(world, world.Rng, world.Scheduler, sink);
        var mortality = new MortalitySystem();

        mortality.HandleEvent(world, ctx, new ScheduledEvent(1, 0, MortalitySystem.SystemName, $"age|{old.Id.Value}|O"));
        mortality.HandleEvent(world, ctx, new ScheduledEvent(2, 0, MortalitySystem.SystemName, $"age|{young.Id.Value}|A"));

        Assert.Contains(sink.Events, e => e.Kind == WorldEventKind.OldAge && e.Payload == old.Id.Value.ToString());
        Assert.Contains(sink.Events, e => e.Kind == WorldEventKind.Accident && e.Payload == young.Id.Value.ToString());
        var causes = DeathCauseRules.PreIndustrial;
        Assert.True(causes.AccidentShare(30, male: true, 0.006) > causes.AccidentShare(30, male: false, 0.006));
        Assert.Equal(0, causes.OldAgeShareByAge.At(40));
    }

    [Fact]
    public void The_same_world_gives_the_same_epidemic()
    {
        static List<(long, long)> Run()
        {
            var world = NewWorld(new DiseaseRules(true, [new DiseaseProfile(1, 0.3, 0.3, 4, 30) { IncubationDays = 1, IncubationJitterDays = 3 }],
                int.MaxValue, 0)
            { CommunityContactsPerDay = 4 }, seed: 7);
            var first = Add(world, Here);
            for (int i = 0; i < 40; i++) Add(world, Here);
            Sick(world, first, durationDays: 4);
            Days(world, 30);
            return world.Npcs.Select(n => (n.Id.Value, n.IsAlive ? 1L : 0L)).Concat(world.Infections.Select(i => (i.Npc.Value, i.EndTick))).ToList();
        }

        Assert.Equal(Run(), Run());
    }

    [Fact]
    public void Infections_immunities_and_rules_survive_a_snapshot_round_trip_with_an_equal_hash()
    {
        var world = NewWorld(DiseasePresets.PreIndustrial);
        var a = Add(world, Here);
        var b = Add(world, Here);
        world.AddInfection(new Infection(a.Id, 1, 500) { OnsetTick = 100, Symptomatic = true, Bedridden = true, Caregiver = b.Id, DeathTick = 400, OutbreakTick = 7 });
        world.SetImmunity(b.Id, 1, untilTick: 900);

        var restored = WorldSnapshot.Deserialize(WorldSnapshot.Serialize(world));

        Assert.Equal(world.Infections, restored.Infections);
        Assert.Equal(world.Immunities, restored.Immunities);
        Assert.Equal(world.DiseaseRules.Diseases.Count, restored.DiseaseRules.Diseases.Count);
        Assert.Equal(world.DiseaseRules.BaselineShare(3), restored.DiseaseRules.BaselineShare(3));
        Assert.Equal(WorldSnapshot.CanonicalHash(world), WorldSnapshot.CanonicalHash(restored));
        Assert.True(restored.IsCaregiver(b.Id));
    }

    [Fact]
    [Trait("Category", "Scenario")]
    public void The_village_with_a_moderate_disease_keeps_seventy_percent_of_the_control_population_over_five_years_in_eight_seeds()
    {
        var disease = new DiseaseRules(true, [new DiseaseProfile(1, 0.08, 0.15, 6, 180)], OutbreakPopulationThreshold: 20, OutbreakDailyChance: 0.01);
        long control = 0, sick = 0, outbreaks = 0;
        var perSeed = ParallelSeeds.Map(1, 8, seed =>
        {
            var (ill, _) = ScenarioRunner.Create(seed, diseaseRules: disease);
            var counter = new RecordingSink();
            new WorldClock(ScenarioRunner.DefaultSystems(), sink: counter).Run(ill, 5 * 12 * 30 * 24);
            // controle: a vila padrão no ano 5, lida da corrida compartilhada (ADR-0044) depois do braço doente, que não espera por ela
            long plainAlive = LivingWorld.Tests.Unit.Population.DefaultVillageDecade.Runs[(int)seed - 1].Alive[5];
            return (Control: plainAlive,
                Outbreaks: (long)counter.Events.Count(e => e.Kind == WorldEventKind.OutbreakStarted),
                Sick: (long)ill.Npcs.Count(n => n.IsAlive));
        });
        foreach (var r in perSeed) { control += r.Control; outbreaks += r.Outbreaks; sick += r.Sick; }

        Assert.True(outbreaks > 0, "nenhum surto aconteceu: o teste não mediu nada");
        Assert.True(sick >= 0.7 * control, $"população final com doença {sick} contra controle {control}");
    }
}
