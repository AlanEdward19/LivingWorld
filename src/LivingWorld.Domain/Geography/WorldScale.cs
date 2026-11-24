namespace LivingWorld.Domain.Geography;

/// <summary>ADR-0052 (aceito pelo dono em 2026-11-24, "1,5 m por célula"): a escala única da grade. Uma célula tem
/// <see cref="CellMeters"/> metros para a viagem dos NPCs, o passo do jogador, a planta e o desenho; o custo de andar uma célula de
/// terreno leve sai daqui (<see cref="CellBaseHours"/>), não de um número solto.</summary>
public static class WorldScale
{
    /// <summary>Metros de uma célula.</summary>
    public const double CellMeters = 1.5;

    /// <summary>Passo de referência de um adulto descansado em chão leve (m/s).</summary>
    public const double ReferenceWalkMps = 1.4;

    /// <summary>Horas para atravessar uma célula de terreno leve no passo de referência (~1,07 s): o <c>CostWeights.Base</c> padrão.</summary>
    public const double CellBaseHours = CellMeters / ReferenceWalkMps / 3600.0;
}
