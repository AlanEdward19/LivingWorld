using System.Text.Json.Serialization;

namespace LivingWorld.Domain.Population;

/// <summary>#234: a guarda em curso do corpo (esquiva ou bloqueio).</summary>
public enum CombatGuard { None = 0, Dodge = 1, Block = 2 }

/// <summary>#234: o que a pessoa atingida decidiu fazer (decisão do motor a partir de traços, saúde, vínculo e força).</summary>
public enum CombatReaction { None = 0, Recoil = 1, Flee = 2, Retaliate = 3, CallHelp = 4 }

/// <summary>#234: o resultado do último golpe recebido, para o cliente animar (acertou, esquivou, bloqueou, errou).</summary>
public enum CombatOutcome { None = 0, Hit = 1, Dodged = 2, Blocked = 3, Missed = 4 }

/// <summary>#234: estado de combate do corpo (tempos em ms de mundo). Omitido do JSON (e do hash) enquanto nulo: quem nunca lutou
/// não muda. Canônico: a recarga, a guarda e a reação pendente alimentam decisões do motor.</summary>
public sealed record CombatState
{
    /// <summary>Instante em que o corpo pode golpear de novo.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public long StrikeReadyAtMs { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public CombatGuard Guard { get; init; }

    /// <summary>Até quando a guarda vale.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public long GuardUntilMs { get; init; }

    /// <summary>Início da guarda (a janela vale de <see cref="GuardFromMs"/> a <see cref="GuardUntilMs"/>).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public long GuardFromMs { get; init; }

    /// <summary>Instante em que a guarda pode ser levantada de novo.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public long GuardReadyAtMs { get; init; }

    /// <summary>A reação de quem foi atingido e até quando ela dura (o cliente anima recuo, fuga, revide, pedido de ajuda).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public CombatReaction Reaction { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public long ReactionUntilMs { get; init; }

    /// <summary>Revide pendente: quando o golpe sai (a janela de quem apanhou em que o agressor pode se defender) e contra quem.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public long RetaliateAtMs { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public long RetaliateTarget { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public CombatOutcome LastIncoming { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public int LastIncomingDamage { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public long LastIncomingAtMs { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public long LastIncomingBy { get; init; }
}
