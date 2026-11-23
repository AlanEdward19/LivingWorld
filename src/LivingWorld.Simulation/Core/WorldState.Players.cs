using System.Text.Json.Serialization;
using LivingWorld.Domain.Shared;

namespace LivingWorld.Simulation.Core;

// Parte de WorldState — regras do desaparecimento e da volta (AD-196, Fase 25).
public sealed partial class WorldState
{
    /// <summary>Regras do jogador declaradas pelo cenário. Nulo = <see cref="LivingWorld.Domain.Population.PlayerRules.Default"/>:
    /// omitido do snapshot e do hash canônico (o mundo sem cenário de jogador fica idêntico ao de antes); snapshot antigo abre com
    /// o padrão.</summary>
    [Canonical, JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public LivingWorld.Domain.Population.PlayerRules? PlayerRules { get; private set; }

    /// <summary>As regras em vigor (as do cenário ou o padrão).</summary>
    internal LivingWorld.Domain.Population.PlayerRules Players =>
        PlayerRules ?? LivingWorld.Domain.Population.PlayerRules.Default;

    /// <summary>Declara as regras do jogador (cenário, teste) ou restaura do snapshot. O padrão volta a nulo.</summary>
    public void UsePlayers(LivingWorld.Domain.Population.PlayerRules? rules)
    {
        PlayerRules = rules is null || rules == LivingWorld.Domain.Population.PlayerRules.Default ? null : rules;
        CanonicalHashCache.MarkPropertyDirty(nameof(PlayerRules));
    }
}
