using System.Text.Json;
using LivingWorld.Domain.Extraordinary;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Domain.Population.Skills;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Extraordinary.Culture;
using LivingWorld.Simulation.Extraordinary.Engine;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Tests.Unit.Extraordinary.Engine;

/// <summary>
/// ADR-0040: carga e invocação leem o token pelo mesmo parser. Diferencial contra o motor real, num mundo
/// onde tudo o que depende do mundo está a favor: invocação aceita => carga aceita (F2); carga aceita =>
/// invocação aceita, salvo a lista explícita de tokens que dependem do mundo (F1).
/// </summary>
public sealed class LoadInvocationParityTests
{
    // Tokens que passam na sintaxe mas a invocação recusa por estado do mundo que o mundo de teste não tem.
    private static readonly IReadOnlyDictionary<string, string> WorldDependent = new Dictionary<string, string>
    {
        ["mind.commune"] = "alvo precisa ser fantasma",
        ["mind.implant-memory:5"] = "Fact 5 precisa existir",
        ["mind.implant-memory:fire:5"] = "Fact 5 precisa existir",
        ["transfer.lifespan-years:2:from-target"] = "o doador é o alvo do mundo de teste (AD-130: sem morte agendada, só o estado dele decide)",
    };

    // Casos F1/F2 e tolerados do ADR-0040, na mesma lista do critério da rodada 3.
    private static readonly string[] Effects =
    [
        "area:radius:fire:3", "area:region:fire:2", "area:radius:3", "area:region:0", "area:radius", "area:radius:",
        "area:foo:3", "area:radius:-1", "area: radius:3",
        "matter.transmute :1:2:3", "matter.transmute:1:2:3", "matter.transmutex:1:2:3", "matter.transmute:1:2:0",
        "matter.transmute:-1:2:3", "foresight.preview: ", "foresight.preview:\t", "foresight.preview:", "foresight.preview:death",
        "movement.flight:Infinity", "movement.speed-multiplier:Infinity", "movement.flight", "movement.qualquer",
        "movement.flight:abc", "movement.flight:-1", "movement.", "movement",
        "attribute.strength:abc", "attribute.strength", "attribute.fertility:2.5", "attribute.perception:x",
        "attribute.strengthx:2", "attribute.strength:fire:abc",
        "npc.health:15", "npc.hunger:-10", "npc.health:sunlight:-10", "npc.health:0", "npc.healthy:5", "npc.mana:5",
        "npc.teleport:5", "npc.teleportx:5", "npc.teleport:0", "npc.force-action:1", "npc.force-action:99", "npc.force-actionx:1",
        "construct.create:1x1:10:5:stone", "construct.create :1x1:10:5:stone", "construct.create:1x1:10:5: ",
        "construct.create:9x1:10:5:stone", "construct.create:1x1:10:5:stone:extra",
        "gravity.self:0.5", "gravity.self:abc", "gravity.target:2", "gravity.target:-1", "gravity.target:fire:2",
        "gravity.self:Infinity", "gravity.target:Infinity", "environment.temperature:0:Infinity:10", "skill.learn-rate:Infinity",
        "transfer.health:20", "transfer.thirst:5:from-target", "transfer.health:-1", "transfer.health:fire:5:from-target",
        "transfer.health:fire:20", "transfer.lifespan-years:2", "transfer.mana:3",
        "mind.read", "mind.read:x", "mind.read-memory", "mind.commune", "mind.erase-memory:5", "mind.erase-memory:abc",
        "mind.implant-memory:5", "mind.alter-trait:agreeableness:-5", "mind.alter-trait:mana:-5",
        "mind.alter-trait:agreeableness:fire:-5",
        "luck.capacity-bonus:10", "luck.capacity-bonus:0", "luck.curse:10", "luck.curse:10:100", "luck.curse:0", "luck.curse:fire:10",
        "skill.copy:1", "skill.copy:-1", "skill.copy:x", "skill.learn-rate:2", "skill.learn-rate:-1",
        "environment.temperature:0:-5:10", "environment.temperature:1",
        "dimension.pocket-store", "dimension.pocket-store:2", "dimension.pocket-store:abc", "dimension.pocket-store.x",
        "dimension.portal:0:0:3:3", "dimension.portal:1:1:1:1",
        "fauna.dominate:3", "fauna.dominate:animal-4", "fauna.dominate:-2", "fauna.infect-vector:plague", "fauna.infect-vector: ",
        "flora.growth-rate:5", "flora.growth-rate:-1",
        "combat.strike:8", "combat.strike:-8", "combat.strike:8:1", "combat.engage:3", "combat.engage:-3", "combat.strike:fire:8",
        "npc.clone:1", "npc.clone:-1", "npc.split-on-death:2", "npc.split-on-death:-2", "npc.reincarnate:50", "npc.reincarnate:101",
        "bond.share:health", "bond.share:social:50", "bond.share:healthy", "bond.share:health:0",
        "bond.oath:npc.health:-5", "bond.oath:", "bond.oath:unknown.token:1",
        "soul.persist-as-ghost", "soul.persist-as-ghost:1", "control.possess", "control.possess:Sleep",
        "control.possess:NotAnAction", "control.body-swap", "control.body-swap:1",
        "appearance.impersonate:5", "appearance.impersonate:0", "appearance.impersonate:abc",
    ];

