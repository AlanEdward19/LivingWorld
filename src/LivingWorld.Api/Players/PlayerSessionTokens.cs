using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace LivingWorld.Api.Players;

/// <summary>Sessão de jogador emitida pelo hospedeiro (ADR-0048): token opaco amarrado a um <c>playerId</c> gerado aqui.</summary>
public sealed record PlayerSession(string Token, string PlayerId);

/// <summary>Tokens de sessão do jogador (ADR-0048, AD-197a). Estado do hospedeiro: fora de <c>LivingWorld.Simulation</c>, do
/// snapshot e do hash. O aleatório é do sistema operacional (criptográfico), nunca do RNG do mundo. Validade deslizante: a sessão
/// ociosa por <see cref="IdleLifetime"/> vence.</summary>
public sealed class PlayerSessionTokens(TimeProvider clock)
{
    public static readonly TimeSpan IdleLifetime = TimeSpan.FromHours(12);

    private sealed class Entry(string playerId, DateTimeOffset seen)
    {
        public string PlayerId { get; } = playerId;
        public DateTimeOffset Seen { get; set; } = seen;
    }

    private readonly ConcurrentDictionary<string, Entry> _byToken = new(StringComparer.Ordinal);

    public PlayerSession Create()
    {
        string token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        string playerId = "p-" + Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(8));
        _byToken[token] = new Entry(playerId, clock.GetUtcNow());
        return new PlayerSession(token, playerId);
    }

    /// <summary>O jogador do token, ou nulo se o token não existe ou venceu (e então sai da tabela).</summary>
    public string? PlayerOf(string? token)
    {
        if (string.IsNullOrEmpty(token) || !_byToken.TryGetValue(token, out var entry)) return null;
        var now = clock.GetUtcNow();
        if (now - entry.Seen > IdleLifetime)
        {
            _byToken.TryRemove(token, out _);
            return null;
        }
        entry.Seen = now;
        return entry.PlayerId;
    }
}
