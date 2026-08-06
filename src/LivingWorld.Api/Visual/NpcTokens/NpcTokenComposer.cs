using LivingWorld.Domain;

namespace LivingWorld.Api.Visual.NpcTokens;

/// <summary>Fase 15, T6 (VTT-14..16): compõe o token 2D determinístico de um NPC a partir do
/// estado canônico — pura, sem RNG por chamada (mesmo NPC no mesmo estado sempre gera o mesmo
/// token, VTT-15). Pele/cabelo/cor de acento derivam de <c>Npc.Id</c> (identidade, nunca muda em
/// vida); roupa/acessório derivam de <c>Npc.Profession</c> (só isso muda quando o NPC troca de
/// profissão, VTT-16).</summary>
public static class NpcTokenComposer
{
    public static NpcTokenDescriptor Compose(Npc npc)
    {
        long identity = npc.Id.Value;
        long profession = npc.Profession.Id;

        return new NpcTokenDescriptor(
            NpcTokenCatalog.AssetPackVersion,
            BaseLayer: PickFrom(NpcTokenCatalog.BaseLayers, identity),
            HairLayer: PickFrom(NpcTokenCatalog.HairLayers, identity + 1),
            OutfitLayer: PickFrom(NpcTokenCatalog.OutfitLayers, profession),
            AccessoryLayer: PickFrom(NpcTokenCatalog.AccessoryLayers, profession + 1),
            AccentColor: PickFrom(NpcTokenCatalog.AccentColors, identity + 2));
    }

    private static string PickFrom(IReadOnlyList<string> options, long key) =>
        options[(int)(((key % options.Count) + options.Count) % options.Count)];
}
