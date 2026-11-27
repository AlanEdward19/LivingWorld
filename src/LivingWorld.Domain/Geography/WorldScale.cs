namespace LivingWorld.Domain.Geography;

/// <summary>ADR-0052 (aceito pelo dono em 2026-11-24, "1,5 m por célula"): a escala única da grade e as medidas do que se constrói nela.
/// O motor diz; o cliente desenha (<c>GET /world/info</c>, campo <c>scale</c>).</summary>
public static class WorldScale
{
    /// <summary>Metros de uma célula.</summary>
    public const double CellMeters = 1.5;

    /// <summary>Altura de um andar (pé-direito mais o piso), em metros.</summary>
    public const double FloorMeters = 3.0;

    /// <summary>Altura da porta, em metros.</summary>
    public const double DoorMeters = 2.0;

    /// <summary>Largura da porta, em metros.</summary>
    public const double DoorWidthMeters = 0.9;

    /// <summary>Altura do peitoril e da janela, em metros.</summary>
    public const double WindowSillMeters = 1.0;
    public const double WindowMeters = 1.0;

    /// <summary>Passo de referência de um adulto descansado em chão leve (m/s).</summary>
    public const double ReferenceWalkMps = 1.4;

    /// <summary>AD-197o2: horas para atravessar uma célula de terreno leve no passo de referência (~1,07 s): o <c>CostWeights.Base</c>
    /// padrão do mundo do menu e da vila de código. A viagem dos NPCs custa metros reais, a mesma régua do passo do jogador.</summary>
    public const double CellBaseHours = CellMeters / ReferenceWalkMps / 3600.0;
}
