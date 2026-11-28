using LivingWorld.Simulation.Objects;
using LivingWorld.Simulation.Players;

namespace LivingWorld.Api.Players;

/// <summary>#264: o modo comprar lido do motor (catálogo à venda, peças, cômodos, tintas) e a pergunta "pode?" do fantasma. Só leitura, sob pedido; comprar, mover, vender e construir
/// passam pelo comando <c>interact {kind:"home"}</c> (fila e registro de sempre).</summary>
public sealed partial class PlayerCommandGateway
{
    public PlayerReply HomeState(string playerId)
    {
        lock (library.Gate)
        {
            var world = host.Current;
            if (PlayerCommands.ControlledBy(world, playerId) is not { } npc) return NoBody(playerId);
            return HomeView.Of(world, npc) is { } view
                ? new PlayerReply(StatusCodes.Status200OK, view, null)
                : new PlayerReply(StatusCodes.Status404NotFound, null, $"{PlayerCommands.NotFound} sem casa própria ou sem modo comprar neste mundo");
        }
    }

    /// <summary>Valida o verbo de casa sem aplicar: <c>{ok, reason, price, refund, note}</c> (sempre 200 para quem tem casa).</summary>
    public PlayerReply HomeCheck(string playerId, string verb)
    {
        lock (library.Gate)
        {
            var world = host.Current;
            if (PlayerCommands.ControlledBy(world, playerId) is not { } npc) return NoBody(playerId);
            long dwelling = npc.Household is { } h && world.FindHousehold(h)?.Dwelling is { } d ? d.Value : -1;
            var (building, error) = HomeCommands.HomeOf(world, npc, dwelling);
            if (building is null) return new PlayerReply(StatusCodes.Status200OK, new { ok = false, reason = error, price = 0L, refund = 0L, note = "" }, null);
            var check = HomeCommands.Check(world, npc, building.Id.Value, verb);
            string reason = check.IsSuccess ? "" : check.Error!.Replace(PlayerCommands.Conflict, "").Trim();
            return new PlayerReply(StatusCodes.Status200OK, new { ok = check.IsSuccess, reason, price = check.Value?.Price ?? 0L, refund = check.Value?.Refund ?? 0L, note = check.Value?.Note ?? "", fit = check.Value?.Fit }, null);
        }
    }
}
