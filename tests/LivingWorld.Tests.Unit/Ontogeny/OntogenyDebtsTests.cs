using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Cities;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Ontogeny;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Body;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Behavior.Decision;
using LivingWorld.Simulation.Cities.Construction;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Economy.Market;
using LivingWorld.Simulation.Observation;
using LivingWorld.Simulation.Ontogeny;
using LivingWorld.Simulation.Population.Family;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Tests.Unit.Ontogeny;

/// <summary>Dívidas da Fase 21 (issue #93): acolhimento de órfãos, choro atendido, exposição graduada, criação adotiva e guarda do pool agregado.</summary>
public class OntogenyDebtsTests
{
    private const int Seeds = 20;
    private const long Month = 30L * 24;
    private static readonly Personality Neutral = Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!;
    private static readonly CellCoord Home = default;

    private sealed class Sink : IWorldEventSink
    {
        public List<WorldEvent> Events { get; } = [];
        public void Record(WorldEvent e) => Events.Add(e);
    }

    private static Npc Person(WorldState world, string name, double ageYears, int culture = 1, double upbringing = 50, double altruism = 50, long wallet = 0,
        NpcId? mother = null, NpcId? father = null, double rate = 1.0)
    {
        var personality = Personality.Create(50, 50, 50, 50, 50, 50, 50, (int)altruism, 50, 50).Value!;
        var npc = new Npc(
            world.NextNpcIdAndAdvance(), name, Sex.Female, world.CurrentDate.AddHours(-(long)(ageYears * world.CurrentDate.Calendar.HoursPerYear)),
            new CultureId(culture), Home, mother, father, null, 100, personality, new ProfessionType(1), Home,
            wallet: new Money(wallet), upbringing: upbringing, rateGene: new RateGene(rate), city: default);
        world.AddNpc(npc);
        return npc;
    }

    private static Household House(WorldState world, params Npc[] members)
    {
        var household = new Household(world.NextHouseholdIdAndAdvance(), Home, members[0].Id, members.Select(m => m.Id).ToList());
        world.AddHousehold(household);
        foreach (var m in members) m.JoinHousehold(household.Id);
        return household;
    }

    private static WorldState World(int seed = 1, OntogenyRules? rules = null) =>
        ScenarioRunner.Create((ulong)seed, initialPopulation: 0, ontogenyRules: rules ?? OntogenyDefaults.Human).World;

    // --- Critério 1: acolhimento de órfãos ---

    [Fact]
    public void An_orphan_without_relatives_is_taken_in_by_a_community_adult_within_the_month()
    {
        var world = World();
        var sink = new Sink();
        var clock = new WorldClock([new OrphanCareSystem()], sink: sink);
        var child = Person(world, "orfa", 6);
        House(world, child);
        var neighbour = Person(world, "vizinha", 35, altruism: 80, wallet: 100);
        var neighbourHome = House(world, neighbour);

        clock.Run(world, Month);

        Assert.Equal(neighbourHome.Id, child.Household);
        Assert.Contains(neighbourHome.Members, m => m == child.Id);
        Assert.Contains(sink.Events, e => e.Kind == WorldEventKind.OrphanTakenIn && e.Payload.EndsWith("|comunidade"));
    }

    [Fact]
    public void A_relative_is_preferred_over_a_more_generous_stranger_and_without_adults_nobody_is_taken_in()
    {
        var world = World();
        var clock = new WorldClock([new OrphanCareSystem()]);
        var grandmother = Person(world, "avo", 70);
        var mother = Person(world, "mae", 30, mother: grandmother.Id);
        var aunt = Person(world, "tia", 33, mother: grandmother.Id);
        var child = Person(world, "orfa", 5, mother: mother.Id);
        mother.Die(world.CurrentDate);
        grandmother.Die(world.CurrentDate);
        House(world, child);
        var stranger = Person(world, "estranha", 40, altruism: 100);
        House(world, stranger);
        var auntHome = House(world, aunt);

        clock.Run(world, Month);
        Assert.Equal(auntHome.Id, child.Household);

        var lonely = World(2);
        var lonelyClock = new WorldClock([new OrphanCareSystem()]);
        var kid = Person(lonely, "sozinha", 4);
        var kidHome = House(lonely, kid);
        lonelyClock.Run(lonely, Month);
        Assert.Equal(kidHome.Id, kid.Household);
    }

