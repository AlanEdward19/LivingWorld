using LivingWorld.Domain.Cities;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Infrastructure.EventLog;
using LivingWorld.Simulation.Cities.Construction;
using LivingWorld.Simulation.Cities.Queries;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Players;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Tests.Unit.Players;

/// <summary>AD-197q (Fase 25, task 1): criador de personagem pelo motor. A pessoa nova sai do agregado da cidade (materializar move,
/// nunca cria), com o que o agregado diz dela, e a amostra é reproduzível pelo registro do jogador.</summary>
public class CharacterCreationTests
{
    internal sealed record Arm(WorldState World, WorldClock Clock, BufferingWorldEventSink Sink, City City);

    /// <summary>A vila de sempre, rodada um pouco (ofícios contratados), com <paramref name="pool"/> pessoas no agregado da cidade.</summary>
    internal static Arm Village(ulong seed, long pool = 40)
    {
        var (world, _) = ScenarioRunner.Create(seed);
        var sink = new BufferingWorldEventSink();
        var clock = new WorldClock(ScenarioRunner.DefaultSystems(), sink: sink);
        clock.Run(world, 96);
        sink.DrainAll();
        var city = world.ActiveCities().First();
        if (pool > 0) city.AbsorbPool(new AggregatePopulationPool(pool, pool * 300, pool * 70), world.ReserveNpcIdBlock(pool));
        return new Arm(world, clock, sink, city);
    }

    [Fact]
    public void Candidates_come_from_the_aggregate_and_entering_moves_one_person_without_changing_the_population()
    {
        var arm = Village(3);
        var w = arm.World;
        var service = new PlayerService(arm.Sink);
        long population = CityPopulationQuery.Population(w, arm.City.Id);
        long wealth = CityPopulationQuery.Wealth(w, arm.City.Id);
        int rows = w.Npcs.Count;

        var sample = service.Sample(w, "ana", arm.City.Id, CharacterFilter.None, 6);
        Assert.True(sample.IsSuccess);
        Assert.Equal(6, sample.Value!.Candidates.Count);
        Assert.All(sample.Value.Candidates, c => Assert.Contains(new NpcId(c.NpcId), arm.City.PoolNpcIds));
        var chosen = sample.Value.Candidates.First(c => !c.WillPrevents);

        var entered = service.Create(w, "ana", arm.City.Id, new NpcId(chosen.NpcId), sample.Value.Sample);

        Assert.True(entered.IsSuccess, entered.Error);
        Assert.Equal("ana", entered.Value!.ControllerId);
        Assert.DoesNotContain(new NpcId(chosen.NpcId), arm.City.PoolNpcIds);
        Assert.Equal(39, arm.City.AggregatePool.Count);
        Assert.Equal(rows + 1, w.Npcs.Count);
        Assert.Equal(population, CityPopulationQuery.Population(w, arm.City.Id)); // conservação: só mudou de lado
        Assert.Equal(wealth, CityPopulationQuery.Wealth(w, arm.City.Id));
    }

    [Fact]
    public void The_person_who_enters_is_the_candidate_shown_and_is_coherent_with_the_aggregate_of_origin()
    {
        var arm = Village(5);
        var w = arm.World;
        var adults = w.Npcs.Where(n => n.IsAlive && n.City == arm.City.Id && n.AgeYears(w.CurrentDate) >= MaterializationPrior.AdultYears).ToList();
        var professions = adults.Select(n => n.Profession.Id).ToHashSet();
        var ages = adults.Select(n => n.AgeYears(w.CurrentDate)).ToHashSet();
        var culture = adults.GroupBy(n => n.Culture.Id).OrderByDescending(g => g.Count()).ThenBy(g => g.Key).First().Key;
        long perHead = arm.City.AggregatePool.WealthSum / arm.City.AggregatePool.Count;
        var service = new PlayerService(arm.Sink);
        var sample = service.Sample(w, "bia", arm.City.Id, CharacterFilter.None, 8).Value!;
        var chosen = sample.Candidates.First(c => !c.WillPrevents);

        var npc = service.Create(w, "bia", arm.City.Id, new NpcId(chosen.NpcId), sample.Sample).Value!;

        Assert.Equal((chosen.Name, chosen.Sex, chosen.AgeYears, chosen.ProfessionId, chosen.Wealth, chosen.Health),
            (npc.Name, npc.Sex, npc.AgeYears(w.CurrentDate), npc.Profession.Id, npc.Wallet.Amount, npc.Health));
        Assert.Equal(arm.City.Id, npc.City);
        Assert.Contains(npc.Profession.Id, professions); // ofício da população da cidade, não sorteio livre do catálogo
        Assert.Equal(culture, npc.Culture.Id);
        Assert.Contains(npc.AgeYears(w.CurrentDate), ages); // idade pela pirâmide dos adultos da cidade, não uniforme até a longevidade
        Assert.All(sample.Candidates, c => Assert.Contains(c.AgeYears, ages));
        Assert.Equal(perHead, npc.Wallet.Amount);
        // sem parente inventado: quem a pessoa tem segue no agregado
        Assert.Null(npc.Household);
        Assert.Null(npc.MotherId);
        Assert.Null(npc.FatherId);
        Assert.Null(npc.Spouse);
        Assert.True(npc.AgeYears(w.CurrentDate) >= MaterializationPrior.AdultYears);
    }

