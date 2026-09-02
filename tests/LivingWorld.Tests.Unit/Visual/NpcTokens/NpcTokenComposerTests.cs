using LivingWorld.Api.Visual.NpcTokens;
using LivingWorld.Domain;
using LivingWorld.Simulation;

namespace LivingWorld.Tests.Visual;

/// <summary>Fase 15, T6 (VTT-14..16): <see cref="NpcTokenComposer"/> — determinístico por estado
/// canônico (mesmo NPC no mesmo estado sempre gera o mesmo token), e só as camadas de profissão
/// (roupa/acessório) mudam quando o NPC troca de profissão; pele/cabelo/cor de acento são fixos
/// pela identidade do NPC.</summary>
public class NpcTokenComposerTests
{
    private static Npc MakeNpc(ulong seed = 21)
    {
        var world = ScenarioRunner.Create(seed, initialPopulation: 1).World;
        return world.Npcs.First();
    }

    [Fact]
    public void Compose_is_deterministic_for_the_same_npc_state()
    {
        var npc = MakeNpc();

        var first = NpcTokenComposer.Compose(npc);
        var second = NpcTokenComposer.Compose(npc);

        Assert.Equal(first, second);
    }

    [Fact]
    public void Compose_uses_the_current_catalog_asset_pack_version()
    {
        var npc = MakeNpc();

        var token = NpcTokenComposer.Compose(npc);

        Assert.Equal(NpcTokenCatalog.AssetPackVersion, token.AssetPackVersion);
    }

    [Fact]
    public void Compose_returns_only_layers_that_belong_to_the_catalog()
    {
        var npc = MakeNpc();

        var token = NpcTokenComposer.Compose(npc);

        Assert.Contains(token.BaseLayer, NpcTokenCatalog.BaseLayers);
        Assert.Contains(token.HairLayer, NpcTokenCatalog.HairLayers);
        Assert.Contains(token.OutfitLayer, NpcTokenCatalog.OutfitLayers);
        Assert.Contains(token.AccessoryLayer, NpcTokenCatalog.AccessoryLayers);
        Assert.Contains(token.AccentColor, NpcTokenCatalog.AccentColors);
    }

    [Fact]
    public void Switching_profession_changes_only_outfit_and_accessory_layers()
    {
        var npc = MakeNpc();
        npc.SwitchProfession(new ProfessionType(0));
        var before = NpcTokenComposer.Compose(npc);

        npc.SwitchProfession(new ProfessionType(1));
        var after = NpcTokenComposer.Compose(npc);

        Assert.Equal(before.AssetPackVersion, after.AssetPackVersion);
        Assert.Equal(before.BaseLayer, after.BaseLayer);
        Assert.Equal(before.HairLayer, after.HairLayer);
        Assert.Equal(before.AccentColor, after.AccentColor);
        Assert.NotEqual(before.OutfitLayer, after.OutfitLayer);
        Assert.NotEqual(before.AccessoryLayer, after.AccessoryLayer);
    }

    [Fact]
    public void Composing_tokens_for_every_npc_in_a_real_scenario_never_throws_and_stays_in_catalog()
    {
        var world = ScenarioRunner.Create(seed: 23, initialPopulation: 20).World;

        foreach (var npc in world.Npcs)
        {
            var token = NpcTokenComposer.Compose(npc);

            Assert.Contains(token.BaseLayer, NpcTokenCatalog.BaseLayers);
            Assert.Contains(token.HairLayer, NpcTokenCatalog.HairLayers);
            Assert.Contains(token.OutfitLayer, NpcTokenCatalog.OutfitLayers);
            Assert.Contains(token.AccessoryLayer, NpcTokenCatalog.AccessoryLayers);
            Assert.Contains(token.AccentColor, NpcTokenCatalog.AccentColors);
        }
    }
}
