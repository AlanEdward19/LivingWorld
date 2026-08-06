namespace LivingWorld.Api.Visual.NpcTokens;

/// <summary>Fase 15, T6 (VTT-14, VTT-16): catálogo versionado de assets por camada. <c>OutfitLayers</c>/
/// <c>AccessoryLayers</c> são indexados pelo id numérico de <c>ProfessionType</c> (catálogo aberto,
/// definido por período — Fase 13), não por nome semântico de profissão: não há mapeamento
/// profissão→arte real ainda (nenhum asset pack desenhado por profissão), então cada id de
/// profissão só recebe uma camada consistente e determinística do catálogo genérico — mesmo
/// padrão de fallback do design.md pra asset sem dado específico.</summary>
public static class NpcTokenCatalog
{
    public const string AssetPackVersion = "npc-tokens-v1";

    public static readonly IReadOnlyList<string> BaseLayers = ["skin-a", "skin-b", "skin-c", "skin-d"];
    public static readonly IReadOnlyList<string> HairLayers = ["hair-a", "hair-b", "hair-c", "hair-d", "hair-e"];
    public static readonly IReadOnlyList<string> AccentColors = ["accent-a", "accent-b", "accent-c"];
    public static readonly IReadOnlyList<string> OutfitLayers = ["outfit-a", "outfit-b", "outfit-c", "outfit-d"];
    public static readonly IReadOnlyList<string> AccessoryLayers = ["accessory-a", "accessory-b", "accessory-c"];
}
