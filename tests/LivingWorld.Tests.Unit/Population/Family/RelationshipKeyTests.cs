using LivingWorld.Domain;

namespace LivingWorld.Tests.Population;

/// <summary>Fase 7, T3 (FAM-01, FAM-05): par ordenado — A→B e B→A nunca colidem.</summary>
public class RelationshipKeyTests
{
    [Fact]
    public void Reversed_pair_is_a_different_key()
    {
        var a = new NpcId(1);
        var b = new NpcId(2);

        var aToB = new RelationshipKey(a, b);
        var bToA = new RelationshipKey(b, a);

        Assert.NotEqual(aToB, bToA);
    }

    [Fact]
    public void Same_pair_in_same_order_is_equal()
    {
        var a = new NpcId(1);
        var b = new NpcId(2);

        var first = new RelationshipKey(a, b);
        var second = new RelationshipKey(a, b);

        Assert.Equal(first, second);
    }
}
