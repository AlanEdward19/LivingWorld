using System.Security.Cryptography;
using System.Text;

namespace LivingWorld.Simulation.Hosting;

/// <summary>Credencial de admin do console (Fase 26): só existe se o token apresentado bate com o configurado. Sem token
/// configurado nenhum acesso é concedido, então o padrão é fechado.</summary>
public sealed class AdminAccess
{
    private AdminAccess(string actor) => Actor = actor;

    public string Actor { get; }

    public static AdminAccess? TryGrant(string? presented, string? configured, string? actor)
    {
        if (string.IsNullOrEmpty(presented) || string.IsNullOrEmpty(configured)) return null;
        bool equal = CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(presented), Encoding.UTF8.GetBytes(configured));
        return equal ? new AdminAccess(string.IsNullOrWhiteSpace(actor) ? "admin" : actor.Trim()) : null;
    }
}