    /// <summary>Mortes de crianças por privação e meses-criança <b>seguidos</b> sem adulto no domicílio (com adulto disponível na cidade), em 6 anos de vila.
    /// A morte dos pais dispara depois do sistema mensal no último tick, então uma virada de mês descoberta é esperada; duas seguidas, não.</summary>
    private static (int Deaths, int OrphanMonths, string Hash) RunVillage(ulong seed, bool withOrphanCare)
    {
        var ontogeny = withOrphanCare ? OntogenyDefaults.Human : OntogenyRules.Create(true, OntogenyDefaults.HumanMilestones, OntogenyDefaults.HumanRequirements, orphanCare: false).Value!;
        var (world, _) = ScenarioRunner.Create(seed, initialPopulation: 100, ontogenyRules: ontogeny);
        var systems = ScenarioRunner.DefaultSystems();
        int deaths = 0, orphanMonths = 0;
        var uncoveredBefore = new HashSet<NpcId>();
        var clock = new WorldClock(systems, 1000, new DeathSink(() => world, () => deaths++));
        var rules = world.LifeStageRules;
        for (int month = 0; month < 6 * 12; month++)
        {
            clock.Run(world, Month);
            var now = world.CurrentDate;
            var uncoveredNow = new HashSet<NpcId>();
            foreach (var kid in world.Npcs)
            {
                if (!kid.IsAlive || rules.LifeStageOf(kid.AgeYears(now)) != LifeStage.Child) continue;
                bool covered = kid.Household is { } id && world.FindHousehold(id) is { } home && HouseholdRedistribution.HasLivingAdultOrElder(world, home, rules, now);
                if (covered) continue;
                bool adultAvailable = world.Npcs.Any(n => n.IsAlive && n.Household is not null && n.City == kid.City && rules.LifeStageOf(n.AgeYears(now)) is LifeStage.Adult or LifeStage.Elder);
                if (!adultAvailable) continue;
                uncoveredNow.Add(kid.Id);
                if (uncoveredBefore.Contains(kid.Id)) orphanMonths++;
            }
            uncoveredBefore = uncoveredNow;
        }
        return (deaths, orphanMonths, WorldSnapshot.CanonicalHash(world));
    }

    private sealed class DeathSink(Func<WorldState> world, Action onChildDeath) : IWorldEventSink
    {
        public void Record(WorldEvent e)
        {
            if (e.Kind is not (WorldEventKind.Starvation or WorldEventKind.Dehydration or WorldEventKind.Exhaustion)) return;
            var w = world();
            if (w.FindNpc(new NpcId(long.Parse(e.Payload.Split('|')[0]))) is { } npc && npc.AgeYears(w.CurrentDate) < 14) onChildDeath();
        }
    }

    [Fact]
    [Trait("Category", "Scenario")]
    public void Orphan_care_leaves_no_child_without_an_available_adult_and_lowers_child_deaths()
    {
        int controlDeaths = 0, treatedDeaths = 0, treatedOrphanMonths = 0, worse = 0, fewer = 0, hashesChanged = 0;
        for (ulong seed = 1; seed <= Seeds; seed++)
        {
            var with = RunVillage(seed, true);
            var without = RunVillage(seed, false);
            treatedOrphanMonths += with.OrphanMonths;
            treatedDeaths += with.Deaths;
            controlDeaths += without.Deaths;
            if (with.Hash != without.Hash) hashesChanged++;
            if (with.Deaths > without.Deaths) worse++;
            if (with.Deaths < without.Deaths) fewer++;
        }
        Assert.Equal(0, treatedOrphanMonths);
        // Sem o acolhimento o órfão sem parente morre em horas (antes de qualquer virada de mês): o controle aparece nas mortes, não nos meses-órfão.
        Assert.True(fewer >= 3 && controlDeaths > treatedDeaths, $"mortes de crianças: controle {controlDeaths}, com acolhimento {treatedDeaths}, menos em {fewer} seeds");
        Assert.True(hashesChanged > 0, "desligar o acolhimento devia mudar o hash em alguma seed");
        Assert.True(fewer > worse, $"mais mortes com o sistema em {worse} seeds, menos em {fewer}"); // mundos divergem após a 1ª adoção: por seed há ruído, o saldo tem de favorecer o acolhimento
    }

    [Fact]
    public void A_parent_who_marries_takes_the_young_children_who_would_be_left_alone()
    {
        var world = World();
        var ctx = new TickContext(world, world.Rng, world.Scheduler);
        var widow = Person(world, "viuva", 32);
        var kid1 = Person(world, "k1", 6, mother: widow.Id);
        var kid2 = Person(world, "k2", 3, mother: widow.Id);
        var oldHome = House(world, widow, kid1, kid2);
        var suitor = Person(world, "pretendente", 35);
        House(world, suitor);

        MarriageSystem.Marry(world, ctx, widow, suitor);

        Assert.Equal(widow.Household, kid1.Household);
        Assert.Equal(widow.Household, kid2.Household);
        Assert.NotEqual(oldHome.Id, kid1.Household);
        Assert.Null(world.FindHousehold(oldHome.Id)); // sem ninguém, o domicílio antigo se dissolveu
    }

