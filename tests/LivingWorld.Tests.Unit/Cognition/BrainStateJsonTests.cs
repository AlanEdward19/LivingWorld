using System.Text.Json;
using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Cognition;

namespace LivingWorld.Tests.Unit.Cognition;

/// <summary>AD-160: a participação guardada vai ao snapshot num só número (byte por grupo), volta igual, some quando esquecida,
/// e o snapshot do AD-142 (lista de 8 int16) ainda abre.</summary>
public class BrainStateJsonTests
{
    [Fact]
    public void Remembered_shares_round_trip_and_forgetting_leaves_nothing_in_the_snapshot()
    {
        var brain = BrainState.Neutral();
        brain.Remember(ActionType.Buy, [500, -250, 250, 0, 0, 0, 0, 0]);

        var back = JsonSerializer.Deserialize<BrainState>(JsonSerializer.Serialize(brain))!;
        Assert.Equal(ActionType.Buy, back.PendingAction);
        Assert.Equal([504, -252, 252, 0], Enumerable.Range(0, 4).Select(back.ShareOf));

        brain.Forget();
        string forgotten = JsonSerializer.Serialize(brain);
        Assert.DoesNotContain("Pending", forgotten, StringComparison.Ordinal);
    }

    [Fact]
    public void The_ad142_snapshot_with_a_share_list_still_opens()
    {
        string gains = string.Join(",", new int[BrainState.Actions * BrainState.Groups]);
        string json = $"{{\"Gains\":[{gains}],\"ActionMeans\":[0,0,0,0,0,0,0,0,0,7],\"PendingAction\":3,\"PendingShares\":[1000,0,0,0,0,0,0,0]}}";

        var back = JsonSerializer.Deserialize<BrainState>(json)!;

        Assert.Equal(7, back.ActionMeans[9]);
        Assert.Equal(0, back.ShareOf(0)); // a lista antiga não é lida: perde um passo de aprendizado, não quebra
    }
}
