namespace LivingWorld.Domain.Behavior;

/// <summary>Como a casa se abastece no mercado (AD-148), dado de cenário com padrão. Snapshot ou cenário sem o bloco herda
/// <see cref="Default"/>; <see cref="Legacy"/> volta ao jeito de antes (cada pessoa vai sozinha, a qualquer hora, mesmo sem
/// dinheiro, e a banca sem a meta inteira não vende nada).</summary>
public sealed record ProvisioningRules
{
    /// <summary>A compra completa a meta de dias da casa, não soma a meta inteira por cima do que já há. Antes (fora dos freios
    /// do AD-145) cada ida trazia a água da meta de novo e as casas juntavam milhares de água, pagas com o dinheiro da comida.</summary>
    public bool TopUp { get; init; } = true;

    /// <summary>A banca com menos que a compra inteira vende o que tem; antes a transação falhava inteira e a pessoa voltava
    /// de mãos vazias.</summary>
    public bool BuyWhatIsThere { get; init; } = true;

    /// <summary>Só vai comprar comida quem pode pagar uma unidade dela (backlog 08, item 46); sem moeda a fome não leva ao
    /// mercado (a água sem moeda sai do poço, que continua valendo).</summary>
    public bool OnlyIfCanPay { get; init; } = true;

    /// <summary>Uma pessoa da casa faz a compra: com alguém da casa a caminho do mercado ou nele, os outros não vão (salvo a
    /// própria fome ou sede crítica).</summary>
    public bool OneShopperPerHousehold { get; init; } = true;

    /// <summary>No horário de trabalho a despensa só tira do turno quem a vê abaixo de tantos dias por morador (não dura até o
    /// fim do turno); acima disso a compra fica para depois do trabalho. Negativo = sem essa espera (como antes).</summary>
    public double RestockDuringWorkBelowDays { get; init; } = 0.5;

    /// <summary>Quem achou a banca de comida vazia não volta de hora em hora: a casa espera tantas horas, o dobro a cada ida
    /// vazia seguida, até <see cref="FoodStallMaxRetryHours"/> (a venda é reposta uma vez por dia). 0 = volta quando quiser.</summary>
    public int FoodStallRetryHours { get; init; } = 6;

    public int FoodStallMaxRetryHours { get; init; } = 48;

    public static ProvisioningRules Default { get; } = new();

    public static ProvisioningRules Legacy { get; } = new()
    {
        TopUp = false,
        BuyWhatIsThere = false,
        OnlyIfCanPay = false,
        OneShopperPerHousehold = false,
        RestockDuringWorkBelowDays = -1,
        FoodStallRetryHours = 0,
    };
}
