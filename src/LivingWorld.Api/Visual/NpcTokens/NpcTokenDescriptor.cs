namespace LivingWorld.Api.Visual.NpcTokens;

/// <summary>Fase 15, T6 (VTT-14..16): camadas do token 2D composto de um NPC — pele/cabelo/cor
/// de acento nascem da identidade do NPC (nunca mudam em vida); roupa/acessório nascem da
/// profissão (mudam quando o NPC troca de profissão). Mesmo <see cref="AssetPackVersion"/> em
/// todo NPC — versionamento é do catálogo, não por indivíduo.</summary>
public sealed record NpcTokenDescriptor(
    string AssetPackVersion,
    string BaseLayer,
    string HairLayer,
    string OutfitLayer,
    string AccessoryLayer,
    string AccentColor);
