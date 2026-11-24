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

    /// <summary>AD-197c: Δ (ms) do tick em curso, posto pelo relógio antes de qualquer sistema rodar. Não é estado do mundo: a sequência
    /// de Δ é entrada (registro do jogador), como os comandos; fora do snapshot e do hash.</summary>
    internal int TickDeltaMs { get; set; } = WorldMillis.PerHour;

    /// <summary>AD-197c: o tick é fino (Δ menor que a hora) e o cenário quer decisão, ação e rota finas. Com Δ = 1 h é sempre falso:
    /// o mundo que anda de hora em hora segue a conta de antes, byte a byte.</summary>
    internal bool FineTime => TickDeltaMs < WorldMillis.PerHour && Players.Time.FineDecisions;

    /// <summary>AD-197h: rotas já traçadas para a pose de viagem do passo fino, por par de lugares; derivado, fora do hash.</summary>
    internal PosePathCache PosePaths { get; } = new();

    internal sealed class PosePathCache
    {
        /// <summary>AD-197l: o próximo instante em que a pose de cada viajante troca de célula, para a perna e o índice em que foi
        /// calculada; antes dele a pose é a mesma e não se refaz (derivado da perna canônica).</summary>
        public Dictionary<long, PoseStep> Steps { get; } = new();
        public AliveNpcIndex? Index;

        public (long Obstacles, int Surfaces, bool Ford) Stamp;
        public Dictionary<(LivingWorld.Domain.Geography.CellCoord, LivingWorld.Domain.Geography.CellCoord), IReadOnlyList<LivingWorld.Domain.Geography.CellCoord>> Paths { get; } = new();
    }

    internal readonly record struct PoseStep(long StartMs, LivingWorld.Domain.Geography.CellCoord From, LivingWorld.Domain.Geography.CellCoord To,
        long EndMs, long NextMs);

    /// <summary>Declara as regras do jogador (cenário, teste) ou restaura do snapshot. O padrão volta a nulo.</summary>
    public void UsePlayers(LivingWorld.Domain.Population.PlayerRules? rules)
    {
        PlayerRules = rules is null || rules == LivingWorld.Domain.Population.PlayerRules.Default ? null : rules;
        CanonicalHashCache.MarkPropertyDirty(nameof(PlayerRules));
    }
}
