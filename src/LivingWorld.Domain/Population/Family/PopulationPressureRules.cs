namespace LivingWorld.Domain.Population.Family;

/// <summary>Freios preventivos da pressão da população (AD-145): meios para casar e para o primeiro filho pesados pela
/// prudência, comida da família de origem, esmola racionada, compra que completa a meta e emigração de quem não tem lugar.
/// Fica em <see cref="FamilyRules.Pressure"/>, nulo por padrão (desligado, hash e snapshot iguais aos de antes): a medida de
/// 20 seeds × 100 anos com <see cref="Measured"/> teve 7 extinções e 13% das mortes por sede, e a causa não foi isolada (ver
/// <c>docs/domain/population-pressure.md</c>).</summary>
public sealed record PopulationPressureRules
{
    /// <summary>Quanto a falta de meios do casal (ninguém com trabalho, sem economias) tira da vontade de cada um de casar, na
    /// proporção da prudência dele. 0 = casa-se sem olhar os meios.</summary>
    public double MarriageMeansWeight { get; init; } = 0.9;

    /// <summary>Meses de gasto de uma casa de dois (comida e água ao piso) que, guardados pelos dois, são economias plenas.</summary>
    public double MarriageSavingsMonths { get; init; } = 24;

    /// <summary>Parte dos meios que vem de alguém do casal ter trabalho; o resto vem das economias.</summary>
    public double MarriageEarnerShare { get; init; } = 0.4;

    /// <summary>Quanto a falta de meios da casa (ninguém do casal trabalhando, despensa curta) adia o primeiro filho, na
    /// prudência média do casal. 0 desliga.</summary>
    public double FirstBirthMeansWeight { get; init; } = 0.5;

    /// <summary>A família de origem (pais, avós, irmãos) manda comida da própria despensa, e avós idosos também socorrem.</summary>
    public bool KinFoodAid { get; init; } = true;

    /// <summary>A esmola do dia é repartida entre os pedidos até o tesouro dividido pelos dias até a virada do mês.</summary>
    public bool RationedAlms { get; init; } = true;

    /// <summary>A compra completa a meta da despensa em vez de somar a meta por cima, e só se vai ao mercado pelo que se paga.</summary>
    public bool TopUpPurchases { get; init; } = true;

    /// <summary>Saída de quem não tem lugar.</summary>
    public EmigrationRules Emigration { get; init; } = EmigrationRules.Default;

    /// <summary>A configuração medida no AD-145 (tudo ligado).</summary>
    public static PopulationPressureRules Measured { get; } = new();

    public string? Validate()
    {
        if (MarriageMeansWeight is < 0 or > 1 || FirstBirthMeansWeight is < 0 or > 1) return "Pressure: peso dos meios fora de [0,1]";
        if (MarriageSavingsMonths <= 0 || MarriageEarnerShare is < 0 or > 1) return "Pressure: economias devem ser > 0 e trabalho em [0,1]";
        return Emigration.Validate();
    }
}
