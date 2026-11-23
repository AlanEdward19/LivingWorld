using LivingWorld.Domain.Cognition;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Economy.Credit;
using LivingWorld.Simulation.Players;
using static LivingWorld.Tests.Unit.Players.PlayerAbsenceTests;

namespace LivingWorld.Tests.Unit.Players;

/// <summary>AD-196 (Fase 25, #63): a política de abandono declarada no cenário e a volta tardia.</summary>
public class PlayerAbandonmentTests
{
    private const int Window = 40;

    /// <summary>Casado, com filhos em casa, moeda e uma dívida; encarna e sai.</summary>
    private static (Arm Arm, Npc Missing, Npc Spouse, Npc Lender) Married(ulong seed, PlayerRules rules)
    {
        for (; ; seed++)
        {
            var arm = Village(seed, rules);
            var w = arm.World;
            var npc = w.Npcs.FirstOrDefault(n => n.IsAlive && n.Spouse is { } s && w.FindNpc(s) is { IsAlive: true } sp && sp.Spouse == n.Id
                && n.Household is not null && w.FindHousehold(n.Household!.Value)!.Members.Count >= 3);
            if (npc is null) continue;
            var spouse = w.FindNpc(npc.Spouse!.Value)!;
            var lender = w.Npcs.First(n => n.IsAlive && n.Id != npc.Id && n.Id != spouse.Id && n.Household != npc.Household);
            lender.CreditWallet(new Money(10_000));
            npc.CreditWallet(new Money(3_000));
            Lending.Grant(w, arm.Ctx, new LoanOffer(lender, null, 1_000, 0.1, 12, 1_000), npc, LoanPurpose.Working, 0);
            Assert.True(PlayerCommands.Incarnate(w, arm.Ctx, npc.Id, "alan").IsSuccess);
            Assert.True(PlayerCommands.Logout(w, arm.Ctx, "alan").IsSuccess);
            var elsewhere = new LivingWorld.Domain.Cities.City(w.NextCityId(), new LivingWorld.Domain.Geography.CellCoord(7, 7), 0, null,
                LivingWorld.Domain.Cities.AggregatePopulationPool.Empty);
            w.AddCity(elsewhere);
            npc.JoinCity(elsewhere.Id); // foi embora: a procura não o acha e a janela do cenário corre inteira
            return (arm, npc, spouse, lender);
        }
    }

    private static readonly PlayerRules Declare = PlayerRules.Default with
    {
        Abandonment = AbandonmentPolicy.DeclareDead, DeclareDeadAfterDays = Window,
    };

    private static void Days(Arm arm, int days) => arm.Clock.Run(arm.World, (long)days * arm.World.Calendar.HoursPerDay);

    [Fact]
    public void Declared_dead_after_the_window_goes_down_the_path_of_any_death()
    {
        var (arm, missing, spouse, lender) = Married(3, Declare);
        long lenderBefore = lender.Wallet.Amount;

        Days(arm, Window + 1);

        var events = arm.Sink.Events;
        var declared = Assert.Single(events, e => e.Kind == WorldEventKind.PresumedDead);
        Assert.Equal("NpcDeath", declared.SourceSystem);
        Assert.False(missing.IsAlive);
        Assert.Contains(events, e => e.Kind == WorldEventKind.EstateSettled && e.Payload.StartsWith($"{missing.Id.Value}|", StringComparison.Ordinal)
            && e.Payload.Split('|')[1].Split(',').Contains(spouse.Id.Value.ToString())); // herança pela norma: o cônjuge herda
        Assert.DoesNotContain(arm.World.Credit!.Loans, l => l.Borrower == missing.Id.Value); // a dívida saiu do espólio
        Assert.True(lender.Wallet.Amount > lenderBefore);
        Assert.Equal(missing.Id, spouse.Spouse); // viuvez: o cônjuge aponta para quem morreu
        Assert.Contains(spouse.Psyche!.Memories, m => m.Kind == LifeEventKind.SpouseDied && m.Other == missing.Id.Value); // luto e lembrança
    }

