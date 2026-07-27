namespace LivingWorld.Domain;

/// <summary>Todo peso/limiar/duração/flag da Fase 7 (Relações e Famílias), cenário-driven (R3)
/// — nenhum literal em C#, mesmo padrão de <see cref="NeedsRules"/>/<see cref="EconomyRules"/>/
/// <see cref="SkillsRules"/> (T4, AD-053).</summary>
public sealed record FamilyRules(
    IReadOnlyDictionary<(RelationshipEventType Type, RelationshipAxis Axis), double> RelationshipDeltas,
    double DecayPerDay,
    int ContactLossThresholdDays,
    double NeutralAxisValue,
    IReadOnlyDictionary<AttractionFactor, double> AttractionWeights,
    double CourtshipThreshold,
    int CourtshipDurationDays,
    IReadOnlyDictionary<int, long> MarriageInitialStock,
    int ConceptionHealthFloor,
    double ConceptionRelationshipFloor,
    IReadOnlyDictionary<int, long> ConceptionResourceFloor,
    double MaternalDeathRisk,
    double InfantDeathRisk,
    double VitalityMotherWeight,
    double VitalityFatherWeight,
    double VitalityMutationStdDev,
    double VitalityMortalityWeight,
    double UpbringingWealthWeight,
    bool EnvironmentalWealthChannelEnabled,
    bool NeutralDriftEnabled)
{
    /// <summary>Ponto médio do eixo de <see cref="Relationship"/>/<c>Vitality</c>/<c>Upbringing</c>
    /// (escala <c>[0,100]</c>) usado como centro das fórmulas de <see cref="EffectiveVitalityMultiplier"/>
    /// e <see cref="ApplyUpbringingWeight"/> — constante de algoritmo, não conteúdo de cenário
    /// (mesmo espírito de <see cref="RateGene"/>.Spread).</summary>
    private const double MidpointValue = 50.0;

    public static Result<FamilyRules> Create(
        IReadOnlyDictionary<(RelationshipEventType Type, RelationshipAxis Axis), double> relationshipDeltas,
        double decayPerDay,
        int contactLossThresholdDays,
        double neutralAxisValue,
        IReadOnlyDictionary<AttractionFactor, double> attractionWeights,
        double courtshipThreshold,
        int courtshipDurationDays,
        IReadOnlyDictionary<int, long> marriageInitialStock,
        int conceptionHealthFloor,
        double conceptionRelationshipFloor,
        IReadOnlyDictionary<int, long> conceptionResourceFloor,
        double maternalDeathRisk,
        double infantDeathRisk,
        double vitalityMotherWeight,
        double vitalityFatherWeight,
        double vitalityMutationStdDev,
        double vitalityMortalityWeight,
        double upbringingWealthWeight,
        bool environmentalWealthChannelEnabled,
        bool neutralDriftEnabled)
    {
        foreach (var type in Enum.GetValues<RelationshipEventType>())
        foreach (var axis in Enum.GetValues<RelationshipAxis>())
            if (!relationshipDeltas.ContainsKey((type, axis)))
                return Result<FamilyRules>.Fail(
                    $"RelationshipDeltas[{type},{axis}]: ausente — deltas devem cobrir todo (EventType,Axis)");

        if (decayPerDay < 0)
            return Result<FamilyRules>.Fail("DecayPerDay: deve ser >= 0");
        if (contactLossThresholdDays <= 0)
            return Result<FamilyRules>.Fail("ContactLossThresholdDays: deve ser > 0");
        if (neutralAxisValue is < 0 or > 100)
            return Result<FamilyRules>.Fail("NeutralAxisValue: fora de [0,100]");
        if (courtshipThreshold < 0)
            return Result<FamilyRules>.Fail("CourtshipThreshold: deve ser >= 0");
        if (courtshipDurationDays <= 0)
            return Result<FamilyRules>.Fail("CourtshipDurationDays: deve ser > 0");

        foreach (var (resource, stock) in marriageInitialStock)
            if (stock < 0)
                return Result<FamilyRules>.Fail($"MarriageInitialStock[{resource}]: deve ser >= 0");

        if (conceptionHealthFloor is < 0 or > 100)
            return Result<FamilyRules>.Fail("ConceptionHealthFloor: fora de [0,100]");
        if (conceptionRelationshipFloor is < 0 or > 100)
            return Result<FamilyRules>.Fail("ConceptionRelationshipFloor: fora de [0,100]");

        foreach (var (resource, floor) in conceptionResourceFloor)
            if (floor < 0)
                return Result<FamilyRules>.Fail($"ConceptionResourceFloor[{resource}]: deve ser >= 0");

        if (maternalDeathRisk is < 0 or > 1)
            return Result<FamilyRules>.Fail("MaternalDeathRisk: fora de [0,1]");
        if (infantDeathRisk is < 0 or > 1)
            return Result<FamilyRules>.Fail("InfantDeathRisk: fora de [0,1]");

        if (vitalityMotherWeight < 0)
            return Result<FamilyRules>.Fail("VitalityMotherWeight: deve ser >= 0");
        if (vitalityFatherWeight < 0)
            return Result<FamilyRules>.Fail("VitalityFatherWeight: deve ser >= 0");

        // Soma não precisa ser exatamente 1.0 (clamp documentado do próprio Inherit cobre o
        // resto) — mas soma zero/negativa ou desproporcional (> 2x um blend normal) não é
        // sensata para uma média ponderada de dois pais.
        double weightSum = vitalityMotherWeight + vitalityFatherWeight;
        if (weightSum is <= 0 or > 2)
            return Result<FamilyRules>.Fail("VitalityMotherWeight + VitalityFatherWeight: soma fora de (0,2]");

        if (vitalityMutationStdDev < 0)
            return Result<FamilyRules>.Fail("VitalityMutationStdDev: deve ser >= 0");
        if (vitalityMortalityWeight < 0)
            return Result<FamilyRules>.Fail("VitalityMortalityWeight: deve ser >= 0");
        if (upbringingWealthWeight < 0)
            return Result<FamilyRules>.Fail("UpbringingWealthWeight: deve ser >= 0");

        return Result<FamilyRules>.Ok(new FamilyRules(
            relationshipDeltas, decayPerDay, contactLossThresholdDays, neutralAxisValue, attractionWeights,
            courtshipThreshold, courtshipDurationDays, marriageInitialStock, conceptionHealthFloor,
            conceptionRelationshipFloor, conceptionResourceFloor, maternalDeathRisk, infantDeathRisk,
            vitalityMotherWeight, vitalityFatherWeight, vitalityMutationStdDev, vitalityMortalityWeight,
            upbringingWealthWeight, environmentalWealthChannelEnabled, neutralDriftEnabled));
    }

    /// <summary>Delta declarado para o par (evento, eixo) — FAM-03. Sem entrada correspondente
    /// (não deveria acontecer após <see cref="Create"/> validar cobertura total), devolve 0.</summary>
    public double RelationshipEventDelta(RelationshipEventType type, RelationshipAxis axis) =>
        RelationshipDeltas.TryGetValue((type, axis), out var delta) ? delta : 0.0;

    /// <summary>Peso declarado do fator de atração; fator sem declaração no cenário pesa 0
    /// (mesmo espírito de <see cref="SkillsRules.Gain"/> com fonte sem taxa declarada).</summary>
    public double AttractionWeight(AttractionFactor factor) =>
        AttractionWeights.TryGetValue(factor, out var weight) ? weight : 0.0;

    /// <summary>Multiplicador de mortalidade a partir de <c>Vitality</c> — <c>Vitality</c> acima
    /// do meio da escala reduz o multiplicador, abaixo aumenta; nunca produz saída negativa
    /// (Error Handling do design). Chamado por <c>MortalityPlanner</c>/<c>MortalitySystem</c>
    /// (T9) e pelo cenário de deriva neutra, que passa <c>1.0</c> direto sem chamar este método
    /// (AD-059).</summary>
    public double EffectiveVitalityMultiplier(double vitality)
    {
        double centered = (MidpointValue - vitality) / MidpointValue;
        double multiplier = 1.0 + centered * VitalityMortalityWeight;
        return Math.Max(multiplier, 0.0);
    }

    /// <summary>Aplica o peso do canal ambiental (<c>Upbringing</c>) ao salário (AD-062) — sem-op
    /// se <see cref="EnvironmentalWealthChannelEnabled"/> for falso. <paramref name="upbringing"/>
    /// é clampado a <c>[0,100]</c> antes de aplicar o peso, defesa contra valor fora de faixa
    /// (Error Handling do design) — <c>Upbringing</c> acima do meio da escala paga mais, abaixo
    /// paga menos, nunca produz fator negativo.</summary>
    public double ApplyUpbringingWeight(double wage, double upbringing)
    {
        if (!EnvironmentalWealthChannelEnabled)
            return wage;

        double clampedUpbringing = Math.Clamp(upbringing, 0.0, 100.0);
        double factor = 1.0 + UpbringingWealthWeight * (clampedUpbringing - MidpointValue) / MidpointValue;
        return wage * Math.Max(factor, 0.0);
    }
}
