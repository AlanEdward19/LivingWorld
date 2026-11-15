using LivingWorld.Domain.Crime;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Population.Lifecycle;
using static LivingWorld.Tests.Unit.Crime.CrimeTestWorld;

namespace LivingWorld.Tests.Unit.Crime;

/// <summary>Herança realista (AD-150, achado 30): a viúva fica com a terça parte e os filhos repartem o resto; a oficina vai ao
/// filho homem mais velho; a partilha desigual faz irmãos brigarem (mágoa). Antes o cônjuge herdava tudo; o mundo sem
/// <c>CrimeRules</c> (snapshot antigo) segue assim.</summary>
public class InheritanceTests
{
    private static Npc Child(WorldState world, Npc father, Npc mother, Sex sex, int age, Personality? personality = null)
    {
        var child = new Npc(world.NextNpcIdAndAdvance(), "c", sex, WorldDate.Epoch(Calendar).AddYears(-age), new CultureId(1),
            Here, mother.Id, father.Id, household: null, health: 100, personality: personality ?? Average,
            profession: new ProfessionType(1), currentLocation: Here);
        world.AddNpc(child);
        return child;
    }

    private static (WorldState World, Npc Father, Npc Widow, Npc Elder, Npc Younger) Family(CrimeRules? rules,
        Personality? kids = null)
    {
        var world = New(crime: rules);
        var father = Add(world, Average, wallet: 90, age: 60);
        var widow = Add(world, Average, sex: Sex.Female, age: 55);
        father.Marry(widow.Id);
        widow.Marry(father.Id);
        var younger = Child(world, father, widow, Sex.Male, 25, kids);
        var elder = Child(world, father, widow, Sex.Male, 30, kids);
        return (world, father, widow, elder, younger);
    }

    [Fact]
    public void The_widow_keeps_a_third_and_the_children_split_the_rest_and_coin_is_conserved()
    {
        var (world, father, widow, elder, younger) = Family(CrimeRules.Default with { Inheritance = new InheritanceRules { DisputeChance = 0 } });
        long coin = Coin(world);

        NpcDeath.Apply(world, Ctx(world), father, WorldEventKind.Death);

        Assert.Equal(30, widow.Wallet.Amount); // antes: 90, tudo ao cônjuge
        Assert.Equal(30, elder.Wallet.Amount);
        Assert.Equal(30, younger.Wallet.Amount);
        Assert.Equal(coin, Coin(world));
    }

    [Fact]
    public void Without_crime_rules_the_old_world_still_leaves_everything_to_the_spouse()
    {
        var (world, father, widow, _, _) = Family(null);
        NpcDeath.Apply(world, Ctx(world), father, WorldEventKind.Death);
        Assert.Equal(90, widow.Wallet.Amount);
    }

    [Fact]
    public void The_workshop_goes_to_the_eldest_son_and_the_unequal_share_makes_brothers_quarrel()
    {
        var greedy = LivingWorld.Domain.Population.Personality.Create(50, 0, 50, 50, 50, 50, 50, 50, 50, 50).Value!;
        var (world, father, _, elder, younger) = Family(CrimeRules.Default with { Inheritance = new InheritanceRules { DisputeChance = 5 } }, greedy);
        var forge = Shop(world, 2, new CellCoord(4, 4));
        forge.AssignOwner(father.Id);
        var sink = new Sink();

        NpcDeath.Apply(world, Ctx(world, sink), father, WorldEventKind.Death);

        Assert.Equal(elder.Id, forge.Owner); // antes: ia ao cônjuge, o único herdeiro
        var dispute = Assert.Single(sink.Of(WorldEventKind.InheritanceDisputed), e => e.Payload.Contains($"{elder.Id.Value}") && e.Payload.Contains($"{younger.Id.Value}"));
        Assert.Equal("heranca", world.Crime!.GrudgeOf(younger.Id.Value, elder.Id.Value)!.Motive);
        Assert.True(world.Crime.GrudgeOf(younger.Id.Value, elder.Id.Value)!.Intensity
            > world.Crime.GrudgeOf(elder.Id.Value, younger.Id.Value)!.Intensity); // quem ficou sem a oficina guarda mais
        Assert.NotNull(dispute);
    }
}