    private static readonly string[] Costs =
    [
        "carrier.hunger:2", "carrier.hunger:fire:2", "carrier.hunger", "carrier.hunger:abc","carrier.hunger:-2", "carrier.hunger:0",
        "carrier.health:1", "carrier.sleep:sunlight:3", "carrier.mana:3", "household.resource.9:2",
        "household.resource.abc:1", "household.resource.9", "household.resource.9:-2",
    ];

    public static IEnumerable<object[]> EffectCases() => Variants(Effects).Select(token => new object[] { token });

    public static IEnumerable<object[]> CostCases() => Variants(Costs).Select(token => new object[] { token });

    // Cada token base mais variações de fronteira: o que um parser frouxo ou duplicado deixaria passar.
    private static IEnumerable<string> Variants(IEnumerable<string> bases) =>
        bases.SelectMany(token => new[]
            {
                token, token + "x", token + ":abc", token + ":", token + ":0", token + ":-1", token + " ", " " + token,
            })
            .Distinct(StringComparer.Ordinal);

    [Theory]
    [MemberData(nameof(EffectCases))]
    public void Effect_token_is_accepted_on_load_exactly_when_the_invocation_accepts_it(string token)
    {
        bool loads = Loads(token, null);
        bool invokes = Invokes(token, null, out string? error);

        AssertParity(token, loads, invokes, error);
    }

    [Theory]
    [MemberData(nameof(CostCases))]
    public void Cost_token_is_accepted_on_load_exactly_when_the_invocation_accepts_it(string token)
    {
        bool loads = Loads(NeutralEffect, token);
        bool invokes = Invokes(NeutralEffect, token, out string? error);

        AssertParity(token, loads, invokes, error);
    }

    private static void AssertParity(string token, bool loads, bool invokes, string? error)
    {
        if (WorldDependent.Keys.Any(key => token.StartsWith(key, StringComparison.Ordinal)))
        {
            // Sintaxe aceita pelo mundo de teste: nenhum aceite da invocação pode faltar na carga; o token
            // listado passa na carga e a invocação o recusa por estado do mundo.
            Assert.True(!invokes || loads, $"{token}: a invocação aceita e a carga recusa");
            if (WorldDependent.ContainsKey(token))
            {
                Assert.True(loads, $"{token}: dependente do mundo, a sintaxe deve passar na carga");
                Assert.False(invokes, $"{token}: saiu da lista de dependentes do mundo, remova-o");
            }

            return;
        }

        Assert.True(loads == invokes, $"{token}: carga={loads} invocação={invokes} ({error})");
    }

    [Theory]
    [InlineData("area:radius:fire:3")]
    [InlineData("area:region:fire:2")]
    public void F1_typed_area_selector_is_refused_on_load_because_every_invocation_refuses_it(string token)
    {
        Assert.False(Loads(token, null));
        Assert.False(Invokes(token, null, out _));
    }