    [Fact]
    public void Children_stay_with_the_remaining_adult_unless_they_are_the_leaving_spouses_own()
    {
        var world = World();
        var ctx = new TickContext(world, world.Rng, world.Scheduler);
        var father = Person(world, "pai", 38);
        var mother = Person(world, "mae", 34);
        var own = Person(world, "filha", 6, mother: mother.Id, father: father.Id);
        var other = Person(world, "sobrinho", 7);
        House(world, father, mother, own, other);
        var suitor = Person(world, "pretendente", 30);
        House(world, suitor);

        MarriageSystem.Marry(world, ctx, mother, suitor);

        Assert.Equal(mother.Household, own.Household);      // a filha vai com a mãe
        Assert.Equal(father.Household, other.Household);    // o sobrinho fica com o adulto que sobrou
        Assert.NotEqual(mother.Household, other.Household);
    }

    // --- Critério 2: choro atendido ---

    [Fact]
    public void A_caregiver_in_the_same_place_hears_the_cry_when_the_pantry_cannot_feed_the_child()
    {
        var world = World();
        var adult = Person(world, "mae", 30, wallet: 100);
        var baby = Person(world, "bebe", 0.2);
        baby.SetDevelopment(DevelopmentState.Newborn);
        var household = House(world, adult, baby);
        baby.SetCurrentAction(ActionType.Cry, 0);

        Assert.True(BehaviorDecisionSystem.CaregiverHearsCry(world, adult, LifeStage.Adult));

        baby.SetCurrentAction(ActionType.Sleep, 0);
        Assert.False(BehaviorDecisionSystem.CaregiverHearsCry(world, adult, LifeStage.Adult)); // não chora, ninguém levanta

        baby.SetCurrentAction(ActionType.Cry, 0);
        household.Deposit(new ResourceType(world.EconomyRules.FoodResourceId), 5);
        household.Deposit(new ResourceType(world.EconomyRules.WaterResourceId), 5);
        Assert.False(BehaviorDecisionSystem.CaregiverHearsCry(world, adult, LifeStage.Adult)); // despensa serve: não há o que comprar

        var broke = Person(world, "pobre", 30, wallet: 0);
        var brokeBaby = Person(world, "bebe2", 0.2);
        brokeBaby.SetCurrentAction(ActionType.Cry, 0);
        House(world, broke, brokeBaby);
        Assert.False(BehaviorDecisionSystem.CaregiverHearsCry(world, broke, LifeStage.Adult)); // sem carteira
        Assert.False(BehaviorDecisionSystem.CaregiverHearsCry(world, adult, LifeStage.Child));
    }

    [Fact]
    public void The_decision_wakes_the_caregiver_to_buy_when_the_child_cries_and_keeps_sleeping_otherwise()
    {
        var world = World();
        var adult = Person(world, "mae", 30, wallet: 100);
        var baby = Person(world, "bebe", 0.2);
        baby.SetDevelopment(DevelopmentState.Newborn);
        House(world, adult, baby);
        var market = MarketIndex.BuildForTick(world);

        baby.SetCurrentAction(ActionType.Sleep, 0);
        Assert.Equal(ActionType.Sleep, BehaviorDecisionSystem.NonUrgentCandidate(world, adult, ActionType.Sleep, LifeStage.Adult, market));

        baby.SetCurrentAction(ActionType.Cry, 0);
        Assert.Equal(ActionType.Buy, BehaviorDecisionSystem.NonUrgentCandidate(world, adult, ActionType.Sleep, LifeStage.Adult, market));
    }

    // --- Critério 3: exposição graduada ---

    [Fact]
    public void Exposure_is_graded_by_caregivers_and_one_point_zero_is_the_binary_exposure()
    {
        var half = OntogenyRules.Create(true, OntogenyDefaults.HumanMilestones, OntogenyDefaults.HumanRequirements, attentionPerCaregiver: 0.5).Value!;
        Assert.Equal(0.0, half.ExposureFor(0));
        Assert.Equal(0.5, half.ExposureFor(1), 9);
        Assert.Equal(0.75, half.ExposureFor(2), 9);
        Assert.Equal(1.0, OntogenyDefaults.Human.ExposureFor(1));
        Assert.Equal(1.0, OntogenyDefaults.Human.ExposureFor(3));

        var binary = OntogenyDefaults.Human.Advance(DevelopmentState.Newborn, 0, 1.0, exposed: true, caregiverCultures: null);
        var graded = OntogenyDefaults.Human.Advance(DevelopmentState.Newborn, 0, 1.0, exposure: 1.0, caregiverCultures: null);
        Assert.Equal(binary, graded);
        var partial = half.Advance(DevelopmentState.Newborn, 0, 1.0, exposure: 0.5, caregiverCultures: null);
        Assert.Equal(binary.Language / 2, partial.Language, 9);
    }