    [Fact]
    public void The_abandonment_policy_is_scenario_data_and_absent_keeps_the_default_out_of_the_hash()
    {
        string dir = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(dir, "LivingWorld.sln"))) dir = Path.GetDirectoryName(dir)!;
        var root = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(Path.Combine(dir, "scenarios", "default.json")))!.AsObject();
        var plain = LivingWorld.Simulation.Scenarios.ScenarioLoaderV2.LoadWorld(root.ToJsonString());
        root["Players"] = new System.Text.Json.Nodes.JsonObject
        {
            ["Abandonment"] = "DeclareDead", ["DeclareDeadAfterDays"] = 90, ["PresumedDeadMayReturn"] = false,
        };

        var declared = LivingWorld.Simulation.Scenarios.ScenarioLoaderV2.LoadWorld(root.ToJsonString());

        Assert.True(declared.IsSuccess, declared.Error);
        var rules = declared.Value.World.PlayerRules!;
        Assert.Equal((AbandonmentPolicy.DeclareDead, 90, false), (rules.Abandonment, rules.DeclareDeadAfterDays, rules.PresumedDeadMayReturn));
        Assert.Null(plain.Value.World.PlayerRules);
        Assert.DoesNotContain("\"PlayerRules\"", WorldSnapshot.Serialize(plain.Value.World), StringComparison.Ordinal);
    }

    [Fact]
    public void Staying_missing_never_declares_anyone_dead()
    {
        var (arm, missing, _, _) = Married(3, PlayerRules.Default with { DeclareDeadAfterDays = Window });
        Days(arm, Window + 1);
        Assert.DoesNotContain(arm.Sink.Events, e => e.Kind == WorldEventKind.PresumedDead);
        Assert.True(missing.IsAlive);
    }

    [Fact]
    public void With_AD196_off_the_declared_policy_is_ignored()
    {
        var (arm, missing, _, _) = Married(3, PlayerRules.Off with { Abandonment = AbandonmentPolicy.DeclareDead, DeclareDeadAfterDays = Window });
        Days(arm, Window + 1);
        Assert.DoesNotContain(arm.Sink.Events, e => e.Kind == WorldEventKind.PresumedDead);
        Assert.True(missing.IsAlive);
    }

    [Fact]
    public void By_default_the_declared_dead_cannot_come_back()
    {
        var (arm, missing, _, _) = Married(3, Declare);
        Days(arm, Window + 1);
        Assert.StartsWith(PlayerCommands.NotFound, PlayerCommands.Incarnate(arm.World, arm.Ctx, missing.Id, "alan").Error);
    }

    [Fact]
    public void When_the_scenario_lets_the_declared_return_the_estate_and_widowhood_run_and_are_not_undone()
    {
        var (arm, missing, spouse, _) = Married(3, Declare with { PresumedDeadMayReturn = true });
        Days(arm, Window + 1);

        Assert.True(missing.IsAlive);
        Assert.NotNull(missing.PresumedDeadAtTick);
        Assert.Contains(arm.Sink.Events, e => e.Kind == WorldEventKind.EstateSettled && e.Payload.StartsWith($"{missing.Id.Value}|", StringComparison.Ordinal));
        Assert.Null(spouse.Spouse); // livre para se unir de novo
        Assert.Contains(spouse.Psyche!.Memories, m => m.Kind == LifeEventKind.SpouseDied && m.Other == missing.Id.Value);

        var other = arm.World.Npcs.First(n => n.IsAlive && n.Spouse is null && n.Sex != spouse.Sex && n.Id != missing.Id
            && n.AgeYears(arm.World.CurrentDate) >= 18);
        spouse.Marry(other.Id); // o cônjuge se uniu de novo
        other.Marry(spouse.Id);
        long wallet = missing.Wallet.Amount;

        Assert.True(PlayerCommands.Incarnate(arm.World, arm.Ctx, missing.Id, "alan").IsSuccess);

        Assert.Equal(other.Id, spouse.Spouse); // a nova união não é desfeita (AD-136)
        Assert.Null(missing.Spouse);
        Assert.Equal(wallet, missing.Wallet.Amount); // a herança dividida não volta
        Assert.Contains(arm.Sink.Events, e => e.Kind == WorldEventKind.Returned && e.Payload.EndsWith("|jogador", StringComparison.Ordinal));
    }

    [Fact]
    public void Back_to_the_pool_the_engine_brings_the_missing_home_to_a_waiting_spouse()
    {
        var (arm, missing, spouse, _) = Married(3, PlayerRules.Default with
        {
            Abandonment = AbandonmentPolicy.ReturnToPool, ReturnToPoolAfterDays = Window,
        });
        Days(arm, Window + 1);

        Assert.True(missing.IsAlive);
        Assert.Null(missing.DisappearedAtTick);
        Assert.Contains(arm.Sink.Events, e => e.Kind == WorldEventKind.Returned && e.Payload.EndsWith("|pool", StringComparison.Ordinal));
        Assert.Equal(spouse.Household, missing.Household); // o cônjuge que esperou o recebe em casa
    }

    /// <summary>Saliência da lembrança de reencontro do cônjuge depois de <paramref name="days"/> dias sumido, com vínculo
    /// <paramref name="bond"/>.</summary>
    private static int Reunion(ulong seed, int days, double bond)
    {
        var (arm, missing, spouse, _) = Married(seed, PlayerRules.Default);
        long now = arm.World.CurrentDate.TotalHours;
        arm.World.GetOrCreateRelationship(new RelationshipKey(spouse.Id, missing.Id), now).CopyAxesFrom(Relationship.FromAxes(bond, bond, 50, 0, now));
        missing.CreditWallet(new Money(20_000));
        Days(arm, days);
        if (!missing.IsAlive || !spouse.IsAlive) return -1;
        arm.World.GetOrCreateRelationship(new RelationshipKey(spouse.Id, missing.Id), arm.World.CurrentDate.TotalHours)
            .CopyAxesFrom(Relationship.FromAxes(bond, bond, 50, 0, now));
        Assert.True(PlayerCommands.Incarnate(arm.World, arm.Ctx, missing.Id, "alan").IsSuccess);
        return spouse.Psyche?.Memories.Where(m => m.Kind == LifeEventKind.Reunited && m.Other == missing.Id.Value).Sum(m => m.Salience) ?? 0;
    }

    [Fact]
    public void The_late_return_reaction_grows_with_the_bond_and_with_the_time_away()
    {
        int shortStrong = Reunion(3, 10, 95), longStrong = Reunion(3, 200, 95), longWeak = Reunion(3, 200, 30);
        Assert.True(longStrong > shortStrong, $"tempo: {longStrong} <= {shortStrong}");
        Assert.True(longStrong > longWeak, $"vínculo: {longStrong} <= {longWeak}");
    }
}
