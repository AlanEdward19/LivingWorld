using System.Net;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;

namespace LivingWorld.Api.Players;

/// <summary>Quando o jogador precisa de token (ADR-0048, AD-197e, decisão do dono: "token por padrão em servidor público").
/// <c>PLAYER_AUTH=required</c> exige sempre; <c>PLAYER_AUTH=local</c> aceita o modo local sempre; sem configuração, o servidor é
/// **público** quando escuta em algum endereço fora de loopback (0.0.0.0, [::], um IP da rede, um nome que não é localhost), e
/// então exige token. Só escutando em 127.0.0.1, [::1] ou localhost o modo local (o <c>playerId</c> da rota, sem token) vale: só
/// quem está na mesma máquina chega. Hospedeiro em memória (testes) não escuta em nada e fica local.</summary>
public sealed class PlayerAuthPolicy(string? configured, IServer server)
{
    private bool? _public;

    public bool Required => configured switch
    {
        "required" => true,
        "local" => false,
        _ => _public ??= IsPublic(server.Features.Get<IServerAddressesFeature>()?.Addresses ?? []),
    };

    /// <summary>Algum endereço de escuta fora de loopback.</summary>
    public static bool IsPublic(IEnumerable<string> addresses) => addresses.Any(address => !IsLoopback(address));

    private static bool IsLoopback(string address)
    {
        if (!Uri.TryCreate(address.Replace("://+", "://0.0.0.0", StringComparison.Ordinal).Replace("://*", "://0.0.0.0", StringComparison.Ordinal),
                UriKind.Absolute, out var uri))
            return false;
        if (string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase)) return true;
        return IPAddress.TryParse(uri.Host.Trim('[', ']'), out var ip) && IPAddress.IsLoopback(ip);
    }
}
