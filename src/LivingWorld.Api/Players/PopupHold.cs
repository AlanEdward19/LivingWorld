using LivingWorld.Simulation.Players;

namespace LivingWorld.Api.Players;

/// <summary>#240: a pausa de popup. Quem joga sozinho abre um popup (conversa, mochila, metas…) e o cliente pede "hold": o laço
/// não roda ticks enquanto ele durar. É um pedido do jogador, distinto da pausa do admin (essa continua recusando comandos com 409;
/// esta aceita os comandos do popup). Estado do hospedeiro, fora de <c>LivingWorld.Simulation</c>, do snapshot e do hash: o motor
/// só deixa de receber ticks, nunca lê relógio. Segurança: o pedido vence se não for renovado em <see cref="Lease"/>, cai se o
/// jogador desconecta e só vale enquanto ele for o ÚNICO conectado (um segundo jogador o derruba).</summary>
public sealed class PopupHold(TimeProvider clock)
{
    public static readonly TimeSpan Lease = TimeSpan.FromSeconds(30);

    private readonly object _gate = new();
    private readonly Dictionary<string, int> _sockets = new(StringComparer.Ordinal); // jogador -> canais abertos
    private readonly Dictionary<string, DateTimeOffset> _expires = new(StringComparer.Ordinal); // jogador -> vence em

    public void Connected(string playerId)
    {
        lock (_gate) _sockets[playerId] = _sockets.GetValueOrDefault(playerId) + 1;
    }

    public void Disconnected(string playerId)
    {
        lock (_gate)
        {
            if (_sockets.GetValueOrDefault(playerId) > 1) { _sockets[playerId]--; return; }
            _sockets.Remove(playerId);
            _expires.Remove(playerId);
        }
    }

    /// <summary>Quantos jogadores estão com canal aberto agora.</summary>
    public int ConnectedPlayers
    {
        get { lock (_gate) return _sockets.Count; }
    }

    /// <summary>Liga (ou renova) / solta o hold de <paramref name="playerId"/>. Devolve se o servidor o honra agora: só o único conectado.</summary>
    public bool Set(string playerId, bool on)
    {
        lock (_gate)
        {
            if (!on) { _expires.Remove(playerId); return false; }
            if (_sockets.Count != 1 || !_sockets.ContainsKey(playerId)) { _expires.Remove(playerId); return false; }
            _expires[playerId] = clock.GetUtcNow() + Lease;
            return true;
        }
    }

    /// <summary>O mundo está parado por popup agora: há um hold vivo e quem o fez é o único conectado.</summary>
    public bool IsActive
    {
        get
        {
            lock (_gate)
            {
                if (_expires.Count == 0) return false;
                var now = clock.GetUtcNow();
                foreach (var gone in _expires.Where(e => e.Value <= now).Select(e => e.Key).ToList()) _expires.Remove(gone);
                return _expires.Count > 0 && _sockets.Count == 1 && _expires.Keys.All(_sockets.ContainsKey);
            }
        }
    }
}