    [Fact]
    public void With_partial_attention_two_caregivers_raise_more_than_one_in_every_seed()
    {
        var rules = OntogenyRules.Create(true, OntogenyDefaults.HumanMilestones, OntogenyDefaults.HumanRequirements, attentionPerCaregiver: 0.5).Value!;
        int more = 0;
        for (int seed = 1; seed <= Seeds; seed++)
        {
            double Score(int caregivers)
            {
                var world = World(seed, rules);
                var clock = new WorldClock([new MilestoneProgressSystem()]);
                var members = new List<Npc>();
                for (int i = 0; i < caregivers; i++) members.Add(Person(world, "c", 30));
                var child = Person(world, "k", 0, rate: RateGene.RollInitial(new WorldRng((ulong)seed)).Value);
                child.SetDevelopment(DevelopmentState.Newborn);
                members.Add(child);
                House(world, members.ToArray());
                clock.Run(world, 3 * 12 * Month);
                return child.Development is { } d ? Enum.GetValues<DevelopmentAxis>().Sum(d.Get) : 6;
            }
            if (Score(2) > Score(1)) more++;
        }
        Assert.Equal(Seeds, more);
    }

    // --- Critério 4: criação adotiva ---

    private static Npc Raise(int seed, double adoptiveUpbringing, bool parentIsCaregiver)
    {
        var world = World(seed);
        var clock = new WorldClock([new MilestoneProgressSystem()]);
        var caregiver = Person(world, "cuidador", 30, upbringing: adoptiveUpbringing);
        var child = Person(world, "crianca", 0, upbringing: 10, mother: parentIsCaregiver ? caregiver.Id : null);
        child.SetDevelopment(DevelopmentState.Newborn);
        House(world, caregiver, child);
        clock.Run(world, 22L * 12 * Month);
        return child;
    }

    [Fact]
    public void An_adopted_child_ends_closer_to_the_caregivers_upbringing_while_the_parents_children_keep_theirs()
    {
        for (int seed = 1; seed <= Seeds; seed++)
        {
            var adopted = Raise(seed, 90, parentIsCaregiver: false);
            Assert.True(adopted.Upbringing > 50, $"seed {seed}: {adopted.Upbringing}");

            var biological = Raise(seed, 90, parentIsCaregiver: true);
            Assert.Equal(10, biological.Upbringing);
        }
    }

    // --- Critério 5: guarda do pool agregado ---

    [Fact]
    public void A_child_in_development_cannot_be_dematerialized_into_the_aggregate_pool()
    {
        var world = World();
        var pool = new AggregatePopulationPool(3, 300, 240);
        var city = new City(
            world.NextCityId(), ScenarioRunner.DefaultVillageLocation, foundedAtTick: 0, foundedFromCityId: null,
            aggregatePool: pool, poolNpcIds: world.ReserveNpcIdBlock(pool.Count));
        world.AddCity(city);
        world.ObservationRegistry.SetScope("test", SpaceScope.City(city.Id));
        var ctx = new TickContext(world, world.Rng, world.Scheduler);

        var npc = MaterializationSystem.MaterializeOne(world, ctx, city.Id).Value!;
        npc.SetDevelopment(DevelopmentState.Newborn);
        var refused = MaterializationSystem.Dematerialize(world, npc.Id);
        Assert.False(refused.IsSuccess);
        Assert.Contains("criança", refused.Error);

        npc.SetDevelopment(null);
        Assert.True(MaterializationSystem.Dematerialize(world, npc.Id).IsSuccess);
    }

    [Fact]
    public void Invalid_attention_is_rejected_by_name()
    {
        Assert.Contains("AttentionPerCaregiver", OntogenyRules.Create(true, OntogenyDefaults.HumanMilestones, OntogenyDefaults.HumanRequirements, attentionPerCaregiver: 0).Error);
        Assert.Contains("OrphanCare", OntogenyScenarioLoader.Load("""{"Ontogeny":{"Enabled":true,"OrphanCare":"x"}}""").Error);
        Assert.Contains("AttentionPerCaregiver", OntogenyScenarioLoader.Load("""{"Ontogeny":{"Enabled":true,"AttentionPerCaregiver":"x"}}""").Error);
    }
}
