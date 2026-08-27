namespace LivingWorld.Domain;

/// <summary>
/// Capacidade extraordinária autorada como dados composicionais. Os textos são ids de catálogo
/// do cenário, não categorias fechadas do motor; nenhum arquétipo exige um tipo C# próprio.
/// </summary>
public sealed record PowerDescriptor(
    string Id,
    string Source,
    IReadOnlyList<string> Effects,
    string Mode,
    IReadOnlyList<string> Costs,
    string Reliability,
    IReadOnlyList<string> FailureModes,
    IReadOnlyList<string> IntrinsicVulnerabilities,
    IReadOnlyList<string> Manifestations,
    IReadOnlyList<string> AcquisitionRules,
    ExtraordinaryAppearanceDescriptor? Appearance = null,
    NeedSubstitutionDescriptor? NeedSubstitution = null,
    double SenescenceRateMultiplier = 1,
    string? ManifestationCondition = null,
    IReadOnlyList<PowerEvolutionStage>? Stages = null);

/// <summary>Indícios visuais genéricos; strings são tokens autorados, nunca arquétipos.</summary>
public sealed record ExtraordinaryAppearanceDescriptor(
    double ScaleMultiplier,
    string SkinTint,
    string MovementTrail);

/// <summary>Troca uma necessidade por consumo de um recurso específico do catálogo.</summary>
public sealed record NeedSubstitutionDescriptor(
    string ReplacesNeed,
    ResourceType Resource,
    long UnitsPerUse);

/// <summary>Estado resolvido de apresentação que uma projeção pode consumir sem interpretar poder.</summary>
public sealed record ExtraordinaryAppearanceState(
    double ScaleMultiplier,
    string SkinTint,
    string MovementTrail);

/// <summary>
/// Estado canônico de um portador. Senescência zero impede envelhecimento, não outras mortes.
/// </summary>
public sealed record ExtraordinaryCarrierState(
    NpcId CarrierId,
    IReadOnlyList<string> PowerIds,
    bool IsManifested,
    string ManifestationState,
    ExtraordinaryAppearanceState Appearance,
    NeedSubstitutionDescriptor? NeedSubstitution,
    double SenescenceRateMultiplier,
    IReadOnlyDictionary<string, double>? PreAlterationTraits = null,
    IReadOnlySet<FactId>? ForgottenFactIds = null,
    NpcId? BondPartnerId = null,
    int LuckCurseAmount = 0,
    long LuckCurseUntilTick = 0,
    double GravityTargetMultiplier = 1,
    IReadOnlySet<FactId>? ImplantedFactIds = null,
    IReadOnlyList<DimensionalPocketEntry>? DimensionalPocket = null,
    IReadOnlyList<DimensionalPortal>? DimensionalPortals = null,
    PendingReincarnationPayload? PendingReincarnation = null,
    NpcId? PossessedBy = null,
    NpcId? BodySwapPartner = null,
    NpcId? ImpersonatingId = null,
    int UseCount = 0,
    int CurrentStageIndex = 0);

/// <summary>Fração de skills/traços a aplicar no próximo nascimento natural (PWR-106).</summary>
public sealed record PendingReincarnationPayload(
    IReadOnlyDictionary<int, double> Skills,
    Personality Personality,
    int FractionPercent,
    long QueuedTick);

/// <summary>Recurso fora do estoque/mapa normal, associado ao portador (PWR-117).</summary>
public sealed record DimensionalPocketEntry(int ResourceId, long Quantity);

/// <summary>Portal bidirecional entre duas células enquanto o poder existir (PWR-118).</summary>
public sealed record DimensionalPortal(CellCoord CellA, CellCoord CellB, string PowerId, long SourceInvocationId);

/// <summary>Footprint físico temporário criado por um efeito extraordinário genérico.</summary>
public sealed record ExtraordinaryConstruct(
    long Id,
    NpcId CreatorId,
    string PowerId,
    long SourceInvocationId,
    CellCoord Origin,
    IReadOnlyList<CellCoord> Footprint,
    int Durability,
    int MaxDurability,
    long CreatedAtTick,
    long ExpiresAtTick,
    string AppearanceToken);

/// <summary>Interpretação social autorada pela cultura, nunca pelo nome ou fonte do poder.</summary>
public sealed record ExtraordinaryCulturalResponseRule(
    int CultureId,
    string Manifestation,
    string Response);

/// <summary>Conteúdo extraordinário resolvido na borda do cenário.</summary>
public sealed record ExtraordinaryScenarioData
{
    public bool Enabled { get; init; }
    public double Prevalence { get; init; }
    public IReadOnlyList<PowerDescriptor> Descriptors { get; private set; }
    public IReadOnlyList<ExtraordinaryCulturalResponseRule> CulturalResponses { get; init; }
    /// <summary>Herança de poder (16.2). Null → <see cref="PowerInheritanceRules.Default"/>.</summary>
    public PowerInheritanceRules? InheritanceRules { get; init; }

    public ExtraordinaryScenarioData(
        bool enabled,
        IReadOnlyList<PowerDescriptor> descriptors,
        IReadOnlyList<ExtraordinaryCulturalResponseRule>? culturalResponses = null,
        double prevalence = 0,
        PowerInheritanceRules? inheritanceRules = null)
    {
        Enabled = enabled;
        Prevalence = prevalence;
        Descriptors = descriptors.ToList();
        CulturalResponses = culturalResponses ?? [];
        InheritanceRules = inheritanceRules;
    }

    /// <summary>Registra descritor gerado em runtime (ex.: mistura genética) sem duplicar Id.</summary>
    public void EnsureDescriptor(PowerDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        if (Descriptors.Any(d => string.Equals(d.Id, descriptor.Id, StringComparison.Ordinal)))
            return;
        var next = new List<PowerDescriptor>(Descriptors.Count + 1);
        next.AddRange(Descriptors);
        next.Add(descriptor);
        Descriptors = next;
    }

    public static ExtraordinaryScenarioData Disabled { get; } = new(false, [], [], 0);
}
