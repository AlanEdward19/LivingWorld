namespace LivingWorld.Domain.History.Distortion;

/// <summary>Meio de transmissão de um relato (Fase 10, HIST-08) — enum fechado; parâmetros
/// por meio vivem em <see cref="MediumFidelity"/> dentro de <see cref="HistoryRules"/>.</summary>
public enum TransmissionMediumType
{
    LivingMemory,
    OralTradition,
    Book,
    Monument,
    Song,
}

/// <summary>Condição de morte de um meio de transmissão (Fase 10).</summary>
public enum DeathConditionType
{
    WitnessExtinct,
    LineageExtinct,
    Decay,
    StateCollapse,
}
