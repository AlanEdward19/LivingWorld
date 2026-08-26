namespace LivingWorld.Domain;

/// <summary>Limiares de relevância do Attention Router (Fase 16.3 P2a, COH-43 / doc#59) —
/// cenário-driven, mesmo template de <see cref="BodyRules"/>/<see cref="CausalRules"/>:
/// validação em <see cref="Create"/>, <see cref="Default"/> documentado.</summary>
/// <remarks>
/// Defaults documentados:
/// <list type="bullet">
/// <item><see cref="MinPriceChangeMagnitude"/> = 0.05 — variação de preço &lt; 5% não acorda
/// dependentes econômicos cidade-inteira (caso negativo de baixa magnitude).</item>
/// <item><see cref="MaxLocationDistanceCells"/> = 8 — raio de localização relevante.</item>
/// <item><see cref="MinRelationshipStrength"/> = 10 — familiaridade/confiança mínima para
/// relação contar como critério de wake.</item>
/// <item><see cref="ThreatRadiusCells"/> = 4 — raio de ameaça física.</item>
/// </list>
/// </remarks>
public sealed record AttentionRules(
    double MinPriceChangeMagnitude,
    int MaxLocationDistanceCells,
    double MinRelationshipStrength,
    int ThreatRadiusCells,
    bool Enabled)
{
    public const double DefaultMinPriceChangeMagnitude = 0.05;
    public const int DefaultMaxLocationDistanceCells = 8;
    public const double DefaultMinRelationshipStrength = 10.0;
    public const int DefaultThreatRadiusCells = 4;

    public static Result<AttentionRules> Create(
        double minPriceChangeMagnitude,
        int maxLocationDistanceCells,
        double minRelationshipStrength,
        int threatRadiusCells,
        bool enabled)
    {
        if (minPriceChangeMagnitude < 0)
            return Result<AttentionRules>.Fail("MinPriceChangeMagnitude: deve ser >= 0");
        if (maxLocationDistanceCells < 0)
            return Result<AttentionRules>.Fail("MaxLocationDistanceCells: deve ser >= 0");
        if (minRelationshipStrength < 0)
            return Result<AttentionRules>.Fail("MinRelationshipStrength: deve ser >= 0");
        if (threatRadiusCells < 0)
            return Result<AttentionRules>.Fail("ThreatRadiusCells: deve ser >= 0");

        return Result<AttentionRules>.Ok(new AttentionRules(
            minPriceChangeMagnitude, maxLocationDistanceCells, minRelationshipStrength,
            threatRadiusCells, enabled));
    }

    /// <summary>Default documentado — limiares que filtram eventos triviais sem silenciar
    /// dependências reais. Use <see cref="Resolve"/>.</summary>
    public static readonly AttentionRules Default = Create(
        minPriceChangeMagnitude: DefaultMinPriceChangeMagnitude,
        maxLocationDistanceCells: DefaultMaxLocationDistanceCells,
        minRelationshipStrength: DefaultMinRelationshipStrength,
        threatRadiusCells: DefaultThreatRadiusCells,
        enabled: true).Value
        ?? throw new InvalidOperationException(
            "AttentionRules.Default inválida — bug no cenário");

    /// <summary>Router desligado — <c>RouteRelevantNpcs</c> devolve conjunto vazio.</summary>
    public static readonly AttentionRules Disabled = Default with { Enabled = false };

    /// <summary>Usa a regra declarada; se ausente, devolve <see cref="Default"/>.</summary>
    public static AttentionRules Resolve(AttentionRules? declared) => declared ?? Default;
}
