using System.Security.Cryptography;

namespace LivingWorld.Api.World;

/// <summary>Identidade pública e determinística de um mundo criado: hash puro da
/// seed, nunca persistida — recomputável a qualquer momento, nunca dessincroniza do snapshot
/// porque não é parte dele.</summary>
public static class WorldIdentity
{
    public static Guid WorldIdFor(ulong seed)
    {
        var hash = SHA256.HashData(BitConverter.GetBytes(seed));
        return new Guid(hash[..16]);
    }
}
