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

    /// <summary>AD-166 (08c item 148): a moeda só tira a casa da ajuda se compra comida — algum mercado a até
    /// <see cref="AidMarketReachHours"/> de caminho tem comida à venda e a casa não acabou de achar a banca vazia. Na quebra de
    /// colheita a banca esvazia e a casa com moeda e despensa vazia passa a ser socorrida, em comida.</summary>
    public bool AidSeesFoodForSale { get; init; } = true;

    /// <summary>AD-182 (08c item 149): a moeda só tira a casa da ajuda se alguém dela sabe comprar (o marco de comprar do
    /// desenvolvimento); a casa de quem não sabe (o adolescente que ficou só depois de regredir na doença) é socorrida em comida,
    /// como a casa sem banca. Falso = o de antes.</summary>
    public bool AidSeesWhoCanBuy { get; init; } = true;

    /// <summary>Caminho (horas, ida) até onde o mercado conta para a ajuda. Bracton (séc. XIII): mercados a 6⅔ milhas, um terço
    /// da jornada de 20 milhas (ir, negociar, voltar) — umas 3–4 h a pé [precisa de conferência].</summary>
    public int AidMarketReachHours { get; init; } = 4;

    /// <summary>AD-166: quem socorre (parente ou gente de afeto) manda comida da despensa da própria casa acima de
    /// <see cref="FoodAidReserveDays"/> por morador. Antes só o parente mandava, e só acima de 14 dias, que despensa nenhuma
    /// tinha (a meta da compra é de poucos dias): a ajuda em comida era zero.</summary>
    public bool FoodAidFromSurplus { get; init; } = true;

    /// <summary>Dias de comida por morador que a casa de quem socorre guarda para si antes de mandar comida: a meta da compra da
    /// casa (2 dias, `ProvisionTargetDays`). Medido na seed 14: 3 dias deixava 433 furtos na quebra, 2 dias 352 (sem a carga, 394).</summary>
    public int FoodAidReserveDays { get; init; } = 2;

    /// <summary>A compra completa a meta da despensa em vez de somar a meta por cima, e só se vai ao mercado pelo que se paga.</summary>
    public bool TopUpPurchases { get; init; } = true;

    /// <summary>Só casa quem tem do que viver: posse de terra vaga, ofício fora da lavoura ou casa própria (AD-158). Nulo desliga.</summary>
    public HoldingRules? Holdings { get; init; } = HoldingRules.Default;

    /// <summary>Saída de quem não tem lugar.</summary>
    public EmigrationRules Emigration { get; init; } = EmigrationRules.Default;

    /// <summary>A configuração medida no AD-145 (tudo ligado).</summary>
    public static PopulationPressureRules Measured { get; } = new();

    public string? Validate()
    {
        if (MarriageMeansWeight is < 0 or > 1 || FirstBirthMeansWeight is < 0 or > 1) return "Pressure: peso dos meios fora de [0,1]";
        if (MarriageSavingsMonths <= 0 || MarriageEarnerShare is < 0 or > 1) return "Pressure: economias devem ser > 0 e trabalho em [0,1]";
        if (AidMarketReachHours < 0 || FoodAidReserveDays < 0) return "Pressure: alcance do mercado e reserva da ajuda devem ser >= 0";
        return Holdings?.Validate() ?? Emigration.Validate();
    }
}
