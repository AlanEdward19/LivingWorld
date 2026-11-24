using LivingWorld.Domain.Crime;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Tests.Unit.Crime;

/// <summary>Mundo pequeno para os testes de crime (AD-150): pessoas, casas e oficinas montadas à mão numa vila de uma cidade
/// só (a cidade padrão), com furto, violência e justiça configuráveis.</summary>
internal static class CrimeTestWorld
{
    public static readonly WorldCalendar Calendar = new(24, 30, 12);
    public static readonly CellCoord Here = new(1, 1);

    public static readonly Personality Criminal = Personality.Create(50, 50, 0, 50, 50, 50, 50, 0, 100, 0).Value!;
    public static readonly Personality Honest = Personality.Create(50, 50, 100, 50, 50, 50, 50, 100, 0, 100).Value!;
    public static readonly Personality Average = Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!;

    public sealed class Sink : IWorldEventSink
    {
        public List<WorldEvent> Events { get; } = [];
        public void Record(WorldEvent evt) => Events.Add(evt);
        public IEnumerable<WorldEvent> Of(LivingWorld.Domain.History.WorldEventKind kind) => Events.Where(e => e.Kind == kind);
    }

    public static WorldState New(TheftRules? theft = null, CrimeRules? crime = null, ulong seed = 1)
    {
        var world = new WorldState(
            Calendar, seed, ScenarioRunner.DefaultMap(1), ScenarioRunner.DefaultPopulationCatalog,
            ScenarioRunner.DefaultPopulationRules, ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog,
            ScenarioRunner.DefaultLifeStageRules, economyRules: ScenarioRunner.DefaultEconomyRules,
            economyCatalog: ScenarioRunner.DefaultEconomyCatalog, familyRules: ScenarioRunner.DefaultFamilyRules,
            theftRules: theft ?? TheftRules.Default);
        world.UseCrime(crime);
        return world;
    }

    public static Npc Add(WorldState world, Personality personality, long wallet = 0, CellCoord? at = null, HouseholdId? home = null,
        Sex sex = Sex.Male, int age = 25, int profession = 1)
    {
        var where = at ?? Here;
        var npc = new Npc(
            world.NextNpcIdAndAdvance(), "npc", sex, WorldDate.Epoch(Calendar).AddYears(-age), new CultureId(1),
            where, null, null, household: home, health: 100, personality: personality,
            profession: new ProfessionType(profession), currentLocation: where);
        if (wallet > 0) npc.CreditWallet(new Money(wallet));
        world.AddNpc(npc);
        return npc;
    }

    public static Household Home(WorldState world, CellCoord at, params Npc[] members)
    {
        var id = new HouseholdId(world.Households.Count + 100);
        var household = new Household(id, at, members[0].Id, members.Select(m => m.Id).ToList());
        world.AddHousehold(household);
        foreach (var m in members) m.JoinHousehold(id);
        return household;
    }

    public static LivingWorld.Domain.Economy.Workplace Shop(WorldState world, int locationTypeId, CellCoord at,
        Dictionary<int, long>? stock = null, long treasury = 0, Dictionary<int, long>? prices = null)
    {
        var shop = new LivingWorld.Domain.Economy.Workplace(
            world.NextWorkplaceIdAndAdvance(), new LocationType(locationTypeId), at, 10, [],
            (stock ?? []).ToDictionary(p => new ResourceType(p.Key), p => p.Value), new Money(treasury),
            (prices ?? []).ToDictionary(p => new ResourceType(p.Key), p => p.Value));
        world.AddWorkplace(shop);
        return shop;
    }

    public static TickContext Ctx(WorldState world, IWorldEventSink? sink = null) => new(world, world.Rng, world.Scheduler, sink);

    public static long Coin(WorldState world) =>
        world.Npcs.Sum(n => n.Wallet.Amount) + world.Workplaces.Sum(w => w.Treasury.Amount) + world.Cities.Sum(c => c.Treasury.Amount);
}
