namespace LivingWorld.Domain.Invention;

/// <summary>Levar por terra (AD-153): as leis da sonda de terra e a carga por viagem a pé fora da água. Nenhum veículo aqui: trenó,
/// padiola ou carroça é o nome que alguém dá ao que a sonda mostrou deslizar ou rolar com carga. Números do mundo real no meio das
/// faixas que lembramos, todos "precisa de conferência" (registro AD-153 e backlog 08).</summary>
public sealed partial record InventionRules
{
    /// <summary>Força que uma pessoa sustenta puxando por horas, N (tração humana; não há animal de tiro no mundo).</summary>
    public int HumanPullN { get; init; } = 250;

    /// <summary>Coeficiente de atrito (‰) de arrastar no chão: a carga ou a peça larga raspa e afunda na terra.</summary>
    public int DragFrictionPm { get; init; } = 550;

    /// <summary>Atrito (‰) de deslizar sobre patim (peça esguia no sentido do movimento) de acabamento áspero; o acabamento liso
    /// do material (ou do revestimento) tira até <see cref="SkidFinishGainPm"/>. AD-162: o piso é o de madeira lisa sobre terra, 0,22
    /// (madeira/madeira 0,25–0,5; os 0,10–0,14 da madeira encerada valem só em neve, que o mundo não tem; Engineering ToolBox, [L]).</summary>
    public int SkidFrictionPm { get; init; } = 300;
    public int SkidFinishGainPm { get; init; } = 80;

    /// <summary>Quantas vezes mais comprida que larga uma peça de contato precisa ser para deslizar como patim.</summary>
    public int RunnerSlenderness { get; init; } = 4;

    /// <summary>Braço de rolamento (mm): resistência ao rolar = carga × este braço ÷ raio da roda (chão de terra batida).</summary>
    public int RollingResistanceMm { get; init; } = 15;

    /// <summary>Atrito (‰) do eixo no cubo (madeira engraxada): resistência = carga × atrito × raio do eixo ÷ raio da roda.</summary>
    public int AxleFrictionPm { get; init; } = 150;

    /// <summary>Altura (mm) que a carga empilha sobre um tabuleiro chato e densidade (kg/m³) da carga a granel (grão, sacos, água).</summary>
    public int PileHeightMm { get; init; } = 500;
    public int CargoBulkKgM3 { get; init; } = 800;

    /// <summary>A compra (e a água do poço) por terra também corta no que uma pessoa leva (<see cref="FootCarryKg"/>, ou o recipiente
    /// ou o veículo da casa), como já cortava com água no caminho (AD-143). Falso = por terra leva-se tudo (o mundo de antes).</summary>
    public bool LandCargo { get; init; } = true;

    /// <summary>Quem tem a compra cortada também pensa em arrastar, deslizar ou rolar a carga (ideias com tabuleiro e apoio por
    /// baixo, julgadas pela sonda de terra). Falso = só recipientes (AD-147).</summary>
    public bool LandVehicles { get; init; } = true;

    /// <summary>O que ficou no mercado custa uma ida e volta inteira por carga que ficou (quem volta não traz meia carga), e não a
    /// fração cortada da viagem (AD-143). É o tamanho do problema que a pessoa sente.</summary>
    public bool WholeTripsForCut { get; init; } = true;
}
