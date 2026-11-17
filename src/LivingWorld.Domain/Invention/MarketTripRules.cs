namespace LivingWorld.Domain.Invention;

/// <summary>A ida ao mercado com a carga da casa (AD-182, 08c itens 111 e 147). <see cref="HoldsCarrier"/>: quem sai para comprar
/// leva o recipiente da casa mesmo sem arrumar lanche (mercado perto), e ninguém mais da casa sai com ele até a compra acabar.
/// <see cref="ReturnByLeftUnits"/>: a compra cortada pela carga guarda quanto ficou na banca, e a casa só volta antes da hora pelo
/// resto se ele vale a viagem (ao menos <see cref="ReturnMinDays"/> dia de consumo da casa); antes voltava por qualquer sobra.
/// Desligado = o de antes (AD-157b).</summary>
public sealed record MarketTripRules
{
    public bool HoldsCarrier { get; init; } = true;

    public bool ReturnByLeftUnits { get; init; } = true;

    /// <summary>Dias de consumo da casa (um por morador e dia) que o resto deixado na banca precisa somar para a volta antecipada.
    /// Escolha do modelo: menos de um dia de comida não pede uma viagem só por ele (precisa de conferência).</summary>
    public int ReturnMinDays { get; init; } = 1;

    public static MarketTripRules Default { get; } = new();

    public static MarketTripRules Disabled { get; } = new() { HoldsCarrier = false, ReturnByLeftUnits = false };
}