    [Fact]
    public void Same_seed_and_same_log_give_the_same_candidates_and_the_same_hash()
    {
        var a = Village(7);
        var b = Village(7);
        var sa = new PlayerService(a.Sink).Sample(a.World, "c", a.City.Id, CharacterFilter.None, 6).Value!;
        var sb = new PlayerService(b.Sink).Sample(b.World, "c", b.City.Id, CharacterFilter.None, 6).Value!;
        Assert.Equal(sa.Candidates.Select(c => c.NpcId), sb.Candidates.Select(c => c.NpcId));

        var arm = Village(9);
        var w = arm.World;
        string snapshot = WorldSnapshot.Serialize(w);
        var service = new PlayerService(arm.Sink);
        var first = service.Sample(w, "dora", arm.City.Id, CharacterFilter.None, 6).Value!;
        var second = service.Sample(w, "dora", arm.City.Id, CharacterFilter.None, 6).Value!; // outra amostra: também registrada
        Assert.NotEqual(first.Candidates.Select(c => c.NpcId), second.Candidates.Select(c => c.NpcId));
        arm.Clock.Run(w, 2);
        var chosen = second.Candidates.First(c => !c.WillPrevents);
        Assert.True(service.Create(w, "dora", arm.City.Id, new NpcId(chosen.NpcId), second.Sample).IsSuccess);
        arm.Clock.Run(w, 24);

        Assert.Equal(2, service.Log.Records.Count(r => r.Kind == PlayerCommandKind.Sample));
        var replayed = PlayerReplay.Run(snapshot, service.Log.Records,
            new WorldClock(ScenarioRunner.DefaultSystems(), sink: new BufferingWorldEventSink()), new BufferingWorldEventSink(),
            w.CurrentDate.TotalMilliseconds);
        Assert.Equal(WorldSnapshot.CanonicalHash(w), WorldSnapshot.CanonicalHash(replayed));
        Assert.Equal("dora", replayed.FindNpc(new NpcId(chosen.NpcId))!.ControllerId);
    }

    [Fact]
    public void An_empty_aggregate_is_refused_with_conflict_and_nothing_changes()
    {
        var arm = Village(11, pool: 0);
        var w = arm.World;
        string before = WorldSnapshot.CanonicalHash(w);
        var service = new PlayerService(arm.Sink);

        var sample = service.Sample(w, "eva", arm.City.Id, CharacterFilter.None, 6);
        var create = service.Create(w, "eva", arm.City.Id, new NpcId(1), 0);

        Assert.StartsWith(PlayerCommands.Conflict, sample.Error);
        Assert.StartsWith(PlayerCommands.Conflict, create.Error);
        Assert.Empty(service.Log.Records);
        Assert.Equal(before, WorldSnapshot.CanonicalHash(w));
    }

    [Fact]
    public void The_filter_only_narrows_who_the_aggregate_has_and_a_stale_or_foreign_choice_is_refused_without_effect()
    {
        var arm = Village(13);
        var w = arm.World;
        var service = new PlayerService(arm.Sink);
        var women = service.Sample(w, "flor", arm.City.Id, new CharacterFilter(Sex: Sex.Female, MinAge: 30), 12).Value!;
        Assert.NotEmpty(women.Candidates);
        Assert.All(women.Candidates, c => Assert.True(c.Sex == Sex.Female && c.AgeYears >= 30));
        Assert.StartsWith(PlayerCommands.Conflict,
            service.Sample(w, "flor", arm.City.Id, new CharacterFilter(MinAge: 500), 6).Error);

        string before = WorldSnapshot.CanonicalHash(w);
        long outside = arm.City.PoolNpcIds.First(id => women.Candidates.All(c => c.NpcId != id.Value)).Value;
        Assert.StartsWith(PlayerCommands.Conflict, service.Create(w, "flor", arm.City.Id, new NpcId(outside), women.Sample).Error);
        Assert.StartsWith(PlayerCommands.Conflict, service.Create(w, "outro", arm.City.Id, new NpcId(women.Candidates[0].NpcId), women.Sample).Error);
        Assert.Equal(before, WorldSnapshot.CanonicalHash(w));
    }
}