    [Theory]
    [InlineData("matter.transmute :1:2:3")]
    [InlineData("foresight.preview: ")]
    [InlineData("foresight.preview:\t")]
    [InlineData("movement.flight:Infinity")]
    [InlineData("movement.speed-multiplier:Infinity")]
    public void F2_token_the_invocation_applies_is_accepted_on_load(string token)
    {
        Assert.True(Invokes(token, null, out string? error), error);
        Assert.True(Loads(token, null));
    }

    [Theory]
    [InlineData("movement.qualquer")]
    [InlineData("movement.flight")]
    [InlineData("attribute.strength:abc")]
    [InlineData("attribute.fertility")]
    [InlineData("area:radius")]
    [InlineData("area:foo:3")]
    public void Tolerated_tokens_the_invocation_accepts_without_effect_are_accepted_on_load(string token)
    {
        Assert.True(Invokes(token, null, out string? error), error);
        Assert.True(Loads(token, null));
    }

    private const string NeutralEffect = "npc.health:1";

    private static bool Loads(string effect, string? cost)
    {
        string json = JsonSerializer.Serialize(new
        {
            Extraordinary = new
            {
                Enabled = true,
                Descriptors = new[]
                {
                    new
                    {
                        Id = "p", Source = "s", Effects = new[] { effect }, Mode = "Active",
                        Costs = cost is null ? Array.Empty<string>() : [cost], Reliability = "Guaranteed",
                    },
                },
            },
        });
        return ExtraordinaryScenarioLoader.Load(json).IsSuccess;
    }

    private static bool Invokes(string effect, string? cost, out string? error)
    {
        var descriptor = new PowerDescriptor(
            "p", "s", [effect], "Active", cost is null ? [] : [cost], "Guaranteed", [], [], [], []);
        var (world, carrier, target) = RichWorld(descriptor);
        var result = ExtraordinaryInvocationEngine.Invoke(
            world, new TickContext(world, world.Rng, world.Scheduler),
            new ExtraordinaryInvocation(1, carrier.Id, "p", target.Id, TargetCell: new CellCoord(3, 3)));
        error = result.Error;
        return result.IsSuccess;
    }

    // Mundo a favor de qualquer efeito: estoque, atributos altos, habilidade no alvo, NPCs 1..9.
    private static (WorldState World, Npc Carrier, Npc Target) RichWorld(PowerDescriptor descriptor)
    {
        var state = new ExtraordinaryCarrierState(
            new NpcId(1), [descriptor.Id], true, "active",
            new ExtraordinaryAppearanceState(1, "", ""), null, 1);
        var world = new WorldState(
            ScenarioRunner.DefaultCalendar, 42, ScenarioRunner.DefaultMap(42),
            ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
            ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog,
            ScenarioRunner.DefaultLifeStageRules,
            extraordinary: new ExtraordinaryScenarioData(true, [descriptor]), extraordinaryCarriers: [state]);
        var stock = Enumerable.Range(0, 12).ToDictionary(id => new ResourceType(id), _ => 1000L);
        var home = new Household(new HouseholdId(1), new CellCoord(0, 0), new NpcId(1), [new NpcId(1)], stock);
        world.AddHousehold(home);
        for (long id = 1; id <= 9; id++)
        {
            var npc = Npc(new NpcId(id));
            npc.MoveTo(new CellCoord((int)id - 1, 0), 0);
            npc.SetHunger(80, 0);
            npc.SetThirst(80, 0);
            npc.SetSleep(80, 0);
            npc.SetSocial(80, 0);
            world.AddNpc(npc);
        }

        var target = world.FindNpc(new NpcId(2))!;
        target.GainSkill(new SkillType(1), 10, 100);
        return (world, world.FindNpc(new NpcId(1))!, target);
    }

    private static Npc Npc(NpcId id) => new(
        id, "npc" + id.Value, Sex.Male, WorldDate.Epoch(ScenarioRunner.DefaultCalendar),
        ScenarioRunner.DefaultCulture, new CellCoord(0, 0), motherId: null, fatherId: null,
        household: id == new NpcId(1) ? new HouseholdId(1) : null, health: 90,
        personality: Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!,
        profession: ProfessionType.None, currentLocation: new CellCoord(0, 0));
}
