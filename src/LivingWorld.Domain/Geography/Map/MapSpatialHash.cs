using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace LivingWorld.Domain;

/// <summary>Hash determinístico só da geografia (Fase 15.1, T43): dimensões, células e âncoras —
/// nunca catálogo/custo/regiões, que são regras/índices derivados, não a forma do mapa em si.
/// Serve pra provar que o preview (<see cref="MapScenarioLoader"/> puro) e o mundo criado (que
/// passa pelo mesmo loader) descrevem exatamente a mesma geografia para a mesma seed.</summary>
public static class MapSpatialHash
{
    public static string Compute(WorldMap map)
    {
        var json = JsonSerializer.Serialize(new
        {
            map.Width,
            map.Height,
            map.Cells,
            map.Settlements,
        });
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
    }
}
